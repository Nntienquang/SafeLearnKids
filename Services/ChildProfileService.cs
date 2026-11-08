using System.Data;
using System.Globalization;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SafeLearnKids.Web.Data;
using SafeLearnKids.Web.Models;
using SafeLearnKids.Web.Models.Entities;

namespace SafeLearnKids.Web.Services;

// Business tables exist only on SQL, so InMemory demo runs keep children in localStorage.
public sealed class ChildProfileService(IServiceProvider services)
{
    private const int MaxChildren = 4;
    private const int MinBirthYear = 2005;
    private const string SaveFailed = "Chưa thể lưu hồ sơ. Vui lòng thử lại.";
    private const string NameTaken = "Đã có bé dùng tên này.";

    private SafeLearnDbContext? Db => services.GetService<SafeLearnDbContext>();

    // Subject keys, names and colours for the parent and child pages; null without SQL.
    public async Task<IReadOnlyList<SubjectLook>?> SubjectsAsync(CancellationToken cancellationToken) =>
        Db is { } db ? await SubjectCatalog.LooksAsync(db, cancellationToken) : null;

    public async Task<IReadOnlyList<ParentChildRow>?> ForParentAsync(string parentId, CancellationToken cancellationToken)
    {
        if (Db is not { } db) return null;
        var children = await db.ChildProfiles.AsNoTracking()
            .Where(child => child.ParentId == parentId && child.IsActive)
            .OrderBy(child => child.CreatedAt).ThenBy(child => child.Id)
            .Select(child => new
            {
                child.Id,
                child.DisplayName,
                child.GradeLevel.Level,
                child.BirthYear,
                Codes = child.ChildSubjects.Select(item => item.Subject.Code).ToList()
            })
            .ToListAsync(cancellationToken);
        var rows = new List<ParentChildRow>();
        foreach (var child in children)
        {
            var today = await StudyTimeService.StatusAsync(db, child.Id, cancellationToken);
            rows.Add(ToRow(child.Id, child.DisplayName, child.Level, child.BirthYear, child.Codes.Select(SubjectCatalog.Key))
                with { Limit = today.LimitMinutes + today.ExtraMinutes, Used = today.UsedMinutes });
        }
        return rows;
    }

    public async Task<ChildSaveResult> CreateAsync(string parentId, ChildProfileInput input, CancellationToken cancellationToken)
    {
        if (Db is not { } db) return new(null, SaveFailed);
        if (Validate(input) is { } error) return new(null, error);
        var name = input.Name.Trim();
        try
        {
            // Serializable keeps two simultaneous requests from passing the limit together.
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
            if (await db.ChildProfiles.CountAsync(child => child.ParentId == parentId && child.IsActive, cancellationToken) >= MaxChildren)
                return new(null, "Mỗi tài khoản có tối đa 4 hồ sơ.");
            if (await db.ChildProfiles.AnyAsync(child => child.ParentId == parentId && child.DisplayName == name, cancellationToken))
                return new(null, NameTaken);
            if (await GradeIdAsync(db, input.Cls, cancellationToken) is not { } gradeId) return new(null, SaveFailed);
            var (subjects, problem) = await ResolveSubjectsAsync(db, input.Subjects, [], cancellationToken);
            if (problem is not null) return new(null, problem);
            var child = new ChildProfile
            {
                ParentId = parentId,
                DisplayName = name,
                GradeLevelId = gradeId,
                BirthYear = input.BirthYear,
                IsActive = true,
                ChildSubjects = subjects.Values.Select(id => new ChildSubject { SubjectId = id }).ToList()
            };
            db.ChildProfiles.Add(child);
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new(ToRow(child.Id, name, input.Cls, input.BirthYear, subjects.Keys), null);
        }
        catch (Exception exception) when (SqlError(exception) is 1205 or 2601 or 2627)
        {
            db.ChangeTracker.Clear();
            return new(null, SqlError(exception) == 1205 ? SaveFailed : NameTaken);
        }
    }

    public async Task<ChildSaveResult> UpdateAsync(string parentId, int id, ChildProfileInput input, CancellationToken cancellationToken)
    {
        if (Db is not { } db) return new(null, SaveFailed);
        if (Validate(input) is { } error) return new(null, error);
        var child = await db.ChildProfiles.Include(item => item.ChildSubjects)
            .FirstOrDefaultAsync(item => item.Id == id && item.ParentId == parentId && item.IsActive, cancellationToken);
        if (child is null) return new(null, null, NotFound: true);
        var name = input.Name.Trim();
        if (await db.ChildProfiles.AnyAsync(item => item.ParentId == parentId && item.Id != id && item.DisplayName == name, cancellationToken))
            return new(null, NameTaken);
        if (await GradeIdAsync(db, input.Cls, cancellationToken) is not { } gradeId) return new(null, SaveFailed);
        // A subject hidden by the admins can stay on a child who already has it, but cannot be added.
        var (chosen, problem) = await ResolveSubjectsAsync(db, input.Subjects, child.ChildSubjects.Select(item => item.SubjectId).ToList(), cancellationToken);
        if (problem is not null) return new(null, problem);

        child.DisplayName = name;
        child.GradeLevelId = gradeId;
        child.BirthYear = input.BirthYear;
        db.ChildSubjects.RemoveRange(child.ChildSubjects.Where(item => !chosen.ContainsValue(item.SubjectId)));
        foreach (var subjectId in chosen.Values.Where(subjectId => child.ChildSubjects.All(item => item.SubjectId != subjectId)))
            child.ChildSubjects.Add(new ChildSubject { SubjectId = subjectId });
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return new(ToRow(id, name, input.Cls, input.BirthYear, chosen.Keys), null);
        }
        catch (DbUpdateException exception) when (SqlError(exception) is 2601 or 2627)
        {
            db.ChangeTracker.Clear();
            return new(null, NameTaken);
        }
    }

    public async Task<ChildSaveResult> DeleteAsync(string parentId, int id, CancellationToken cancellationToken)
    {
        if (Db is not { } db) return new(null, SaveFailed);
        var children = await db.ChildProfiles.Where(child => child.ParentId == parentId && child.IsActive).ToListAsync(cancellationToken);
        if (children.FirstOrDefault(child => child.Id == id) is not { } target) return new(null, null, NotFound: true);
        if (children.Count < 2) return new(null, "Cần giữ ít nhất 1 hồ sơ.");
        // SQL cascades the delete to the child's subjects and learning data.
        db.ChildProfiles.Remove(target);
        await db.SaveChangesAsync(cancellationToken);
        return new(null, null);
    }

    private static string? Validate(ChildProfileInput input)
    {
        if (input.Subjects.Count == 0) return "Cần ít nhất 1 môn.";
        return input.BirthYear is { } year && (year < MinBirthYear || year > DateTime.UtcNow.Year) ? "Năm sinh không hợp lệ." : null;
    }

    private static Task<int?> GradeIdAsync(SafeLearnDbContext db, int level, CancellationToken cancellationToken) =>
        db.GradeLevels.Where(grade => grade.Level == level).Select(grade => (int?)grade.Id).FirstOrDefaultAsync(cancellationToken);

    private static async Task<(Dictionary<string, int> Subjects, string? Problem)> ResolveSubjectsAsync(
        SafeLearnDbContext db, IEnumerable<string> keys, IReadOnlyCollection<int> kept, CancellationToken cancellationToken)
    {
        var rows = await db.Subjects.AsNoTracking().Select(subject => new { subject.Id, subject.Code, subject.Name, subject.IsActive }).ToListAsync(cancellationToken);
        var chosen = new Dictionary<string, int>();
        foreach (var key in keys.Distinct())
        {
            var row = rows.FirstOrDefault(item => SubjectCatalog.Key(item.Code) == key);
            if (row is null) return (chosen, "Môn học không hợp lệ.");
            if (!row.IsActive && !kept.Contains(row.Id)) return (chosen, $"Môn {row.Name} đang tạm ẩn nên chưa chọn được.");
            chosen[key] = row.Id;
        }
        return (chosen, null);
    }

    private static ParentChildRow ToRow(int id, string name, int level, int? birthYear, IEnumerable<string> subjects)
    {
        var keys = subjects.Distinct().ToList();
        var initial = new StringInfo(name).SubstringByTextElements(0, 1).ToUpperInvariant();
        return new ParentChildRow(id.ToString(CultureInfo.InvariantCulture), name, level, birthYear, initial, keys);
    }

    private static int? SqlError(Exception exception) => (exception.GetBaseException() as SqlException)?.Number;
}
