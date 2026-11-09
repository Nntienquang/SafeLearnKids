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

// Phase A: a family writes its own lessons (reading, approved videos, images, PDFs, questions) for its own children.
// Only that family sees them until it shares one and an admin approves it (phase C).
[Area("Parent")]
[Authorize(Roles = "Parent")]
public class FamilyLessonsController(AccountActivityLog activity, LessonFileStorage files, IServiceProvider services) : Controller
{
    private const string SaveFailed = "Chưa thể lưu thay đổi. Vui lòng thử lại.";

    private SafeLearnDbContext? Db => services.GetService<SafeLearnDbContext>();
    private string ParentId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    [HttpGet("family-lessons")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        if (Db is not { } db) return View("SqlOnly");
        return View(new FamilyLessonsViewModel
        {
            Rows = await FamilyLessonService.Own(db, ParentId).AsNoTracking().OrderBy(lesson => lesson.Status == LessonStatus.Archived).ThenByDescending(lesson => lesson.UpdatedAt)
                .Select(lesson => new FamilyLessonRow(lesson.Id, lesson.Title, lesson.Subject.Name, lesson.GradeLevel.Level, lesson.Status, lesson.ShareStatus,
                    lesson.LessonContents.Count, lesson.Questions.Count(question => question.Status == QuestionStatus.Published),
                    lesson.LessonActivities.Count, lesson.UpdatedAt))
                .ToListAsync(cancellationToken)
        });
    }

    [HttpGet("family-lessons/new")]
    public async Task<IActionResult> Create(CancellationToken cancellationToken)
    {
        if (Db is not { } db) return View("SqlOnly");
        var subjects = await SubjectsAsync(db, cancellationToken);
        return View("Editor", new FamilyEditorViewModel { Form = new FamilyLessonForm { SubjectId = subjects.FirstOrDefault()?.Id ?? 0 }, Subjects = subjects });
    }

    [HttpPost("family-lessons/new")]
    [EnableRateLimiting("account")]
    public async Task<IActionResult> Create(FamilyLessonForm form, CancellationToken cancellationToken)
    {
        if (Db is not { } db) return View("SqlOnly");
        if (await FamilyLessonService.Own(db, ParentId).CountAsync(lesson => lesson.Status != LessonStatus.Archived, cancellationToken) >= FamilyRules.MaxLessons)
            ModelState.AddModelError("", $"Mỗi gia đình có tối đa {FamilyRules.MaxLessons} bài. Hãy ẩn hoặc xóa bớt bài cũ.");
        form.Status = LessonStatus.Draft;
        var gradeId = await ValidateAsync(db, form, cancellationToken);
        if (!ModelState.IsValid || gradeId is null) return View("Editor", new FamilyEditorViewModel { Form = form, Subjects = await SubjectsAsync(db, cancellationToken) });
        var now = DateTime.UtcNow;
        var lesson = new Lesson
        {
            OwnerParentId = ParentId, CreatedById = ParentId, CreatedAt = now, UpdatedAt = now, SortOrder = 0,
            Title = form.Title, Summary = form.Summary, SubjectId = form.SubjectId, GradeLevelId = gradeId.Value, LessonType = form.LessonType,
            DurationMin = form.DurationMin, Status = LessonStatus.Draft
        };
        db.Lessons.Add(lesson);
        if (!await SaveAsync(db, cancellationToken))
        {
            ModelState.AddModelError("", SaveFailed);
            return View("Editor", new FamilyEditorViewModel { Form = form, Subjects = await SubjectsAsync(db, cancellationToken) });
        }
        await activity.ContentActionAsync(ParentId, "Lesson", lesson.Id, "FamilyLessonCreate", lesson.Title);
        TempData["Info"] = "Đã tạo bài. Thêm nội dung rồi chuyển sang \"Bé học được\" để bé thấy bài.";
        return RedirectToAction(nameof(Edit), new { id = lesson.Id });
    }

    [HttpGet("family-lessons/{id:int}")]
    public async Task<IActionResult> Edit(int id, CancellationToken cancellationToken)
    {
        if (Db is not { } db) return View("SqlOnly");
        var model = await EditorAsync(db, id, null, cancellationToken);
        return model is null ? NotFound() : View("Editor", model);
    }

    [HttpPost("family-lessons/{id:int}")]
    [EnableRateLimiting("account")]
    public async Task<IActionResult> Edit(int id, FamilyLessonForm form, CancellationToken cancellationToken)
    {
        if (Db is not { } db) return View("SqlOnly");
        var lesson = await FamilyLessonService.Own(db, ParentId).FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (lesson is null) return NotFound();
        if (!(form.Status is LessonStatus.Draft or LessonStatus.Published)) ModelState.AddModelError(nameof(form.Status), "Chọn trạng thái.");
        var gradeId = await ValidateAsync(db, form, cancellationToken);
        if (form.Status == LessonStatus.Published && !await db.LessonContents.AnyAsync(item => item.LessonId == id, cancellationToken))
            ModelState.AddModelError(nameof(form.Status), "Thêm ít nhất 1 nội dung trước khi cho bé học.");
        if (!ModelState.IsValid || gradeId is null) return View("Editor", await EditorAsync(db, id, form, cancellationToken));
        var now = DateTime.UtcNow;
        lesson.Title = form.Title;
        lesson.Summary = form.Summary;
        lesson.SubjectId = form.SubjectId;
        lesson.GradeLevelId = gradeId.Value;
        lesson.LessonType = form.LessonType;
        lesson.DurationMin = form.DurationMin;
        if (form.Status == LessonStatus.Published && lesson.Status != LessonStatus.Published) lesson.PublishedAt = now;
        lesson.Status = form.Status;
        lesson.UpdatedAt = now;
        var review = FamilyLessonService.NeedsReview(lesson);
        await FamilyLessonService.SyncQuizAsync(db, lesson, cancellationToken);
        if (!await SaveAsync(db, cancellationToken)) return Back(id, SaveFailed);
        await activity.ContentActionAsync(ParentId, "Lesson", id, "FamilyLessonUpdate", $"{lesson.Status} · {lesson.Title}");
        TempData["Info"] = "Đã lưu bài." + (review ? " Bài đã chia sẻ được đưa về chờ duyệt lại." : "");
        return RedirectToAction(nameof(Edit), new { id });
    }

    // Lessons children never touched are deleted; used ones are hidden (archived) so progress and results stay.
    [HttpPost("family-lessons/{id:int}/delete")]
    [EnableRateLimiting("account")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        if (Db is not { } db) return View("SqlOnly");
        var lesson = await FamilyLessonService.Own(db, ParentId).Include(item => item.LessonContents).FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (lesson is null) return NotFound();
        var used = await db.LessonActivities.AnyAsync(item => item.LessonId == id, cancellationToken)
            || await db.Assignments.AnyAsync(item => item.LessonId == id || (item.Quiz != null && item.Quiz.LessonId == id), cancellationToken)
            || await db.QuizAttempts.AnyAsync(item => item.Quiz.LessonId == id, cancellationToken)
            || await db.Favorites.AnyAsync(item => item.LessonId == id, cancellationToken)
            || await db.ContentReports.AnyAsync(item => item.LessonId == id, cancellationToken)
            || await db.LearningPathItems.AnyAsync(item => item.LessonId == id, cancellationToken);
        if (used)
        {
            lesson.Status = LessonStatus.Archived;
            lesson.ShareStatus = null;
            lesson.UpdatedAt = DateTime.UtcNow;
            if (!await SaveAsync(db, cancellationToken)) return Back(id, SaveFailed);
            await activity.ContentActionAsync(ParentId, "Lesson", id, "FamilyLessonArchive", lesson.Title);
            TempData["Info"] = "Bé đã học hoặc làm bài này nên bài được ẩn (không xóa) để giữ tiến độ và điểm.";
            return RedirectToAction(nameof(Index));
        }
        var urls = lesson.LessonContents.Select(item => item.FileUrl).ToList();
        var quizzes = await db.Quizzes.Include(item => item.QuizQuestions).Where(item => item.LessonId == id).ToListAsync(cancellationToken);
        db.QuizQuestions.RemoveRange(quizzes.SelectMany(item => item.QuizQuestions));
        db.Quizzes.RemoveRange(quizzes);
        db.Questions.RemoveRange(await db.Questions.Include(item => item.QuestionOptions).Where(item => item.LessonId == id).ToListAsync(cancellationToken));
        db.LessonContents.RemoveRange(lesson.LessonContents);
        db.Lessons.Remove(lesson);
        if (!await SaveAsync(db, cancellationToken)) return Back(id, SaveFailed);
        urls.ForEach(files.Delete);
        await activity.ContentActionAsync(ParentId, "Lesson", id, "FamilyLessonDelete", lesson.Title);
        TempData["Info"] = "Đã xóa bài.";
        return RedirectToAction(nameof(Index));
    }

    // ---------- contents ----------

    [HttpPost("family-lessons/{id:int}/contents")]
    [EnableRateLimiting("account")]
    [RequestSizeLimit(LessonFileStorage.MaxRequestBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = LessonFileStorage.MaxRequestBytes)]
    public async Task<IActionResult> AddContent(int id, LessonContentInput input, IFormFile? file, CancellationToken cancellationToken)
    {
        if (Db is not { } db) return View("SqlOnly");
        var lesson = await FamilyLessonService.Own(db, ParentId).FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (lesson is null) return NotFound();
        if (!ModelState.IsValid) return Back(id, ModelState.Values.SelectMany(entry => entry.Errors).First().ErrorMessage);
        var title = string.IsNullOrWhiteSpace(input.Title) ? null : input.Title.Trim();
        if (await FamilyLessonService.BlockedWordAsync(db, ParentId, [title, input.Body], cancellationToken) is { } word) return Back(id, FamilyLessonService.BlockedMessage(word));
        var content = new LessonContent { LessonId = id, ContentKind = input.Kind, Title = title };
        switch (input.Kind)
        {
            case "Reading":
                if (string.IsNullOrWhiteSpace(input.Body)) return Back(id, "Nhập nội dung bài đọc.");
                content.Body = input.Body.Trim();
                break;
            case "Video":
                // Only videos an admin approved, from a source the family did not block.
                var video = await FamilyLessonService.PlayableVideos(db, ParentId).Where(item => item.Id == input.VideoId).Select(item => new { item.Id, item.Title }).FirstOrDefaultAsync(cancellationToken);
                if (video is null) return Back(id, "Chọn một video trong danh sách video đã được duyệt.");
                content.SourceVideoId = video.Id;
                content.Title ??= FamilyLessonService.Cut(video.Title, 150);
                break;
            case "Document" or "Image":
                var url = await files.SaveAsync(file, input.Kind, cancellationToken);
                if (url is null) return Back(id, LessonFileStorage.Rejected);
                content.FileUrl = url;
                var name = Path.GetFileName(file!.FileName);
                content.FileName = name.Length > 150 ? name[^150..] : name;
                break;
            default:
                return Back(id, "Loại nội dung không hợp lệ.");
        }
        content.SortOrder = (await db.LessonContents.Where(item => item.LessonId == id).MaxAsync(item => (int?)item.SortOrder, cancellationToken) ?? 0) + 1;
        db.LessonContents.Add(content);
        lesson.UpdatedAt = DateTime.UtcNow;
        var review = FamilyLessonService.NeedsReview(lesson);
        if (!await SaveAsync(db, cancellationToken))
        {
            files.Delete(content.FileUrl);
            return Back(id, SaveFailed);
        }
        await activity.ContentActionAsync(ParentId, "Lesson", id, "FamilyLessonAddContent", LessonKind.ContentLabel(input.Kind));
        TempData["Info"] = "Đã thêm nội dung." + (review ? " Bài đã chia sẻ được đưa về chờ duyệt lại." : "");
        return RedirectToAction(nameof(Edit), new { id });
    }

    [HttpPost("family-lessons/{id:int}/contents/{contentId:int}/move")]
    public async Task<IActionResult> MoveContent(int id, int contentId, [FromForm] bool up, CancellationToken cancellationToken)
    {
        if (Db is not { } db) return View("SqlOnly");
        var lesson = await FamilyLessonService.Own(db, ParentId).FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (lesson is null) return NotFound();
        var items = await db.LessonContents.Where(item => item.LessonId == id).OrderBy(item => item.SortOrder).ThenBy(item => item.Id).ToListAsync(cancellationToken);
        var index = items.FindIndex(item => item.Id == contentId);
        if (index < 0) return NotFound();
        var other = up ? index - 1 : index + 1;
        if (other >= 0 && other < items.Count)
        {
            (items[index], items[other]) = (items[other], items[index]);
            for (var i = 0; i < items.Count; i++) items[i].SortOrder = i + 1;
            lesson.UpdatedAt = DateTime.UtcNow;
            FamilyLessonService.NeedsReview(lesson);
            if (!await SaveAsync(db, cancellationToken)) return Back(id, SaveFailed);
        }
        return RedirectToAction(nameof(Edit), new { id });
    }

    [HttpPost("family-lessons/{id:int}/contents/{contentId:int}/delete")]
    public async Task<IActionResult> DeleteContent(int id, int contentId, CancellationToken cancellationToken)
    {
        if (Db is not { } db) return View("SqlOnly");
        var lesson = await FamilyLessonService.Own(db, ParentId).FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        var content = await db.LessonContents.FirstOrDefaultAsync(item => item.Id == contentId && item.LessonId == id, cancellationToken);
        if (lesson is null || content is null) return NotFound();
        if (lesson.Status == LessonStatus.Published && await db.LessonContents.CountAsync(item => item.LessonId == id, cancellationToken) == 1)
            return Back(id, "Bài bé đang học cần ít nhất 1 nội dung. Hãy thêm nội dung khác hoặc chuyển bài về nháp trước.");
        db.LessonContents.Remove(content);
        lesson.UpdatedAt = DateTime.UtcNow;
        FamilyLessonService.NeedsReview(lesson);
        if (!await SaveAsync(db, cancellationToken)) return Back(id, SaveFailed);
        files.Delete(content.FileUrl);
        TempData["Info"] = "Đã xóa nội dung.";
        return RedirectToAction(nameof(Edit), new { id });
    }

    // ---------- questions ----------

    [HttpPost("family-lessons/{id:int}/questions")]
    [EnableRateLimiting("account")]
    public async Task<IActionResult> AddQuestion(int id, FamilyQuestionForm form, CancellationToken cancellationToken)
    {
        if (Db is not { } db) return View("SqlOnly");
        var lesson = await FamilyLessonService.Own(db, ParentId).FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (lesson is null) return NotFound();
        var (options, correct, problem) = FamilyLessonService.CleanQuestion(form);
        if (problem is not null) return Back(id, problem, "#questions");
        if (await db.Questions.CountAsync(item => item.LessonId == id && item.Status == QuestionStatus.Published, cancellationToken) >= FamilyRules.MaxQuestions)
            return Back(id, $"Mỗi bài tối đa {FamilyRules.MaxQuestions} câu hỏi.", "#questions");
        if (await FamilyLessonService.BlockedWordAsync(db, ParentId, [form.Text, form.Explanation, .. options], cancellationToken) is { } word)
            return Back(id, FamilyLessonService.BlockedMessage(word), "#questions");
        var now = DateTime.UtcNow;
        db.Questions.Add(new Question
        {
            LessonId = id, Text = form.Text, Explanation = form.Explanation, QuestionType = form.Type, Difficulty = 1, Origin = QuestionOrigin.Manual,
            Status = QuestionStatus.Published, CreatedById = ParentId, CreatedAt = now, ApprovedById = ParentId, ApprovedAt = now,
            QuestionOptions = options.Select((text, index) => new QuestionOption { Text = text, IsCorrect = index == correct, SortOrder = index + 1 }).ToList()
        });
        lesson.UpdatedAt = now;
        var review = FamilyLessonService.NeedsReview(lesson);
        if (!await SaveAsync(db, cancellationToken)) return Back(id, SaveFailed, "#questions");
        await FamilyLessonService.SyncQuizAsync(db, lesson, cancellationToken);
        if (!await SaveAsync(db, cancellationToken)) return Back(id, SaveFailed, "#questions");
        TempData["Info"] = "Đã thêm câu hỏi. Bé làm được ở mục \"Làm bài tập\" trong bài." + (review ? " Bài đã chia sẻ được đưa về chờ duyệt lại." : "");
        return Redirect(Url.Action(nameof(Edit), new { id }) + "#questions");
    }

    // A question children already answered is archived (kept for their results); otherwise it is removed.
    [HttpPost("family-lessons/{id:int}/questions/{questionId:int}/delete")]
    public async Task<IActionResult> DeleteQuestion(int id, int questionId, CancellationToken cancellationToken)
    {
        if (Db is not { } db) return View("SqlOnly");
        var lesson = await FamilyLessonService.Own(db, ParentId).FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        var question = await db.Questions.Include(item => item.QuestionOptions).FirstOrDefaultAsync(item => item.Id == questionId && item.LessonId == id, cancellationToken);
        if (lesson is null || question is null) return NotFound();
        if (await db.QuizAttemptAnswers.AnyAsync(item => item.QuestionId == questionId, cancellationToken)) question.Status = QuestionStatus.Archived;
        else
        {
            db.QuizQuestions.RemoveRange(await db.QuizQuestions.Where(item => item.QuestionId == questionId).ToListAsync(cancellationToken));
            db.Questions.Remove(question);
        }
        lesson.UpdatedAt = DateTime.UtcNow;
        FamilyLessonService.NeedsReview(lesson);
        if (!await SaveAsync(db, cancellationToken)) return Back(id, SaveFailed, "#questions");
        await FamilyLessonService.SyncQuizAsync(db, lesson, cancellationToken);
        await SaveAsync(db, cancellationToken);
        TempData["Info"] = "Đã bỏ câu hỏi.";
        return Redirect(Url.Action(nameof(Edit), new { id }) + "#questions");
    }

    // ---------- phase C: sharing ----------

    [HttpPost("family-lessons/{id:int}/share")]
    [EnableRateLimiting("account")]
    public async Task<IActionResult> Share(int id, [FromForm] bool agree, CancellationToken cancellationToken)
    {
        if (Db is not { } db) return View("SqlOnly");
        var lesson = await FamilyLessonService.Own(db, ParentId).FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (lesson is null) return NotFound();
        string? problem = !agree ? "Hãy đồng ý cho hiển thị bài và tên của bạn trong kho cộng đồng."
            : lesson.Status != LessonStatus.Published ? "Chỉ chia sẻ được bài đang ở trạng thái \"Bé học được\"."
            : lesson.ShareStatus is LessonAccess.Pending or LessonAccess.Approved ? "Bài này đã được gửi chia sẻ." : null;
        if (problem is not null) return Back(id, problem, "#share");
        lesson.ShareStatus = LessonAccess.Pending;
        lesson.ShareNote = null;
        lesson.SharedAt = DateTime.UtcNow;
        lesson.ShareReviewedAt = null;
        if (!await SaveAsync(db, cancellationToken)) return Back(id, SaveFailed, "#share");
        await activity.ContentActionAsync(ParentId, "Lesson", id, "FamilyLessonShare", lesson.Title);
        TempData["Info"] = "Đã gửi bài cho quản trị viên duyệt. Sau khi duyệt, mọi gia đình đều dùng được bài này.";
        return Redirect(Url.Action(nameof(Edit), new { id }) + "#share");
    }

    [HttpPost("family-lessons/{id:int}/unshare")]
    [EnableRateLimiting("account")]
    public async Task<IActionResult> Unshare(int id, CancellationToken cancellationToken)
    {
        if (Db is not { } db) return View("SqlOnly");
        var lesson = await FamilyLessonService.Own(db, ParentId).FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (lesson is null) return NotFound();
        if (lesson.ShareStatus is not null)
        {
            lesson.ShareStatus = null;
            lesson.ShareNote = null;
            if (!await SaveAsync(db, cancellationToken)) return Back(id, SaveFailed, "#share");
            await activity.ContentActionAsync(ParentId, "Lesson", id, "FamilyLessonUnshare", lesson.Title);
            TempData["Info"] = "Bài đã rút khỏi kho cộng đồng, chỉ gia đình bạn thấy. Bài các gia đình khác đã giao cho con họ sẽ không mở được nữa.";
        }
        return Redirect(Url.Action(nameof(Edit), new { id }) + "#share");
    }

    // ---------- helpers ----------

    private async Task<int?> ValidateAsync(SafeLearnDbContext db, FamilyLessonForm form, CancellationToken cancellationToken)
    {
        form.Title = form.Title?.Trim() ?? "";
        form.Summary = string.IsNullOrWhiteSpace(form.Summary) ? null : form.Summary.Trim();
        if (!LessonKind.Types.Contains(form.LessonType)) ModelState.AddModelError(nameof(form.LessonType), "Chọn loại bài.");
        var gradeId = await db.SubjectGrades.Where(item => item.SubjectId == form.SubjectId && item.Subject.IsActive && item.GradeLevel.Level == form.Grade)
            .Select(item => (int?)item.GradeLevelId).FirstOrDefaultAsync(cancellationToken);
        if (gradeId is null) ModelState.AddModelError(nameof(form.Grade), "Môn này chưa có lớp đã chọn. Hãy chọn môn hoặc lớp khác.");
        if (ModelState.IsValid && await FamilyLessonService.BlockedWordAsync(db, ParentId, [form.Title, form.Summary], cancellationToken) is { } word)
            ModelState.AddModelError("", FamilyLessonService.BlockedMessage(word));
        return gradeId;
    }

    private async Task<FamilyEditorViewModel?> EditorAsync(SafeLearnDbContext db, int id, FamilyLessonForm? posted, CancellationToken cancellationToken)
    {
        var lesson = await FamilyLessonService.Own(db, ParentId).AsNoTracking().Where(item => item.Id == id).Select(item => new
        {
            Form = new FamilyLessonForm
            {
                Title = item.Title, Summary = item.Summary, SubjectId = item.SubjectId, Grade = item.GradeLevel.Level, LessonType = item.LessonType,
                DurationMin = item.DurationMin, Status = item.Status
            },
            item.ShareStatus, item.ShareNote, Learners = item.LessonActivities.Count,
            QuizId = item.Quizzes.Where(quiz => quiz.Status == QuestionStatus.Published).Select(quiz => (int?)quiz.Id).FirstOrDefault()
        }).FirstOrDefaultAsync(cancellationToken);
        if (lesson is null) return null;
        return new FamilyEditorViewModel
        {
            Id = id,
            Form = posted ?? lesson.Form,
            Subjects = await SubjectsAsync(db, cancellationToken),
            ShareStatus = lesson.ShareStatus,
            ShareNote = lesson.ShareNote,
            Learners = lesson.Learners,
            QuizId = lesson.QuizId,
            Videos = await FamilyLessonService.Videos(db, ParentId).Take(300).ToListAsync(cancellationToken),
            Questions = await FamilyLessonService.QuestionRows(db.Questions.AsNoTracking().Where(item => item.LessonId == id && item.Status == QuestionStatus.Published))
                .ToListAsync(cancellationToken),
            Contents = await db.LessonContents.AsNoTracking().Where(item => item.LessonId == id).OrderBy(item => item.SortOrder).ThenBy(item => item.Id)
                .Select(item => new FamilyContentRow(item.Id, item.ContentKind, item.Title, item.Body, item.SourceVideo != null ? item.SourceVideo.Title : null, item.FileUrl, item.FileName))
                .ToListAsync(cancellationToken)
        };
    }

    // Each subject is offered for some grades only; the option shows them so the parent picks a valid pair.
    private static async Task<IReadOnlyList<SubjectRowOption>> SubjectsAsync(SafeLearnDbContext db, CancellationToken cancellationToken) =>
        (await db.Subjects.AsNoTracking().Where(item => item.IsActive).OrderBy(item => item.SortOrder).ThenBy(item => item.Name)
            .Select(item => new { item.Id, item.Name, Grades = item.SubjectGrades.Select(link => link.GradeLevel.Level).OrderBy(level => level).ToList() })
            .ToListAsync(cancellationToken))
        .Select(item => new SubjectRowOption(item.Id, item.Grades.Count == 0 ? item.Name : $"{item.Name} · lớp {string.Join(", ", item.Grades)}")).ToList();

    private RedirectResult Back(int id, string message, string anchor = "")
    {
        TempData["Error"] = message;
        return Redirect(Url.Action(nameof(Edit), new { id }) + anchor);
    }

    private static async Task<bool> SaveAsync(SafeLearnDbContext db, CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException)
        {
            db.ChangeTracker.Clear();
            return false;
        }
    }
}
