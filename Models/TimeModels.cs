using System.ComponentModel.DataAnnotations;

namespace SafeLearnKids.Web.Models;

// Values allowed by the CHECK constraints on dbo.TimeExtensionRequests and dbo.ContentReports.
public static class RequestStatus
{
    public const string Pending = "Pending";
    public const string Approved = "Approved";
    public const string Denied = "Denied";
    public const string Expired = "Expired";

    public static string Label(string status) => status switch
    {
        Pending => "Đang chờ",
        Approved => "Đã cho thêm",
        Denied => "Từ chối",
        _ => "Hết hạn"
    };

    public static string Tone(string status) => status switch
    {
        Pending => "t-a",
        Approved => "t-g",
        Denied => "t-r",
        _ => "t-n"
    };
}

public static class ReportStatus
{
    public const string Open = "Open";
    public const string InReview = "InReview";
    public const string Resolved = "Resolved";
    public const string Dismissed = "Dismissed";
    public static readonly string[] All = [Open, InReview, Resolved, Dismissed];
    public static readonly string[] Reasons = ["Inappropriate", "Scary", "Ads", "Wrong", "Other"];

    public static string Label(string status) => status switch
    {
        Open => "Chờ xử lý",
        InReview => "Đang xem",
        Resolved => "Đã gỡ nội dung",
        _ => "Giữ nội dung"
    };

    public static string Tone(string status) => status switch
    {
        Open => "t-r",
        InReview => "t-a",
        Resolved => "t-g",
        _ => "t-n"
    };

    public static string Reason(string reason) => reason switch
    {
        "Inappropriate" => "Không phù hợp với trẻ",
        "Scary" => "Hình ảnh làm bé sợ",
        "Ads" => "Có quảng cáo lạ",
        "Wrong" => "Nội dung sai",
        _ => "Lý do khác"
    };
}

// Today's allowance for one child, as the server enforces it.
public record TimeStatus(int LimitMinutes, int ExtraMinutes, int UsedSeconds, TimeOnly? WindowStart, TimeOnly? WindowEnd, int? BreakEvery, string? Blocked)
{
    public int RemainingSeconds => Math.Max(0, (LimitMinutes + ExtraMinutes) * 60 - UsedSeconds);
    public int UsedMinutes => UsedSeconds / 60;
    public int RemainingMinutes => (RemainingSeconds + 59) / 60;
    public bool Allowed => Blocked is null;
}

public class TimeLimitForm
{
    [Range(0, 600, ErrorMessage = "Mỗi ngày từ 0 đến 600 phút.")]
    [Display(Name = "Số phút mỗi ngày")]
    public int DailyMinutes { get; set; } = 60;

    // Index 0 = Monday ... 6 = Sunday; empty means "same as every day".
    public List<int?> Weekdays { get; set; } = [null, null, null, null, null, null, null];

    [Display(Name = "Được học từ")]
    public TimeOnly? StudyStart { get; set; }

    [Display(Name = "Đến")]
    public TimeOnly? StudyEnd { get; set; }

    [Range(5, 180, ErrorMessage = "Nhắc nghỉ sau 5 đến 180 phút.")]
    [Display(Name = "Nhắc nghỉ sau mỗi (phút)")]
    public int? BreakEveryMinutes { get; set; }
}

public record ExtensionRow(int Id, int ChildId, string ChildName, int RequestedMinutes, int? GrantedMinutes, string? Reason, string Status, DateTime RequestedAt, string RowVersion);

public class TimePageViewModel
{
    public required KidChoice Child { get; init; }
    public IReadOnlyList<KidChoice> Children { get; init; } = [];
    public required TimeLimitForm Form { get; init; }
    public required TimeStatus Today { get; init; }
    public IReadOnlyList<ExtensionRow> Requests { get; init; } = [];
}

public record HistoryLessonRow(string Title, string Subject, string Status, int Percent, int WatchMinutes, DateTime StartedAt, DateTime UpdatedAt);

public record HistorySearchRow(string Query, int Results, bool Blocked, DateTime At);

public record ReportRow(int Id, string Target, string Reason, string? Comment, string Status, string? Resolution, string From, DateTime CreatedAt);

public class HistoryViewModel
{
    public required KidChoice Child { get; init; }
    public IReadOnlyList<KidChoice> Children { get; init; } = [];
    public IReadOnlyList<HistoryLessonRow> Lessons { get; init; } = [];
    public IReadOnlyList<(DateOnly Day, int Minutes)> Days { get; init; } = [];
    public IReadOnlyList<HistorySearchRow> Searches { get; init; } = [];
    public IReadOnlyList<ReportRow> Reports { get; init; } = [];
    public IReadOnlyList<HistoryAttemptRow> Attempts { get; init; } = [];
}

public class TimeUpViewModel
{
    public required KidInfo Kid { get; init; }
    public required TimeStatus Today { get; init; }
    public ExtensionRow? Latest { get; init; }
    public int RequestsToday { get; init; }
}

public record AdminReportRow(int Id, string Target, int? LessonId, int? VideoId, string? VideoKey, string Reason, string? Comment, string Status, string? Resolution,
    string From, DateTime CreatedAt, string? HandledBy, string RowVersion);

public class AdminReportsViewModel
{
    public string? Status { get; init; }
    public IReadOnlyDictionary<string, int> Counts { get; init; } = new Dictionary<string, int>();
    public IReadOnlyList<AdminReportRow> Rows { get; init; } = [];
}
