using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using SafeLearnKids.Web.Data;
using SafeLearnKids.Web.Models;
using SafeLearnKids.Web.Models.Entities;
using SafeLearnKids.Web.Services;

namespace SafeLearnKids.Web.Areas.Parent.Controllers;

// US-52: a parent downloads everything stored about a child, or asks for it to be deleted (an admin carries it out).
// Every request is recorded in dbo.DataRequests.
[Area("Parent")]
[Authorize(Roles = "Parent")]
public class PrivacyController(KidContext kids, AccountActivityLog activity, IServiceProvider services) : Controller
{
    private SafeLearnDbContext? Db => services.GetService<SafeLearnDbContext>();
    private string ParentId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    [HttpGet("privacy")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        if (Db is not { } db) return View("SqlOnly");
        return View(new PrivacyViewModel
        {
            Children = await kids.ChoicesAsync(cancellationToken),
            Requests = await db.DataRequests.AsNoTracking().Where(item => item.ParentId == ParentId).OrderByDescending(item => item.RequestedAt).Take(30)
                .Select(item => new DataRequestRow(item.Id, item.RequestType, item.Status, item.ChildName, item.ChildProfileId, item.DecisionNote, item.RequestedAt, item.CompletedAt, null, null))
                .ToListAsync(cancellationToken),
            Consents = await db.UserConsents.AsNoTracking().Where(item => item.UserId == ParentId).OrderByDescending(item => item.DecidedAt)
                .Select(item => new ConsentRow(item.ConsentType, item.Version, item.Decision, item.DecidedAt, db.ChildProfiles.Where(child => child.Id == item.ChildProfileId).Select(child => child.DisplayName).FirstOrDefault()))
                .ToListAsync(cancellationToken),
            ChildConsents = await ChildConsent.GivenAsync(db, ParentId, cancellationToken)
        });
    }

    // US-10: agree (again) to processing one child's learning data.
    [HttpPost("privacy/{child:int}/consent")]
    [EnableRateLimiting("account")]
    public async Task<IActionResult> Consent(int child, [FromForm] bool agree, CancellationToken cancellationToken)
    {
        if (Db is not { } db) return View("SqlOnly");
        var name = await db.ChildProfiles.Where(item => item.Id == child && item.ParentId == ParentId && item.IsActive).Select(item => item.DisplayName).FirstOrDefaultAsync(cancellationToken);
        if (name is null) return NotFound();
        if (!agree)
        {
            TempData["Error"] = "Đánh dấu ô đồng ý để xác nhận.";
            return RedirectToAction(nameof(Index));
        }
        await ChildConsent.GrantAsync(db, ParentId, child, IpAddress, cancellationToken);
        await activity.ContentActionAsync(ParentId, "ChildProfile", child, "ChildConsentAccepted", $"{ChildConsent.Type} {ChildConsent.Version}");
        TempData["Info"] = $"Đã ghi nhận sự đồng ý cho {name}. Bé có thể vào học.";
        return RedirectToAction(nameof(Index));
    }

    // US-10: withdraw consent. The child is locked out of child mode at once; the data stays until the parent asks to delete it.
    [HttpPost("privacy/{child:int}/consent/revoke")]
    [EnableRateLimiting("account")]
    public async Task<IActionResult> RevokeConsent(int child, CancellationToken cancellationToken)
    {
        if (Db is not { } db) return View("SqlOnly");
        var name = await db.ChildProfiles.Where(item => item.Id == child && item.ParentId == ParentId && item.IsActive).Select(item => item.DisplayName).FirstOrDefaultAsync(cancellationToken);
        if (name is null) return NotFound();
        if (await ChildConsent.RevokeAsync(db, ParentId, child, IpAddress, cancellationToken))
            await activity.ContentActionAsync(ParentId, "ChildProfile", child, "ChildConsentRevoked", $"{ChildConsent.Type} {ChildConsent.Version}");
        TempData["Info"] = $"Đã rút sự đồng ý cho {name}. Bé không vào học được cho tới khi ba mẹ đồng ý lại. Muốn xóa hẳn dữ liệu, hãy gửi yêu cầu xóa bên dưới.";
        return RedirectToAction(nameof(Index));
    }

    private string? IpAddress => HttpContext.Connection.RemoteIpAddress?.ToString() is { Length: <= 45 } ip ? ip : null;

    [HttpPost("privacy/{child:int}/export")]
    [EnableRateLimiting("account")]
    public async Task<IActionResult> Export(int child, CancellationToken cancellationToken)
    {
        if (Db is not { } db) return View("SqlOnly");
        var name = await db.ChildProfiles.Where(item => item.Id == child && item.ParentId == ParentId).Select(item => item.DisplayName).FirstOrDefaultAsync(cancellationToken);
        if (name is null) return NotFound();
        var bytes = await ChildDataExport.BuildAsync(db, child, cancellationToken);
        var now = DateTime.UtcNow;
        db.DataRequests.Add(new DataRequest
        {
            ParentId = ParentId, ChildProfileId = child, ChildName = Cut(name), RequestType = "Export", Status = DataRequestStatus.Done, RequestedAt = now, CompletedAt = now
        });
        await db.SaveChangesAsync(cancellationToken);
        await activity.ContentActionAsync(ParentId, "ChildProfile", child, "ChildDataExport");
        return File(bytes, "application/json; charset=utf-8", $"du-lieu-be-{child}-{now:yyyyMMdd}.json");
    }

    // The parent types the child's name to confirm; one open deletion request per child.
    [HttpPost("privacy/{child:int}/delete")]
    [EnableRateLimiting("account")]
    public async Task<IActionResult> RequestDelete(int child, [FromForm] string? confirmName, CancellationToken cancellationToken)
    {
        if (Db is not { } db) return View("SqlOnly");
        var name = await db.ChildProfiles.Where(item => item.Id == child && item.ParentId == ParentId).Select(item => item.DisplayName).FirstOrDefaultAsync(cancellationToken);
        if (name is null) return NotFound();
        if (!string.Equals(confirmName?.Trim(), name, StringComparison.CurrentCultureIgnoreCase))
        {
            TempData["Error"] = $"Nhập đúng tên \"{name}\" để xác nhận yêu cầu xóa.";
            return RedirectToAction(nameof(Index));
        }
        if (await db.DataRequests.AnyAsync(item => item.ChildProfileId == child && item.RequestType == "Delete"
                && (item.Status == DataRequestStatus.Pending || item.Status == DataRequestStatus.Processing), cancellationToken))
        {
            TempData["Error"] = "Đã có yêu cầu xóa đang chờ xử lý cho bé này.";
            return RedirectToAction(nameof(Index));
        }
        db.DataRequests.Add(new DataRequest
        {
            ParentId = ParentId, ChildProfileId = child, ChildName = Cut(name), RequestType = "Delete", Status = DataRequestStatus.Pending, RequestedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync(cancellationToken);
        await activity.ContentActionAsync(ParentId, "ChildProfile", child, "ChildDataDeleteRequested");
        TempData["Info"] = $"Đã gửi yêu cầu xóa dữ liệu của {name}. Quản trị viên sẽ xử lý trong tối đa 30 ngày; ba mẹ có thể hủy trước khi xử lý.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("privacy/requests/{id:int}/cancel")]
    [EnableRateLimiting("account")]
    public async Task<IActionResult> Cancel(int id, CancellationToken cancellationToken)
    {
        if (Db is not { } db) return View("SqlOnly");
        var request = await db.DataRequests.FirstOrDefaultAsync(item => item.Id == id && item.ParentId == ParentId, cancellationToken);
        if (request is null) return NotFound();
        if (request.Status == DataRequestStatus.Pending)
        {
            request.Status = DataRequestStatus.Rejected;
            request.DecisionNote = "Phụ huynh đã hủy yêu cầu.";
            request.CompletedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
            TempData["Info"] = "Đã hủy yêu cầu xóa.";
        }
        else TempData["Error"] = "Yêu cầu này đã được xử lý, không hủy được nữa.";
        return RedirectToAction(nameof(Index));
    }

    private static string Cut(string name) => name.Length > 60 ? name[..60] : name;
}
