using System.ComponentModel.DataAnnotations;
using SafeLearnKids.Web.Services;

namespace SafeLearnKids.Web.Models;

public static class CatalogOptions
{
    // Icon classes from theme.css and tone classes t-*, stored as their bare keys.
    public static readonly string[] Icons = ["calc", "pen", "flask", "globe", "book", "heart", "shield", "star", "sparkles", "smile"];
    public static readonly string[] Tones = ["p", "g", "a", "r", "t", "v", "b"];
    public static readonly string[] Statuses = LessonWorkflow.Statuses;

    // Seed rows use keys like "subject-1" and "blue", so unknown values fall back to neutral.
    public static string Icon(string? key) => Icons.Contains(key) ? key! : "book";
    public static string Tone(string? key) => Tones.Contains(key) ? key! : "n";

    public static (string Label, string Tone) Status(string status) => status switch
    {
        LessonWorkflow.Published => ("Đã xuất bản", "g"),
        LessonWorkflow.InReview => ("Chờ duyệt", "b"),
        LessonWorkflow.Archived => ("Lưu trữ", "n"),
        _ => ("Bản nháp", "a")
    };

    public static string LessonTypeLabel(string type) => type switch
    {
        LessonWorkflow.ReadingLesson => "Bài đọc",
        LessonWorkflow.MixedLesson => "Kết hợp",
        _ => "Video"
    };

    public static string LessonIcon(string type) => type == LessonWorkflow.ReadingLesson ? "book" : "playc";
}

public record SubjectRow(int Id, string Code, string Name, string Icon, string Tone, bool IsActive, int Lessons, IReadOnlyList<int> Grades);

public record LessonRow(int Id, string Title, string Grade, string LessonType, int Contents, string Status, DateTime UpdatedAt);

public record GradeOption(int Id, string Name);

public record CatalogSaveResult(int? Id, string? Error, bool NotFound = false);

public class CatalogIndexViewModel
{
    public bool Available { get; set; } = true;
    public IReadOnlyList<SubjectRow> Subjects { get; set; } = [];
    public SubjectRow? Selected { get; set; }
    public IReadOnlyList<LessonRow> Lessons { get; set; } = [];
    public IReadOnlyList<GradeOption> Grades { get; set; } = [];
    public int? Grade { get; set; }
    public string? Status { get; set; }
}

public class SubjectFormViewModel
{
    public int? Id { get; set; }

    [Display(Name = "Mã môn")]
    [Required(ErrorMessage = "Nhập mã môn.")]
    [StringLength(30, MinimumLength = 2, ErrorMessage = "Mã môn cần từ 2 đến 30 ký tự.")]
    [RegularExpression("^[A-Za-z0-9_-]+$", ErrorMessage = "Mã môn chỉ gồm chữ không dấu, số, _ và -.")]
    public string Code { get; set; } = string.Empty;

    [Display(Name = "Tên môn")]
    [Required(ErrorMessage = "Nhập tên môn.")]
    [StringLength(80, MinimumLength = 2, ErrorMessage = "Tên môn cần từ 2 đến 80 ký tự.")]
    public string Name { get; set; } = string.Empty;

    [Display(Name = "Mô tả")]
    [StringLength(300, ErrorMessage = "Mô tả tối đa 300 ký tự.")]
    public string? Description { get; set; }

    [Display(Name = "Thứ tự hiển thị")]
    [Range(0, 999, ErrorMessage = "Thứ tự từ 0 đến 999.")]
    public int SortOrder { get; set; }

    [Display(Name = "Biểu tượng")]
    public string IconKey { get; set; } = "book";

    [Display(Name = "Màu")]
    public string ColorKey { get; set; } = "p";

    [Display(Name = "Lớp áp dụng")]
    public List<int> GradeLevelIds { get; set; } = [];

    public bool IsActive { get; set; } = true;
    public int Lessons { get; set; }
    public IReadOnlyList<GradeOption> Grades { get; set; } = [];
}
