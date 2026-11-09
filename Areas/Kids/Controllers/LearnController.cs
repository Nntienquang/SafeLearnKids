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
public class LearnController(KidContext kids, ParentSettings settings, Microsoft.AspNetCore.Identity.UserManager<ApplicationUser> users, IServiceProvider services) : Controller
{
    private const int MaxRequestsPerDay = 3;
    private static readonly int[] RequestChoices = [5, 10, 15, 30];

    private SafeLearnDbContext? Db => services.GetService<SafeLearnDbContext>();

    [HttpGet("kids-picker")]
    public async Task<IActionResult> Picker(CancellationToken cancellationToken)
    {
        // Switching child keeps child mode on; only the PIN screen ends it.
        await KidAsync(cancellationToken);
        return View(await PickerModelAsync(null, null, cancellationToken));
    }

    // US-08: a child with a PIN types it before entering; US-10: a child without consent cannot enter.
    [HttpPost("kids-picker/{id:int}")]
    [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("account")]
    public async Task<IActionResult> Choose(int id, [FromForm] string? pin, CancellationToken cancellationToken)
    {
        if (Db is not { } db) return NotFound();
        var parentId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var child = await db.ChildProfiles.FirstOrDefaultAsync(item => item.Id == id && item.ParentId == parentId && item.IsActive, cancellationToken);
        if (child is null) return NotFound();
        if (!await ChildConsent.Active(db, parentId).AnyAsync(item => item.ChildProfileId == id, cancellationToken))
        {
            Response.StatusCode = StatusCodes.Status400BadRequest;
            return View("Picker", await PickerModelAsync(null, $"Ba mẹ cần đồng ý chính sách dữ liệu cho {child.DisplayName} trước khi bé vào học.", cancellationToken));
        }
        if (child.PinHash is not null)
        {
            if (string.IsNullOrEmpty(pin) && ChildPins.LockedMinutes(child, DateTime.UtcNow) is null)
                return View("Picker", await PickerModelAsync(id, null, cancellationToken));
            var check = ChildPins.Verify(child, pin);
            await db.SaveChangesAsync(cancellationToken);
            if (!check.Ok)
            {
                Response.StatusCode = StatusCodes.Status400BadRequest;
                var error = check.LockedMinutes is { } minutes ? $"Sai quá nhiều lần. Con thử lại sau {minutes} phút nhé." : $"Sai mã PIN. Còn {check.Left} lần thử.";
                return View("Picker", await PickerModelAsync(id, error, cancellationToken));
            }
        }
        return await kids.ChooseAsync(id, cancellationToken) is null ? NotFound() : RedirectToAction(nameof(Home));
    }

    private async Task<KidPickerViewModel> PickerModelAsync(int? ask, string? error, CancellationToken cancellationToken)
    {
        var choices = await kids.ChoicesAsync(cancellationToken);
        if (Db is not { } db) return new KidPickerViewModel { Kids = choices, Ask = ask, Error = error };
        var parentId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var withPin = await db.ChildProfiles.AsNoTracking().Where(item => item.ParentId == parentId && item.IsActive && item.PinHash != null)
            .Select(item => item.Id).ToListAsync(cancellationToken);
        var consented = await ChildConsent.Active(db, parentId).Select(item => item.ChildProfileId!.Value).ToListAsync(cancellationToken);
        return new KidPickerViewModel { Kids = choices, WithPin = withPin.ToHashSet(), Consented = consented.ToHashSet(), Ask = ask, Error = error };
    }

    // US-50: leaving child mode needs the parent PIN, or the account password when no PIN is set.
    [HttpGet("kids-exit")]
    public async Task<IActionResult> Exit(CancellationToken cancellationToken) => View(await ExitModelAsync(null, cancellationToken));

    [HttpPost("kids-exit")]
    [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("account")]
    public async Task<IActionResult> Exit([FromForm] string? pin, [FromForm] string? password, CancellationToken cancellationToken)
    {
        if (await users.GetUserAsync(User) is not { } user) return Challenge();
        var hasPin = (await settings.FindAsync(user.Id, cancellationToken))?.ParentPinHash is not null;
        ParentSettings.PinCheck check;
        if (settings.LockedMinutes(user.Id) is { } locked) check = new(false, 0, locked);
        else if (!string.IsNullOrEmpty(password)) check = settings.Record(user.Id, await users.CheckPasswordAsync(user, password));
        else if (hasPin) check = await settings.CheckPinAsync(user, pin, cancellationToken);
        else check = settings.Record(user.Id, false);
        if (check.Ok)
        {
            kids.Clear();
            return Redirect("/parent-home");
        }
        var error = check.LockedMinutes is { } minutes ? $"Sai quá nhiều lần. Thử lại sau {minutes} phút."
            : !string.IsNullOrEmpty(password) || !hasPin ? $"Mật khẩu chưa đúng. Còn {check.Left} lần thử." : $"Sai PIN. Còn {check.Left} lần thử.";
        Response.StatusCode = StatusCodes.Status400BadRequest;
        return View(await ExitModelAsync(error, cancellationToken));
    }

    private async Task<KidExitViewModel> ExitModelAsync(string? error, CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var kid = await KidAsync(cancellationToken);
        return new KidExitViewModel
        {
            HasPin = (await settings.FindAsync(userId, cancellationToken))?.ParentPinHash is not null,
            Error = error,
            LockedMinutes = settings.LockedMinutes(userId),
            Back = kid is null ? "/kids-picker" : "/kids-home"
        };
    }

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
            Favorites = await Cards(db, kid, openable.Where(lesson => lesson.Favorites.Any(item => item.ChildProfileId == kid.Id))).ToListAsync(cancellationToken),
            Quizzes = (await QuizService.Cards(QuizService.Playable(db, kid).Where(quiz => quiz.Assignments.Any(item => item.ChildProfileId == kid.Id
                    && (item.Status == AssignmentStatus.Assigned || item.Status == AssignmentStatus.InProgress))), kid.Id).ToListAsync(cancellationToken))
                .OrderBy(card => card.DueDate ?? DateOnly.MaxValue).ToList(),
            Streak = await ProgressService.StreakAsync(db, kid.Id, cancellationToken),
            Week = await ProgressService.WeekAsync(db, kid.Id, ProgressService.WeekStart(StudyTimeService.Today), cancellationToken),
            Paths = (await ProgressService.PathsAsync(db, kid.Id, followedOnly: true, cancellationToken)).Where(path => path.Status != PathStatus.Completed).ToList(),
            ReviewCount = (await ProgressService.ReviewAsync(db, kid.Id, cancellationToken)).Count
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
            AssignmentNote = lesson.Note,
            Quizzes = await QuizService.Cards(QuizService.Playable(db, kid).Where(quiz => quiz.LessonId == id), kid.Id).ToListAsync(cancellationToken)
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
        await AfterStudyAsync(db, kid, cancellationToken);
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
        var badges = seconds > 0 ? await ProgressService.AfterStudyAsync(db, kid.Id, ParentId, kid.Name, cancellationToken) : [];
        return Json(new { left = status.RemainingMinutes, allowed = status.Allowed, breakDue, badges });
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
            await NotificationWriter.AddAsync(db, User.FindFirstValue(ClaimTypes.NameIdentifier)!, "ContentReport", $"{kid.Name} báo cáo một bài học",
                $"{title} · {ReportStatus.Reason(reason)}", $"/history?child={kid.Id}", cancellationToken);
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

    // US-38: starts an attempt, or resumes the open one so a reload does not restart the clock.
    [HttpGet("kids-quiz")]
    public async Task<IActionResult> Quiz(int? id, CancellationToken cancellationToken)
    {
        if (Db is not { } db) return View("SqlOnly");
        if (await KidAsync(cancellationToken) is not { } kid) return RedirectToAction(nameof(Picker));
        if (await GateAsync(db, kid, cancellationToken) is { } stop) return stop;
        if (id is null) return RedirectToAction(nameof(Home));
        var quiz = await QuizService.Playable(db, kid).Where(item => item.Id == id)
            .Select(item => new { item.Id, item.Title, item.LessonId, Lesson = item.Lesson.Title, item.TimeLimitSec, item.ShuffleQuestions, item.PassPercent })
            .FirstOrDefaultAsync(cancellationToken);
        if (quiz is null) return NotFound();
        var attempt = await db.QuizAttempts.Where(item => item.QuizId == quiz.Id && item.ChildProfileId == kid.Id && item.SubmittedAt == null)
            .OrderByDescending(item => item.StartedAt).FirstOrDefaultAsync(cancellationToken);
        if (attempt is not null && quiz.TimeLimitSec is { } limit && (DateTime.UtcNow - attempt.StartedAt).TotalSeconds > limit + QuizRules.GraceSeconds)
        {
            // The clock ran out while the child was away: that attempt is closed with what was answered (nothing).
            await QuizService.GradeAsync(db, attempt, quiz.PassPercent, new Dictionary<int, int>(), discard: true, cancellationToken);
            await CompleteAssignmentAsync(db, kid, attempt, quiz.Title, cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
            attempt = null;
        }
        if (attempt is null)
        {
            var assignment = await db.Assignments.Where(item => item.ChildProfileId == kid.Id && item.QuizId == quiz.Id
                    && (item.Status == AssignmentStatus.Assigned || item.Status == AssignmentStatus.InProgress))
                .OrderBy(item => item.AssignedAt).FirstOrDefaultAsync(cancellationToken);
            if (assignment is { Status: AssignmentStatus.Assigned }) assignment.Status = AssignmentStatus.InProgress;
            attempt = new QuizAttempt { QuizId = quiz.Id, ChildProfileId = kid.Id, AssignmentId = assignment?.Id, StartedAt = DateTime.UtcNow };
            db.QuizAttempts.Add(attempt);
            await db.SaveChangesAsync(cancellationToken);
        }
        var due = attempt.AssignmentId is { } assignmentId
            ? await db.Assignments.Where(item => item.Id == assignmentId).Select(item => item.DueDate).FirstOrDefaultAsync(cancellationToken) : null;
        return View(new KidQuizViewModel
        {
            AttemptId = attempt.Id,
            QuizId = quiz.Id,
            Title = quiz.Title,
            LessonId = quiz.LessonId,
            LessonTitle = quiz.Lesson,
            SecondsLeft = QuizService.SecondsLeft(quiz.TimeLimitSec, attempt.StartedAt),
            DueDate = due,
            Questions = await QuizService.QuestionsAsync(db, quiz.Id, attempt.Id, quiz.ShuffleQuestions, cancellationToken)
        });
    }

    // Submitting is not blocked by the study-time gate, so an answer sheet is never lost when the minutes run out mid-quiz.
    [HttpPost("kids-quiz/{attemptId:long}/submit")]
    public async Task<IActionResult> Submit(long attemptId, CancellationToken cancellationToken)
    {
        if (Db is not { } db) return View("SqlOnly");
        if (await KidAsync(cancellationToken) is not { } kid) return RedirectToAction(nameof(Picker));
        var attempt = await db.QuizAttempts.FirstOrDefaultAsync(item => item.Id == attemptId && item.ChildProfileId == kid.Id, cancellationToken);
        if (attempt is null) return NotFound();
        if (attempt.SubmittedAt is not null) return RedirectToAction(nameof(Result), new { id = attemptId });
        var quiz = await db.Quizzes.AsNoTracking().Where(item => item.Id == attempt.QuizId).Select(item => new { item.Title, item.PassPercent, item.TimeLimitSec }).FirstAsync(cancellationToken);
        // Answers arrive as q{questionId}=optionId; GradeAsync ignores options that do not belong to the question.
        var chosen = new Dictionary<int, int>();
        foreach (var (key, value) in Request.Form)
            if (key.Length > 1 && key[0] == 'q' && int.TryParse(key.AsSpan(1), out var questionId) && int.TryParse(value.ToString(), out var optionId))
                chosen[questionId] = optionId;
        var late = quiz.TimeLimitSec is { } limit && (DateTime.UtcNow - attempt.StartedAt).TotalSeconds > limit + QuizRules.GraceSeconds;
        await QuizService.GradeAsync(db, attempt, quiz.PassPercent, chosen, discard: late, cancellationToken);
        await CompleteAssignmentAsync(db, kid, attempt, quiz.Title, cancellationToken);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // A double tap submitted the same sheet a moment earlier; any other failure is not hidden.
            if (!await SubmittedAsync(db, attemptId, cancellationToken)) throw;
            return RedirectToAction(nameof(Result), new { id = attemptId });
        }
        await AfterStudyAsync(db, kid, cancellationToken);
        return RedirectToAction(nameof(Result), new { id = attemptId });
    }

    [HttpPost("kids-quiz/{attemptId:long}/heartbeat")]
    public async Task<IActionResult> QuizHeartbeat(long attemptId, [FromForm] int seconds, CancellationToken cancellationToken)
    {
        if (Db is not { } db) return NoContent();
        if (await KidAsync(cancellationToken) is not { } kid) return Unauthorized();
        if (!await db.QuizAttempts.AnyAsync(item => item.Id == attemptId && item.ChildProfileId == kid.Id && item.SubmittedAt == null, cancellationToken)) return NotFound();
        seconds = Math.Clamp(seconds, 0, StudyTimeService.MaxReportSeconds);
        var breakDue = await StudyTimeService.AddAsync(db, kid.Id, seconds, cancellationToken);
        var status = await StudyTimeService.StatusAsync(db, kid.Id, cancellationToken);
        if (!status.Allowed) await StudyTimeService.EndSessionAsync(db, kid.Id, "TimeUp", cancellationToken);
        var badges = seconds > 0 ? await ProgressService.AfterStudyAsync(db, kid.Id, ParentId, kid.Name, cancellationToken) : [];
        return Json(new { left = status.RemainingMinutes, allowed = status.Allowed, breakDue, badges });
    }

    // US-39: the child's own result with the right answers and explanations.
    [HttpGet("kids-result")]
    public async Task<IActionResult> Result(long? id, CancellationToken cancellationToken)
    {
        if (Db is not { } db) return View("SqlOnly");
        if (await KidAsync(cancellationToken) is not { } kid) return RedirectToAction(nameof(Picker));
        if (id is null || !await db.QuizAttempts.AnyAsync(item => item.Id == id && item.ChildProfileId == kid.Id, cancellationToken)) return RedirectToAction(nameof(Home));
        var result = await QuizService.ResultAsync(db, id.Value, cancellationToken);
        if (result is null) return RedirectToAction(nameof(Home));
        var status = await StudyTimeService.StatusAsync(db, kid.Id, cancellationToken);
        ViewData["KidLeft"] = status.RemainingMinutes;
        var canRetry = status.Allowed && await QuizService.Playable(db, kid).AnyAsync(item => item.Id == result.QuizId, cancellationToken);
        return View(new QuizResultViewModel
        {
            AttemptId = result.AttemptId, QuizId = result.QuizId, Title = result.Title, LessonId = result.LessonId, LessonTitle = result.LessonTitle,
            ChildName = result.ChildName, ChildId = result.ChildId, Score = result.Score, Correct = result.Correct, Total = result.Total, Passed = result.Passed,
            PassPercent = result.PassPercent, SubmittedAt = result.SubmittedAt, DueDate = result.DueDate, Late = result.Late, TimedOut = result.TimedOut,
            CanRetry = canRetry, Questions = result.Questions
        });
    }

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
            await NotificationWriter.AddAsync(db, User.FindFirstValue(ClaimTypes.NameIdentifier)!, "TimeRequest", $"{kid.Name} xin thêm {minutes} phút", reason, $"/time?child={kid.Id}", cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
        }
        TempData["Kid"] = problem ?? "Đã gửi cho ba mẹ. Đợi ba mẹ trả lời nhé!";
        return RedirectToAction(nameof(TimeUp));
    }

    // US-43/44: earned and upcoming badges with how far the child is, plus the streak.
    [HttpGet("kids-badges")]
    public async Task<IActionResult> Badges(CancellationToken cancellationToken)
    {
        if (Db is not { } db) return View("SqlOnly");
        if (await KidAsync(cancellationToken) is not { } kid) return RedirectToAction(nameof(Picker));
        if (await GateAsync(db, kid, cancellationToken) is { } stop) return stop;
        return View(new KidBadgesViewModel
        {
            Kid = kid,
            Badges = await ProgressService.BadgesAsync(db, kid.Id, cancellationToken),
            Streak = await ProgressService.StreakAsync(db, kid.Id, cancellationToken),
            Stars = await ProgressService.StarsAsync(db, kid.Id, cancellationToken)
        });
    }

    // US-46: questions the child got wrong the last time, with the right answer one tap away.
    [HttpGet("kids-review")]
    public async Task<IActionResult> Review(CancellationToken cancellationToken)
    {
        if (Db is not { } db) return View("SqlOnly");
        if (await KidAsync(cancellationToken) is not { } kid) return RedirectToAction(nameof(Picker));
        if (await GateAsync(db, kid, cancellationToken) is { } stop) return stop;
        return View(new KidReviewViewModel { Kid = kid, Questions = await ProgressService.ReviewAsync(db, kid.Id, cancellationToken) });
    }

    // Practice only: the answer is checked on the server and explained, nothing is scored. Retaking the quiz updates the list.
    [HttpPost("kids-review/{questionId:int}")]
    public async Task<IActionResult> CheckReview(int questionId, [FromForm] int? option, CancellationToken cancellationToken)
    {
        if (Db is not { } db) return View("SqlOnly");
        if (await KidAsync(cancellationToken) is not { } kid) return RedirectToAction(nameof(Picker));
        if (await GateAsync(db, kid, cancellationToken) is { } stop) return stop;
        if (!(await ProgressService.ReviewAsync(db, kid.Id, cancellationToken)).Any(item => item.QuestionId == questionId)) return NotFound();
        var right = option is { } id && await db.QuestionOptions.AnyAsync(item => item.Id == id && item.QuestionId == questionId && item.IsCorrect, cancellationToken);
        TempData["ReviewId"] = questionId;
        TempData["ReviewRight"] = right;
        return Redirect(Url.Action(nameof(Review)) + $"#r{questionId}");
    }

    private static async Task<bool> SubmittedAsync(SafeLearnDbContext db, long attemptId, CancellationToken cancellationToken)
    {
        db.ChangeTracker.Clear();
        return await db.QuizAttempts.AnyAsync(item => item.Id == attemptId && item.SubmittedAt != null, cancellationToken);
    }

    private string ParentId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    // Streak, subject progress, paths and badges after the child finished something; new badges are celebrated on the next page.
    private async Task AfterStudyAsync(SafeLearnDbContext db, KidInfo kid, CancellationToken cancellationToken)
    {
        var badges = await ProgressService.AfterStudyAsync(db, kid.Id, ParentId, kid.Name, cancellationToken);
        if (badges.Count > 0) TempData["Badge"] = "Huy hiệu mới: " + string.Join(", ", badges) + "!";
    }

    // The first submitted attempt of an assigned quiz completes the assignment and tells the parent the score.
    private async Task CompleteAssignmentAsync(SafeLearnDbContext db, KidInfo kid, QuizAttempt attempt, string title, CancellationToken cancellationToken)
    {
        if (attempt.AssignmentId is not { } assignmentId) return;
        var assignment = await db.Assignments.FirstOrDefaultAsync(item => item.Id == assignmentId, cancellationToken);
        if (assignment is null || assignment.Status is not (AssignmentStatus.Assigned or AssignmentStatus.InProgress)) return;
        var now = DateTime.UtcNow;
        assignment.Status = AssignmentStatus.Completed;
        assignment.CompletedAt = now < assignment.AssignedAt ? assignment.AssignedAt : now;
        var late = QuizService.Late(assignment.DueDate, now);
        NotificationWriter.Add(db, User.FindFirstValue(ClaimTypes.NameIdentifier)!, "Assignment", $"{kid.Name} đã nộp bài tập",
            $"{title} · {attempt.ScorePercent:0.#}% ({attempt.CorrectCount}/{attempt.TotalCount} câu đúng){(late ? " · nộp muộn" : "")}", $"/quiz-result/{attempt.Id}");
    }

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
                Total = subject.Lessons.Count(lesson => lesson.Status == LessonStatus.Published && lesson.GradeLevelId == kid.GradeLevelId
                    && (lesson.OwnerParentId == null || lesson.ShareStatus == LessonAccess.Approved || lesson.OwnerParentId == kid.ParentId)),
                Done = subject.Lessons.Count(lesson => lesson.Status == LessonStatus.Published && lesson.GradeLevelId == kid.GradeLevelId
                    && (lesson.OwnerParentId == null || lesson.ShareStatus == LessonAccess.Approved || lesson.OwnerParentId == kid.ParentId)
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
