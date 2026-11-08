using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using SafeLearnKids.Web.Data;
using SafeLearnKids.Web.Models;
using SafeLearnKids.Web.Services;

namespace SafeLearnKids.Web.Areas.Parent.Controllers;

// US-50: personal settings (theme, which alerts to receive, weekly email). US-51: the parent's own sign-in history.
[Area("Parent")]
[Authorize(Roles = "Parent")]
public class SettingsController(UserManager<ApplicationUser> users, AvatarStorage avatars, ParentSettings settings, IServiceProvider services) : Controller
{
    private const int LoginRows = 15;

    [HttpGet("settings")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var userId = users.GetUserId(User)!;
        var db = services.GetService<SafeLearnDbContext>();
        return View(new SettingsViewModel
        {
            Form = await settings.FormAsync(userId, cancellationToken),
            Logins = db is null ? [] : await db.LoginHistories.AsNoTracking().Where(item => item.UserId == userId)
                .OrderByDescending(item => item.CreatedAt).Take(LoginRows)
                .Select(item => new LoginRow(item.CreatedAt, item.Succeeded, item.FailReason, item.IpAddress, item.UserAgent)).ToListAsync(cancellationToken)
        });
    }

    [HttpPost("settings")]
    [EnableRateLimiting("account")]
    public async Task<IActionResult> Save(SettingsForm form, CancellationToken cancellationToken)
    {
        if (!ThemeChoice.All.Contains(form.Theme)) form.Theme = ThemeChoice.Auto;
        var saved = await settings.SaveAsync(users.GetUserId(User)!, row =>
        {
            row.Theme = form.Theme;
            row.WeeklyEmail = form.WeeklyEmail;
            row.NotifyTimeRequest = form.NotifyTimeRequest;
            row.NotifyContentReport = form.NotifyContentReport;
        }, cancellationToken);
        TempData[saved ? "Info" : "Error"] = saved ? "Đã lưu cài đặt." : "Chưa thể lưu thay đổi. Vui lòng thử lại.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("settings/avatar")]
    [EnableRateLimiting("account")]
    [RequestSizeLimit(AvatarStorage.MaxRequestBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = AvatarStorage.MaxRequestBytes)]
    public async Task<IActionResult> Avatar(IFormFile? file, bool removeAvatar, CancellationToken cancellationToken)
    {
        var user = await users.GetUserAsync(User);
        if (user is null) return Challenge();
        if (removeAvatar && file is null)
        {
            if (await avatars.RemoveAsync(user)) TempData["Info"] = "Đã gỡ ảnh đại diện.";
            else TempData["Error"] = "Chưa thể lưu thay đổi. Vui lòng thử lại.";
        }
        else if (await avatars.ReplaceAsync(user, file, cancellationToken)) TempData["Info"] = "Đã cập nhật ảnh đại diện.";
        else TempData["Error"] = AvatarStorage.Rejected;
        return RedirectToAction(nameof(Index));
    }
}
