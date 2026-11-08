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

// Parents only ever see published lessons of active subjects; drafts stay with the admins.
[Area("Parent")]
[Authorize(Roles = "Parent")]
public class LibraryController(IServiceProvider services) : Controller
{
    private const int PageSize = 12;
    private const int MaxDueDays = 366;

    private SafeLearnDbContext? Db => services.GetService<SafeLearnDbContext>();

    [HttpGet("library")]
    public async Task<IActionResult> Index(string? subject, int? grade, string? type, int? tag, string? q, int page = 1, CancellationToken cancellationToken = default)
    {
        if (Db is not { } db) return View("SqlOnly");
        var parentId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var looks = (await SubjectCatalog.LooksAsync(db, cancellationToken)).Where(look => look.Active).ToList();
        var lessons = Visible(db);
        var codes = await db.Subjects.Where(item => item.IsActive).Select(item => item.Code).ToListAsync(cancellationToken);
        var code = codes.FirstOrDefault(item => SubjectCatalog.Key(item) == subject);
        if (code is not null) lessons = lessons.Where(lesson => lesson.Subject.Code == code);
        if (grade is not null) lessons = lessons.Where(lesson => lesson.GradeLevel.Level == grade);
        if (!string.IsNullOrEmpty(type)) lessons = lessons.Where(lesson => lesson.LessonType == type);
        if (tag is not null) lessons = lessons.Where(lesson => lesson.LessonTags.Any(item => item.TagId == tag));
        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            lessons = lessons.Where(lesson => lesson.Title.Contains(term) || (lesson.Summary != null && lesson.Summary.Contains(term))
                || lesson.LessonTags.Any(item => item.Tag.Name.Contains(term)));
        }
        var matching = await lessons.CountAsync(cancellationToken);
        var totalPages = Math.Max(1, (int)Math.Ceiling(matching / (double)PageSize));
        page = Math.Clamp(page, 1, totalPages);
        var rows = await lessons
            .OrderBy(lesson => lesson.Subject.SortOrder).ThenBy(lesson => lesson.GradeLevel.Level).ThenBy(lesson => lesson.SortOrder).ThenBy(lesson => lesson.Id)
            .Skip((page - 1) * PageSize).Take(PageSize)
            .Select(lesson => new
            {
                lesson.Id, lesson.Title, lesson.Subject.Code, Subject = lesson.Subject.Name, lesson.GradeLevel.Level, lesson.LessonType, lesson.DurationMin,
                Tags = lesson.LessonTags.OrderBy(item => item.Tag.Name).Select(item => item.Tag.Name).ToList(),
                AssignedTo = lesson.Assignments.Count(item => item.ChildProfile.ParentId == parentId && item.ChildProfile.IsActive
                    && (item.Status == AssignmentStatus.Assigned || item.Status == AssignmentStatus.InProgress))
            })
            .ToListAsync(cancellationToken);
        var tags = await db.Tags.AsNoTracking()
            .Where(item => item.LessonTags.Any(link => link.Lesson.Status == LessonStatus.Published && link.Lesson.Subject.IsActive))
            .OrderBy(item => item.Name).Select(item => new TagOption(item.Id, item.Name)).ToListAsync(cancellationToken);
        return View(new LibraryViewModel
        {
            Subject = code is null ? null : subject,
            Grade = grade,
            Type = type,
            Tag = tag,
            Q = q,
            Page = page,
            TotalPages = totalPages,
            Matching = matching,
            Subjects = looks,
            Tags = tags,
            Cards = rows.Select(row => new LibraryCard(row.Id, row.Title, SubjectCatalog.Key(row.Code), row.Subject, row.Level, row.LessonType, row.DurationMin, row.Tags, row.AssignedTo)).ToList()
        });
    }

    [HttpGet("lesson-detail")]
    public IActionResult LegacyDetails() => RedirectToAction(nameof(Index));

    [HttpGet("lesson-detail/{id:int}")]
    public async Task<IActionResult> Details(int id, CancellationToken cancellationToken)
    {
        if (Db is not { } db) return View("SqlOnly");
        var parentId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var lesson = await Visible(db).Where(item => item.Id == id).Select(item => new
        {
            item.Id, item.Title, item.Summary, item.Subject.Code, Subject = item.Subject.Name, item.Subject.ColorKey, item.Subject.IconKey,
            item.GradeLevel.Level, item.LessonType, item.DurationMin,
            Tags = item.LessonTags.OrderBy(link => link.Tag.Name).Select(link => link.Tag.Name).ToList(),
            Contents = item.LessonContents.OrderBy(content => content.SortOrder).ThenBy(content => content.Id).Select(content => new
            {
                content.ContentKind, content.Title, content.Body, content.FileUrl, content.FileName,
                // Playable: approved, from a still-approved source, and not from a source this family blocked.
                VideoId = content.SourceVideo != null && content.SourceVideo.Status == "Approved" && content.SourceVideo.Platform == "YouTube"
                    && (!content.SourceVideo.SourceVideoSources.Any() || content.SourceVideo.SourceVideoSources.Any(link => link.Source.Status == "Approved"))
                    && !content.SourceVideo.SourceVideoSources.Any(link => link.Source.ParentSources.Any(choice => choice.ParentId == parentId && choice.Status == "Blocked"))
                    ? content.SourceVideo.VideoId : null
            }).ToList()
        }).FirstOrDefaultAsync(cancellationToken);
        if (lesson is null) return NotFound();
        return View(new LibraryDetailViewModel
        {
            Lesson = new LibraryCard(lesson.Id, lesson.Title, SubjectCatalog.Key(lesson.Code), lesson.Subject, lesson.Level, lesson.LessonType, lesson.DurationMin, lesson.Tags),
            Summary = lesson.Summary,
            Look = SubjectCatalog.Look(lesson.Code, lesson.Subject, lesson.ColorKey, lesson.IconKey, true),
            // A video that is no longer approved, or an id that is not a plain YouTube id, is never embedded.
            Children = await ChildrenAsync(db, parentId!, id, cancellationToken),
            Contents = lesson.Contents
                .Where(content => content.ContentKind != "Video" || (content.VideoId is not null && SourceUrl.EmbedId().IsMatch(content.VideoId)))
                .Select(content => new LibraryContent(content.ContentKind, content.Title, content.Body, content.VideoId, content.FileUrl, content.FileName)).ToList()
        });
    }

    // US-24: one active assignment per child and lesson (the filtered unique index on dbo.Assignments enforces it).
    [HttpPost("lesson-detail/{id:int}/assign")]
    [EnableRateLimiting("account")]
    public async Task<IActionResult> Assign(int id, [FromForm] List<int> childIds, [FromForm] DateOnly? dueDate, [FromForm] string? note, CancellationToken cancellationToken)
    {
        if (Db is not { } db) return View("SqlOnly");
        var parentId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        if (!await Visible(db).AnyAsync(lesson => lesson.Id == id, cancellationToken)) return NotFound();
        var today = DateOnly.FromDateTime(DateTime.Now);
        note = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        string? problem = childIds.Count == 0 ? "Chọn ít nhất 1 bé để giao bài."
            : dueDate is { } due && (due < today || due > today.AddDays(MaxDueDays)) ? "Hạn hoàn thành phải từ hôm nay đến trong vòng 1 năm."
            : note is { Length: > 300 } ? "Lời nhắn tối đa 300 ký tự." : null;
        if (problem is not null)
        {
            TempData["Error"] = problem;
            return RedirectToAction(nameof(Details), new { id });
        }
        var mine = await db.ChildProfiles.Where(child => child.ParentId == parentId && child.IsActive && childIds.Contains(child.Id))
            .Select(child => child.Id).ToListAsync(cancellationToken);
        if (mine.Count != childIds.Distinct().Count()) return NotFound();
        var busy = await db.Assignments.Where(item => item.LessonId == id && mine.Contains(item.ChildProfileId)
                && (item.Status == AssignmentStatus.Assigned || item.Status == AssignmentStatus.InProgress))
            .Select(item => item.ChildProfileId).ToListAsync(cancellationToken);
        var now = DateTime.UtcNow;
        foreach (var childId in mine.Except(busy))
            db.Assignments.Add(new Assignment { ChildProfileId = childId, LessonId = id, AssignedById = parentId, DueDate = dueDate, Note = note, Status = AssignmentStatus.Assigned, AssignedAt = now });
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // A second tap assigned the same lesson a moment earlier.
            db.ChangeTracker.Clear();
        }
        var added = mine.Count - busy.Count;
        TempData[added > 0 ? "Info" : "Error"] = added > 0
            ? $"Đã giao bài cho {added} bé" + (busy.Count > 0 ? $"; {busy.Count} bé đang có bài này nên giữ nguyên." : ".")
            : "Các bé đã chọn đang có bài này rồi.";
        return RedirectToAction(nameof(Details), new { id });
    }

    // US-33: a parent reports a lesson; one open report per parent and lesson.
    [HttpPost("lesson-detail/{id:int}/report")]
    [EnableRateLimiting("account")]
    public async Task<IActionResult> Report(int id, [FromForm] string? reason, [FromForm] string? comment, CancellationToken cancellationToken)
    {
        if (Db is not { } db) return View("SqlOnly");
        var parentId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        if (!await Visible(db).AnyAsync(lesson => lesson.Id == id, cancellationToken)) return NotFound();
        comment = string.IsNullOrWhiteSpace(comment) ? null : comment.Trim();
        if (reason is null || !ReportStatus.Reasons.Contains(reason) || comment is { Length: > 500 })
        {
            TempData["Error"] = "Hãy chọn lý do báo cáo (ghi chú tối đa 500 ký tự).";
            return RedirectToAction(nameof(Details), new { id });
        }
        if (!await db.ContentReports.AnyAsync(item => item.ReportedById == parentId && item.LessonId == id
                && (item.Status == ReportStatus.Open || item.Status == ReportStatus.InReview), cancellationToken))
        {
            db.ContentReports.Add(new ContentReport { ReportedById = parentId, LessonId = id, Reason = reason, Comment = comment, Status = ReportStatus.Open, CreatedAt = DateTime.UtcNow });
            await db.SaveChangesAsync(cancellationToken);
        }
        TempData["Info"] = "Đã gửi báo cáo. Quản trị viên sẽ xem và báo lại kết quả.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost("assignments/{assignmentId:int}/cancel")]
    [EnableRateLimiting("account")]
    public async Task<IActionResult> CancelAssignment(int assignmentId, CancellationToken cancellationToken)
    {
        if (Db is not { } db) return View("SqlOnly");
        var parentId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var assignment = await db.Assignments.FirstOrDefaultAsync(item => item.Id == assignmentId && item.ChildProfile.ParentId == parentId, cancellationToken);
        if (assignment is null) return NotFound();
        if (assignment.Status is AssignmentStatus.Assigned or AssignmentStatus.InProgress)
        {
            assignment.Status = AssignmentStatus.Cancelled;
            await db.SaveChangesAsync(cancellationToken);
            TempData["Info"] = "Đã hủy bài đã giao. Tiến độ bé đã học vẫn được giữ.";
        }
        return assignment.LessonId is { } lessonId ? RedirectToAction(nameof(Details), new { id = lessonId }) : RedirectToAction(nameof(Index));
    }

    private static async Task<IReadOnlyList<LessonAssignmentRow>> ChildrenAsync(SafeLearnDbContext db, string parentId, int lessonId, CancellationToken cancellationToken)
    {
        var rows = await db.ChildProfiles.AsNoTracking().Where(child => child.ParentId == parentId && child.IsActive)
            .OrderBy(child => child.CreatedAt).ThenBy(child => child.Id)
            .Select(child => new
            {
                child.Id, child.DisplayName, child.GradeLevel.Level,
                Latest = child.Assignments.Where(item => item.LessonId == lessonId && item.Status != AssignmentStatus.Cancelled)
                    .OrderByDescending(item => item.AssignedAt).Select(item => new { item.Id, item.Status, item.DueDate, item.Note }).FirstOrDefault()
            }).ToListAsync(cancellationToken);
        return rows.Select(row => new LessonAssignmentRow(row.Latest?.Id, row.Id, row.DisplayName, row.Level, row.Latest?.Status, row.Latest?.DueDate, row.Latest?.Note)).ToList();
    }

    private static IQueryable<Lesson> Visible(SafeLearnDbContext db) =>
        db.Lessons.AsNoTracking().Where(lesson => lesson.Status == LessonStatus.Published && lesson.Subject.IsActive);

}
