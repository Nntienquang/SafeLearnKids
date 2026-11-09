using System.ComponentModel.DataAnnotations;

namespace SafeLearnKids.Web.Models;

// ---------- US-47 weekly report ----------

public record WeeklySubjectRow(string Name, string[] C, int Lessons, int Attempts, decimal? AvgQuiz);

public class WeeklyViewModel
{
    public required KidChoice Child { get; init; }
    public IReadOnlyList<KidChoice> Children { get; init; } = [];
    public DateOnly WeekStart { get; init; }
    public IReadOnlyList<DateOnly> Weeks { get; init; } = [];
    public bool CurrentWeek { get; init; }
    public int TotalMinutes { get; init; }
    public int LessonsCompleted { get; init; }
    public int QuizAttempts { get; init; }
    public decimal? AvgQuizPercent { get; init; }
    public string? StrongSubject { get; init; }
    public string? NeedsAttentionSubject { get; init; }
    public required string Summary { get; init; }
    public IReadOnlyList<(DateOnly Day, int Minutes)> Days { get; init; } = [];
    public IReadOnlyList<WeeklySubjectRow> Subjects { get; init; } = [];
    public IReadOnlyList<string> Badges { get; init; } = [];
    public WeekGoalView? Goal { get; init; }
    public int? PreviousMinutes { get; init; }
    public DateTime GeneratedAt { get; init; }
    public DateTime? EmailedAt { get; init; }
}

// ---------- US-48 admin dashboard ----------

public record DailyMetric(DateOnly Day, int ActiveParents, int ActiveChildren, long Minutes, int Lessons, int OpenReports, int NewSources);

public class AdminDashboardViewModel
{
    public int Parents { get; init; }
    public int NewParents7 { get; init; }
    public int Children { get; init; }
    public int ActiveChildren7 { get; init; }
    public int PublishedLessons { get; init; }
    public int ApprovedVideos { get; init; }
    public required DailyMetric Today { get; init; }
    public IReadOnlyList<DailyMetric> Trend { get; init; } = [];
    public int OpenReports { get; init; }
    public int PendingSources { get; init; }
    public int PendingVideos { get; init; }
    public int PendingQuestions { get; init; }
    public int PendingDataRequests { get; init; }
    public int PendingCommunity { get; init; }
    public int OpenLessonRequests { get; init; }
    public int FailedLogins24h { get; init; }
}

// ---------- US-49 content statistics ----------

public record LessonStatRow(int Id, string Title, string Subject, int Grade, int Learners, int Completed, int WatchMinutes, int Favorites, int Reports)
{
    public int CompletionRate => Learners == 0 ? 0 : Completed * 100 / Learners;
}

public record QuizStatRow(int Id, string Title, string Lesson, int Attempts, int Children, decimal? AvgScore, int Passed)
{
    public int PassRate => Attempts == 0 ? 0 : Passed * 100 / Attempts;
}

public record QuestionStatRow(int Id, string Text, string Quiz, int Answers, int Wrong)
{
    public int WrongRate => Answers == 0 ? 0 : Wrong * 100 / Answers;
}

public record SubjectStatRow(string Name, int Lessons, int Learners, int Completed, int Minutes);

public record SearchStatRow(string Query, int Count, int Blocked, int NoResult);

public class ContentStatsViewModel
{
    public int Days { get; init; }
    public IReadOnlyList<SubjectStatRow> Subjects { get; init; } = [];
    public IReadOnlyList<LessonStatRow> Lessons { get; init; } = [];
    public IReadOnlyList<QuizStatRow> Quizzes { get; init; } = [];
    public IReadOnlyList<QuestionStatRow> HardQuestions { get; init; } = [];
    public IReadOnlyList<SearchStatRow> Searches { get; init; } = [];
    public int SearchTotal { get; init; }
    public int SearchBlocked { get; init; }
}

// ---------- US-50 notifications and settings ----------

public static class NotificationKind
{
    public static string Icon(string type) => type switch
    {
        "TimeRequest" => "clock",
        "ContentReport" => "flag",
        "Badge" => "trophy",
        "Assignment" => "pen",
        "WeeklyReport" => "file",
        "Security" => "shield",
        _ => "bell"
    };

    public static string Tone(string type) => type switch
    {
        "TimeRequest" => "t-a",
        "ContentReport" => "t-r",
        "Badge" => "t-v",
        "Assignment" => "t-p",
        "WeeklyReport" => "t-b",
        "Security" => "t-r",
        _ => "t-n"
    };

    public static string Label(string type) => type switch
    {
        "TimeRequest" => "Xin thêm giờ",
        "ContentReport" => "Báo cáo nội dung",
        "Badge" => "Huy hiệu",
        "Assignment" => "Bài tập",
        "WeeklyReport" => "Báo cáo tuần",
        "Security" => "Bảo mật",
        _ => "Hệ thống"
    };
}

public record NotificationRow(int Id, string Type, string Title, string? Body, string? Link, bool IsRead, DateTime CreatedAt);

public class NotificationsViewModel
{
    public IReadOnlyList<NotificationRow> Rows { get; init; } = [];
    public bool UnreadOnly { get; init; }
    public int Unread { get; init; }
    public int Page { get; init; }
    public int TotalPages { get; init; }
}

public static class ThemeChoice
{
    public const string Light = "Light";
    public const string Dark = "Dark";
    public const string Auto = "Auto";
    public static readonly string[] All = [Light, Dark, Auto];

    // The page scripts use lower-case names.
    public static string Client(string theme) => theme switch
    {
        Dark => "dark",
        Auto => "auto",
        _ => "light"
    };
}

public class SettingsForm
{
    public string Theme { get; set; } = ThemeChoice.Light;
    public bool WeeklyEmail { get; set; } = true;
    public bool NotifyTimeRequest { get; set; } = true;
    public bool NotifyContentReport { get; set; } = true;
}

public record LoginRow(DateTime At, bool Succeeded, string? Reason, string? Ip, string? Agent);

public class SettingsViewModel
{
    public required SettingsForm Form { get; init; }
    public IReadOnlyList<LoginRow> Logins { get; init; } = [];
}

public class PinViewModel
{
    public bool HasPin { get; init; }
    public DateTime? UpdatedAt { get; init; }
    public bool ChildMode { get; init; }
    public string? ChildName { get; init; }
}

public class PinForm
{
    [Required(ErrorMessage = "Nhập mã PIN mới.")]
    [RegularExpression(@"^\d{4}$", ErrorMessage = "PIN gồm đúng 4 chữ số.")]
    public string Pin { get; set; } = "";

    [Required(ErrorMessage = "Nhập lại mã PIN.")]
    [Compare(nameof(Pin), ErrorMessage = "Hai mã PIN chưa khớp.")]
    public string ConfirmPin { get; set; } = "";

    [Required(ErrorMessage = "Nhập mật khẩu tài khoản để xác nhận.")]
    public string Password { get; set; } = "";
}

public class KidExitViewModel
{
    public bool HasPin { get; init; }
    public string? Error { get; init; }
    public int? LockedMinutes { get; init; }
    public string Back { get; init; } = "/kids-picker";
}

// ---------- US-51 logs ----------

public record AuditRow(long Id, string? Actor, string Action, string EntityType, string? EntityId, string? Detail, string? Ip, DateTime At);

public record LoginHistoryRow(long Id, string? User, string Tried, bool Succeeded, string? Reason, string? Ip, string? Agent, DateTime At);

public class AuditViewModel
{
    public string Tab { get; init; } = "actions";
    public string? Q { get; init; }
    public string? Action { get; init; }
    public string? Result { get; init; }
    public DateOnly? From { get; init; }
    public DateOnly? To { get; init; }
    public int Page { get; init; }
    public int TotalPages { get; init; }
    public int Matching { get; init; }
    public IReadOnlyList<string> Actions { get; init; } = [];
    public IReadOnlyList<AuditRow> Audits { get; init; } = [];
    public IReadOnlyList<LoginHistoryRow> Logins { get; init; } = [];
}

// ---------- US-52 child data ----------

public static class DataRequestStatus
{
    public const string Pending = "Pending";
    public const string Processing = "Processing";
    public const string Done = "Done";
    public const string Rejected = "Rejected";

    public static string Label(string status) => status switch
    {
        Pending => "Chờ xử lý",
        Processing => "Đang xử lý",
        Done => "Đã xong",
        Rejected => "Đã hủy / từ chối",
        _ => status
    };

    public static string Tone(string status) => status switch
    {
        Pending or Processing => "t-a",
        Done => "t-g",
        _ => "t-n"
    };
}

public record DataRequestRow(int Id, string Type, string Status, string? ChildName, int? ChildId, string? Note, DateTime RequestedAt, DateTime? CompletedAt,
    string? Parent = null, string? ProcessedBy = null);

public record ConsentRow(string Type, string Version, string Decision, DateTime DecidedAt, string? Child);

public class PrivacyViewModel
{
    public IReadOnlyList<KidChoice> Children { get; init; } = [];
    public IReadOnlyList<DataRequestRow> Requests { get; init; } = [];
    public IReadOnlyList<ConsentRow> Consents { get; init; } = [];
}

public class AdminDataRequestsViewModel
{
    public string Status { get; init; } = "open";
    public IReadOnlyList<DataRequestRow> Rows { get; init; } = [];
    public int Open { get; init; }
}

// ---------- parent home ----------

public record HomeAttention(string Icon, string Tone, string Title, string? Detail, string Link, string Action);

public record HomeContinue(int LessonId, string Title, string Subject, int Percent);

public class ParentHomeViewModel
{
    public required KidChoice Child { get; init; }
    public IReadOnlyList<KidChoice> Children { get; init; } = [];
    public string? ParentName { get; init; }
    public required TimeStatus Today { get; init; }
    public required WeekGoalView Week { get; init; }
    public required StreakView Streak { get; init; }
    public decimal? AvgQuiz { get; init; }
    public int OpenAssignments { get; init; }
    public IReadOnlyList<(DateOnly Day, int Minutes)> Days { get; init; } = [];
    public IReadOnlyList<SubjectProgressRow> Subjects { get; init; } = [];
    public IReadOnlyList<HomeAttention> Attention { get; init; } = [];
    public HomeContinue? Continue { get; init; }
}
