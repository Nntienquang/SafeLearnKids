namespace SafeLearnKids.Web.Services;

// Values match the CHECK constraints on dbo.Lessons and dbo.LessonContents.
public static class LessonWorkflow
{
    public const string Draft = "Draft";
    public const string InReview = "InReview";
    public const string Published = "Published";
    public const string Archived = "Archived";

    public const string VideoContent = "Video";
    public const string ReadingContent = "Reading";
    public const string DocumentContent = "Document";
    public const string ImageContent = "Image";

    public const string VideoLesson = "Video";
    public const string ReadingLesson = "Reading";
    public const string MixedLesson = "Mixed";

    public const string VideoApproved = "Approved";
    public const string YouTubePlatform = "YouTube";

    // The privacy-enhanced YouTube host plays videos without setting tracking cookies until playback starts.
    private const string YouTubeEmbedBase = "https://www.youtube-nocookie.com/embed/";

    public static readonly string[] Statuses = [Draft, InReview, Published, Archived];

    // Admins publish straight from Draft; InReview only exists in older data and can go back to Draft.
    private static readonly HashSet<(string From, string To)> Transitions =
    [
        (Draft, Published),
        (Published, Archived),
        (InReview, Draft),
        (Published, Draft),
        (Archived, Draft)
    ];

    public static bool CanChange(string from, string to) => Transitions.Contains((from, to));

    // Published lessons stay frozen so children never see unreviewed changes.
    public static bool IsEditable(string status) => status == Draft;

    // A video counts only while it is still approved, so a video blocked later stops the lesson being published again.
    public static bool IsPublishable(string kind, string? videoStatus, string? body, string? fileUrl) => kind switch
    {
        VideoContent => videoStatus == VideoApproved,
        ReadingContent => !string.IsNullOrWhiteSpace(body),
        _ => !string.IsNullOrWhiteSpace(fileUrl)
    };

    public static string? EmbedUrlFor(string? platform, string? videoKey) =>
        platform == YouTubePlatform && !string.IsNullOrWhiteSpace(videoKey)
            ? YouTubeEmbedBase + Uri.EscapeDataString(videoKey)
            : null;

    // Rough reading pace for primary-school children, used only to estimate a lesson's length.
    public const int ReadingWordsPerMinute = 100;
    private const int SecondsPerMinute = 60;

    // Videos count their real length and readings their estimated reading time; other parts add nothing.
    public static int DurationMinutesFor(IEnumerable<(string Kind, int? VideoSeconds, string? Body)> parts)
    {
        var seconds = parts.Sum(part => part.Kind switch
        {
            VideoContent => part.VideoSeconds ?? 0,
            ReadingContent => WordCount(part.Body) * SecondsPerMinute / ReadingWordsPerMinute,
            _ => 0
        });
        return (seconds + SecondsPerMinute - 1) / SecondsPerMinute;
    }

    private static int WordCount(string? text) =>
        string.IsNullOrWhiteSpace(text) ? 0 : text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;

    public static string LessonTypeFor(IEnumerable<string> contentKinds)
    {
        var kinds = contentKinds.Distinct().ToList();
        if (kinds.Count == 0 || kinds.All(kind => kind == VideoContent)) return VideoLesson;
        return kinds.Contains(VideoContent) ? MixedLesson : ReadingLesson;
    }
}
