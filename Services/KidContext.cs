using System.Globalization;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using SafeLearnKids.Web.Data;
using SafeLearnKids.Web.Models;
using SafeLearnKids.Web.Models.Entities;

namespace SafeLearnKids.Web.Services;

// Child mode runs on the parent's sign-in; the chosen child lives in a cookie that is re-checked against the parent on every request.
public sealed class KidContext(IHttpContextAccessor accessor, IServiceProvider services)
{
    public const string CookieName = ".SafeLearnKids.Kid";

    private HttpContext Http => accessor.HttpContext!;
    private SafeLearnDbContext? Db => services.GetService<SafeLearnDbContext>();
    private string? ParentId => Http.User.FindFirstValue(ClaimTypes.NameIdentifier);

    public async Task<KidInfo?> CurrentAsync(CancellationToken cancellationToken)
    {
        if (Db is not { } db || ParentId is not { } parentId) return null;
        if (!int.TryParse(Http.Request.Cookies[CookieName], NumberStyles.None, CultureInfo.InvariantCulture, out var id)) return null;
        return await FindAsync(db, parentId, id, cancellationToken);
    }

    public async Task<KidInfo?> ChooseAsync(int id, CancellationToken cancellationToken)
    {
        if (Db is not { } db || ParentId is not { } parentId) return null;
        var kid = await FindAsync(db, parentId, id, cancellationToken);
        if (kid is not null)
            Http.Response.Cookies.Append(CookieName, id.ToString(CultureInfo.InvariantCulture), new CookieOptions
            {
                HttpOnly = true,
                SameSite = SameSiteMode.Lax,
                Secure = Http.Request.IsHttps,
                IsEssential = true
            });
        return kid;
    }

    public void Clear() => Http.Response.Cookies.Delete(CookieName);

    public async Task<IReadOnlyList<KidChoice>> ChoicesAsync(CancellationToken cancellationToken)
    {
        if (Db is not { } db || ParentId is not { } parentId) return [];
        var rows = await db.ChildProfiles.AsNoTracking().Where(child => child.ParentId == parentId && child.IsActive)
            .OrderBy(child => child.CreatedAt).ThenBy(child => child.Id)
            .Select(child => new { child.Id, child.DisplayName, child.GradeLevel.Level }).ToListAsync(cancellationToken);
        return rows.Select(row => new KidChoice(row.Id, row.DisplayName, row.Level, new StringInfo(row.DisplayName).SubstringByTextElements(0, 1).ToUpperInvariant())).ToList();
    }

    // Lessons a child may open: visible to the family (standard, community or their own), published, subject in use,
    // and either assigned to them or in their grade and chosen subjects.
    public static IQueryable<Lesson> Openable(SafeLearnDbContext db, KidInfo kid) =>
        LessonAccess.ForFamily(db.Lessons.AsNoTracking(), kid.ParentId).Where(lesson => lesson.Status == LessonStatus.Published && lesson.Subject.IsActive
            && ((lesson.GradeLevelId == kid.GradeLevelId && kid.SubjectIds.Contains(lesson.SubjectId))
                || lesson.Assignments.Any(item => item.ChildProfileId == kid.Id && item.Status != AssignmentStatus.Cancelled)));

    private static async Task<KidInfo?> FindAsync(SafeLearnDbContext db, string parentId, int id, CancellationToken cancellationToken)
    {
        var row = await db.ChildProfiles.AsNoTracking().Where(child => child.Id == id && child.ParentId == parentId && child.IsActive)
            .Select(child => new
            {
                child.Id, child.DisplayName, child.GradeLevel.Level, child.GradeLevelId,
                Subjects = child.ChildSubjects.Select(item => item.SubjectId).ToList()
            }).FirstOrDefaultAsync(cancellationToken);
        return row is null ? null : new KidInfo(row.Id, row.DisplayName, row.Level, row.GradeLevelId, row.Subjects, parentId);
    }
}
