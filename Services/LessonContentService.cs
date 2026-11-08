using Microsoft.EntityFrameworkCore;
using SafeLearnKids.Web.Data;
using SafeLearnKids.Web.Models;
using SafeLearnKids.Web.Models.Entities;

namespace SafeLearnKids.Web.Services;

// Parts of a lesson in the order children study them (Trello US-13).
public sealed class LessonContentService(IServiceProvider services)
{
    private const int MaxTitleLength = 150;
    private const int MaxBodyLength = 20_000;
    private const string SaveFailed = "Chưa thể lưu nội dung. Vui lòng thử lại.";
    private const string Locked = "Đưa bài về Bản nháp để sửa nội dung.";

    private SafeLearnDbContext? Db => services.GetService<SafeLearnDbContext>();

    public async Task<IReadOnlyList<VideoOption>> ApprovedVideosAsync(CancellationToken cancellationToken)
    {
        if (Db is not { } db) return [];
        return await db.SourceVideos.AsNoTracking().Where(video => video.Status == LessonWorkflow.VideoApproved)
            .OrderBy(video => video.Title)
            .Select(video => new VideoOption(video.Id, video.Title, video.DurationSec))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<LessonContentRow>> ContentsAsync(int lessonId, CancellationToken cancellationToken)
    {
        if (Db is not { } db) return [];
        var rows = await db.LessonContents.AsNoTracking().Where(content => content.LessonId == lessonId)
            .OrderBy(content => content.SortOrder).ThenBy(content => content.Id)
            .Select(content => new
            {
                content.Id,
                content.ContentKind,
                content.Title,
                content.SourceVideoId,
                VideoTitle = content.SourceVideo != null ? content.SourceVideo.Title : null,
                VideoDurationSec = content.SourceVideo != null ? content.SourceVideo.DurationSec : null,
                VideoStatus = content.SourceVideo != null ? content.SourceVideo.Status : null,
                VideoPlatform = content.SourceVideo != null ? content.SourceVideo.Platform : null,
                VideoKey = content.SourceVideo != null ? content.SourceVideo.VideoId : null,
                content.Body,
                content.FileUrl,
                content.FileName
            })
            .ToListAsync(cancellationToken);
        return rows.Select(row => new LessonContentRow(row.Id, row.ContentKind, row.Title, row.SourceVideoId, row.VideoTitle,
            row.VideoDurationSec, row.Body, row.FileName,
            LessonWorkflow.IsPublishable(row.ContentKind, row.VideoStatus, row.Body, row.FileUrl),
            LessonWorkflow.EmbedUrlFor(row.VideoPlatform, row.VideoKey))).ToList();
    }

    public async Task<LessonSaveResult> AddAsync(int lessonId, LessonContentInput input, CancellationToken cancellationToken)
    {
        if (Db is not { } db) return new(null, SaveFailed);
        var (lesson, failure) = await LoadAsync(db, lessonId, input.RowVersion, cancellationToken);
        if (lesson is null) return failure;
        var content = new LessonContent { ContentKind = input.ContentKind };
        if (await ApplyAsync(db, content, input, cancellationToken) is { } error) return new(null, error);
        content.SortOrder = lesson.LessonContents.Count + 1;
        lesson.LessonContents.Add(content);
        return await FinishAsync(db, lesson, cancellationToken);
    }

    public async Task<LessonSaveResult> UpdateAsync(int lessonId, LessonContentInput input, CancellationToken cancellationToken)
    {
        if (Db is not { } db) return new(null, SaveFailed);
        var (lesson, failure) = await LoadAsync(db, lessonId, input.RowVersion, cancellationToken);
        if (lesson is null) return failure;
        if (lesson.LessonContents.FirstOrDefault(item => item.Id == input.Id) is not { } content) return new(null, null, NotFound: true);
        // The kind is fixed once a part exists; it never comes from the posted form.
        input.ContentKind = content.ContentKind;
        if (await ApplyAsync(db, content, input, cancellationToken) is { } error) return new(null, error);
        return await FinishAsync(db, lesson, cancellationToken);
    }

    public async Task<LessonSaveResult> MoveAsync(int lessonId, int contentId, string? rowVersion, bool up, CancellationToken cancellationToken)
    {
        if (Db is not { } db) return new(null, SaveFailed);
        var (lesson, failure) = await LoadAsync(db, lessonId, rowVersion, cancellationToken);
        if (lesson is null) return failure;
        var ordered = Ordered(lesson);
        var index = ordered.FindIndex(item => item.Id == contentId);
        if (index < 0) return new(null, null, NotFound: true);
        var target = up ? index - 1 : index + 1;
        if (target >= 0 && target < ordered.Count) (ordered[index], ordered[target]) = (ordered[target], ordered[index]);
        Renumber(ordered);
        return await FinishAsync(db, lesson, cancellationToken);
    }

    public async Task<LessonSaveResult> DeleteAsync(int lessonId, int contentId, string? rowVersion, CancellationToken cancellationToken)
    {
        if (Db is not { } db) return new(null, SaveFailed);
        var (lesson, failure) = await LoadAsync(db, lessonId, rowVersion, cancellationToken);
        if (lesson is null) return failure;
        if (lesson.LessonContents.FirstOrDefault(item => item.Id == contentId) is not { } content) return new(null, null, NotFound: true);
        lesson.LessonContents.Remove(content);
        db.LessonContents.Remove(content);
        return await FinishAsync(db, lesson, cancellationToken);
    }

    private static async Task<(Lesson? Lesson, LessonSaveResult Result)> LoadAsync(
        SafeLearnDbContext db, int lessonId, string? rowVersion, CancellationToken cancellationToken)
    {
        var lesson = await db.Lessons.Include(item => item.LessonContents).FirstOrDefaultAsync(item => item.Id == lessonId, cancellationToken);
        if (lesson is null) return (null, new(null, null, NotFound: true));
        if (!LessonWorkflow.IsEditable(lesson.Status)) return (null, new(null, Locked));
        if (!LessonConcurrency.Expect(db, lesson, rowVersion)) return (null, new(null, LessonConcurrency.Conflict, Conflict: true));
        return (lesson, new(lessonId, null));
    }

    private static async Task<string?> ApplyAsync(SafeLearnDbContext db, LessonContent content, LessonContentInput input, CancellationToken cancellationToken)
    {
        var title = input.Title?.Trim();
        if (title?.Length > MaxTitleLength) return "Tiêu đề phần nội dung tối đa 150 ký tự.";
        switch (input.ContentKind)
        {
            case LessonWorkflow.VideoContent:
                var video = await db.SourceVideos.AsNoTracking()
                    .Where(item => item.Id == input.SourceVideoId && item.Status == LessonWorkflow.VideoApproved)
                    .Select(item => item.Title).FirstOrDefaultAsync(cancellationToken);
                if (video is null) return "Chọn một video đã được duyệt.";
                content.SourceVideoId = input.SourceVideoId;
                content.Title = string.IsNullOrEmpty(title) ? Limit(video, MaxTitleLength) : title;
                return null;
            case LessonWorkflow.ReadingContent:
                var body = input.Body?.Trim();
                if (string.IsNullOrEmpty(body)) return "Nhập nội dung bài đọc.";
                if (body.Length > MaxBodyLength) return "Bài đọc tối đa 20.000 ký tự.";
                content.Body = body;
                content.Title = string.IsNullOrEmpty(title) ? null : title;
                return null;
            default:
                // Documents and images from seed data can be moved or removed, not created or edited yet.
                return "Chỉ thêm hoặc sửa được video và bài đọc.";
        }
    }

    private static async Task<LessonSaveResult> FinishAsync(SafeLearnDbContext db, Lesson lesson, CancellationToken cancellationToken)
    {
        Renumber(Ordered(lesson));
        lesson.LessonType = LessonWorkflow.LessonTypeFor(lesson.LessonContents.Select(item => item.ContentKind));
        var videoIds = lesson.LessonContents.Where(item => item.SourceVideoId is not null).Select(item => item.SourceVideoId!.Value).Distinct().ToList();
        var videoSeconds = await db.SourceVideos.AsNoTracking().Where(video => videoIds.Contains(video.Id))
            .ToDictionaryAsync(video => video.Id, video => video.DurationSec, cancellationToken);
        lesson.DurationMin = LessonWorkflow.DurationMinutesFor(lesson.LessonContents.Select(item => (
            item.ContentKind,
            item.SourceVideoId is { } videoId ? videoSeconds.GetValueOrDefault(videoId) : null,
            item.Body)));
        LessonConcurrency.Touch(lesson);
        return await LessonConcurrency.SaveAsync(db, lesson.Id, cancellationToken);
    }

    // New parts have Id 0 and SortOrder past the end, so they stay last.
    private static List<LessonContent> Ordered(Lesson lesson) =>
        lesson.LessonContents.OrderBy(item => item.SortOrder).ThenBy(item => item.Id == 0).ThenBy(item => item.Id).ToList();

    private static void Renumber(List<LessonContent> ordered)
    {
        for (var index = 0; index < ordered.Count; index++) ordered[index].SortOrder = index + 1;
    }

    private static string Limit(string value, int length) => value.Length > length ? value[..length] : value;
}
