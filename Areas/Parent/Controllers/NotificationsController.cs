using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SafeLearnKids.Web.Data;
using SafeLearnKids.Web.Models;

namespace SafeLearnKids.Web.Areas.Parent.Controllers;

// US-50: the parent's notifications (time requests, reports, badges, quizzes handed in, weekly reports).
[Area("Parent")]
[Authorize(Roles = "Parent")]
public class NotificationsController(IServiceProvider services) : Controller
{
    private const int PageSize = 20;

    private SafeLearnDbContext? Db => services.GetService<SafeLearnDbContext>();
    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    [HttpGet("notifications")]
    public async Task<IActionResult> Index(bool unread = false, int page = 1, CancellationToken cancellationToken = default)
    {
        if (Db is not { } db) return View("SqlOnly");
        var rows = db.Notifications.AsNoTracking().Where(item => item.UserId == UserId);
        if (unread) rows = rows.Where(item => !item.IsRead);
        var total = await rows.CountAsync(cancellationToken);
        var pages = Math.Max(1, (int)Math.Ceiling(total / (double)PageSize));
        page = Math.Clamp(page, 1, pages);
        return View(new NotificationsViewModel
        {
            UnreadOnly = unread,
            Page = page,
            TotalPages = pages,
            Unread = await db.Notifications.CountAsync(item => item.UserId == UserId && !item.IsRead, cancellationToken),
            Rows = await rows.OrderByDescending(item => item.CreatedAt).ThenByDescending(item => item.Id).Skip((page - 1) * PageSize).Take(PageSize)
                .Select(item => new NotificationRow(item.Id, item.NotificationType, item.Title, item.Body, item.LinkUrl, item.IsRead, item.CreatedAt))
                .ToListAsync(cancellationToken)
        });
    }

    // Opening a notification marks it read and follows its link (only links inside this site).
    [HttpPost("notifications/{id:int}/open")]
    public async Task<IActionResult> Open(int id, CancellationToken cancellationToken)
    {
        if (Db is not { } db) return View("SqlOnly");
        var row = await db.Notifications.FirstOrDefaultAsync(item => item.Id == id && item.UserId == UserId, cancellationToken);
        if (row is null) return NotFound();
        if (!row.IsRead)
        {
            row.IsRead = true;
            await db.SaveChangesAsync(cancellationToken);
        }
        return Url.IsLocalUrl(row.LinkUrl) ? Redirect(row.LinkUrl!) : RedirectToAction(nameof(Index));
    }

    [HttpPost("notifications/read-all")]
    public async Task<IActionResult> ReadAll(CancellationToken cancellationToken)
    {
        if (Db is not { } db) return View("SqlOnly");
        var changed = await db.Notifications.Where(item => item.UserId == UserId && !item.IsRead)
            .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.IsRead, true), cancellationToken);
        TempData["Info"] = changed > 0 ? $"Đã đánh dấu {changed} thông báo là đã đọc." : "Không còn thông báo chưa đọc.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("notifications/{id:int}/delete")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        if (Db is not { } db) return View("SqlOnly");
        var removed = await db.Notifications.Where(item => item.Id == id && item.UserId == UserId).ExecuteDeleteAsync(cancellationToken);
        return removed == 0 ? NotFound() : RedirectToAction(nameof(Index));
    }
}
