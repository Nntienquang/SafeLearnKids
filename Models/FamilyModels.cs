using System.ComponentModel.DataAnnotations;

namespace SafeLearnKids.Web.Models;

// ---------- A: family lessons ----------

public static class FamilyRules
{
    public const int MaxLessons = 100;
    public const int MaxQuestions = 30;
    public const int MaxOpenRequests = 10;

    // A family lesson is either a draft or ready for the family's children; Archived hides it once children used it.
    public static string Label(string status) => status switch
    {
        LessonStatus.Published => "Bé học được",
        LessonStatus.Archived => "Đã ẩn",
        _ => "Nháp"
    };

    public static string Tone(string status) => status switch
    {
        LessonStatus.Published => "t-g",
        LessonStatus.Archived => "t-n",
        _ => "t-a"
    };
}

public class FamilyLessonForm
{
    [Display(Name = "Tên bài")]
    [Required(ErrorMessage = "Nhập tên bài.")]
    [StringLength(200, ErrorMessage = "Tên bài tối đa 200 ký tự.")]
    public string Title { get; set; } = "";

    [Display(Name = "Mục tiêu bài học")]
    [StringLength(500, ErrorMessage = "Mục tiêu tối đa 500 ký tự.")]
    public string? Summary { get; set; }

    [Display(Name = "Môn")]
    public int SubjectId { get; set; }

    [Display(Name = "Lớp")]
    [Range(1, 5, ErrorMessage = "Chọn lớp 1 đến 5.")]
    public int Grade { get; set; } = 1;

    [Display(Name = "Loại bài")]
    public string LessonType { get; set; } = "Reading";

    [Display(Name = "Thời lượng (phút)")]
    [Range(1, 180, ErrorMessage = "Thời lượng từ 1 đến 180 phút.")]
    public int DurationMin { get; set; } = 10;

    [Display(Name = "Trạng thái")]
    public string Status { get; set; } = LessonStatus.Draft;
}

public class FamilyQuestionForm
{
    [Required(ErrorMessage = "Nhập câu hỏi.")]
    [StringLength(500, ErrorMessage = "Câu hỏi tối đa 500 ký tự.")]
    public string Text { get; set; } = "";

    public string Type { get; set; } = QuestionType.SingleChoice;

    [StringLength(500, ErrorMessage = "Giải thích tối đa 500 ký tự.")]
    public string? Explanation { get; set; }

    public List<string?> Options { get; set; } = ["", "", "", ""];

    public int Correct { get; set; }
}

public record FamilyLessonRow(int Id, string Title, string Subject, int Grade, string Status, string? ShareStatus, int Contents, int Questions, int Learners, DateTime UpdatedAt);

public record FamilyContentRow(int Id, string Kind, string? Title, string? Body, string? VideoTitle, string? FileUrl, string? FileName);

public record VideoChoice(int Id, string Title);

public class FamilyEditorViewModel
{
    public int? Id { get; init; }
    public required FamilyLessonForm Form { get; init; }
    public IReadOnlyList<SubjectRowOption> Subjects { get; init; } = [];
    public IReadOnlyList<FamilyContentRow> Contents { get; init; } = [];
    public IReadOnlyList<QuestionRow> Questions { get; init; } = [];
    public IReadOnlyList<VideoChoice> Videos { get; init; } = [];
    public string? ShareStatus { get; init; }
    public string? ShareNote { get; init; }
    public int Learners { get; init; }
    public int? QuizId { get; init; }
}

public class FamilyLessonsViewModel
{
    public IReadOnlyList<FamilyLessonRow> Rows { get; init; } = [];
}

// ---------- B: lesson requests ----------

public static class RequestState
{
    public const string Open = "Open";
    public const string Planned = "Planned";
    public const string Done = "Done";
    public const string Declined = "Declined";
    public static readonly string[] All = [Open, Planned, Done, Declined];

    public static string Label(string status) => status switch
    {
        Open => "Đang chờ",
        Planned => "Đang soạn",
        Done => "Đã có bài",
        Declined => "Chưa thực hiện",
        _ => status
    };

    public static string Tone(string status) => status switch
    {
        Open => "t-a",
        Planned => "t-b",
        Done => "t-g",
        _ => "t-n"
    };
}

public class LessonRequestForm
{
    [Display(Name = "Môn")]
    public int SubjectId { get; set; }

    [Display(Name = "Lớp")]
    [Range(1, 5, ErrorMessage = "Chọn lớp 1 đến 5.")]
    public int Grade { get; set; } = 1;

    [Display(Name = "Chủ đề bé cần học")]
    [Required(ErrorMessage = "Nhập chủ đề.")]
    [StringLength(200, MinimumLength = 3, ErrorMessage = "Chủ đề từ 3 đến 200 ký tự.")]
    public string Topic { get; set; } = "";

    [Display(Name = "Ghi chú thêm (không bắt buộc)")]
    [StringLength(500, ErrorMessage = "Ghi chú tối đa 500 ký tự.")]
    public string? Note { get; set; }
}

public record LessonRequestRow(int Id, string Subject, int Grade, string Topic, string? Note, string Status, string? Reply, int? LessonId, int Votes,
    bool Mine, bool Voted, DateTime CreatedAt, string? Parent = null);

public class LessonRequestsViewModel
{
    public required LessonRequestForm Form { get; init; }
    public IReadOnlyList<SubjectRowOption> Subjects { get; init; } = [];
    public IReadOnlyList<LessonRequestRow> Mine { get; init; } = [];
    public IReadOnlyList<LessonRequestRow> Others { get; init; } = [];
}

public class AdminLessonRequestsViewModel
{
    public string Status { get; init; } = RequestState.Open;
    public IReadOnlyDictionary<string, int> Counts { get; init; } = new Dictionary<string, int>();
    public IReadOnlyList<LessonRequestRow> Rows { get; init; } = [];
}

// ---------- C: community library ----------

public record CommunityRow(int Id, string Title, string Subject, int Grade, string Author, string? ShareStatus, string? ShareNote, DateTime? SharedAt,
    int Contents, int Questions, int Users);

public class CommunityQueueViewModel
{
    public string Status { get; init; } = "Pending";
    public IReadOnlyDictionary<string, int> Counts { get; init; } = new Dictionary<string, int>();
    public IReadOnlyList<CommunityRow> Rows { get; init; } = [];
}

public class CommunityPreviewViewModel
{
    public required CommunityRow Lesson { get; init; }
    public string? Summary { get; init; }
    public string LessonType { get; init; } = "";
    public int DurationMin { get; init; }
    public IReadOnlyList<LibraryContent> Contents { get; init; } = [];
    public IReadOnlyList<QuestionRow> Questions { get; init; } = [];
}
