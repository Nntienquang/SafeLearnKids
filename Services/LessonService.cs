using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SafeLearnKids.Web.Data;
using SafeLearnKids.Web.Models;
using SafeLearnKids.Web.Models.Entities;

namespace SafeLearnKids.Web.Services;

// Business tables exist only on SQL, so InMemory demo runs show lessons as unavailable.
public sealed class LessonService(IServiceProvider services)
{
    private const int MaxTitleLength = 200;
    private const string SaveFailed = "Chưa thể lưu bài học. Vui lòng thử lại.";
    private const string GradeNotAllowed = "Lớp này chưa được gán cho môn của bài.";
    private const string Locked = "Đưa bài về Bản nháp để sửa.";
    private const string InUse = "Bài đã có dữ liệu liên quan nên không xóa được. Hãy lưu trữ bài.";

    private SafeLearnDbContext? Db => services.GetService<SafeLearnDbContext>();

    public bool Available => Db is not null;

    public async Task<LessonFormViewModel?> NewFormAsync(int subjectId, CancellationToken cancellationToken)
    {
        if (Db is not { } db) return null;
        var subjectName = await db.Subjects.Where(subject => subject.Id == subjectId)
            .Select(subject => subject.Name).FirstOrDefaultAsync(cancellationToken);
        if (subjectName is null) return null;
        return new LessonFormViewModel
        {
            SubjectId = subjectId,
            SubjectName = subjectName,
            Grades = await GradesAsync(db, subjectId, cancellationToken)
        };
    }

    public async Task<LessonEditorViewModel?> EditorAsync(int id, CancellationToken cancellationToken)
    {
        if (Db is not { } db) return null;
        var editor = await db.Lessons.AsNoTracking().Where(lesson => lesson.Id == id)
            .Select(lesson => new LessonEditorViewModel
            {
                Form = new LessonFormViewModel
                {
                    Id = lesson.Id,
                    SubjectId = lesson.SubjectId,
                    GradeLevelId = lesson.GradeLevelId,
                    Title = lesson.Title,
                    Summary = lesson.Summary,
                    SortOrder = lesson.SortOrder,
                    RowVersion = Convert.ToBase64String(lesson.RowVersion),
                    SubjectName = lesson.Subject.Name
                },
                Status = lesson.Status,
                LessonType = lesson.LessonType,
                DurationMin = lesson.DurationMin,
                SubjectActive = lesson.Subject.IsActive,
                UpdatedAt = lesson.UpdatedAt,
                PublishedAt = lesson.PublishedAt,
                PublisherName = lesson.ApprovedBy != null ? lesson.ApprovedBy.FullName ?? lesson.ApprovedBy.Email : null
            })
            .FirstOrDefaultAsync(cancellationToken);
        if (editor is null) return null;
        editor.Editable = LessonWorkflow.IsEditable(editor.Status);
        editor.Form.Grades = await GradesAsync(db, editor.Form.SubjectId, cancellationToken);
        return editor;
    }

    // Rebuilds the option list when a posted form is shown again with errors.
    public async Task<IReadOnlyList<GradeOption>> GradesAsync(int subjectId, CancellationToken cancellationToken) =>
        Db is { } db ? await GradesAsync(db, subjectId, cancellationToken) : [];

    public async Task<LessonSaveResult> CreateAsync(LessonFormViewModel input, string actorId, CancellationToken cancellationToken)
    {
        if (Db is not { } db) return new(null, SaveFailed);
        if (await ValidateAsync(db, input, cancellationToken) is { } error) return new(null, error);
        var now = DateTime.UtcNow;
        var lesson = new Lesson
        {
            SubjectId = input.SubjectId,
            GradeLevelId = input.GradeLevelId,
            Title = input.Title.Trim(),
            Summary = Clean(input.Summary),
            LessonType = LessonWorkflow.VideoLesson,
            // A new lesson has no content yet; the length follows the content from then on.
            DurationMin = 0,
            SortOrder = input.SortOrder ?? await NextSortOrderAsync(db, input.SubjectId, input.GradeLevelId, cancellationToken),
            Status = LessonWorkflow.Draft,
            CreatedById = actorId,
            CreatedAt = now,
            UpdatedAt = now
        };
        db.Lessons.Add(lesson);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return new(lesson.Id, null);
        }
        catch (DbUpdateException exception) when (SqlErrors.Number(exception) == SqlErrors.ForeignKeyViolation)
        {
            // The subject lost this grade after validation.
            db.ChangeTracker.Clear();
            return new(null, GradeNotAllowed);
        }
    }

    public async Task<LessonSaveResult> UpdateAsync(int id, LessonFormViewModel input, CancellationToken cancellationToken)
    {
        if (Db is not { } db) return new(null, SaveFailed);
        var lesson = await db.Lessons.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (lesson is null) return new(null, null, NotFound: true);
        if (!LessonWorkflow.IsEditable(lesson.Status)) return new(null, Locked);
        input.SubjectId = lesson.SubjectId;
        if (await ValidateAsync(db, input, cancellationToken) is { } error) return new(null, error);
        if (!LessonConcurrency.Expect(db, lesson, input.RowVersion)) return new(null, LessonConcurrency.Conflict, Conflict: true);

        lesson.GradeLevelId = input.GradeLevelId;
        lesson.Title = input.Title.Trim();
        lesson.Summary = Clean(input.Summary);
        lesson.SortOrder = input.SortOrder ?? lesson.SortOrder;
        LessonConcurrency.Touch(lesson);
        return await LessonConcurrency.SaveAsync(db, id, cancellationToken);
    }

    public async Task<LessonSaveResult> ChangeStatusAsync(int id, string? target, string? rowVersion, string actorId, CancellationToken cancellationToken)
    {
        if (Db is not { } db) return new(null, SaveFailed);
        var lesson = await db.Lessons.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (lesson is null) return new(null, null, NotFound: true);
        var from = lesson.Status;
        if (target is null || !LessonWorkflow.CanChange(from, target))
            return new(null, $"Không thể chuyển bài từ \"{CatalogOptions.Status(from).Label}\" sang trạng thái này.");
        if (target == LessonWorkflow.Published && await PublishBlockerAsync(db, id, cancellationToken) is { } blocker) return new(null, blocker);
        if (!LessonConcurrency.Expect(db, lesson, rowVersion)) return new(null, LessonConcurrency.Conflict, Conflict: true);

        lesson.Status = target;
        if (target == LessonWorkflow.Published)
        {
            lesson.ApprovedById = actorId;
            lesson.PublishedAt = DateTime.UtcNow;
        }
        // PublishedAt stays as the time of the last publication.
        if (target == LessonWorkflow.Draft) lesson.ApprovedById = null;
        LessonConcurrency.Touch(lesson);
        return await LessonConcurrency.SaveAsync(db, id, cancellationToken) with { Detail = $"{from}→{target}" };
    }

    public async Task<LessonSaveResult> DeleteAsync(int id, string? rowVersion, CancellationToken cancellationToken)
    {
        if (Db is not { } db) return new(null, SaveFailed);
        var lesson = await db.Lessons.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (lesson is null) return new(null, null, NotFound: true);
        if (!LessonWorkflow.IsEditable(lesson.Status)) return new(null, "Chỉ xóa được bài đang là Bản nháp.");
        if (await HasRelatedDataAsync(db, id, cancellationToken)) return new(null, InUse);
        if (!LessonConcurrency.Expect(db, lesson, rowVersion)) return new(null, LessonConcurrency.Conflict, Conflict: true);
        // Contents and tags are removed by ON DELETE CASCADE.
        db.Lessons.Remove(lesson);
        try
        {
            return await LessonConcurrency.SaveAsync(db, id, cancellationToken) with { Detail = lesson.Title };
        }
        catch (DbUpdateException exception) when (SqlErrors.Number(exception) == SqlErrors.ForeignKeyViolation)
        {
            // Learning data started referencing the lesson after the check above.
            db.ChangeTracker.Clear();
            return new(null, InUse);
        }
    }

    private static async Task<string?> PublishBlockerAsync(SafeLearnDbContext db, int lessonId, CancellationToken cancellationToken)
    {
        var parts = await db.LessonContents.AsNoTracking().Where(content => content.LessonId == lessonId)
            .Select(content => new
            {
                content.ContentKind,
                Title = content.Title ?? (content.SourceVideo != null ? content.SourceVideo.Title : null),
                VideoStatus = content.SourceVideo != null ? content.SourceVideo.Status : null,
                content.Body,
                content.FileUrl
            })
            .ToListAsync(cancellationToken);
        var blockedVideos = parts
            .Where(part => part.ContentKind == LessonWorkflow.VideoContent && part.VideoStatus != LessonWorkflow.VideoApproved)
            .Select(part => part.Title).ToList();
        if (blockedVideos.Count > 0)
            return $"Video không còn được duyệt: {string.Join(", ", blockedVideos)}. Hãy thay hoặc xóa trước khi xuất bản.";
        return parts.Any(part => LessonWorkflow.IsPublishable(part.ContentKind, part.VideoStatus, part.Body, part.FileUrl))
            ? null
            : "Cần ít nhất 1 nội dung hợp lệ (video đã duyệt hoặc bài đọc) để xuất bản.";
    }

    private static async Task<bool> HasRelatedDataAsync(SafeLearnDbContext db, int lessonId, CancellationToken cancellationToken) =>
        await db.Assignments.AnyAsync(item => item.LessonId == lessonId, cancellationToken)
        || await db.ContentReports.AnyAsync(item => item.LessonId == lessonId, cancellationToken)
        || await db.Favorites.AnyAsync(item => item.LessonId == lessonId, cancellationToken)
        || await db.LearningPathItems.AnyAsync(item => item.LessonId == lessonId, cancellationToken)
        || await db.LessonActivities.AnyAsync(item => item.LessonId == lessonId, cancellationToken)
        || await db.Questions.AnyAsync(item => item.LessonId == lessonId, cancellationToken)
        || await db.Quizzes.AnyAsync(item => item.LessonId == lessonId, cancellationToken);

    private static async Task<string?> ValidateAsync(SafeLearnDbContext db, LessonFormViewModel input, CancellationToken cancellationToken)
    {
        var title = input.Title.Trim();
        if (title.Length == 0) return "Nhập tên bài.";
        if (title.Length > MaxTitleLength) return "Tên bài tối đa 200 ký tự.";
        var gradeAllowed = await db.SubjectGrades.AnyAsync(
            item => item.SubjectId == input.SubjectId && item.GradeLevelId == input.GradeLevelId, cancellationToken);
        return gradeAllowed ? null : GradeNotAllowed;
    }

    private static async Task<IReadOnlyList<GradeOption>> GradesAsync(SafeLearnDbContext db, int subjectId, CancellationToken cancellationToken) =>
        await db.SubjectGrades.AsNoTracking().Where(item => item.SubjectId == subjectId)
            .OrderBy(item => item.GradeLevel.Level)
            .Select(item => new GradeOption(item.GradeLevelId, item.GradeLevel.Name))
            .ToListAsync(cancellationToken);

    private static async Task<int> NextSortOrderAsync(SafeLearnDbContext db, int subjectId, int gradeLevelId, CancellationToken cancellationToken) =>
        (await db.Lessons.Where(lesson => lesson.SubjectId == subjectId && lesson.GradeLevelId == gradeLevelId)
            .MaxAsync(lesson => (int?)lesson.SortOrder, cancellationToken) ?? 0) + 1;

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

// Every change to a lesson or its parts goes through the lesson RowVersion so two admins never overwrite each other.
internal static class LessonConcurrency
{
    public const string Conflict = "Bài đã được người khác thay đổi. Hãy tải lại trang.";

    public static bool Expect(SafeLearnDbContext db, Lesson lesson, string? rowVersion)
    {
        Span<byte> buffer = stackalloc byte[16];
        if (string.IsNullOrEmpty(rowVersion) || !Convert.TryFromBase64String(rowVersion, buffer, out var length)) return false;
        db.Entry(lesson).Property(item => item.RowVersion).OriginalValue = buffer[..length].ToArray();
        return true;
    }

    public static void Touch(Lesson lesson) => lesson.UpdatedAt = DateTime.UtcNow;

    public static async Task<LessonSaveResult> SaveAsync(SafeLearnDbContext db, int lessonId, CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return new(lessonId, null);
        }
        catch (DbUpdateConcurrencyException)
        {
            db.ChangeTracker.Clear();
            return new(null, Conflict, Conflict: true);
        }
    }
}

// SQL Server error numbers the catalog and lesson services turn into friendly messages.
internal static class SqlErrors
{
    public const int ForeignKeyViolation = 547;
    public const int UniqueIndexViolation = 2601;
    public const int UniqueConstraintViolation = 2627;

    public static int? Number(Exception exception) => (exception.GetBaseException() as SqlException)?.Number;

    public static bool IsDuplicate(Exception exception) => Number(exception) is UniqueIndexViolation or UniqueConstraintViolation;
}
