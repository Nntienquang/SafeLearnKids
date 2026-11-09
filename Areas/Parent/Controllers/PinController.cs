using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using SafeLearnKids.Web.Data;
using SafeLearnKids.Web.Models;
using SafeLearnKids.Web.Services;

namespace SafeLearnKids.Web.Areas.Parent.Controllers;

// US-50: the parent PIN that ends child mode, stored hashed on the server. Setting or changing it needs the account password.
[Area("Parent")]
[Authorize(Roles = "Parent")]
public class PinController(UserManager<ApplicationUser> users, ParentSettings settings, AccountActivityLog activity, IServiceProvider services) : Controller
{
    [HttpGet("pin")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        if (services.GetService<SafeLearnDbContext>() is null) return View("SqlOnly");
        return View(await ModelAsync(cancellationToken));
    }

    [HttpPost("pin")]
    [EnableRateLimiting("account")]
    public async Task<IActionResult> Save(PinForm form, CancellationToken cancellationToken)
    {
        if (services.GetService<SafeLearnDbContext>() is null) return View("SqlOnly");
        if (await users.GetUserAsync(User) is not { } user) return Challenge();
        if (ModelState.IsValid && !await users.CheckPasswordAsync(user, form.Password))
            ModelState.AddModelError(nameof(form.Password), "Mật khẩu chưa đúng.");
        if (ModelState.IsValid && !await settings.SetPinAsync(user, form.Pin, cancellationToken))
            ModelState.AddModelError("", "Chưa lưu được PIN. Vui lòng thử lại.");
        if (!ModelState.IsValid)
        {
            Response.StatusCode = StatusCodes.Status400BadRequest;
            return View("Index", await ModelAsync(cancellationToken));
        }
        await activity.ContentActionAsync(user.Id, "UserSetting", 0, "ParentPinChanged");
        TempData["Info"] = "Đã lưu mã PIN. Bé cần PIN này để rời chế độ trẻ.";
        return RedirectToAction(nameof(Index));
    }

    // US-08: set, change or remove one child's PIN. The parent is signed in and outside child mode (ChildModeFilter).
    [HttpPost("pin/child/{id:int}")]
    [EnableRateLimiting("account")]
    public async Task<IActionResult> SaveChild(int id, [FromForm] string? pin, [FromForm] string? confirmPin, CancellationToken cancellationToken)
    {
        if (services.GetService<SafeLearnDbContext>() is not { } db) return View("SqlOnly");
        var parentId = users.GetUserId(User)!;
        var child = await db.ChildProfiles.FirstOrDefaultAsync(item => item.Id == id && item.ParentId == parentId && item.IsActive, cancellationToken);
        if (child is null) return NotFound();
        if (!ChildPins.Valid(pin)) TempData["ChildPinError"] = $"PIN của {child.DisplayName} gồm đúng 4 chữ số.";
        else if (pin != confirmPin) TempData["ChildPinError"] = $"Hai mã PIN của {child.DisplayName} chưa khớp.";
        else
        {
            var changing = child.PinHash is not null;
            ChildPins.Set(child, pin!);
            await db.SaveChangesAsync(cancellationToken);
            await activity.ContentActionAsync(parentId, "ChildProfile", id, changing ? "ChildPinChanged" : "ChildPinSet");
            TempData["Info"] = $"Đã lưu mã PIN cho {child.DisplayName}. Bé nhập PIN này khi chọn hồ sơ của mình.";
        }
        return Redirect("/pin#child-pins");
    }

    [HttpPost("pin/child/{id:int}/remove")]
    [EnableRateLimiting("account")]
    public async Task<IActionResult> RemoveChild(int id, CancellationToken cancellationToken)
    {
        if (services.GetService<SafeLearnDbContext>() is not { } db) return View("SqlOnly");
        var parentId = users.GetUserId(User)!;
        var child = await db.ChildProfiles.FirstOrDefaultAsync(item => item.Id == id && item.ParentId == parentId && item.IsActive, cancellationToken);
        if (child is null) return NotFound();
        if (child.PinHash is not null)
        {
            ChildPins.Clear(child);
            await db.SaveChangesAsync(cancellationToken);
            await activity.ContentActionAsync(parentId, "ChildProfile", id, "ChildPinRemoved");
        }
        TempData["Info"] = $"Đã bỏ mã PIN của {child.DisplayName}. Bé vào học không cần PIN.";
        return Redirect("/pin#child-pins");
    }

    private async Task<PinViewModel> ModelAsync(CancellationToken cancellationToken)
    {
        var parentId = users.GetUserId(User)!;
        var row = await settings.FindAsync(parentId, cancellationToken);
        var now = DateTime.UtcNow;
        var children = new List<ChildPinRow>();
        if (services.GetService<SafeLearnDbContext>() is { } db)
        {
            var rows = await db.ChildProfiles.AsNoTracking().Where(item => item.ParentId == parentId && item.IsActive)
                .OrderBy(item => item.CreatedAt).ThenBy(item => item.Id)
                .Select(item => new { item.Id, item.DisplayName, item.PinHash, item.PinChangedAt, item.PinLockedUntil }).ToListAsync(cancellationToken);
            children = rows.Select(item => new ChildPinRow(item.Id, item.DisplayName, item.PinHash != null, item.PinHash == null ? null : item.PinChangedAt,
                item.PinLockedUntil is { } until && until > now ? (int)Math.Ceiling((until - now).TotalMinutes) : null)).ToList();
        }
        return new PinViewModel { HasPin = row?.ParentPinHash is not null, UpdatedAt = row?.ParentPinHash is null ? null : row.UpdatedAt, Children = children };
    }
}
