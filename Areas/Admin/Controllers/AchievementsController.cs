using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using SafeLearnKids.Web.Data;
using SafeLearnKids.Web.Models;
using SafeLearnKids.Web.Models.Entities;

namespace SafeLearnKids.Web.Areas.Admin.Controllers;

// US-42: learning paths (ordered lessons of one subject and grade). US-43: badges and their criteria.
[Area("Admin")]
[Authorize(Roles = "Admin")]
public class AchievementsController(UserManager<ApplicationUser> users, Services.AccountActivityLog activity, IServiceProvider services) : Controller
{
    private const int MaxSteps = 40;
    private const string SaveFailed = "Chưa thể lưu thay đổi. Vui lòng thử lại.";

    private SafeLearnDbContext? Db => services.GetService<SafeLearnDbContext>();
    private string ActorId => users.GetUserId(User)!;

    // ---------- paths ----------

    [HttpGet("admin-paths")]
    public async Task<IActionResult> Paths(CancellationToken cancellationToken)
    {
        if (Db is not { } db) return View("SqlOnly");
        ViewData["Subjects"] = await SubjectsAsync(db, cancellationToken);
        return View(await db.LearningPaths.AsNoTracking().OrderBy(path => path.Subject.SortOrder).ThenBy(path => path.GradeLevel.Level).ThenBy(path => path.Name)
            .Select(path => new AdminPathRow(path.Id, path.Name, path.Subject.Name, path.GradeLevel.Level, path.IsActive, path.LearningPathItems.Count, path.ChildPaths.Count))
            .ToListAsync(cancellationToken));
    }

    // Step 1 picks the subject and grade (GET), step 2 shows that subject's lessons for the grade.
    [HttpGet("admin-paths/new")]
    public async Task<IActionResult> CreatePath(int? subject, int grade = 1, CancellationToken cancellationToken = default)
    {
        if (Db is not { } db) return View("SqlOnly");
        var subjects = await SubjectsAsync(db, cancellationToken);
        var chosen = subjects.FirstOrDefault(item => item.Id == subject) ?? subjects.FirstOrDefault();
        if (chosen is null) return RedirectToAction(nameof(Paths));
        var model = new PathFormViewModel { SubjectId = chosen.Id, SubjectName = chosen.Name, Grade = Math.Clamp(grade, 1, 5), Subjects = subjects };
        model.Steps = await StepsAsync(db, model.SubjectId, model.Grade, [], cancellationToken);
        return View("PathForm", model);
    }

    [HttpPost("admin-paths/new")]
    [EnableRateLimiting("account")]
    public async Task<IActionResult> CreatePath(PathFormViewModel model, CancellationToken cancellationToken)
    {
        if (Db is not { } db) return View("SqlOnly");
        model.Id = null;
        var gradeId = await db.GradeLevels.Where(item => item.Level == model.Grade).Select(item => (int?)item.Id).FirstOrDefaultAsync(cancellationToken);
        var subjectName = await db.Subjects.Where(item => item.Id == model.SubjectId).Select(item => item.Name).FirstOrDefaultAsync(cancellationToken);
        if (gradeId is null || subjectName is null) return NotFound();
        model.SubjectName = subjectName;
        var steps = await ValidatePathAsync(db, model, cancellationToken);
        if (!ModelState.IsValid) return await PathAgainAsync(db, model, cancellationToken);
        var path = new LearningPath { Name = model.Name, Description = model.Description, SubjectId = model.SubjectId, GradeLevelId = gradeId.Value, IsActive = model.IsActive };
        path.LearningPathItems = steps.Select((step, index) => new LearningPathItem { LessonId = step.LessonId, SortOrder = index + 1, IsRequired = step.Required }).ToList();
        db.LearningPaths.Add(path);
        if (!await SaveAsync(db, cancellationToken))
        {
            ModelState.AddModelError("", SaveFailed);
            return await PathAgainAsync(db, model, cancellationToken);
        }
        await activity.ContentActionAsync(ActorId, "LearningPath", path.Id, "PathCreate", $"{path.Name} · {steps.Count} bài");
        TempData["Info"] = "Đã tạo lộ trình.";
        return RedirectToAction(nameof(Paths));
    }

    [HttpGet("admin-paths/{id:int}")]
    public async Task<IActionResult> EditPath(int id, CancellationToken cancellationToken)
    {
        if (Db is not { } db) return View("SqlOnly");
        var path = await db.LearningPaths.AsNoTracking().Where(item => item.Id == id).Select(item => new
        {
            item.Id, item.Name, item.Description, item.SubjectId, Subject = item.Subject.Name, item.GradeLevel.Level, item.IsActive, Followers = item.ChildPaths.Count,
            Items = item.LearningPathItems.OrderBy(step => step.SortOrder).ThenBy(step => step.Id).Select(step => new { step.LessonId, step.IsRequired }).ToList()
        }).FirstOrDefaultAsync(cancellationToken);
        if (path is null) return NotFound();
        var model = new PathFormViewModel
        {
            Id = path.Id, Name = path.Name, Description = path.Description, SubjectId = path.SubjectId, SubjectName = path.Subject, Grade = path.Level,
            IsActive = path.IsActive, Followers = path.Followers
        };
        model.Steps = await StepsAsync(db, path.SubjectId, path.Level, path.Items.Select(item => (item.LessonId, item.IsRequired)).ToList(), cancellationToken);
        return View("PathForm", model);
    }

    [HttpPost("admin-paths/{id:int}")]
    [EnableRateLimiting("account")]
    public async Task<IActionResult> EditPath(int id, PathFormViewModel model, CancellationToken cancellationToken)
    {
        if (Db is not { } db) return View("SqlOnly");
        var path = await db.LearningPaths.Include(item => item.LearningPathItems).Include(item => item.Subject).Include(item => item.GradeLevel)
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (path is null) return NotFound();
        // Subject and grade are fixed after creation.
        model.Id = id;
        model.SubjectId = path.SubjectId;
        model.SubjectName = path.Subject.Name;
        model.Grade = path.GradeLevel.Level;
        model.Followers = await db.ChildPaths.CountAsync(item => item.PathId == id, cancellationToken);
        var steps = await ValidatePathAsync(db, model, cancellationToken);
        if (!ModelState.IsValid) return await PathAgainAsync(db, model, cancellationToken);
        path.Name = model.Name;
        path.Description = model.Description;
        path.IsActive = model.IsActive;
        var keep = steps.Select(step => step.LessonId).ToList();
        var removed = path.LearningPathItems.Where(item => !keep.Contains(item.LessonId)).ToList();
        if (removed.Count > 0)
        {
            // A child standing on a removed step moves on at the next study activity.
            var removedIds = removed.Select(item => item.Id).ToList();
            foreach (var followed in await db.ChildPaths.Where(item => item.CurrentItemId != null && removedIds.Contains(item.CurrentItemId.Value)).ToListAsync(cancellationToken))
                followed.CurrentItemId = null;
            db.LearningPathItems.RemoveRange(removed);
        }
        for (var index = 0; index < steps.Count; index++)
        {
            var step = steps[index];
            var item = path.LearningPathItems.FirstOrDefault(existing => existing.LessonId == step.LessonId);
            if (item is null) path.LearningPathItems.Add(new LearningPathItem { LessonId = step.LessonId, SortOrder = index + 1, IsRequired = step.Required });
            else
            {
                item.SortOrder = index + 1;
                item.IsRequired = step.Required;
            }
        }
        if (!await SaveAsync(db, cancellationToken))
        {
            ModelState.AddModelError("", SaveFailed);
            return await PathAgainAsync(db, model, cancellationToken);
        }
        await activity.ContentActionAsync(ActorId, "LearningPath", id, "PathUpdate", $"{path.Name} · {steps.Count} bài · {(path.IsActive ? "đang dùng" : "tạm ẩn")}");
        TempData["Info"] = "Đã lưu lộ trình.";
        return RedirectToAction(nameof(Paths));
    }

    [HttpPost("admin-paths/{id:int}/delete")]
    [EnableRateLimiting("account")]
    public async Task<IActionResult> DeletePath(int id, CancellationToken cancellationToken)
    {
        if (Db is not { } db) return View("SqlOnly");
        var path = await db.LearningPaths.Include(item => item.LearningPathItems).FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (path is null) return NotFound();
        if (await db.ChildPaths.AnyAsync(item => item.PathId == id, cancellationToken))
        {
            TempData["Error"] = "Đã có bé theo lộ trình này. Hãy tắt \"Đang dùng\" thay vì xóa.";
            return RedirectToAction(nameof(Paths));
        }
        db.LearningPathItems.RemoveRange(path.LearningPathItems);
        db.LearningPaths.Remove(path);
        if (!await SaveAsync(db, cancellationToken)) TempData["Error"] = SaveFailed;
        else
        {
            await activity.ContentActionAsync(ActorId, "LearningPath", id, "PathDelete", path.Name);
            TempData["Info"] = "Đã xóa lộ trình.";
        }
        return RedirectToAction(nameof(Paths));
    }

    // ---------- badges ----------

    [HttpGet("admin-badges")]
    public async Task<IActionResult> Badges(CancellationToken cancellationToken)
    {
        if (Db is not { } db) return View("SqlOnly");
        return View(await db.Badges.AsNoTracking().OrderBy(badge => badge.CriteriaType).ThenBy(badge => badge.Threshold).ThenBy(badge => badge.Id)
            .Select(badge => new AdminBadgeRow(badge.Id, badge.Code, badge.Name, badge.Description, badge.IconKey, badge.CriteriaType, badge.Threshold, badge.IsActive, badge.ChildBadges.Count))
            .ToListAsync(cancellationToken));
    }

    [HttpGet("admin-badges/new")]
    public IActionResult CreateBadge() => Db is null ? View("SqlOnly") : View("BadgeForm", new BadgeFormViewModel());

    [HttpPost("admin-badges/new")]
    [EnableRateLimiting("account")]
    public async Task<IActionResult> CreateBadge(BadgeFormViewModel model, CancellationToken cancellationToken)
    {
        if (Db is not { } db) return View("SqlOnly");
        model.Id = null;
        model.Code = model.Code?.Trim().ToUpperInvariant() ?? "";
        ValidateBadge(model);
        if (ModelState.IsValid && await db.Badges.AnyAsync(item => item.Code == model.Code, cancellationToken)) ModelState.AddModelError(nameof(model.Code), "Mã này đã có.");
        if (!ModelState.IsValid) return View("BadgeForm", model);
        var badge = new Badge { Code = model.Code };
        ApplyBadge(badge, model);
        db.Badges.Add(badge);
        if (!await SaveAsync(db, cancellationToken))
        {
            ModelState.AddModelError(nameof(model.Code), "Mã này đã có.");
            return View("BadgeForm", model);
        }
        await activity.ContentActionAsync(ActorId, "Badge", badge.Id, "BadgeCreate", $"{badge.Code} · {badge.CriteriaType} {badge.Threshold}");
        TempData["Info"] = "Đã thêm huy hiệu. Bé đủ điều kiện sẽ nhận ở lần học tiếp theo.";
        return RedirectToAction(nameof(Badges));
    }

    [HttpGet("admin-badges/{id:int}")]
    public async Task<IActionResult> EditBadge(int id, CancellationToken cancellationToken)
    {
        if (Db is not { } db) return View("SqlOnly");
        var badge = await db.Badges.AsNoTracking().Where(item => item.Id == id).Select(item => new BadgeFormViewModel
        {
            Id = item.Id, Code = item.Code, Name = item.Name, Description = item.Description, IconKey = item.IconKey, CriteriaType = item.CriteriaType,
            Threshold = item.Threshold, IsActive = item.IsActive, Awarded = item.ChildBadges.Count
        }).FirstOrDefaultAsync(cancellationToken);
        if (badge is null) return NotFound();
        badge.IconKey = BadgeCriteria.Icon(badge.IconKey, badge.CriteriaType);
        return View("BadgeForm", badge);
    }

    [HttpPost("admin-badges/{id:int}")]
    [EnableRateLimiting("account")]
    public async Task<IActionResult> EditBadge(int id, BadgeFormViewModel model, CancellationToken cancellationToken)
    {
        if (Db is not { } db) return View("SqlOnly");
        var badge = await db.Badges.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (badge is null) return NotFound();
        model.Id = id;
        // The code identifies the badge; it does not change.
        model.Code = badge.Code;
        model.Awarded = await db.ChildBadges.CountAsync(item => item.BadgeId == id, cancellationToken);
        ModelState.Remove(nameof(model.Code));
        ValidateBadge(model);
        if (!ModelState.IsValid) return View("BadgeForm", model);
        ApplyBadge(badge, model);
        if (!await SaveAsync(db, cancellationToken))
        {
            ModelState.AddModelError("", SaveFailed);
            return View("BadgeForm", model);
        }
        await activity.ContentActionAsync(ActorId, "Badge", id, "BadgeUpdate", $"{badge.Code} · {badge.CriteriaType} {badge.Threshold} · {(badge.IsActive ? "đang dùng" : "tạm ẩn")}");
        TempData["Info"] = "Đã lưu huy hiệu. Huy hiệu bé đã nhận được giữ nguyên.";
        return RedirectToAction(nameof(Badges));
    }

    [HttpPost("admin-badges/{id:int}/delete")]
    [EnableRateLimiting("account")]
    public async Task<IActionResult> DeleteBadge(int id, CancellationToken cancellationToken)
    {
        if (Db is not { } db) return View("SqlOnly");
        var badge = await db.Badges.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (badge is null) return NotFound();
        if (await db.ChildBadges.AnyAsync(item => item.BadgeId == id, cancellationToken))
        {
            TempData["Error"] = "Đã có bé nhận huy hiệu này. Hãy tắt \"Đang dùng\" thay vì xóa.";
            return RedirectToAction(nameof(Badges));
        }
        db.Badges.Remove(badge);
        if (!await SaveAsync(db, cancellationToken)) TempData["Error"] = SaveFailed;
        else
        {
            await activity.ContentActionAsync(ActorId, "Badge", id, "BadgeDelete", badge.Code);
            TempData["Info"] = "Đã xóa huy hiệu.";
        }
        return RedirectToAction(nameof(Badges));
    }

    // ---------- helpers ----------

    private async Task<List<PathStepInput>> ValidatePathAsync(SafeLearnDbContext db, PathFormViewModel model, CancellationToken cancellationToken)
    {
        model.Name = model.Name?.Trim() ?? "";
        model.Description = string.IsNullOrWhiteSpace(model.Description) ? null : model.Description.Trim();
        var chosen = model.Steps.Select((step, index) => (step, index)).Where(pair => pair.step.Include)
            .OrderBy(pair => pair.step.Order).ThenBy(pair => pair.index).Select(pair => pair.step)
            .DistinctBy(step => step.LessonId).ToList();
        if (chosen.Count == 0) ModelState.AddModelError(nameof(model.Steps), "Chọn ít nhất 1 bài cho lộ trình.");
        else if (chosen.Count > MaxSteps) ModelState.AddModelError(nameof(model.Steps), "Mỗi lộ trình tối đa 40 bài.");
        else if (!chosen.Any(step => step.Required)) ModelState.AddModelError(nameof(model.Steps), "Cần ít nhất 1 bài bắt buộc.");
        var ids = chosen.Select(step => step.LessonId).ToList();
        var valid = await db.Lessons.CountAsync(lesson => ids.Contains(lesson.Id) && lesson.SubjectId == model.SubjectId && lesson.GradeLevel.Level == model.Grade
            && lesson.Status == LessonStatus.Published, cancellationToken);
        if (valid != ids.Count) ModelState.AddModelError(nameof(model.Steps), "Chỉ chọn bài đã xuất bản đúng môn và lớp của lộ trình.");
        return chosen;
    }

    private async Task<IActionResult> PathAgainAsync(SafeLearnDbContext db, PathFormViewModel model, CancellationToken cancellationToken)
    {
        var picked = model.Steps.Where(step => step.Include).OrderBy(step => step.Order).Select(step => (step.LessonId, step.Required)).ToList();
        model.Steps = await StepsAsync(db, model.SubjectId, model.Grade, picked, cancellationToken);
        if (model.Id is null) model.Subjects = await SubjectsAsync(db, cancellationToken);
        return View("PathForm", model);
    }

    // Published lessons of the subject and grade: those on the path first, in path order, then the rest.
    private static async Task<List<PathStepInput>> StepsAsync(SafeLearnDbContext db, int subjectId, int grade, IReadOnlyList<(int LessonId, bool Required)> picked, CancellationToken cancellationToken)
    {
        var lessons = await db.Lessons.AsNoTracking().Where(lesson => lesson.SubjectId == subjectId && lesson.GradeLevel.Level == grade && lesson.Status == LessonStatus.Published)
            .OrderBy(lesson => lesson.SortOrder).ThenBy(lesson => lesson.Id).Select(lesson => new { lesson.Id, lesson.Title }).ToListAsync(cancellationToken);
        var order = picked.Select(item => item.LessonId).ToList();
        return lessons.OrderBy(lesson => order.Contains(lesson.Id) ? order.IndexOf(lesson.Id) : int.MaxValue)
            .Select((lesson, index) => new PathStepInput
            {
                LessonId = lesson.Id, Title = lesson.Title, Include = order.Contains(lesson.Id),
                Required = !order.Contains(lesson.Id) || picked.First(item => item.LessonId == lesson.Id).Required, Order = index + 1
            }).ToList();
    }

    private void ValidateBadge(BadgeFormViewModel model)
    {
        model.Name = model.Name?.Trim() ?? "";
        model.Description = model.Description?.Trim() ?? "";
        if (!BadgeCriteria.All.Contains(model.CriteriaType)) ModelState.AddModelError(nameof(model.CriteriaType), "Chọn tiêu chí.");
        if (!BadgeCriteria.Icons.Contains(model.IconKey)) ModelState.AddModelError(nameof(model.IconKey), "Chọn biểu tượng.");
    }

    private static void ApplyBadge(Badge badge, BadgeFormViewModel model)
    {
        badge.Name = model.Name;
        badge.Description = model.Description;
        badge.IconKey = model.IconKey;
        badge.CriteriaType = model.CriteriaType;
        badge.Threshold = model.Threshold;
        badge.IsActive = model.IsActive;
    }

    private static async Task<IReadOnlyList<SubjectRowOption>> SubjectsAsync(SafeLearnDbContext db, CancellationToken cancellationToken) =>
        await db.Subjects.AsNoTracking().OrderBy(item => item.SortOrder).ThenBy(item => item.Name).Select(item => new SubjectRowOption(item.Id, item.Name)).ToListAsync(cancellationToken);

    private static async Task<bool> SaveAsync(SafeLearnDbContext db, CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException)
        {
            db.ChangeTracker.Clear();
            return false;
        }
    }
}
