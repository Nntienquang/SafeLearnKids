using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SafeLearnKids.Web.Data;
using SafeLearnKids.Web.Services;

namespace SafeLearnKids.Web.Areas.Parent.Controllers;

[Area("Parent")]
[Authorize(Roles = "Parent")]
public class SettingsController(UserManager<ApplicationUser> users, AvatarStorage avatars) : Controller
{
    [HttpGet("settings")]
    public IActionResult Index() => View();

    [HttpPost("settings/avatar")]
    [EnableRateLimiting("account")]
    [RequestSizeLimit(AvatarStorage.MaxRequestBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = AvatarStorage.MaxRequestBytes)]
    public async Task<IActionResult> Avatar(IFormFile? file, CancellationToken cancellationToken)
    {
        var user = await users.GetUserAsync(User);
        if (user is null) return Challenge();
        if (await avatars.ReplaceAsync(user, file, cancellationToken)) TempData["Info"] = "Đã cập nhật ảnh đại diện.";
        else TempData["Error"] = AvatarStorage.Rejected;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("settings/avatar/remove")]
    [EnableRateLimiting("account")]
    public async Task<IActionResult> RemoveAvatar()
    {
        var user = await users.GetUserAsync(User);
        if (user is null) return Challenge();
        if (await avatars.RemoveAsync(user)) TempData["Info"] = "Đã gỡ ảnh đại diện.";
        else TempData["Error"] = "Chưa thể lưu thay đổi. Vui lòng thử lại.";
        return RedirectToAction(nameof(Index));
    }
}
