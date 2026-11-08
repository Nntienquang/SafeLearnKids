using System.ComponentModel.DataAnnotations;

namespace SafeLearnKids.Web.Models;

// Values allowed by the CHECK constraints on dbo.Badges and dbo.ChildPaths.
public static class BadgeCriteria
{
    public const string LessonsCompleted = "LessonsCompleted";
    public const string StreakDays = "StreakDays";
    public const string QuizPerfect = "QuizPerfect";
    public const string MinutesTotal = "MinutesTotal";
    public static readonly string[] All = [LessonsCompleted, StreakDays, QuizPerfect, MinutesTotal];

    public static string Label(string type) => type switch
    {
        LessonsCompleted => "Số bài học đã hoàn thành",
        StreakDays => "Số ngày học liên tiếp",
        QuizPerfect => "Số bài tập đạt 100%",
        MinutesTotal => "Tổng số phút học",
        _ => type
    };

    // How the child reads the goal, e.g. "Học xong 5 bài".
    public static string Goal(string type, int threshold) => type switch
    {
        LessonsCompleted => $"Học xong {threshold} bài",
        StreakDays => $"Học {threshold} ngày liên tiếp",
        QuizPerfect => $"Đạt 100% ở {threshold} bài tập",
        MinutesTotal => $"Học tổng cộng {threshold} phút",
        _ => threshold.ToString()
    };

    // Icon names the stylesheet knows; anything else falls back to one that fits the criteria.
    public static readonly string[] Icons = ["trophy", "star", "flame", "book", "target", "clock", "zap", "smile", "grad", "heart", "calc", "flask"];

    public static string Icon(string? key, string type) => key is not null && Icons.Contains(key) ? key : type switch
    {
        StreakDays => "flame",
        LessonsCompleted => "book",
        QuizPerfect => "star",
        MinutesTotal => "clock",
        _ => "trophy"
    };
}

public static class PathStatus
{
    public const string NotStarted = "NotStarted";
    public const string InProgress = "InProgress";
    public const string Completed = "Completed";

    public static string Label(string status) => status switch
    {
        NotStarted => "Chưa bắt đầu",
        InProgress => "Đang học",
        Completed => "Đã hoàn thành",
        _ => status
    };

    public static string Tone(string status) => status switch
    {
        InProgress => "t-a",
        Completed => "t-g",
        _ => "t-n"
    };
}

public record BadgeView(int Id, string Name, string Description, string Icon, string CriteriaType, int Threshold, int Current, DateTime? AwardedAt)
{
    public bool Earned => AwardedAt is not null;
    public int Percent => Threshold <= 0 ? 100 : Math.Min(100, Current * 100 / Threshold);
}

public record StreakView(int Current, int Longest, bool StudiedToday);

public record SubjectProgressRow(int SubjectId, string Name, string Key, string[] C, int Completed, int Total, decimal? AvgQuiz)
{
    public int Percent => Total == 0 ? 0 : Completed * 100 / Total;
}

public record PathStep(int ItemId, int LessonId, string Title, bool Required, bool Done, bool Current);

public record PathView(int Id, string Name, string? Description, string Subject, int Grade, string Status, IReadOnlyList<PathStep> Steps, int? ChildPathId)
{
    public int Done => Steps.Count(step => step.Done && step.Required);
    public int Required => Steps.Count(step => step.Required);
    public PathStep? Next => Steps.FirstOrDefault(step => step.Current);
}

public record WeekGoalView(DateOnly WeekStart, int? TargetLessons, int? TargetMinutes, int Lessons, int Minutes)
{
    public bool HasGoal => TargetLessons is not null || TargetMinutes is not null;
    public bool Reached => HasGoal && (TargetLessons is null || Lessons >= TargetLessons) && (TargetMinutes is null || Minutes >= TargetMinutes);
}

public record ReviewQuestion(int QuestionId, string Text, string? Explanation, string Quiz, int QuizId, string Lesson, IReadOnlyList<QuizOptionView> Options, int? LastChosen, DateTime AnsweredAt);

public class ProgressViewModel
{
    public required KidChoice Child { get; init; }
    public IReadOnlyList<KidChoice> Children { get; init; } = [];
    public IReadOnlyList<SubjectProgressRow> Subjects { get; init; } = [];
    public IReadOnlyList<PathView> Paths { get; init; } = [];
    public IReadOnlyList<BadgeView> Badges { get; init; } = [];
    public required StreakView Streak { get; init; }
    public required WeekGoalView Week { get; init; }
    public WeekGoalView? LastWeek { get; init; }
    public int ReviewCount { get; init; }
    public decimal? AvgQuiz { get; init; }
    public int CompletedLessons { get; init; }
    public IReadOnlyList<(string Title, decimal Score, long AttemptId)> Weak { get; init; } = [];
}

public class KidBadgesViewModel
{
    public required KidInfo Kid { get; init; }
    public IReadOnlyList<BadgeView> Badges { get; init; } = [];
    public required StreakView Streak { get; init; }
    public int Stars { get; init; }
}

public class KidReviewViewModel
{
    public required KidInfo Kid { get; init; }
    public IReadOnlyList<ReviewQuestion> Questions { get; init; } = [];
}

public class BadgeFormViewModel
{
    public int? Id { get; set; }

    [Display(Name = "Mã")]
    [Required(ErrorMessage = "Nhập mã huy hiệu.")]
    [RegularExpression("^[A-Za-z0-9_-]{2,40}$", ErrorMessage = "Mã gồm 2–40 chữ không dấu, số, gạch dưới hoặc gạch ngang.")]
    public string Code { get; set; } = "";

    [Display(Name = "Tên huy hiệu")]
    [Required(ErrorMessage = "Nhập tên huy hiệu.")]
    [StringLength(80, ErrorMessage = "Tên tối đa 80 ký tự.")]
    public string Name { get; set; } = "";

    [Display(Name = "Mô tả cho bé")]
    [Required(ErrorMessage = "Nhập mô tả.")]
    [StringLength(200, ErrorMessage = "Mô tả tối đa 200 ký tự.")]
    public string Description { get; set; } = "";

    [Display(Name = "Biểu tượng")]
    public string IconKey { get; set; } = "trophy";

    [Display(Name = "Tiêu chí")]
    public string CriteriaType { get; set; } = BadgeCriteria.LessonsCompleted;

    [Display(Name = "Mốc cần đạt")]
    [Range(1, 100000, ErrorMessage = "Mốc từ 1 đến 100000.")]
    public int Threshold { get; set; } = 5;

    [Display(Name = "Đang dùng")]
    public bool IsActive { get; set; } = true;

    public int Awarded { get; set; }
}

public record AdminBadgeRow(int Id, string Code, string Name, string Description, string Icon, string CriteriaType, int Threshold, bool IsActive, int Awarded);

public record AdminPathRow(int Id, string Name, string Subject, int Grade, bool IsActive, int Lessons, int Children);

public class PathFormViewModel
{
    public int? Id { get; set; }

    [Display(Name = "Tên lộ trình")]
    [Required(ErrorMessage = "Nhập tên lộ trình.")]
    [StringLength(120, ErrorMessage = "Tên tối đa 120 ký tự.")]
    public string Name { get; set; } = "";

    [Display(Name = "Mục tiêu của lộ trình")]
    [StringLength(300, ErrorMessage = "Mô tả tối đa 300 ký tự.")]
    public string? Description { get; set; }

    [Display(Name = "Môn học")]
    public int SubjectId { get; set; }

    [Display(Name = "Lớp")]
    [Range(1, 5, ErrorMessage = "Chọn lớp 1 đến 5.")]
    public int Grade { get; set; } = 1;

    [Display(Name = "Đang dùng")]
    public bool IsActive { get; set; } = true;

    public List<PathStepInput> Steps { get; set; } = [];

    public IReadOnlyList<SubjectRowOption> Subjects { get; set; } = [];
    public string SubjectName { get; set; } = "";

    // Children already following it: the subject and grade stay fixed once created.
    public int Followers { get; set; }
}

// One published lesson of the path's subject and grade; Include puts it on the path at position Order.
public class PathStepInput
{
    public int LessonId { get; set; }
    public string Title { get; set; } = "";
    public bool Include { get; set; }
    public bool Required { get; set; } = true;
    public int Order { get; set; }
}

public class GoalForm
{
    [Range(0, 100, ErrorMessage = "Số bài từ 0 đến 100.")]
    public int? TargetLessons { get; set; }

    [Range(0, 3000, ErrorMessage = "Số phút từ 0 đến 3000.")]
    public int? TargetMinutes { get; set; }
}
