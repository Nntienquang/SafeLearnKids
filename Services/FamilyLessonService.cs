using Microsoft.EntityFrameworkCore;
using SafeLearnKids.Web.Data;
using SafeLearnKids.Web.Models;
using SafeLearnKids.Web.Models.Entities;

namespace SafeLearnKids.Web.Services;

// Rules shared by the family lesson editor (phase A) and the community review (phase C).
public static class FamilyLessonService
{
    private static readonly string[] TrueFalse = ["Đúng", "Sai"];

    // The family's own lessons, never archived ones unless asked.
    public static IQueryable<Lesson> Own(SafeLearnDbContext db, string parentId) => db.Lessons.Where(lesson => lesson.OwnerParentId == parentId);

    // Global blocked keywords plus the family's own; returns the first one found in any of the texts.
    public static async Task<string?> BlockedWordAsync(SafeLearnDbContext db, string parentId, IEnumerable<string?> texts, CancellationToken cancellationToken)
    {
        var keywords = await db.BlockedKeywords.AsNoTracking().Where(item => item.ParentId == null || item.ParentId == parentId)
            .Select(item => item.Keyword).ToListAsync(cancellationToken);
        var all = string.Join("\n", texts.Where(text => !string.IsNullOrEmpty(text)));
        return keywords.FirstOrDefault(keyword => all.Contains(keyword, StringComparison.OrdinalIgnoreCase));
    }

    public static string BlockedMessage(string keyword) => $"Nội dung có từ khóa bị chặn \"{keyword}\". Hãy sửa lại cho phù hợp với trẻ.";

    // Same checks as the admin question form. Returns the answers to store and the index of the right one, or an error.
    public static (List<string> Options, int Correct, string? Error) CleanQuestion(FamilyQuestionForm form)
    {
        form.Text = form.Text?.Trim() ?? "";
        form.Explanation = string.IsNullOrWhiteSpace(form.Explanation) ? null : form.Explanation.Trim();
        if (form.Text.Length is 0 or > 500) return ([], 0, "Câu hỏi từ 1 đến 500 ký tự.");
        if (form.Explanation is { Length: > 500 }) return ([], 0, "Giải thích tối đa 500 ký tự.");
        if (form.Type == QuestionType.TrueFalse) return ([.. TrueFalse], form.Correct is 0 or 1 ? form.Correct : 0, null);
        if (form.Type != QuestionType.SingleChoice) return ([], 0, "Chọn loại câu hỏi.");
        var filled = form.Options.Select((text, index) => (Text: text?.Trim() ?? "", index)).Where(item => item.Text.Length > 0).ToList();
        if (filled.Count < QuizRules.MinOptions) return ([], 0, "Cần ít nhất 2 đáp án.");
        if (filled.Count > QuizRules.MaxOptions) return ([], 0, "Tối đa 4 đáp án.");
        if (filled.Any(item => item.Text.Length > 300)) return ([], 0, "Mỗi đáp án tối đa 300 ký tự.");
        if (filled.Select(item => item.Text).Distinct(StringComparer.OrdinalIgnoreCase).Count() != filled.Count) return ([], 0, "Các đáp án không được trùng nhau.");
        var correct = filled.FindIndex(item => item.index == form.Correct);
        return correct < 0 ? ([], 0, "Chọn đáp án đúng trong các đáp án đã nhập.") : (filled.Select(item => item.Text).ToList(), correct, null);
    }

    // Each family lesson has one quiz that always holds its approved questions, so children can practise and be scored.
    // Questions children already answered are archived rather than removed, so their links stay.
    public static async Task SyncQuizAsync(SafeLearnDbContext db, Lesson lesson, CancellationToken cancellationToken)
    {
        var quiz = await db.Quizzes.Include(item => item.QuizQuestions).FirstOrDefaultAsync(item => item.LessonId == lesson.Id, cancellationToken);
        var questions = await db.Questions.Where(item => item.LessonId == lesson.Id && item.Status == QuestionStatus.Published)
            .OrderBy(item => item.Id).Select(item => item.Id).ToListAsync(cancellationToken);
        if (quiz is null)
        {
            if (questions.Count == 0) return;
            quiz = new Quiz { LessonId = lesson.Id, PassPercent = 70, ShuffleQuestions = false, CreatedAt = DateTime.UtcNow, Status = QuestionStatus.Draft, Title = "" };
            db.Quizzes.Add(quiz);
        }
        quiz.Title = Cut("Bài tập: " + lesson.Title, 150);
        quiz.Status = questions.Count > 0 ? QuestionStatus.Published : QuestionStatus.Draft;
        var linked = quiz.QuizQuestions.Select(link => link.QuestionId).ToHashSet();
        var order = quiz.QuizQuestions.Count == 0 ? 0 : quiz.QuizQuestions.Max(link => link.SortOrder);
        foreach (var id in questions.Where(id => !linked.Contains(id)))
            quiz.QuizQuestions.Add(new QuizQuestion { QuestionId = id, SortOrder = ++order, Points = 1 });
    }

    // Changing a lesson the community already sees sends it back for review; until then other families do not see it.
    public static bool NeedsReview(Lesson lesson)
    {
        if (lesson.ShareStatus != LessonAccess.Approved) return false;
        lesson.ShareStatus = LessonAccess.Pending;
        lesson.ShareNote = "Đã sửa sau khi được duyệt, đang chờ duyệt lại.";
        lesson.SharedAt = DateTime.UtcNow;
        return true;
    }

    // Approved videos from sources that are still approved and that this family did not block.
    // Filters stay on the entity: EF cannot translate a filter on the projected record.
    public static IQueryable<SourceVideo> PlayableVideos(SafeLearnDbContext db, string parentId) =>
        db.SourceVideos.AsNoTracking().Where(video => video.Status == "Approved" && video.Platform == "YouTube"
            && (!video.SourceVideoSources.Any() || video.SourceVideoSources.Any(link => link.Source.Status == "Approved"))
            && !video.SourceVideoSources.Any(link => link.Source.ParentSources.Any(choice => choice.ParentId == parentId && choice.Status == "Blocked")));

    public static IQueryable<VideoChoice> Videos(SafeLearnDbContext db, string parentId) =>
        PlayableVideos(db, parentId).OrderBy(video => video.Title).Select(video => new VideoChoice(video.Id, video.Title));

    public static IQueryable<QuestionRow> QuestionRows(IQueryable<Question> questions) =>
        questions.OrderBy(item => item.Id).Select(item => new QuestionRow(item.Id, item.Text, item.QuestionType, item.Difficulty, item.Origin, item.Status,
            item.QuestionOptions.Count, item.QuizQuestions.Count, item.AiModelName,
            item.QuestionOptions.OrderBy(option => option.SortOrder).ThenBy(option => option.Id).Select(option => new AnswerView(option.Text, option.IsCorrect)).ToList(),
            item.Explanation));

    public static string Cut(string text, int length) => text.Length > length ? text[..length] : text;
}
