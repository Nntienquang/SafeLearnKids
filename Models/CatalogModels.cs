using System.ComponentModel.DataAnnotations;

namespace SafeLearnKids.Web.Models;

// Values allowed by the CHECK constraints on dbo.Lessons and dbo.LessonContents.
public static class LessonStatus
{
    public const string Draft = "Draft";
    public const string InReview = "InReview";
    public const string Published = "Published";
    public const string Archived = "Archived";
    public static readonly string[] All = [Draft, InReview, Published, Archived];

    public static string Label(string status) => status switch
    {
        Draft => "Bản nháp",
        InReview => "Chờ duyệt",
        Published => "Đã xuất bản",
        Archived => "Lưu trữ",
        _ => status
    };

    public static string Tone(string status) => status switch
    {
        Draft => "t-a",
        InReview => "t-b",
        Published => "t-g",
        _ => "t-n"
    };
}

public static class LessonKind
{
    public static readonly string[] Types = ["Video", "Reading", "Mixed"];
    public static readonly string[] Contents = ["Reading", "Video", "Document", "Image"];

    public static string TypeLabel(string type) => type switch
    {
        "Video" => "Video",
        "Reading" => "Bài đọc",
        "Mixed" => "Tổng hợp",
        _ => type
    };

    public static string ContentLabel(string kind) => kind switch
    {
        "Reading" => "Bài đọc",
        "Video" => "Video",
        "Document" => "Tài liệu PDF",
        "Image" => "Hình ảnh",
        _ => kind
    };

    public static string Icon(string kind) => kind switch
    {
        "Video" => "playc",
        "Reading" => "book",
        "Document" => "file",
        "Image" => "image",
        _ => "layers"
    };
}

public record SubjectLook(string Key, string Name, string Tone, string Icon, string[] C, bool Active);

public record CatalogSubjectRow(int Id, string Code, string Name, string? IconKey, string? ColorKey, bool IsActive, int Lessons, IReadOnlyList<int> Grades);

public record CatalogLessonRow(int Id, string Title, string Subject, int Grade, string LessonType, string Status, int Contents, IReadOnlyList<string> Tags, DateTime UpdatedAt);

public record TagOption(int Id, string Name);

public class CatalogIndexViewModel
{
    public int? Subject { get; init; }
    public int? Grade { get; init; }
    public string? Status { get; init; }
    public string? Type { get; init; }
    public int? Tag { get; init; }
    public string? Q { get; init; }
    public int Page { get; init; }
    public int TotalPages { get; init; }
    public int Matching { get; init; }
    public IReadOnlyList<CatalogSubjectRow> Subjects { get; init; } = [];
    public IReadOnlyList<TagOption> Tags { get; init; } = [];
    public IReadOnlyList<CatalogLessonRow> Rows { get; init; } = [];
}

public class SubjectFormViewModel
{
    public int? Id { get; set; }

    [Required(ErrorMessage = "Nhập tên môn.")]
    [StringLength(80, MinimumLength = 2, ErrorMessage = "Tên môn cần từ 2 đến 80 ký tự.")]
    [Display(Name = "Tên môn")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Nhập mã môn.")]
    [RegularExpression("^[A-Za-z][A-Za-z0-9_]{1,29}$", ErrorMessage = "Mã gồm 2 đến 30 chữ cái không dấu, số hoặc dấu _, bắt đầu bằng chữ.")]
    [Display(Name = "Mã môn")]
    public string Code { get; set; } = string.Empty;

    [StringLength(300, ErrorMessage = "Mô tả tối đa 300 ký tự.")]
    [Display(Name = "Mô tả")]
    public string? Description { get; set; }

    [Display(Name = "Biểu tượng")]
    public string IconKey { get; set; } = "book";

    [Display(Name = "Màu")]
    public string ColorKey { get; set; } = "blue";

    [Range(0, 999, ErrorMessage = "Thứ tự từ 0 đến 999.")]
    [Display(Name = "Thứ tự hiển thị")]
    public int SortOrder { get; set; }

    [Display(Name = "Đang dùng")]
    public bool IsActive { get; set; } = true;

    public List<int> Grades { get; set; } = [];

    public int Lessons { get; set; }
}

public class LessonFormViewModel
{
    public int? Id { get; set; }

    [Required(ErrorMessage = "Nhập tên bài.")]
    [StringLength(200, MinimumLength = 3, ErrorMessage = "Tên bài cần từ 3 đến 200 ký tự.")]
    [Display(Name = "Tên bài")]
    public string Title { get; set; } = string.Empty;

    [StringLength(500, ErrorMessage = "Mục tiêu tối đa 500 ký tự.")]
    [Display(Name = "Mục tiêu bài học")]
    public string? Summary { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Chọn môn học.")]
    [Display(Name = "Môn")]
    public int SubjectId { get; set; }

    [Range(1, 5, ErrorMessage = "Chọn lớp.")]
    [Display(Name = "Lớp")]
    public int Grade { get; set; }

    [Display(Name = "Loại bài")]
    public string LessonType { get; set; } = "Video";

    [Range(1, 180, ErrorMessage = "Thời lượng từ 1 đến 180 phút.")]
    [Display(Name = "Thời lượng (phút)")]
    public int DurationMin { get; set; } = 10;

    [Range(0, 9999, ErrorMessage = "Thứ tự từ 0 đến 9999.")]
    [Display(Name = "Thứ tự trong môn")]
    public int SortOrder { get; set; }

    [StringLength(400, ErrorMessage = "Nhãn quá dài.")]
    [Display(Name = "Nhãn")]
    public string? Tags { get; set; }

    public string? RowVersion { get; set; }

    public string Status { get; set; } = LessonStatus.Draft;
    public DateTime? PublishedAt { get; set; }
    public string? ApprovedBy { get; set; }
    public IReadOnlyList<LessonContentRow> Contents { get; set; } = [];
    public IReadOnlyList<CatalogSubjectRow> Subjects { get; set; } = [];
    public IReadOnlyList<VideoOption> Videos { get; set; } = [];
    public IReadOnlyList<TagOption> KnownTags { get; set; } = [];
}

public record LessonContentRow(int Id, string Kind, string? Title, string? Detail, string? FileUrl, string? VideoStatus);

public record VideoOption(int Id, string Title, int? DurationSec);

public class LessonContentInput
{
    public string Kind { get; set; } = "Reading";

    [StringLength(150, ErrorMessage = "Tiêu đề tối đa 150 ký tự.")]
    public string? Title { get; set; }

    [StringLength(20000, ErrorMessage = "Bài đọc tối đa 20.000 ký tự.")]
    public string? Body { get; set; }

    public int? VideoId { get; set; }
}

public record LibraryCard(int Id, string Title, string SubjectKey, string Subject, int Grade, string LessonType, int DurationMin, IReadOnlyList<string> Tags, int AssignedTo = 0);

public class LibraryViewModel
{
    public string? Subject { get; init; }
    public int? Grade { get; init; }
    public string? Type { get; init; }
    public int? Tag { get; init; }
    public string? Q { get; init; }
    public int Page { get; init; }
    public int TotalPages { get; init; }
    public int Matching { get; init; }
    public IReadOnlyList<SubjectLook> Subjects { get; init; } = [];
    public IReadOnlyList<TagOption> Tags { get; init; } = [];
    public IReadOnlyList<LibraryCard> Cards { get; init; } = [];
}

public record LibraryContent(string Kind, string? Title, string? Body, string? VideoId, string? FileUrl, string? FileName);

public class LibraryDetailViewModel
{
    public required LibraryCard Lesson { get; init; }
    public string? Summary { get; init; }
    public required SubjectLook Look { get; init; }
    public IReadOnlyList<LibraryContent> Contents { get; init; } = [];
    public IReadOnlyList<LessonAssignmentRow> Children { get; init; } = [];
    public IReadOnlyList<LessonQuizOption> Quizzes { get; init; } = [];
    public IReadOnlyList<QuizAssignmentRow> QuizAssignments { get; init; } = [];
}
