using SafeLearnKids.Web.Services;

namespace SafeLearnKids.Web.Models;

// Values allowed by the CHECK constraints on dbo.Assignments and dbo.LessonActivities.
public static class AssignmentStatus
{
    public const string Assigned = "Assigned";
    public const string InProgress = "InProgress";
    public const string Completed = "Completed";
    public const string Cancelled = "Cancelled";
    public static readonly string[] Active = [Assigned, InProgress];

    public static string Label(string status) => status switch
    {
        Assigned => "Đã giao",
        InProgress => "Đang học",
        Completed => "Đã xong",
        Cancelled => "Đã hủy",
        _ => status
    };

    public static string Tone(string status) => status switch
    {
        Assigned => "t-b",
        InProgress => "t-a",
        Completed => "t-g",
        _ => "t-n"
    };
}

public record KidInfo(int Id, string Name, int Grade, int GradeLevelId, IReadOnlyList<int> SubjectIds);

public record KidLessonCard(int Id, string Title, string SubjectCode, string Subject, string? ColorKey, string LessonType, int DurationMin, int Percent, bool Done, bool Favorite, DateOnly? DueDate)
{
    public string[] Gradient => SubjectCatalog.Look(SubjectCode, Subject, ColorKey, null, true).C;
}

public record KidSubjectTile(string Key, string Name, string Icon, string[] C, int Done, int Total);

public class KidHomeViewModel
{
    public required KidInfo Kid { get; init; }
    public KidLessonCard? Continue { get; init; }
    public IReadOnlyList<KidSubjectTile> Subjects { get; init; } = [];
    public IReadOnlyList<KidLessonCard> Assigned { get; init; } = [];
    public IReadOnlyList<KidLessonCard> Favorites { get; init; } = [];
}

public class KidSubjectViewModel
{
    public required KidInfo Kid { get; init; }
    public required KidSubjectTile Subject { get; init; }
    public IReadOnlyList<KidLessonCard> Lessons { get; init; } = [];
}

public class KidLessonViewModel
{
    public required KidLessonCard Lesson { get; init; }
    public string? Summary { get; init; }
    public required SubjectLook Look { get; init; }
    public IReadOnlyList<LibraryContent> Sections { get; init; } = [];
    // Sections the child has finished, in order; the next one is where the lesson resumes.
    public int SectionsDone { get; init; }
    public string? AssignmentNote { get; init; }
}

public class KidSearchViewModel
{
    public string? Q { get; init; }
    public bool Blocked { get; init; }
    public IReadOnlyList<KidLessonCard> Results { get; init; } = [];
}

public record KidChoice(int Id, string Name, int Grade, string Init);

public record LessonAssignmentRow(int? AssignmentId, int ChildId, string ChildName, int Grade, string? Status, DateOnly? DueDate, string? Note);
