using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
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

    private async Task<PinViewModel> ModelAsync(CancellationToken cancellationToken)
    {
        var row = await settings.FindAsync(users.GetUserId(User)!, cancellationToken);
        return new PinViewModel { HasPin = row?.ParentPinHash is not null, UpdatedAt = row?.ParentPinHash is null ? null : row.UpdatedAt };
    }
}
