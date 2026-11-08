using System.ComponentModel.DataAnnotations;

namespace SafeLearnKids.Web.Models;

public class LessonFormViewModel
{
    public int? Id { get; set; }

    public int SubjectId { get; set; }

    [Display(Name = "Lớp")]
    [Range(1, int.MaxValue, ErrorMessage = "Chọn lớp cho bài.")]
    public int GradeLevelId { get; set; }

    [Display(Name = "Tên bài")]
    [Required(ErrorMessage = "Nhập tên bài.")]
    [StringLength(200, ErrorMessage = "Tên bài tối đa 200 ký tự.")]
    public string Title { get; set; } = string.Empty;

    [Display(Name = "Mục tiêu / tóm tắt")]
    [StringLength(500, ErrorMessage = "Tóm tắt tối đa 500 ký tự.")]
    public string? Summary { get; set; }

    [Display(Name = "Thứ tự trong môn")]
    [Range(0, 999, ErrorMessage = "Thứ tự từ 0 đến 999.")]
    public int? SortOrder { get; set; }

    public string? RowVersion { get; set; }

    public string SubjectName { get; set; } = string.Empty;
    public IReadOnlyList<GradeOption> Grades { get; set; } = [];
}

public class LessonEditorViewModel
{
    public LessonFormViewModel Form { get; set; } = new();
    public string Status { get; set; } = string.Empty;
    public string LessonType { get; set; } = string.Empty;
    public int DurationMin { get; set; }
    public bool Editable { get; set; }
    public bool SubjectActive { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? PublishedAt { get; set; }
    public string? PublisherName { get; set; }

    public IReadOnlyList<LessonContentRow> Contents { get; set; } = [];
    public IReadOnlyList<VideoOption> Videos { get; set; } = [];

    // Set when an add or edit of a part fails, so the form shows what the admin typed.
    public LessonContentInput? FailedContent { get; set; }
    public string? ContentError { get; set; }
}

public class LessonContentInput
{
    public int? Id { get; set; }
    public string ContentKind { get; set; } = string.Empty;
    public string? Title { get; set; }
    public int? SourceVideoId { get; set; }
    public string? Body { get; set; }
    public string? RowVersion { get; set; }
}

public record LessonContentRow(
    int Id,
    string Kind,
    string? Title,
    int? SourceVideoId,
    string? VideoTitle,
    int? VideoDurationSec,
    string? Body,
    string? FileName,
    bool IsValid,
    string? EmbedUrl);

public record VideoOption(int Id, string Title, int? DurationSec);

// Detail is a short audit text such as "Draft→Published".
public record LessonSaveResult(int? Id, string? Error, bool NotFound = false, bool Conflict = false, string? Detail = null);
