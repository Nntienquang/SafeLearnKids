using Microsoft.EntityFrameworkCore;
using SafeLearnKids.Web.Data;
using SafeLearnKids.Web.Models;
using SafeLearnKids.Web.Models.Entities;

namespace SafeLearnKids.Web.Services;

// Business tables exist only on SQL, so InMemory demo runs show the catalog as unavailable.
public sealed class CatalogService(IServiceProvider services)
{
    private const string SaveFailed = "Chưa thể lưu môn học. Vui lòng thử lại.";
    private const string CodeTaken = "Mã môn đã tồn tại.";
    private const string InUse = "Môn đã có bài hoặc được bé chọn nên không xóa được. Hãy ẩn môn.";

    private SafeLearnDbContext? Db => services.GetService<SafeLearnDbContext>();

    public bool Available => Db is not null;

    public async Task<IReadOnlyList<SubjectRow>> SubjectsAsync(CancellationToken cancellationToken)
    {
        if (Db is not { } db) return [];
        var rows = await db.Subjects.AsNoTracking()
            .OrderBy(subject => subject.SortOrder).ThenBy(subject => subject.Name)
            .Select(subject => new
            {
                subject.Id,
                subject.Code,
                subject.Name,
                subject.IconKey,
                subject.ColorKey,
                subject.IsActive,
                Lessons = subject.Lessons.Count,
                Grades = subject.SubjectGrades.Select(item => item.GradeLevelId).ToList()
            })
            .ToListAsync(cancellationToken);
        return rows.Select(row => new SubjectRow(row.Id, row.Code, row.Name, CatalogOptions.Icon(row.IconKey), CatalogOptions.Tone(row.ColorKey),
            row.IsActive, row.Lessons, row.Grades)).ToList();
    }

    public async Task<IReadOnlyList<LessonRow>> LessonsAsync(int subjectId, int? gradeLevelId, string? status, CancellationToken cancellationToken)
    {
        if (Db is not { } db) return [];
        var lessons = db.Lessons.AsNoTracking().Where(lesson => lesson.SubjectId == subjectId);
        if (gradeLevelId is { } grade) lessons = lessons.Where(lesson => lesson.GradeLevelId == grade);
        if (status is not null) lessons = lessons.Where(lesson => lesson.Status == status);
        return await lessons
            .OrderBy(lesson => lesson.GradeLevel.Level).ThenBy(lesson => lesson.SortOrder).ThenBy(lesson => lesson.Title)
            .Select(lesson => new LessonRow(lesson.Id, lesson.Title, lesson.GradeLevel.Name, lesson.LessonType,
                lesson.LessonContents.Count, lesson.Status, lesson.UpdatedAt))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<GradeOption>> GradesAsync(CancellationToken cancellationToken)
    {
        if (Db is not { } db) return [];
        return await db.GradeLevels.AsNoTracking().OrderBy(grade => grade.Level)
            .Select(grade => new GradeOption(grade.Id, grade.Name)).ToListAsync(cancellationToken);
    }

    public async Task<SubjectFormViewModel?> FormAsync(int id, CancellationToken cancellationToken)
    {
        if (Db is not { } db) return null;
        return await db.Subjects.AsNoTracking().Where(subject => subject.Id == id)
            .Select(subject => new SubjectFormViewModel
            {
                Id = subject.Id,
                Code = subject.Code,
                Name = subject.Name,
                Description = subject.Description,
                SortOrder = subject.SortOrder,
                IconKey = subject.IconKey ?? string.Empty,
                ColorKey = subject.ColorKey ?? string.Empty,
                IsActive = subject.IsActive,
                Lessons = subject.Lessons.Count,
                GradeLevelIds = subject.SubjectGrades.Select(item => item.GradeLevelId).ToList()
            })
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<CatalogSaveResult> CreateAsync(SubjectFormViewModel input, CancellationToken cancellationToken)
    {
        if (Db is not { } db) return new(null, SaveFailed);
        if (await ValidateAsync(db, input, cancellationToken) is { } error) return new(null, error);
        var code = input.Code.Trim().ToUpperInvariant();
        if (await db.Subjects.AnyAsync(subject => subject.Code == code, cancellationToken)) return new(null, CodeTaken);
        var subject = new Subject
        {
            Code = code,
            Name = input.Name.Trim(),
            Description = Clean(input.Description),
            IconKey = input.IconKey,
            ColorKey = input.ColorKey,
            SortOrder = input.SortOrder,
            IsActive = true,
            SubjectGrades = input.GradeLevelIds.Distinct().Select(grade => new SubjectGrade { GradeLevelId = grade }).ToList()
        };
        db.Subjects.Add(subject);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return new(subject.Id, null);
        }
        catch (DbUpdateException exception) when (SqlErrors.IsDuplicate(exception))
        {
            // Another request took the code after the check above.
            db.ChangeTracker.Clear();
            return new(null, CodeTaken);
        }
    }

    public async Task<CatalogSaveResult> UpdateAsync(int id, SubjectFormViewModel input, CancellationToken cancellationToken)
    {
        if (Db is not { } db) return new(null, SaveFailed);
        var subject = await db.Subjects.Include(item => item.SubjectGrades).FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (subject is null) return new(null, null, NotFound: true);
        if (await ValidateAsync(db, input, cancellationToken) is { } error) return new(null, error);

        var chosen = input.GradeLevelIds.Distinct().ToList();
        var dropped = subject.SubjectGrades.Where(item => !chosen.Contains(item.GradeLevelId)).ToList();
        var droppedIds = dropped.Select(item => item.GradeLevelId).ToList();
        // Lessons reference (SubjectId, GradeLevelId), so a grade with lessons must stay.
        var busy = await db.Lessons.Where(lesson => lesson.SubjectId == id && droppedIds.Contains(lesson.GradeLevelId))
            .Select(lesson => lesson.GradeLevel.Name).Distinct().OrderBy(name => name).ToListAsync(cancellationToken);
        if (busy.Count > 0) return new(null, $"Môn đã có bài ở {string.Join(", ", busy)} nên không bỏ được lớp này.");

        // The code is fixed after creation because parent pages map subjects by code.
        subject.Name = input.Name.Trim();
        subject.Description = Clean(input.Description);
        subject.IconKey = input.IconKey;
        subject.ColorKey = input.ColorKey;
        subject.SortOrder = input.SortOrder;
        db.SubjectGrades.RemoveRange(dropped);
        foreach (var grade in chosen.Where(grade => subject.SubjectGrades.All(item => item.GradeLevelId != grade)))
            subject.SubjectGrades.Add(new SubjectGrade { GradeLevelId = grade });
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return new(id, null);
        }
        catch (DbUpdateException exception) when (SqlErrors.Number(exception) == SqlErrors.ForeignKeyViolation || SqlErrors.IsDuplicate(exception))
        {
            // A lesson was added to a dropped grade, or the same grade was added concurrently.
            db.ChangeTracker.Clear();
            return new(null, SaveFailed);
        }
    }

    public async Task<CatalogSaveResult> SetActiveAsync(int id, bool active, CancellationToken cancellationToken)
    {
        if (Db is not { } db) return new(null, SaveFailed);
        var subject = await db.Subjects.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (subject is null) return new(null, null, NotFound: true);
        subject.IsActive = active;
        await db.SaveChangesAsync(cancellationToken);
        return new(id, null);
    }

    public async Task<CatalogSaveResult> DeleteAsync(int id, CancellationToken cancellationToken)
    {
        if (Db is not { } db) return new(null, SaveFailed);
        var subject = await db.Subjects.Include(item => item.SubjectGrades).FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (subject is null) return new(null, null, NotFound: true);
        if (await db.Lessons.AnyAsync(item => item.SubjectId == id, cancellationToken)
            || await db.ChildSubjects.AnyAsync(item => item.SubjectId == id, cancellationToken)
            || await db.LearningPaths.AnyAsync(item => item.SubjectId == id, cancellationToken)
            || await db.SubjectProgresses.AnyAsync(item => item.SubjectId == id, cancellationToken))
            return new(null, InUse);
        db.SubjectGrades.RemoveRange(subject.SubjectGrades);
        db.Subjects.Remove(subject);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return new(id, null);
        }
        catch (DbUpdateException exception) when (SqlErrors.Number(exception) == SqlErrors.ForeignKeyViolation)
        {
            // Data started referencing the subject after the checks above.
            db.ChangeTracker.Clear();
            return new(null, InUse);
        }
    }

    private static async Task<string?> ValidateAsync(SafeLearnDbContext db, SubjectFormViewModel input, CancellationToken cancellationToken)
    {
        if (input.Name.Trim().Length < 2) return "Tên môn cần từ 2 đến 80 ký tự.";
        if (!CatalogOptions.Icons.Contains(input.IconKey)) return "Chọn biểu tượng cho môn.";
        if (!CatalogOptions.Tones.Contains(input.ColorKey)) return "Chọn màu cho môn.";
        if (input.GradeLevelIds.Count == 0) return "Chọn ít nhất 1 lớp.";
        var grades = input.GradeLevelIds.Distinct().ToList();
        return await db.GradeLevels.CountAsync(grade => grades.Contains(grade.Id), cancellationToken) == grades.Count ? null : "Lớp không hợp lệ.";
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
