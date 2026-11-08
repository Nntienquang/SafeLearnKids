using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SafeLearnKids.Web.Data;
using SafeLearnKids.Web.Models;
using SafeLearnKids.Web.Models.Entities;
using SafeLearnKids.Web.Services;

namespace SafeLearnKids.Web.Areas.Kids.Controllers;

// Child mode (US-23 to US-28). Every action re-reads the chosen child and checks it belongs to the signed-in parent.
[Area("Kids")]
[Authorize(Roles = "Parent")]
public class LearnController(KidContext kids, IServiceProvider services) : Controller
{
    private const int MaxRequestsPerDay = 3;
    private static readonly int[] RequestChoices = [5, 10, 15, 30];

    private SafeLearnDbContext? Db => services.GetService<SafeLearnDbContext>();

    [HttpGet("kids-picker")]
    public async Task<IActionResult> Picker(CancellationToken cancellationToken)
    {
        kids.Clear();
        return View(await kids.ChoicesAsync(cancellationToken));
    }

    [HttpPost("kids-picker/{id:int}")]
    public async Task<IActionResult> Choose(int id, CancellationToken cancellationToken) =>
        await kids.ChooseAsync(id, cancellationToken) is null ? NotFound() : RedirectToAction(nameof(Home));

    [HttpGet("kids-exit")]
    public IActionResult Exit() => View();

    [HttpGet("kids-home")]
    public async Task<IActionResult> Home(CancellationToken cancellationToken)
    {
        if (Db is not { } db) return View("SqlOnly");
        if (await KidAsync(cancellationToken) is not { } kid) return RedirectToAction(nameof(Picker));
        if (await GateAsync(db, kid, cancellationToken) is { } stop) return stop;
        var openable = KidContext.Openable(db, kid);
        var assigned = await Cards(db, kid, openable.Where(lesson => lesson.Assignments.Any(item => item.ChildProfileId == kid.Id
                && (item.Status == AssignmentStatus.Assigned || item.Status == AssignmentStatus.InProgress))))
            .ToListAsync(cancellationToken);
        var continuing = await Cards(db, kid, openable.Where(lesson => lesson.LessonActivities.Any(item => item.ChildProfileId == kid.Id && item.Status == "Started")))
            .ToListAsync(cancellationToken);
        var lastStarted = await db.LessonActivities.AsNoTracking().Where(item => item.ChildProfileId == kid.Id && item.Status == "Started")
            .OrderByDescending(item => item.UpdatedAt).Select(item => item.LessonId).ToListAsync(cancellationToken);
        return View(new KidHomeViewModel
        {
            Kid = kid,
            Continue = lastStarted.Select(id => continuing.FirstOrDefault(card => card.Id == id)).FirstOrDefault(card => card is not null),
            Subjects = await TilesAsync(db, kid, cancellationToken),
            Assigned = assigned.OrderBy(card => card.DueDate ?? DateOnly.MaxValue).ThenBy(card => card.Title).ToList(),
            Favorites = await Cards(db, kid, openable.Where(lesson => lesson.Favorites.Any(item => item.ChildProfileId == kid.Id))).ToListAsync(cancellationToken)
        });
    }

    [HttpGet("kids-subject")]
    public async Task<IActionResult> Subject(string? s, CancellationToken cancellationToken)
    {
        if (Db is not { } db) return View("SqlOnly");
        if (await KidAsync(cancellationToken) is not { } kid) return RedirectToAction(nameof(Picker));
        if (await GateAsync(db, kid, cancellationToken) is { } stop) return stop;
        var tile = (await TilesAsync(db, kid, cancellationToken)).FirstOrDefault(item => item.Key == s);
        if (tile is null) return RedirectToAction(nameof(Home));
        var subjectId = await SubjectIdAsync(db, tile.Key, cancellationToken);
        var lessons = await Cards(db, kid, KidContext.Openable(db, kid).Where(lesson => lesson.SubjectId == subjectId && lesson.GradeLevelId == kid.GradeLevelId))
            .ToListAsync(cancellationToken);
        return View(new KidSubjectViewModel { Kid = kid, Subject = tile, Lessons = lessons });
    }

    [HttpGet("kids-reading")]
    public IActionResult Reading(int? id) => id is null ? RedirectToAction(nameof(Home)) : RedirectToAction(nameof(Lesson), new { id });

    [HttpGet("kids-lesson")]
    public async Task<IActionResult> Lesson(int? id, CancellationToken cancellationToken)
    {
        if (Db is not { } db) return View("SqlOnly");
        if (await KidAsync(cancellationToken) is not { } kid) return RedirectToAction(nameof(Picker));
        if (await GateAsync(db, kid, cancellationToken) is { } stop) return stop;
        if (id is null) return RedirectToAction(nameof(Home));
        var card = await Cards(db, kid, KidContext.Openable(db, kid).Where(lesson => lesson.Id == id)).FirstOrDefaultAsync(cancellationToken);
        if (card is null) return NotFound();
        await StartAsync(db, kid, id.Value, cancellationToken);
        var lesson = await db.Lessons.AsNoTracking().Where(item => item.Id == id).Select(item => new
        {
            item.Summary, item.Subject.Code, item.Subject.Name, item.Subject.ColorKey, item.Subject.IconKey,
            Note = item.Assignments.Where(link => link.ChildProfileId == kid.Id && (link.Status == AssignmentStatus.Assigned || link.Status == AssignmentStatus.InProgress))
                .Select(link => link.Note).FirstOrDefault()
        }).FirstAsync(cancellationToken);
        var sections = await SectionsAsync(db, id.Value, cancellationToken);
        return View(new KidLessonViewModel
        {
            Lesson = card,
            Summary = lesson.Summary,
            Look = SubjectCatalog.Look(lesson.Code, lesson.Name, lesson.ColorKey, lesson.IconKey, true),
            Sections = sections,
            SectionsDone = card.Done ? sections.Count : Math.Min(sections.Count, (int)Math.Round(card.Percent * sections.Count / 100.0)),
            AssignmentNote = lesson.Note
        });
    }

    [HttpPost("kids-lesson/{id:int}/next")]
    public async Task<IActionResult> Next(int id, CancellationToken cancellationToken)
    {
        if (Db is not { } db) return View("SqlOnly");
        if (await KidAsync(cancellationToken) is not { } kid) return RedirectToAction(nameof(Picker));
        if (await GateAsync(db, kid, cancellationToken) is { } stop) return stop;
        if (!await KidContext.Openable(db, kid).AnyAsync(lesson => lesson.Id == id, cancellationToken)) return NotFound();
        var activity = await StartAsync(db, kid, id, cancellationToken);
        // The server counts the sections itself, so a tampered form cannot skip ahead.
        var sections = (await SectionsAsync(db, id, cancellationToken)).Count;
        if (activity.Status != "Completed" && sections > 0)
        {
            // Progress only moves forward, one section per step.
            // Stored percentages are rounded (1 of 3 parts = 33), so round back to whole parts.
            var done = Math.Min(sections, (int)Math.Round(activity.ProgressPercent * sections / 100.0) + 1);
            activity.ProgressPercent = Math.Max(activity.ProgressPercent, Math.Min(100, (int)Math.Round(done * 100.0 / sections)));
            activity.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
        }
        return Redirect(Url.Action(nameof(Lesson), new { id }) + "#current");
    }

    [HttpPost("kids-lesson/{id:int}/complete")]
    public async Task<IActionResult> Complete(int id, CancellationToken cancellationToken)
    {
        if (Db is not { } db) return View("SqlOnly");
        if (await KidAsync(cancellationToken) is not { } kid) return RedirectToAction(nameof(Picker));
        if (await GateAsync(db, kid, cancellationToken) is { } stop) return stop;
        if (!await KidContext.Openable(db, kid).AnyAsync(lesson => lesson.Id == id, cancellationToken)) return NotFound();
        var activity = await StartAsync(db, kid, id, cancellationToken);
        if (activity.Status != "Completed" && activity.ProgressPercent < 100 && (await SectionsAsync(db, id, cancellationToken)).Count > 0)
        {
            TempData["Kid"] = "Con học hết các phần rồi mới bấm hoàn thành nhé.";
            return RedirectToAction(nameof(Lesson), new { id });
        }
        var now = DateTime.UtcNow;
        var first = activity.Status != "Completed";
        if (first)
        {
            activity.Status = "Completed";
            activity.ProgressPercent = 100;
            activity.CompletedAt = now;
            activity.UpdatedAt = now;
        }
        foreach (var assignment in await db.Assignments.Where(item => item.ChildProfileId == kid.Id && item.LessonId == id
                     && (item.Status == AssignmentStatus.Assigned || item.Status == AssignmentStatus.InProgress)).ToListAsync(cancellationToken))
        {
            assignment.Status = AssignmentStatus.Completed;
            assignment.CompletedAt = now < assignment.AssignedAt ? assignment.AssignedAt : now;
        }
        await db.SaveChangesAsync(cancellationToken);
        TempData["Kid"] = first ? "Giỏi lắm! Con đã học xong bài này." : "Con đã học xong bài này rồi.";
        return RedirectToAction(nameof(Lesson), new { id });
    }

    [HttpPost("kids-lesson/{id:int}/favorite")]
    public async Task<IActionResult> Favorite(int id, [FromForm] string? back, CancellationToken cancellationToken)
    {
        if (Db is not { } db) return View("SqlOnly");
        if (await KidAsync(cancellationToken) is not { } kid) return RedirectToAction(nameof(Picker));
        if (await GateAsync(db, kid, cancellationToken) is { } stop) return stop;
        if (!await KidContext.Openable(db, kid).AnyAsync(lesson => lesson.Id == id, cancellationToken)) return NotFound();
        var favorite = await db.Favorites.FirstOrDefaultAsync(item => item.ChildProfileId == kid.Id && item.LessonId == id, cancellationToken);
        if (favorite is null) db.Favorites.Add(new Favorite { ChildProfileId = kid.Id, LessonId = id, FavoritedAt = DateTime.UtcNow });
        else db.Favorites.Remove(favorite);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // A double tap already saved it.
            db.ChangeTracker.Clear();
        }
        return Redirect(Url.IsLocalUrl(back) ? back! : Url.Action(nameof(Lesson), new { id })!);
    }

    [HttpPost("kids-lesson/{id:int}/heartbeat")]
    public async Task<IActionResult> Heartbeat(int id, [FromForm] int seconds, CancellationToken cancellationToken)
    {
        if (Db is not { } db) return NoContent();
        if (await KidAsync(cancellationToken) is not { } kid) return Unauthorized();
        if (!await KidContext.Openable(db, kid).AnyAsync(lesson => lesson.Id == id, cancellationToken)) return NotFound();
        var activity = await StartAsync(db, kid, id, cancellationToken);
        // The page reports at most one minute at a time, so a tampered value cannot inflate study time.
        seconds = Math.Clamp(seconds, 0, StudyTimeService.MaxReportSeconds);
        activity.WatchSeconds += seconds;
        activity.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        var breakDue = await StudyTimeService.AddAsync(db, kid.Id, seconds, cancellationToken);
        var status = await StudyTimeService.StatusAsync(db, kid.Id, cancellationToken);
        if (!status.Allowed) await StudyTimeService.EndSessionAsync(db, kid.Id, "TimeUp", cancellationToken);
        return Json(new { left = status.RemainingMinutes, allowed = status.Allowed, breakDue });
    }

    [HttpPost("kids-lesson/{id:int}/report")]
    public async Task<IActionResult> Report(int id, [FromForm] string? reason, [FromForm] string? comment, CancellationToken cancellationToken)
    {
        if (Db is not { } db) return View("SqlOnly");
        if (await KidAsync(cancellationToken) is not { } kid) return RedirectToAction(nameof(Picker));
        if (!await KidContext.Openable(db, kid).AnyAsync(lesson => lesson.Id == id, cancellationToken)) return NotFound();
        comment = string.IsNullOrWhiteSpace(comment) ? null : comment.Trim();
        if (reason is null || !ReportStatus.Reasons.Contains(reason) || comment is { Length: > 500 })
        {
            TempData["Kid"] = "Con chọn một lý do nhé.";
            return RedirectToAction(nameof(Lesson), new { id });
        }
        // One open report per child and lesson is enough for the admins to look.
        if (!await db.ContentReports.AnyAsync(item => item.ChildProfileId == kid.Id && item.LessonId == id
                && (item.Status == ReportStatus.Open || item.Status == ReportStatus.InReview), cancellationToken))
        {
            db.ContentReports.Add(new ContentReport { ChildProfileId = kid.Id, LessonId = id, Reason = reason, Comment = comment, Status = ReportStatus.Open, CreatedAt = DateTime.UtcNow });
            var title = await db.Lessons.Where(item => item.Id == id).Select(item => item.Title).FirstAsync(cancellationToken);
            NotificationWriter.Add(db, User.FindFirstValue(ClaimTypes.NameIdentifier)!, "ContentReport", $"{kid.Name} báo cáo một bài học",
                $"{title} · {ReportStatus.Reason(reason)}", $"/history?child={kid.Id}");
            await db.SaveChangesAsync(cancellationToken);
        }
        TempData["Kid"] = "Cảm ơn con! Ba mẹ và thầy cô sẽ xem lại bài này.";
        return RedirectToAction(nameof(Lesson), new { id });
    }

    [HttpGet("kids-search")]
    public async Task<IActionResult> Search(string? q, CancellationToken cancellationToken)
    {
        if (Db is not { } db) return View("SqlOnly");
        if (await KidAsync(cancellationToken) is not { } kid) return RedirectToAction(nameof(Picker));
        if (await GateAsync(db, kid, cancellationToken) is { } stop) return stop;
        var term = q?.Trim();
        if (string.IsNullOrEmpty(term)) return View(new KidSearchViewModel());
        if (term.Length > 200) term = term[..200];
        var parentId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var keywords = await db.BlockedKeywords.AsNoTracking().Where(item => item.ParentId == null || item.ParentId == parentId)
            .Select(item => item.Keyword).ToListAsync(cancellationToken);
        var blocked = keywords.Any(keyword => term.Contains(keyword, StringComparison.OrdinalIgnoreCase));
        var results = blocked ? [] : await Cards(db, kid, KidContext.Openable(db, kid).Where(lesson => lesson.Title.Contains(term)
                || (lesson.Summary != null && lesson.Summary.Contains(term))
                || lesson.Subject.Name.Contains(term)
                || lesson.LessonTags.Any(link => link.Tag.Name.Contains(term))))
            .Take(30).ToListAsync(cancellationToken);
        db.SearchLogs.Add(new SearchLog { ChildProfileId = kid.Id, Query = term, ResultCount = results.Count, WasBlocked = blocked, CreatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync(cancellationToken);
        return View(new KidSearchViewModel { Q = term, Blocked = blocked, Results = results });
    }

    [HttpGet("kids-quiz")]
    public IActionResult Quiz() => View();

    [HttpGet("kids-result")]
    public IActionResult Result() => View();

    [HttpGet("kids-time-up")]
    public async Task<IActionResult> TimeUp(CancellationToken cancellationToken)
    {
        if (Db is not { } db) return View("SqlOnly");
        if (await KidAsync(cancellationToken) is not { } kid) return RedirectToAction(nameof(Picker));
        await StudyTimeService.ExpireAsync(db, cancellationToken);
        var today = StudyTimeService.Today;
        var requests = await db.TimeExtensionRequests.AsNoTracking().Where(item => item.ChildProfileId == kid.Id && item.ForDate == today)
            .OrderByDescending(item => item.RequestedAt)
            .Select(item => new ExtensionRow(item.Id, kid.Id, kid.Name, item.RequestedMinutes, item.GrantedMinutes, item.Reason, item.Status, item.RequestedAt, ""))
            .ToListAsync(cancellationToken);
        var status = await StudyTimeService.StatusAsync(db, kid.Id, cancellationToken);
        ViewData["KidLeft"] = status.RemainingMinutes;
        return View(new TimeUpViewModel { Kid = kid, Today = status, Latest = requests.FirstOrDefault(), RequestsToday = requests.Count });
    }

    // US-32: one pending request at a time, a few per day, only when the minutes (not the study window) ran out.
    [HttpPost("kids-time-up/request")]
    public async Task<IActionResult> RequestTime([FromForm] int minutes, [FromForm] string? reason, CancellationToken cancellationToken)
    {
        if (Db is not { } db) return View("SqlOnly");
        if (await KidAsync(cancellationToken) is not { } kid) return RedirectToAction(nameof(Picker));
        var today = StudyTimeService.Today;
        var status = await StudyTimeService.StatusAsync(db, kid.Id, cancellationToken);
        reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
        var asked = await db.TimeExtensionRequests.Where(item => item.ChildProfileId == kid.Id && item.ForDate == today)
            .Select(item => item.Status).ToListAsync(cancellationToken);
        string? problem = !RequestChoices.Contains(minutes) ? "Con chọn số phút muốn xin nhé."
            : reason is { Length: > 200 } ? "Lời nhắn hơi dài, con viết ngắn lại nhé."
            // Extra minutes only help once today's minutes are used up; outside the study window they would not.
            : status.RemainingSeconds > 0 ? "Bây giờ chưa cần xin thêm giờ."
            : asked.Contains(RequestStatus.Pending) ? "Con đã gửi rồi, đợi ba mẹ trả lời nhé."
            : asked.Count >= MaxRequestsPerDay ? "Hôm nay con đã xin đủ số lần rồi. Mai học tiếp nhé." : null;
        if (problem is null)
        {
            db.TimeExtensionRequests.Add(new TimeExtensionRequest
            {
                ChildProfileId = kid.Id, ForDate = today, RequestedMinutes = minutes, Reason = reason, Status = RequestStatus.Pending, RequestedAt = DateTime.UtcNow
            });
            NotificationWriter.Add(db, User.FindFirstValue(ClaimTypes.NameIdentifier)!, "TimeRequest", $"{kid.Name} xin thêm {minutes} phút", reason, $"/time?child={kid.Id}");
            await db.SaveChangesAsync(cancellationToken);
        }
        TempData["Kid"] = problem ?? "Đã gửi cho ba mẹ. Đợi ba mẹ trả lời nhé!";
        return RedirectToAction(nameof(TimeUp));
    }

    [HttpGet("kids-badges")]
    public IActionResult Badges() => View();

    // Every learning page checks today's allowance on the server; the time-up page explains what happened.
    private async Task<IActionResult?> GateAsync(SafeLearnDbContext db, KidInfo kid, CancellationToken cancellationToken)
    {
        var status = await StudyTimeService.StatusAsync(db, kid.Id, cancellationToken);
        ViewData["KidLeft"] = status.RemainingMinutes;
        return status.Allowed ? null : RedirectToAction(nameof(TimeUp));
    }

    private async Task<KidInfo?> KidAsync(CancellationToken cancellationToken)
    {
        var kid = await kids.CurrentAsync(cancellationToken);
        if (kid is not null) ViewData["KidId"] = kid.Id;
        return kid;
    }

    private static IQueryable<KidLessonCard> Cards(SafeLearnDbContext db, KidInfo kid, IQueryable<Lesson> lessons) =>
        lessons.OrderBy(lesson => lesson.Subject.SortOrder).ThenBy(lesson => lesson.SortOrder).ThenBy(lesson => lesson.Id).Select(lesson => new KidLessonCard(
            lesson.Id, lesson.Title, lesson.Subject.Code, lesson.Subject.Name, lesson.Subject.ColorKey, lesson.LessonType, lesson.DurationMin,
            lesson.LessonActivities.Where(item => item.ChildProfileId == kid.Id).Select(item => item.ProgressPercent).FirstOrDefault(),
            lesson.LessonActivities.Any(item => item.ChildProfileId == kid.Id && item.Status == "Completed"),
            lesson.Favorites.Any(item => item.ChildProfileId == kid.Id),
            lesson.Assignments.Where(item => item.ChildProfileId == kid.Id && (item.Status == AssignmentStatus.Assigned || item.Status == AssignmentStatus.InProgress))
                .Select(item => item.DueDate).FirstOrDefault()));

    private static async Task<IReadOnlyList<KidSubjectTile>> TilesAsync(SafeLearnDbContext db, KidInfo kid, CancellationToken cancellationToken)
    {
        var rows = await db.Subjects.AsNoTracking().Where(subject => subject.IsActive && kid.SubjectIds.Contains(subject.Id))
            .OrderBy(subject => subject.SortOrder).ThenBy(subject => subject.Name)
            .Select(subject => new
            {
                subject.Code, subject.Name, subject.ColorKey, subject.IconKey,
                Total = subject.Lessons.Count(lesson => lesson.Status == LessonStatus.Published && lesson.GradeLevelId == kid.GradeLevelId),
                Done = subject.Lessons.Count(lesson => lesson.Status == LessonStatus.Published && lesson.GradeLevelId == kid.GradeLevelId
                    && lesson.LessonActivities.Any(item => item.ChildProfileId == kid.Id && item.Status == "Completed"))
            }).ToListAsync(cancellationToken);
        return rows.Select(row =>
        {
            var look = SubjectCatalog.Look(row.Code, row.Name, row.ColorKey, row.IconKey, true);
            return new KidSubjectTile(look.Key, row.Name, look.Icon, look.C, row.Done, row.Total);
        }).ToList();
    }

    private static async Task<int> SubjectIdAsync(SafeLearnDbContext db, string key, CancellationToken cancellationToken)
    {
        var codes = await db.Subjects.AsNoTracking().Select(subject => new { subject.Id, subject.Code }).ToListAsync(cancellationToken);
        return codes.First(item => SubjectCatalog.Key(item.Code) == key).Id;
    }

    // The parts a child sees, in order. Same video rule as the parent library: approved, from a still-approved source,
    // not from a source this family blocked, and a plain id that is safe to put in the embed URL.
    private async Task<List<LibraryContent>> SectionsAsync(SafeLearnDbContext db, int lessonId, CancellationToken cancellationToken)
    {
        var parentId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var contents = await db.LessonContents.AsNoTracking().Where(content => content.LessonId == lessonId)
            .OrderBy(content => content.SortOrder).ThenBy(content => content.Id).Select(content => new
            {
                content.ContentKind, content.Title, content.Body, content.FileUrl, content.FileName,
                VideoId = content.SourceVideo != null && content.SourceVideo.Status == "Approved" && content.SourceVideo.Platform == "YouTube"
                    && (!content.SourceVideo.SourceVideoSources.Any() || content.SourceVideo.SourceVideoSources.Any(link => link.Source.Status == "Approved"))
                    && !content.SourceVideo.SourceVideoSources.Any(link => link.Source.ParentSources.Any(choice => choice.ParentId == parentId && choice.Status == "Blocked"))
                    ? content.SourceVideo.VideoId : null
            }).ToListAsync(cancellationToken);
        return contents.Where(content => content.ContentKind != "Video" || (content.VideoId is not null && SourceUrl.EmbedId().IsMatch(content.VideoId)))
            .Select(content => new LibraryContent(content.ContentKind, content.Title, content.Body, content.VideoId, content.FileUrl, content.FileName)).ToList();
    }

    // The first visit records the lesson as started and moves a waiting assignment to in progress.
    private static async Task<LessonActivity> StartAsync(SafeLearnDbContext db, KidInfo kid, int lessonId, CancellationToken cancellationToken)
    {
        var activity = await db.LessonActivities.FirstOrDefaultAsync(item => item.ChildProfileId == kid.Id && item.LessonId == lessonId, cancellationToken);
        if (activity is not null) return activity;
        var now = DateTime.UtcNow;
        activity = new LessonActivity { ChildProfileId = kid.Id, LessonId = lessonId, Status = "Started", StartedAt = now, UpdatedAt = now };
        db.LessonActivities.Add(activity);
        foreach (var assignment in await db.Assignments.Where(item => item.ChildProfileId == kid.Id && item.LessonId == lessonId && item.Status == AssignmentStatus.Assigned)
                     .ToListAsync(cancellationToken))
            assignment.Status = AssignmentStatus.InProgress;
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Two tabs opened the lesson at once; the other request created the row.
            db.ChangeTracker.Clear();
            activity = await db.LessonActivities.FirstAsync(item => item.ChildProfileId == kid.Id && item.LessonId == lessonId, cancellationToken);
        }
        return activity;
    }
}
