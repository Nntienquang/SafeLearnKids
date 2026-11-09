using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using SafeLearnKids.Web.Data;
using SafeLearnKids.Web.Models;
using SafeLearnKids.Web.Models.Entities;
using SafeLearnKids.Web.Services;

namespace SafeLearnKids.Web.Areas.Admin.Controllers;

// Phase B (lesson requests from parents) and phase C (family lessons shared with the community) for admins.
[Area("Admin")]
[Authorize(Roles = "Admin")]
public class CommunityController(UserManager<ApplicationUser> users, AccountActivityLog activity, IServiceProvider services) : Controller
{
    private const int PageRows = 100;

    private SafeLearnDbContext? Db => services.GetService<SafeLearnDbContext>();
    private string ActorId => users.GetUserId(User)!;

    // ---------- C: community queue ----------

    [HttpGet("admin-community")]
    public async Task<IActionResult> Index(string status = LessonAccess.Pending, CancellationToken cancellationToken = default)
    {
        if (Db is not { } db) return View("SqlOnly");
        status = status is LessonAccess.Approved or LessonAccess.Rejected ? status : LessonAccess.Pending;
        var shared = db.Lessons.AsNoTracking().Where(lesson => lesson.OwnerParentId != null && lesson.ShareStatus != null);
        return View(new CommunityQueueViewModel
        {
            Status = status,
            Counts = await shared.GroupBy(lesson => lesson.ShareStatus!).Select(group => new { group.Key, Count = group.Count() })
                .ToDictionaryAsync(item => item.Key, item => item.Count, cancellationToken),
            Rows = await Rows(shared.Where(lesson => lesson.ShareStatus == status).OrderBy(lesson => lesson.SharedAt).Take(PageRows)).ToListAsync(cancellationToken)
        });
    }

    [HttpGet("admin-community/{id:int}")]
    public async Task<IActionResult> Preview(int id, CancellationToken cancellationToken)
    {
        if (Db is not { } db) return View("SqlOnly");
        var shared = db.Lessons.AsNoTracking().Where(lesson => lesson.Id == id && lesson.OwnerParentId != null && lesson.ShareStatus != null);
        var row = await Rows(shared).FirstOrDefaultAsync(cancellationToken);
        if (row is null) return NotFound();
        var lesson = await shared.Select(item => new
        {
            item.Summary, item.LessonType, item.DurationMin,
            Contents = item.LessonContents.OrderBy(content => content.SortOrder).ThenBy(content => content.Id).Select(content => new
            {
                content.ContentKind, content.Title, content.Body, content.FileUrl, content.FileName,
                VideoId = content.SourceVideo != null && content.SourceVideo.Status == "Approved" ? content.SourceVideo.VideoId : null
            }).ToList()
        }).FirstAsync(cancellationToken);
        return View(new CommunityPreviewViewModel
        {
            Lesson = row,
            Summary = lesson.Summary,
            LessonType = lesson.LessonType,
            DurationMin = lesson.DurationMin,
            Contents = lesson.Contents.Where(content => content.ContentKind != "Video" || (content.VideoId is not null && SourceUrl.EmbedId().IsMatch(content.VideoId)))
                .Select(content => new LibraryContent(content.ContentKind, content.Title, content.Body, content.VideoId, content.FileUrl, content.FileName)).ToList(),
            Questions = await FamilyLessonService.QuestionRows(db.Questions.AsNoTracking().Where(item => item.LessonId == id && item.Status == QuestionStatus.Published))
                .ToListAsync(cancellationToken)
        });
    }

    // approve: everyone may use it. reject: stays with the family, with a reason. withdraw: an approved lesson leaves the community.
    [HttpPost("admin-community/{id:int}")]
    [EnableRateLimiting("account")]
    public async Task<IActionResult> Decide(int id, [FromForm] string? decision, [FromForm] string? note, CancellationToken cancellationToken)
    {
        if (Db is not { } db) return View("SqlOnly");
        var lesson = await db.Lessons.FirstOrDefaultAsync(item => item.Id == id && item.OwnerParentId != null && item.ShareStatus != null, cancellationToken);
        if (lesson is null) return NotFound();
        note = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        string? problem = decision switch
        {
            "approve" when lesson.ShareStatus != LessonAccess.Pending => "Bài này không còn chờ duyệt.",
            "approve" when lesson.Status != LessonStatus.Published => "Gia đình đã đưa bài về nháp, chưa duyệt được.",
            "reject" when lesson.ShareStatus != LessonAccess.Pending => "Bài này không còn chờ duyệt.",
            "withdraw" when lesson.ShareStatus != LessonAccess.Approved => "Chỉ gỡ được bài đang ở kho cộng đồng.",
            "reject" or "withdraw" when note is null => "Ghi lý do để gia đình biết cần sửa gì.",
            "approve" or "reject" or "withdraw" => note is { Length: > 500 } ? "Ghi chú tối đa 500 ký tự." : null,
            _ => "Chọn duyệt, từ chối hoặc gỡ."
        };
        if (problem is not null)
        {
            TempData["Error"] = problem;
            return RedirectToAction(nameof(Preview), new { id });
        }
        var now = DateTime.UtcNow;
        lesson.ShareStatus = decision == "approve" ? LessonAccess.Approved : LessonAccess.Rejected;
        lesson.ShareNote = note;
        lesson.ShareReviewedAt = now;
        if (decision == "approve") lesson.ApprovedById = ActorId;
        var (title, body) = decision switch
        {
            "approve" => ($"Bài \"{lesson.Title}\" đã lên kho cộng đồng", note ?? "Cảm ơn bạn đã chia sẻ. Mọi gia đình giờ đều dùng được bài này."),
            "reject" => ($"Bài \"{lesson.Title}\" chưa được duyệt chia sẻ", note),
            _ => ($"Bài \"{lesson.Title}\" đã được gỡ khỏi kho cộng đồng", note)
        };
        NotificationWriter.Add(db, lesson.OwnerParentId!, "System", FamilyLessonService.Cut(title, 120), body, $"/family-lessons/{id}#share");
        await db.SaveChangesAsync(cancellationToken);
        await activity.ContentActionAsync(ActorId, "Lesson", id, decision switch { "approve" => "CommunityApprove", "reject" => "CommunityReject", _ => "CommunityWithdraw" },
            $"{lesson.Title}{(note is null ? "" : " · " + note)}");
        TempData["Info"] = decision switch
        {
            "approve" => "Đã duyệt. Bài hiện trong Thư viện của mọi gia đình với nhãn Cộng đồng.",
            "reject" => "Đã từ chối và gửi lý do cho gia đình.",
            _ => "Đã gỡ bài khỏi kho cộng đồng."
        };
        return RedirectToAction(nameof(Index));
    }

    // ---------- B: lesson requests ----------

    [HttpGet("admin-lesson-requests")]
    public async Task<IActionResult> Requests(string status = RequestState.Open, CancellationToken cancellationToken = default)
    {
        if (Db is not { } db) return View("SqlOnly");
        status = RequestState.All.Contains(status) ? status : RequestState.Open;
        return View(new AdminLessonRequestsViewModel
        {
            Status = status,
            Counts = await db.LessonRequests.GroupBy(item => item.Status).Select(group => new { group.Key, Count = group.Count() })
                .ToDictionaryAsync(item => item.Key, item => item.Count, cancellationToken),
            // Most wanted first: the requester plus every family that added "we need this too".
            Rows = await db.LessonRequests.AsNoTracking().Where(item => item.Status == status)
                .OrderByDescending(item => item.Votes.Count).ThenBy(item => item.CreatedAt).Take(PageRows)
                .Select(item => new LessonRequestRow(item.Id, item.Subject.Name, item.GradeLevel.Level, item.Topic, item.Note, item.Status, item.Reply, item.LessonId,
                    item.Votes.Count + 1, false, false, item.CreatedAt, item.Parent.Email))
                .ToListAsync(cancellationToken)
        });
    }

    [HttpPost("admin-lesson-requests/{id:int}")]
    [EnableRateLimiting("account")]
    public async Task<IActionResult> Handle(int id, [FromForm] string? status, [FromForm] string? reply, [FromForm] int? lessonId, CancellationToken cancellationToken)
    {
        if (Db is not { } db) return View("SqlOnly");
        var request = await db.LessonRequests.Include(item => item.Votes).FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (request is null) return NotFound();
        reply = string.IsNullOrWhiteSpace(reply) ? null : reply.Trim();
        string? problem = status is not (RequestState.Planned or RequestState.Done or RequestState.Declined) ? "Chọn trạng thái xử lý."
            : reply is { Length: > 500 } ? "Trả lời tối đa 500 ký tự."
            : status == RequestState.Declined && reply is null ? "Ghi lý do để phụ huynh hiểu."
            : status == RequestState.Done && lessonId is null ? "Chọn bài học đã đáp ứng đề xuất." : null;
        if (problem is null && lessonId is { } chosen
            && !await LessonAccess.Public(db.Lessons).AnyAsync(lesson => lesson.Id == chosen && lesson.Status == LessonStatus.Published, cancellationToken))
            problem = "Mã bài không đúng: cần một bài đã xuất bản trong thư viện chung hoặc kho cộng đồng.";
        if (problem is not null)
        {
            TempData["Error"] = problem;
            return RedirectToAction(nameof(Requests), new { status = request.Status });
        }
        request.Status = status!;
        request.Reply = reply;
        request.LessonId = lessonId;
        request.HandledById = ActorId;
        request.HandledAt = DateTime.UtcNow;
        // The family that asked and every family that voted hear back.
        var title = FamilyLessonService.Cut(status switch
        {
            RequestState.Done => $"Đã có bài cho đề xuất \"{request.Topic}\"",
            RequestState.Planned => $"Đề xuất \"{request.Topic}\" đang được soạn",
            _ => $"Đề xuất \"{request.Topic}\" chưa được thực hiện"
        }, 120);
        var link = lessonId is { } done ? $"/lesson-detail/{done}" : "/lesson-requests";
        foreach (var family in request.Votes.Select(vote => vote.ParentId).Append(request.ParentId).Distinct())
            NotificationWriter.Add(db, family, "System", title, reply, link);
        await db.SaveChangesAsync(cancellationToken);
        await activity.ContentActionAsync(ActorId, "LessonRequest", id, "LessonRequestHandle", $"{status} · {request.Topic}");
        TempData["Info"] = $"Đã cập nhật và báo cho {request.Votes.Count + 1} gia đình.";
        return RedirectToAction(nameof(Requests), new { status = RequestState.Open });
    }

    private static IQueryable<CommunityRow> Rows(IQueryable<Lesson> lessons) =>
        lessons.Select(lesson => new CommunityRow(lesson.Id, lesson.Title, lesson.Subject.Name, lesson.GradeLevel.Level,
            lesson.OwnerParent != null ? (lesson.OwnerParent.FullName ?? lesson.OwnerParent.Email ?? "") : "", lesson.ShareStatus, lesson.ShareNote, lesson.SharedAt,
            lesson.LessonContents.Count, lesson.Questions.Count(question => question.Status == QuestionStatus.Published),
            lesson.LessonActivities.Count(item => item.ChildProfile.ParentId != lesson.OwnerParentId)));
}
