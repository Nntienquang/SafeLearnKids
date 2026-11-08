using System.ComponentModel.DataAnnotations;

namespace SafeLearnKids.Web.Models;

// Values allowed by the CHECK constraints on dbo.ContentSources, dbo.SourceVideos and dbo.ParentSources.
public static class SourceStatus
{
    public const string Suggested = "Suggested";
    public const string Approved = "Approved";
    public const string Rejected = "Rejected";
    public const string Suspended = "Suspended";
    public static readonly string[] All = [Suggested, Approved, Rejected, Suspended];

    public static string Label(string status) => status switch
    {
        Suggested => "Chờ duyệt",
        Approved => "Đã duyệt",
        Rejected => "Bị từ chối",
        Suspended => "Tạm ngưng",
        _ => status
    };

    public static string Tone(string status) => status switch
    {
        Suggested => "t-a",
        Approved => "t-g",
        Rejected => "t-r",
        _ => "t-n"
    };

    public static string PlatformLabel(string platform) => platform switch
    {
        "YouTubeChannel" => "Kênh YouTube",
        "YouTubePlaylist" => "Danh sách phát",
        _ => "Website"
    };
}

public static class VideoStatus
{
    public const string Pending = "Pending";
    public const string Approved = "Approved";
    public const string Blocked = "Blocked";
    public static readonly string[] All = [Pending, Approved, Blocked];

    public static string Label(string status) => status switch
    {
        Pending => "Chờ duyệt",
        Approved => "Đã duyệt",
        Blocked => "Đã chặn",
        _ => status
    };

    public static string Tone(string status) => status switch
    {
        Pending => "t-a",
        Approved => "t-g",
        _ => "t-r"
    };
}

public class SourceFormViewModel
{
    public int? Id { get; set; }

    [Required(ErrorMessage = "Dán liên kết của nguồn.")]
    [StringLength(300, ErrorMessage = "Liên kết tối đa 300 ký tự.")]
    [Display(Name = "Liên kết")]
    public string Url { get; set; } = string.Empty;

    [Required(ErrorMessage = "Nhập tên nguồn.")]
    [StringLength(150, MinimumLength = 2, ErrorMessage = "Tên nguồn cần từ 2 đến 150 ký tự.")]
    [Display(Name = "Tên nguồn")]
    public string Name { get; set; } = string.Empty;

    [StringLength(500, ErrorMessage = "Mô tả tối đa 500 ký tự.")]
    [Display(Name = "Mô tả")]
    public string? Description { get; set; }
}

public record SourceRow(int Id, string Name, string Platform, string Url, string Status, int Videos, int PendingVideos, decimal? Score, DateTime? LastSyncedAt, string? LastSyncError, string? SuggestedBy);

public class SourcesViewModel
{
    public string? Status { get; init; }
    public string? Q { get; init; }
    public IReadOnlyDictionary<string, int> Counts { get; init; } = new Dictionary<string, int>();
    public IReadOnlyList<SourceRow> Rows { get; init; } = [];
}

public record VideoRow(int Id, string VideoId, string Title, string Status, string? ThumbnailUrl, DateTime? PublishedAt, string? ReviewNote, IReadOnlyList<string> Sources, int PublishedLessons);

public record AssessmentRow(int Id, decimal Score, string? Flags, string? Summary, string ModelName, string Status, DateTime CreatedAt, string? DecidedBy);

public class SourceDetailViewModel
{
    public required SourceRow Source { get; init; }
    public string? Description { get; init; }
    public string ExternalId { get; init; } = string.Empty;
    public string? ReviewNote { get; init; }
    public string? ReviewedBy { get; init; }
    public DateTime? ReviewedAt { get; init; }
    public DateTime CreatedAt { get; init; }
    public int FamiliesBlocking { get; init; }
    public IReadOnlyList<AssessmentRow> Assessments { get; init; } = [];
    public IReadOnlyList<VideoRow> Videos { get; init; } = [];
}

public class VideosViewModel
{
    public string? Status { get; init; }
    public int? Source { get; init; }
    public string? Q { get; init; }
    public int Page { get; init; }
    public int TotalPages { get; init; }
    public int Matching { get; init; }
    public IReadOnlyDictionary<string, int> Counts { get; init; } = new Dictionary<string, int>();
    public IReadOnlyList<(int Id, string Name)> SourceOptions { get; init; } = [];
    public IReadOnlyList<VideoRow> Rows { get; init; } = [];
}

public record FamilySourceRow(int Id, string Name, string Platform, string Url, int Videos, bool Blocked);

public record SuggestionRow(string Name, string Platform, string Url, string Status, string? ReviewNote, DateTime CreatedAt);

public class FamilySourcesViewModel
{
    public IReadOnlyList<FamilySourceRow> Sources { get; init; } = [];
    public IReadOnlyList<SuggestionRow> Suggestions { get; init; } = [];
    public SourceFormViewModel Suggest { get; init; } = new();
}
