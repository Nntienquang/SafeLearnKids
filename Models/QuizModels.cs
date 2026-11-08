using System.ComponentModel.DataAnnotations;
using SafeLearnKids.Web.Services;

namespace SafeLearnKids.Web.Models;

// Values allowed by the CHECK constraints on dbo.Questions and dbo.Quizzes.
public static class QuestionStatus
{
    public const string Draft = "Draft";
    public const string InReview = "InReview";
    public const string Published = "Published";
    public const string Archived = "Archived";
    public static readonly string[] All = [Draft, InReview, Published, Archived];

    public static string Label(string status) => status switch
    {
        Draft => "Nháp",
        InReview => "Chờ duyệt",
        Published => "Đã duyệt",
        Archived => "Lưu trữ",
        _ => status
    };

    public static string Tone(string status) => status switch
    {
        Draft => "t-n",
        InReview => "t-a",
        Published => "t-g",
        _ => "t-r"
    };

    // A quiz uses the same four states; for a quiz "Published" means children can take it.
    public static string QuizLabel(string status) => status == Published ? "Đang mở" : Label(status);
}

// MultiChoice is allowed by the table but dbo.QuizAttemptAnswers keeps one selected option per question,
// so only single-answer types are offered.
public static class QuestionType
{
    public const string SingleChoice = "SingleChoice";
    public const string TrueFalse = "TrueFalse";
    public static readonly string[] All = [SingleChoice, TrueFalse];

    public static string Label(string type) => type switch
    {
        SingleChoice => "Trắc nghiệm",
        TrueFalse => "Đúng / Sai",
        _ => type
    };
}

public static class QuestionOrigin
{
    public const string Manual = "Manual";
    public const string AiSuggested = "AiSuggested";
}

public static class QuizRules
{
    public const int MinOptions = 2;
    public const int MaxOptions = 4;
    public const int MaxQuestionsPerQuiz = 30;
    public const int MaxTimeLimitMinutes = 60;
    // A slow network may deliver the automatic submit a little after the clock reaches zero.
    public const int GraceSeconds = 60;

    public static int Stars(decimal score) => (int)Math.Round(score / 20m, MidpointRounding.AwayFromZero);
}

public class QuestionFormViewModel
{
    public int? Id { get; set; }

    public int LessonId { get; set; }

    public string LessonTitle { get; set; } = "";

    [Display(Name = "Câu hỏi")]
    [Required(ErrorMessage = "Nhập nội dung câu hỏi.")]
    [StringLength(500, ErrorMessage = "Câu hỏi tối đa 500 ký tự.")]
    public string Text { get; set; } = "";

    [Display(Name = "Loại câu hỏi")]
    public string Type { get; set; } = QuestionType.SingleChoice;

    [Display(Name = "Độ khó")]
    [Range(1, 3, ErrorMessage = "Độ khó từ 1 đến 3.")]
    public int Difficulty { get; set; } = 1;

    [Display(Name = "Giải thích đáp án")]
    [StringLength(500, ErrorMessage = "Giải thích tối đa 500 ký tự.")]
    public string? Explanation { get; set; }

    // Up to four answers; empty rows are ignored. For true/false the two answers are fixed.
    public List<string?> Options { get; set; } = ["", "", "", ""];

    // Index of the correct answer among Options.
    public int Correct { get; set; }

    [Display(Name = "Trạng thái")]
    public string Status { get; set; } = QuestionStatus.Draft;

    public string Origin { get; set; } = QuestionOrigin.Manual;

    public string? AiModelName { get; set; }

    // Once children have answered it, the answers stay as they were so old results remain correct.
    public bool Locked { get; set; }
}

public record QuestionRow(int Id, string Text, string Type, int Difficulty, string Origin, string Status, int Options, int Quizzes, string? AiModel,
    IReadOnlyList<AnswerView> Answers, string? Explanation);

public record AnswerView(string Text, bool Correct);

public record QuizRow(int Id, string Title, string Status, int PassPercent, int? TimeLimitSec, int Questions, int Attempts);

public record LessonPick(int Id, string Title, string Subject, int Grade, int Questions, int Pending);

public class QuestionBankViewModel
{
    public LessonPick? Lesson { get; init; }
    public IReadOnlyList<LessonPick> Lessons { get; init; } = [];
    public IReadOnlyList<SubjectRowOption> Subjects { get; init; } = [];
    public int? Subject { get; init; }
    public string? Q { get; init; }
    public bool PendingOnly { get; init; }
    public IReadOnlyList<QuestionRow> Questions { get; init; } = [];
    public IReadOnlyList<QuizRow> Quizzes { get; init; } = [];
    public int PendingTotal { get; init; }
    public bool AiConfigured { get; init; }
}

public record SubjectRowOption(int Id, string Name);

public class QuizFormViewModel
{
    public int? Id { get; set; }

    public int LessonId { get; set; }

    public string LessonTitle { get; set; } = "";

    [Display(Name = "Tên bài tập")]
    [Required(ErrorMessage = "Nhập tên bài tập.")]
    [StringLength(150, ErrorMessage = "Tên tối đa 150 ký tự.")]
    public string Title { get; set; } = "";

    [Display(Name = "Điểm đạt (%)")]
    [Range(0, 100, ErrorMessage = "Điểm đạt từ 0 đến 100.")]
    public int PassPercent { get; set; } = 70;

    [Display(Name = "Thời gian làm bài (phút, để trống nếu không giới hạn)")]
    [Range(1, QuizRules.MaxTimeLimitMinutes, ErrorMessage = "Thời gian từ 1 đến 60 phút.")]
    public int? TimeLimitMinutes { get; set; }

    [Display(Name = "Xáo trộn thứ tự câu hỏi")]
    public bool Shuffle { get; set; } = true;

    [Display(Name = "Trạng thái")]
    public string Status { get; set; } = QuestionStatus.Draft;

    public List<int> QuestionIds { get; set; } = [];

    public IReadOnlyList<QuestionRow> Available { get; set; } = [];

    // After the first attempt the question set is fixed, so every result is scored on the same questions.
    public bool Locked { get; set; }
}

public record SuggestedQuestion(string Text, string Type, int Difficulty, string? Explanation, IReadOnlyList<string> Options, int Correct);

public record SuggestionResult(IReadOnlyList<SuggestedQuestion> Questions, string Model, string? Problem);

// ---------- children ----------

public record KidQuizCard(int Id, string Title, int LessonId, string LessonTitle, string SubjectCode, string Subject, string? ColorKey, int Questions,
    int? TimeLimitSec, DateOnly? DueDate, decimal? BestScore, bool Assigned)
{
    public string[] Gradient => SubjectCatalog.Look(SubjectCode, Subject, ColorKey, null, true).C;
}

public record QuizOptionView(int Id, string Text);

public record QuizQuestionView(int Id, string Text, string Type, IReadOnlyList<QuizOptionView> Options);

public class KidQuizViewModel
{
    public long AttemptId { get; init; }
    public int QuizId { get; init; }
    public required string Title { get; init; }
    public int LessonId { get; init; }
    public required string LessonTitle { get; init; }
    public int? SecondsLeft { get; init; }
    public DateOnly? DueDate { get; init; }
    public IReadOnlyList<QuizQuestionView> Questions { get; init; } = [];
}

public record ResultOption(string Text, bool IsCorrect, bool Chosen);

public record ResultQuestion(int Number, string Text, string? Explanation, IReadOnlyList<ResultOption> Options, bool Correct, bool Answered);

public class QuizResultViewModel
{
    public long AttemptId { get; init; }
    public int QuizId { get; init; }
    public required string Title { get; init; }
    public int LessonId { get; init; }
    public required string LessonTitle { get; init; }
    public required string ChildName { get; init; }
    public int ChildId { get; init; }
    public decimal Score { get; init; }
    public int Correct { get; init; }
    public int Total { get; init; }
    public bool Passed { get; init; }
    public int PassPercent { get; init; }
    public DateTime SubmittedAt { get; init; }
    public DateOnly? DueDate { get; init; }
    public bool Late { get; init; }
    public bool TimedOut { get; init; }
    public bool CanRetry { get; init; }
    public IReadOnlyList<ResultQuestion> Questions { get; init; } = [];
}

// ---------- parents ----------

public record LessonQuizOption(int Id, string Title, int Questions, int? TimeLimitSec, int PassPercent);

public record QuizAssignmentRow(int AssignmentId, int ChildId, string ChildName, int QuizId, string QuizTitle, string Status, DateOnly? DueDate,
    string? Note, decimal? Score, long? AttemptId, bool Late);

public record HistoryAttemptRow(long Id, string Quiz, string Lesson, decimal? Score, int Correct, int Total, bool? Passed, DateTime SubmittedAt, bool Assigned, bool Late);
