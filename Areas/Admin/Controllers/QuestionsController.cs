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

// US-35: question bank per lesson. US-36: quizzes built from approved questions. US-40: suggested questions wait for approval.
[Area("Admin")]
[Authorize(Roles = "Admin")]
public class QuestionsController(
    UserManager<ApplicationUser> users,
    AccountActivityLog activity,
    QuestionSuggester suggester,
    IServiceProvider services) : Controller
{
    private const int LessonListSize = 60;
    private const string SaveFailed = "Chưa thể lưu thay đổi. Vui lòng thử lại.";
    private static readonly string[] TrueFalseOptions = ["Đúng", "Sai"];

    private SafeLearnDbContext? Db => services.GetService<SafeLearnDbContext>();
    private string ActorId => users.GetUserId(User)!;

    [HttpGet("admin-questions")]
    public async Task<IActionResult> Index(int? lesson, int? subject, string? q, bool pending = false, CancellationToken cancellationToken = default)
    {
        if (Db is not { } db) return View("SqlOnly");
        var lessons = db.Lessons.AsNoTracking().Where(item => item.Status != LessonStatus.Archived);
        if (subject is not null) lessons = lessons.Where(item => item.SubjectId == subject);
        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            lessons = lessons.Where(item => item.Title.Contains(term));
        }
        var picks = await Picks(lessons).Take(LessonListSize).ToListAsync(cancellationToken);
        LessonPick? chosen = null;
        if (lesson is not null)
        {
            chosen = picks.FirstOrDefault(item => item.Id == lesson)
                ?? await Picks(db.Lessons.AsNoTracking().Where(item => item.Id == lesson)).FirstOrDefaultAsync(cancellationToken);
            if (chosen is null) return NotFound();
        }
        var questions = db.Questions.AsNoTracking().AsQueryable();
        questions = pending ? questions.Where(item => item.Status == QuestionStatus.InReview)
            : chosen is null ? questions.Where(_ => false) : questions.Where(item => item.LessonId == chosen.Id);
        return View(new QuestionBankViewModel
        {
            Lesson = chosen,
            Lessons = picks,
            Subjects = await db.Subjects.AsNoTracking().OrderBy(item => item.SortOrder).ThenBy(item => item.Name)
                .Select(item => new SubjectRowOption(item.Id, item.Name)).ToListAsync(cancellationToken),
            Subject = subject,
            Q = q,
            PendingOnly = pending,
            Questions = await Rows(questions.OrderBy(item => item.Status == QuestionStatus.InReview ? 0 : item.Status == QuestionStatus.Archived ? 2 : 1).ThenBy(item => item.Id))
                .ToListAsync(cancellationToken),
            Quizzes = chosen is null ? [] : await db.Quizzes.AsNoTracking().Where(item => item.LessonId == chosen.Id).OrderBy(item => item.Id)
                .Select(item => new QuizRow(item.Id, item.Title, item.Status, item.PassPercent, item.TimeLimitSec, item.QuizQuestions.Count, item.QuizAttempts.Count))
                .ToListAsync(cancellationToken),
            PendingTotal = await db.Questions.CountAsync(item => item.Status == QuestionStatus.InReview, cancellationToken),
            AiConfigured = suggester.Configured
        });
    }

    // ---------- questions ----------

    [HttpGet("admin-questions/new")]
    public async Task<IActionResult> Create(int lesson, CancellationToken cancellationToken)
    {
        if (Db is not { } db) return View("SqlOnly");
        var title = await db.Lessons.Where(item => item.Id == lesson).Select(item => item.Title).FirstOrDefaultAsync(cancellationToken);
        return title is null ? NotFound() : View("QuestionForm", new QuestionFormViewModel { LessonId = lesson, LessonTitle = title });
    }

    [HttpPost("admin-questions/new")]
    [EnableRateLimiting("account")]
    public async Task<IActionResult> Create(QuestionFormViewModel model, CancellationToken cancellationToken)
    {
        if (Db is not { } db) return View("SqlOnly");
        model.Id = null;
        model.Origin = QuestionOrigin.Manual;
        var title = await db.Lessons.Where(item => item.Id == model.LessonId).Select(item => item.Title).FirstOrDefaultAsync(cancellationToken);
        if (title is null) return NotFound();
        model.LessonTitle = title;
        var options = Validate(model, allowReview: false);
        if (!ModelState.IsValid) return View("QuestionForm", model);
        var now = DateTime.UtcNow;
        var question = new Question
        {
            LessonId = model.LessonId, Origin = QuestionOrigin.Manual, CreatedById = ActorId, CreatedAt = now,
            QuestionOptions = options.Select((text, index) => new QuestionOption { Text = text, IsCorrect = index == model.Correct, SortOrder = index + 1 }).ToList()
        };
        Apply(question, model, now);
        db.Questions.Add(question);
        if (!await SaveAsync(db, cancellationToken))
        {
            ModelState.AddModelError("", SaveFailed);
            return View("QuestionForm", model);
        }
        await activity.ContentActionAsync(ActorId, "Question", question.Id, "QuestionCreate", Short(question.Text));
        TempData["Info"] = "Đã thêm câu hỏi.";
        return RedirectToAction(nameof(Index), new { lesson = model.LessonId });
    }

    [HttpGet("admin-questions/{id:int}")]
    public async Task<IActionResult> Edit(int id, CancellationToken cancellationToken)
    {
        if (Db is not { } db) return View("SqlOnly");
        var question = await db.Questions.AsNoTracking().Where(item => item.Id == id).Select(item => new
        {
            item.Id, item.LessonId, Lesson = item.Lesson.Title, item.Text, item.QuestionType, item.Difficulty, item.Explanation, item.Status, item.Origin, item.AiModelName,
            Options = item.QuestionOptions.OrderBy(option => option.SortOrder).ThenBy(option => option.Id).Select(option => new { option.Text, option.IsCorrect }).ToList(),
            Locked = item.QuizAttemptAnswers.Any()
        }).FirstOrDefaultAsync(cancellationToken);
        if (question is null) return NotFound();
        var options = question.Options.Select(option => (string?)option.Text).ToList();
        while (options.Count < QuizRules.MaxOptions) options.Add("");
        return View("QuestionForm", new QuestionFormViewModel
        {
            Id = question.Id, LessonId = question.LessonId, LessonTitle = question.Lesson, Text = question.Text, Type = question.QuestionType,
            Difficulty = question.Difficulty, Explanation = question.Explanation, Status = question.Status, Origin = question.Origin, AiModelName = question.AiModelName,
            Options = options, Correct = Math.Max(0, question.Options.FindIndex(option => option.IsCorrect)), Locked = question.Locked
        });
    }

    [HttpPost("admin-questions/{id:int}")]
    [EnableRateLimiting("account")]
    public async Task<IActionResult> Edit(int id, QuestionFormViewModel model, CancellationToken cancellationToken)
    {
        if (Db is not { } db) return View("SqlOnly");
        var question = await db.Questions.Include(item => item.QuestionOptions).Include(item => item.Lesson).FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (question is null) return NotFound();
        model.Id = id;
        model.LessonId = question.LessonId;
        model.LessonTitle = question.Lesson.Title;
        model.Origin = question.Origin;
        model.AiModelName = question.AiModelName;
        model.Locked = await db.QuizAttemptAnswers.AnyAsync(item => item.QuestionId == id, cancellationToken);
        if (model.Locked)
        {
            // Children already answered: type, answers and the correct one stay as they were.
            model.Type = question.QuestionType;
            var current = question.QuestionOptions.OrderBy(option => option.SortOrder).ThenBy(option => option.Id).ToList();
            model.Options = current.Select(option => (string?)option.Text).ToList();
            model.Correct = Math.Max(0, current.FindIndex(option => option.IsCorrect));
        }
        var options = Validate(model, allowReview: question.Status == QuestionStatus.InReview);
        if (!ModelState.IsValid) return View("QuestionForm", model);
        var now = DateTime.UtcNow;
        Apply(question, model, now);
        if (!model.Locked)
        {
            db.QuestionOptions.RemoveRange(question.QuestionOptions);
            question.QuestionOptions = options.Select((text, index) => new QuestionOption { Text = text, IsCorrect = index == model.Correct, SortOrder = index + 1 }).ToList();
        }
        if (!await SaveAsync(db, cancellationToken))
        {
            ModelState.AddModelError("", SaveFailed);
            return View("QuestionForm", model);
        }
        await activity.ContentActionAsync(ActorId, "Question", id, "QuestionUpdate", $"{question.Status} · {Short(question.Text)}");
        TempData["Info"] = "Đã lưu câu hỏi.";
        return RedirectToAction(nameof(Index), new { lesson = question.LessonId });
    }

    // Quick review buttons: approve, send back to draft, archive.
    [HttpPost("admin-questions/{id:int}/status")]
    [EnableRateLimiting("account")]
    public async Task<IActionResult> Status(int id, [FromForm] string action, [FromForm] bool pending, CancellationToken cancellationToken)
    {
        if (Db is not { } db) return View("SqlOnly");
        var question = await db.Questions.Include(item => item.QuestionOptions).FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (question is null) return NotFound();
        var target = action switch
        {
            "approve" => QuestionStatus.Published,
            "draft" => QuestionStatus.Draft,
            "archive" or "reject" => QuestionStatus.Archived,
            _ => null
        };
        if (target is null) return BadRequest();
        if (target == QuestionStatus.Published && !IsComplete(question))
        {
            TempData["Error"] = "Câu hỏi cần ít nhất 2 đáp án và đúng 1 đáp án đúng trước khi duyệt.";
        }
        else if (question.Status != target)
        {
            var now = DateTime.UtcNow;
            question.Status = target;
            if (target == QuestionStatus.Published)
            {
                question.ApprovedById = ActorId;
                question.ApprovedAt = now < question.CreatedAt ? question.CreatedAt : now;
            }
            if (!await SaveAsync(db, cancellationToken)) TempData["Error"] = SaveFailed;
            else
            {
                await activity.ContentActionAsync(ActorId, "Question", id, action == "reject" ? "QuestionReject" : "QuestionStatus", $"{target} · {Short(question.Text)}");
                TempData["Info"] = target switch
                {
                    QuestionStatus.Published => "Đã duyệt câu hỏi. Có thể đưa vào bài tập.",
                    QuestionStatus.Archived => action == "reject" ? "Đã từ chối câu hỏi gợi ý." : "Đã lưu trữ câu hỏi. Bài tập đang mở sẽ bỏ qua câu này.",
                    _ => "Đã chuyển câu hỏi về nháp."
                };
            }
        }
        return pending ? RedirectToAction(nameof(Index), new { pending = true }) : RedirectToAction(nameof(Index), new { lesson = question.LessonId });
    }

    // Only a question nobody answered and no quiz uses can be removed; otherwise archive it.
    [HttpPost("admin-questions/{id:int}/delete")]
    [EnableRateLimiting("account")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        if (Db is not { } db) return View("SqlOnly");
        var question = await db.Questions.Include(item => item.QuestionOptions).FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (question is null) return NotFound();
        if (await db.QuizQuestions.AnyAsync(item => item.QuestionId == id, cancellationToken) || await db.QuizAttemptAnswers.AnyAsync(item => item.QuestionId == id, cancellationToken))
        {
            TempData["Error"] = "Câu hỏi đang nằm trong bài tập hoặc đã có bé trả lời. Hãy lưu trữ thay vì xóa.";
            return RedirectToAction(nameof(Index), new { lesson = question.LessonId });
        }
        db.QuestionOptions.RemoveRange(question.QuestionOptions);
        db.Questions.Remove(question);
        if (!await SaveAsync(db, cancellationToken)) TempData["Error"] = SaveFailed;
        else
        {
            await activity.ContentActionAsync(ActorId, "Question", id, "QuestionDelete", Short(question.Text));
            TempData["Info"] = "Đã xóa câu hỏi.";
        }
        return RedirectToAction(nameof(Index), new { lesson = question.LessonId });
    }

    // US-40: suggestions are saved as "InReview" with their origin and model, never used until approved.
    [HttpPost("admin-questions/suggest")]
    [EnableRateLimiting("account")]
    public async Task<IActionResult> Suggest([FromForm] int lesson, [FromForm] int count, CancellationToken cancellationToken)
    {
        if (Db is not { } db) return View("SqlOnly");
        var source = await db.Lessons.AsNoTracking().Where(item => item.Id == lesson).Select(item => new
        {
            item.Title, Subject = item.Subject.Name, item.GradeLevel.Level, item.Summary,
            Readings = item.LessonContents.Where(content => content.ContentKind == "Reading" && content.Body != null)
                .OrderBy(content => content.SortOrder).ThenBy(content => content.Id).Select(content => content.Body!).ToList(),
            Existing = item.Questions.Where(question => question.Status != QuestionStatus.Archived).Select(question => question.Text).ToList()
        }).FirstOrDefaultAsync(cancellationToken);
        if (source is null) return NotFound();
        var result = await suggester.SuggestAsync(new LessonSource(source.Title, source.Subject, source.Level, source.Summary, source.Readings, source.Existing), count, cancellationToken);
        if (result.Questions.Count > 0)
        {
            var now = DateTime.UtcNow;
            var added = result.Questions.Select(item => new Question
            {
                LessonId = lesson, Text = item.Text, QuestionType = item.Type, Difficulty = item.Difficulty, Explanation = item.Explanation,
                Origin = QuestionOrigin.AiSuggested, AiModelName = result.Model, Status = QuestionStatus.InReview, CreatedById = ActorId, CreatedAt = now,
                QuestionOptions = item.Options.Select((text, index) => new QuestionOption { Text = text, IsCorrect = index == item.Correct, SortOrder = index + 1 }).ToList()
            }).ToList();
            db.Questions.AddRange(added);
            if (!await SaveAsync(db, cancellationToken))
            {
                TempData["Error"] = SaveFailed;
                return RedirectToAction(nameof(Index), new { lesson });
            }
            await activity.ContentActionAsync(ActorId, "Lesson", lesson, "QuestionSuggest", $"{added.Count} câu · {result.Model}");
            TempData["Info"] = $"Đã gợi ý {added.Count} câu hỏi ({(result.Model == QuestionSuggester.OfflineModel ? "bộ gợi ý cục bộ" : result.Model)}). Hãy kiểm tra và duyệt từng câu trước khi dùng.";
        }
        else TempData["Error"] = result.Problem ?? "Chưa gợi ý được câu hỏi nào.";
        return RedirectToAction(nameof(Index), new { lesson });
    }

    // ---------- quizzes ----------

    [HttpGet("admin-quizzes/new")]
    public async Task<IActionResult> CreateQuiz(int lesson, CancellationToken cancellationToken)
    {
        if (Db is not { } db) return View("SqlOnly");
        var title = await db.Lessons.Where(item => item.Id == lesson).Select(item => item.Title).FirstOrDefaultAsync(cancellationToken);
        if (title is null) return NotFound();
        return View("QuizForm", new QuizFormViewModel
        {
            LessonId = lesson, LessonTitle = title, Title = "Bài tập: " + title, Available = await AvailableAsync(db, lesson, [], cancellationToken)
        });
    }

    [HttpPost("admin-quizzes/new")]
    [EnableRateLimiting("account")]
    public async Task<IActionResult> CreateQuiz(QuizFormViewModel model, CancellationToken cancellationToken)
    {
        if (Db is not { } db) return View("SqlOnly");
        model.Id = null;
        var title = await db.Lessons.Where(item => item.Id == model.LessonId).Select(item => item.Title).FirstOrDefaultAsync(cancellationToken);
        if (title is null) return NotFound();
        model.LessonTitle = title;
        await ValidateQuizAsync(db, model, cancellationToken);
        if (!ModelState.IsValid)
        {
            model.Available = await AvailableAsync(db, model.LessonId, model.QuestionIds, cancellationToken);
            return View("QuizForm", model);
        }
        var quiz = new Quiz { LessonId = model.LessonId, CreatedAt = DateTime.UtcNow };
        ApplyQuiz(quiz, model);
        quiz.QuizQuestions = model.QuestionIds.Select((questionId, index) => new QuizQuestion { QuestionId = questionId, SortOrder = index + 1, Points = 1 }).ToList();
        db.Quizzes.Add(quiz);
        if (!await SaveAsync(db, cancellationToken))
        {
            ModelState.AddModelError("", SaveFailed);
            model.Available = await AvailableAsync(db, model.LessonId, model.QuestionIds, cancellationToken);
            return View("QuizForm", model);
        }
        await activity.ContentActionAsync(ActorId, "Quiz", quiz.Id, "QuizCreate", $"{quiz.Status} · {quiz.Title} · {model.QuestionIds.Count} câu");
        TempData["Info"] = "Đã tạo bài tập.";
        return RedirectToAction(nameof(Index), new { lesson = model.LessonId });
    }

    [HttpGet("admin-quizzes/{id:int}")]
    public async Task<IActionResult> EditQuiz(int id, CancellationToken cancellationToken)
    {
        if (Db is not { } db) return View("SqlOnly");
        var quiz = await db.Quizzes.AsNoTracking().Where(item => item.Id == id).Select(item => new
        {
            item.Id, item.LessonId, Lesson = item.Lesson.Title, item.Title, item.PassPercent, item.TimeLimitSec, item.ShuffleQuestions, item.Status,
            Questions = item.QuizQuestions.OrderBy(link => link.SortOrder).ThenBy(link => link.Id).Select(link => link.QuestionId).ToList(),
            Locked = item.QuizAttempts.Any()
        }).FirstOrDefaultAsync(cancellationToken);
        if (quiz is null) return NotFound();
        return View("QuizForm", new QuizFormViewModel
        {
            Id = quiz.Id, LessonId = quiz.LessonId, LessonTitle = quiz.Lesson, Title = quiz.Title, PassPercent = quiz.PassPercent,
            TimeLimitMinutes = quiz.TimeLimitSec is { } seconds ? (int)Math.Ceiling(seconds / 60.0) : null, Shuffle = quiz.ShuffleQuestions, Status = quiz.Status,
            QuestionIds = quiz.Questions, Locked = quiz.Locked, Available = await AvailableAsync(db, quiz.LessonId, quiz.Questions, cancellationToken)
        });
    }

    [HttpPost("admin-quizzes/{id:int}")]
    [EnableRateLimiting("account")]
    public async Task<IActionResult> EditQuiz(int id, QuizFormViewModel model, CancellationToken cancellationToken)
    {
        if (Db is not { } db) return View("SqlOnly");
        var quiz = await db.Quizzes.Include(item => item.QuizQuestions).Include(item => item.Lesson).FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (quiz is null) return NotFound();
        model.Id = id;
        model.LessonId = quiz.LessonId;
        model.LessonTitle = quiz.Lesson.Title;
        model.Locked = await db.QuizAttempts.AnyAsync(item => item.QuizId == id, cancellationToken);
        if (model.Locked) model.QuestionIds = quiz.QuizQuestions.OrderBy(link => link.SortOrder).ThenBy(link => link.Id).Select(link => link.QuestionId).ToList();
        await ValidateQuizAsync(db, model, cancellationToken);
        if (!ModelState.IsValid)
        {
            model.Available = await AvailableAsync(db, model.LessonId, model.QuestionIds, cancellationToken);
            return View("QuizForm", model);
        }
        ApplyQuiz(quiz, model);
        if (!model.Locked)
        {
            db.QuizQuestions.RemoveRange(quiz.QuizQuestions);
            quiz.QuizQuestions = model.QuestionIds.Select((questionId, index) => new QuizQuestion { QuestionId = questionId, SortOrder = index + 1, Points = 1 }).ToList();
        }
        if (!await SaveAsync(db, cancellationToken))
        {
            ModelState.AddModelError("", SaveFailed);
            model.Available = await AvailableAsync(db, model.LessonId, model.QuestionIds, cancellationToken);
            return View("QuizForm", model);
        }
        await activity.ContentActionAsync(ActorId, "Quiz", id, "QuizUpdate", $"{quiz.Status} · {quiz.Title}");
        TempData["Info"] = "Đã lưu bài tập.";
        return RedirectToAction(nameof(Index), new { lesson = quiz.LessonId });
    }

    [HttpPost("admin-quizzes/{id:int}/delete")]
    [EnableRateLimiting("account")]
    public async Task<IActionResult> DeleteQuiz(int id, CancellationToken cancellationToken)
    {
        if (Db is not { } db) return View("SqlOnly");
        var quiz = await db.Quizzes.Include(item => item.QuizQuestions).FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (quiz is null) return NotFound();
        if (await db.QuizAttempts.AnyAsync(item => item.QuizId == id, cancellationToken) || await db.Assignments.AnyAsync(item => item.QuizId == id, cancellationToken))
        {
            TempData["Error"] = "Bài tập đã được giao hoặc đã có bé làm. Hãy chuyển sang lưu trữ thay vì xóa.";
            return RedirectToAction(nameof(Index), new { lesson = quiz.LessonId });
        }
        db.QuizQuestions.RemoveRange(quiz.QuizQuestions);
        db.Quizzes.Remove(quiz);
        if (!await SaveAsync(db, cancellationToken)) TempData["Error"] = SaveFailed;
        else
        {
            await activity.ContentActionAsync(ActorId, "Quiz", id, "QuizDelete", quiz.Title);
            TempData["Info"] = "Đã xóa bài tập.";
        }
        return RedirectToAction(nameof(Index), new { lesson = quiz.LessonId });
    }

    // ---------- helpers ----------

    private List<string> Validate(QuestionFormViewModel model, bool allowReview)
    {
        model.Text = model.Text?.Trim() ?? "";
        model.Explanation = string.IsNullOrWhiteSpace(model.Explanation) ? null : model.Explanation.Trim();
        if (!QuestionType.All.Contains(model.Type)) ModelState.AddModelError(nameof(model.Type), "Chọn loại câu hỏi.");
        var statuses = allowReview ? QuestionStatus.All : QuestionStatus.All.Where(status => status != QuestionStatus.InReview);
        if (!statuses.Contains(model.Status)) ModelState.AddModelError(nameof(model.Status), "Trạng thái không hợp lệ.");
        if (model.Type == QuestionType.TrueFalse)
        {
            model.Options = [.. TrueFalseOptions];
            if (model.Correct is < 0 or > 1) model.Correct = 0;
            return [.. TrueFalseOptions];
        }
        // Keep the rows the admin filled in, and move the correct mark with its row.
        var filled = model.Options.Select((text, index) => (Text: text?.Trim() ?? "", index)).Where(item => item.Text.Length > 0).ToList();
        var correct = filled.FindIndex(item => item.index == model.Correct);
        if (filled.Count < QuizRules.MinOptions) ModelState.AddModelError(nameof(model.Options), "Cần ít nhất 2 đáp án.");
        else if (filled.Count > QuizRules.MaxOptions) ModelState.AddModelError(nameof(model.Options), "Tối đa 4 đáp án.");
        else if (filled.Any(item => item.Text.Length > 300)) ModelState.AddModelError(nameof(model.Options), "Mỗi đáp án tối đa 300 ký tự.");
        else if (filled.Select(item => item.Text).Distinct(StringComparer.OrdinalIgnoreCase).Count() != filled.Count) ModelState.AddModelError(nameof(model.Options), "Các đáp án không được trùng nhau.");
        else if (correct < 0) ModelState.AddModelError(nameof(model.Correct), "Chọn đáp án đúng trong các đáp án đã nhập.");
        while (model.Options.Count < QuizRules.MaxOptions) model.Options.Add("");
        if (!ModelState.IsValid) return [];
        model.Correct = correct;
        return filled.Select(item => item.Text).ToList();
    }

    private void Apply(Question question, QuestionFormViewModel model, DateTime now)
    {
        question.Text = model.Text;
        question.Explanation = model.Explanation;
        question.Difficulty = model.Difficulty;
        if (!model.Locked) question.QuestionType = model.Type;
        if (model.Status == QuestionStatus.Published && question.Status != QuestionStatus.Published)
        {
            question.ApprovedById = ActorId;
            question.ApprovedAt = question.CreatedAt != default && now < question.CreatedAt ? question.CreatedAt : now;
        }
        question.Status = model.Status;
    }

    private async Task ValidateQuizAsync(SafeLearnDbContext db, QuizFormViewModel model, CancellationToken cancellationToken)
    {
        model.Title = model.Title?.Trim() ?? "";
        model.QuestionIds = model.QuestionIds.Distinct().ToList();
        if (!QuestionStatus.All.Contains(model.Status) || model.Status == QuestionStatus.InReview) ModelState.AddModelError(nameof(model.Status), "Trạng thái không hợp lệ.");
        if (model.QuestionIds.Count > QuizRules.MaxQuestionsPerQuiz) ModelState.AddModelError(nameof(model.QuestionIds), "Mỗi bài tập tối đa 30 câu hỏi.");
        if (model.Locked) return;
        var allowed = await db.Questions.Where(item => item.LessonId == model.LessonId && item.Status == QuestionStatus.Published && model.QuestionIds.Contains(item.Id))
            .Select(item => item.Id).ToListAsync(cancellationToken);
        if (allowed.Count != model.QuestionIds.Count) ModelState.AddModelError(nameof(model.QuestionIds), "Chỉ chọn được câu hỏi đã duyệt của bài này.");
        if (model.Status == QuestionStatus.Published && model.QuestionIds.Count == 0) ModelState.AddModelError(nameof(model.QuestionIds), "Chọn ít nhất 1 câu hỏi trước khi mở bài tập.");
    }

    private static void ApplyQuiz(Quiz quiz, QuizFormViewModel model)
    {
        quiz.Title = model.Title;
        quiz.PassPercent = model.PassPercent;
        quiz.TimeLimitSec = model.TimeLimitMinutes * 60;
        quiz.ShuffleQuestions = model.Shuffle;
        quiz.Status = model.Status;
    }

    private static bool IsComplete(Question question) =>
        question.QuestionOptions.Count >= QuizRules.MinOptions && question.QuestionOptions.Count(option => option.IsCorrect) == 1;

    // Approved questions of the lesson, plus any already in the quiz (for example archived later) so the form shows them.
    private static async Task<IReadOnlyList<QuestionRow>> AvailableAsync(SafeLearnDbContext db, int lessonId, IReadOnlyList<int> chosen, CancellationToken cancellationToken)
    {
        var rows = await Rows(db.Questions.AsNoTracking().Where(item => item.LessonId == lessonId && (item.Status == QuestionStatus.Published || chosen.Contains(item.Id))))
            .ToListAsync(cancellationToken);
        return rows.OrderBy(row => chosen.Contains(row.Id) ? chosen.ToList().IndexOf(row.Id) : int.MaxValue).ThenBy(row => row.Id).ToList();
    }

    private static IQueryable<QuestionRow> Rows(IQueryable<Question> questions) =>
        questions.Select(item => new QuestionRow(item.Id, item.Text, item.QuestionType, item.Difficulty, item.Origin, item.Status, item.QuestionOptions.Count,
            item.QuizQuestions.Count, item.AiModelName,
            item.QuestionOptions.OrderBy(option => option.SortOrder).ThenBy(option => option.Id).Select(option => new AnswerView(option.Text, option.IsCorrect)).ToList(),
            item.Explanation));

    private static IQueryable<LessonPick> Picks(IQueryable<Lesson> lessons) =>
        lessons.OrderBy(item => item.Subject.SortOrder).ThenBy(item => item.GradeLevel.Level).ThenBy(item => item.SortOrder).ThenBy(item => item.Id)
            .Select(item => new LessonPick(item.Id, item.Title, item.Subject.Name, item.GradeLevel.Level,
                item.Questions.Count(question => question.Status != QuestionStatus.Archived),
                item.Questions.Count(question => question.Status == QuestionStatus.InReview)));

    private static string Short(string text) => text.Length > 120 ? text[..120] + "…" : text;

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
