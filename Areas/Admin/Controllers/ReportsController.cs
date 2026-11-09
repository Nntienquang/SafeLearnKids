using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SafeLearnKids.Web.Data;
using SafeLearnKids.Web.Models;
using SafeLearnKids.Web.Services;

namespace SafeLearnKids.Web.Areas.Admin.Controllers;

// US-34: admins review reports, then keep the content or take it down; the reporting family hears back.
[Area("Admin")]
[Authorize(Roles = "Admin")]
public class ReportsController(UserManager<ApplicationUser> users, AccountActivityLog activity, IServiceProvider services) : Controller
{
    private SafeLearnDbContext? Db => services.GetService<SafeLearnDbContext>();
    private string ActorId => users.GetUserId(User)!;

    [HttpGet("admin-reports")]
    public async Task<IActionResult> Index(string? status, CancellationToken cancellationToken)
    {
        if (Db is not { } db) return View("SqlOnly");
        status ??= "open";
        // Reports about a family's private lesson stay with that family (they see them in History).
        var query = db.ContentReports.AsNoTracking().Where(item => item.Lesson == null || item.Lesson.OwnerParentId == null || item.Lesson.ShareStatus == LessonAccess.Approved);
        query = status switch
        {
            "open" => query.Where(item => item.Status == ReportStatus.Open || item.Status == ReportStatus.InReview),
            "done" => query.Where(item => item.Status == ReportStatus.Resolved || item.Status == ReportStatus.Dismissed),
            _ => query
        };
        var rows = await query.OrderBy(item => item.Status == ReportStatus.Open ? 0 : item.Status == ReportStatus.InReview ? 1 : 2).ThenByDescending(item => item.CreatedAt).Take(100)
            .Select(item => new
            {
                item.Id, Lesson = item.Lesson != null ? item.Lesson.Title : null, item.LessonId, item.SourceVideoId,
                Video = item.SourceVideo != null ? item.SourceVideo.Title : null, VideoKey = item.SourceVideo != null ? item.SourceVideo.VideoId : null,
                item.Reason, item.Comment, item.Status, item.Resolution, item.CreatedAt, item.RowVersion,
                Child = item.ChildProfile != null ? item.ChildProfile.DisplayName : null,
                Parent = item.ReportedBy != null ? item.ReportedBy.Email : item.ChildProfile != null ? item.ChildProfile.Parent.Email : null,
                HandledBy = item.HandledBy != null ? item.HandledBy.Email : null
            }).ToListAsync(cancellationToken);
        return View(new AdminReportsViewModel
        {
            Status = status,
            Counts = await db.ContentReports.Where(item => item.Lesson == null || item.Lesson.OwnerParentId == null || item.Lesson.ShareStatus == LessonAccess.Approved).GroupBy(item => item.Status).Select(group => new { group.Key, Count = group.Count() })
                .ToDictionaryAsync(item => item.Key, item => item.Count, cancellationToken),
            Rows = rows.Select(item => new AdminReportRow(item.Id, item.Lesson ?? item.Video ?? "Nội dung đã xóa", item.LessonId, item.SourceVideoId, item.VideoKey,
                item.Reason, item.Comment, item.Status, item.Resolution, item.Child is null ? $"Phụ huynh · {item.Parent}" : $"Bé {item.Child} · {item.Parent}",
                item.CreatedAt, item.HandledBy, Convert.ToBase64String(item.RowVersion))).ToList()
        });
    }

    [HttpPost("admin-reports/{id:int}")]
    public async Task<IActionResult> Handle(int id, [FromForm] string? action, [FromForm] string? resolution, [FromForm] string? rowVersion, CancellationToken cancellationToken)
    {
        if (Db is not { } db) return View("SqlOnly");
        var report = await db.ContentReports.Include(item => item.ChildProfile).Include(item => item.Lesson).Include(item => item.SourceVideo)
            .FirstOrDefaultAsync(item => item.Id == id && (item.Lesson == null || item.Lesson.OwnerParentId == null || item.Lesson.ShareStatus == LessonAccess.Approved), cancellationToken);
        if (report is null) return NotFound();
        resolution = string.IsNullOrWhiteSpace(resolution) ? null : resolution.Trim();
        if (report.Status is ReportStatus.Resolved or ReportStatus.Dismissed) return Back("Báo cáo này đã được xử lý.");
        if (action is not ("review" or "keep" or "remove")) return Back("Hành động không hợp lệ.");
        if (action != "review" && resolution is null) return Back("Hãy ghi kết quả xử lý để gia đình hiểu quyết định.");
        if (resolution is { Length: > 500 }) return Back("Kết quả xử lý tối đa 500 ký tự.");

        string? removed = null;
        if (action == "remove")
        {
            // A reported video is blocked everywhere; a lesson without a video is archived.
            if (report.SourceVideo is { } video)
            {
                video.Status = VideoStatus.Blocked;
                video.ReviewNote = "Gỡ theo báo cáo #" + id;
                video.ReviewedById = ActorId;
                video.ReviewedAt = DateTime.UtcNow;
                removed = "video";
            }
            else if (report.Lesson is { OwnerParentId: not null } shared)
            {
                // A community lesson leaves the community library; the family that wrote it keeps it.
                shared.ShareStatus = LessonAccess.Rejected;
                shared.ShareNote = "Gỡ khỏi cộng đồng theo báo cáo: " + resolution;
                if (shared.ShareNote.Length > 500) shared.ShareNote = shared.ShareNote[..500];
                shared.ShareReviewedAt = DateTime.UtcNow;
                removed = "bài cộng đồng";
            }
            else if (report.Lesson is { } lesson)
            {
                lesson.Status = LessonStatus.Archived;
                lesson.PublishedAt = null;
                lesson.UpdatedAt = DateTime.UtcNow;
                removed = "bài học";
            }
        }
        report.Status = action switch { "review" => ReportStatus.InReview, "keep" => ReportStatus.Dismissed, _ => ReportStatus.Resolved };
        report.HandledById = ActorId;
        if (action != "review")
        {
            report.Resolution = resolution;
            report.ResolvedAt = DateTime.UtcNow;
            var family = report.ReportedById ?? report.ChildProfile?.ParentId;
            if (family is not null)
                await NotificationWriter.AddAsync(db, family, "ContentReport", "Báo cáo nội dung đã được xử lý",
                    $"{report.Lesson?.Title ?? report.SourceVideo?.Title}: {resolution}", report.ChildProfileId is { } childId ? $"/history?child={childId}" : null, cancellationToken);
        }
        try
        {
            if (!string.IsNullOrEmpty(rowVersion)) db.Entry(report).Property(item => item.RowVersion).OriginalValue = Convert.FromBase64String(rowVersion);
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is DbUpdateConcurrencyException or FormatException)
        {
            return Back("Báo cáo vừa được người khác xử lý. Trang đã tải lại.");
        }
        await activity.ContentActionAsync(ActorId, "ContentReport", id, "HandleReport", report.Status + (resolution is null ? "" : ": " + resolution));
        TempData["Info"] = action switch
        {
            "review" => "Đã nhận xem báo cáo.",
            "keep" => "Đã giữ nội dung và báo lại cho gia đình.",
            _ => removed is null ? "Đã đóng báo cáo." : $"Đã gỡ {removed} và báo lại cho gia đình."
        };
        return RedirectToAction(nameof(Index));
    }

    private RedirectToActionResult Back(string message)
    {
        TempData["Error"] = message;
        return RedirectToAction(nameof(Index));
    }
}
