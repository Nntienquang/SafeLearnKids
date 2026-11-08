using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using SafeLearnKids.Web.Data;
using SafeLearnKids.Web.Models;
using SafeLearnKids.Web.Models.Entities;
using SafeLearnKids.Web.Services;

namespace SafeLearnKids.Web.Areas.Parent.Controllers;

// US-41 to US-46 for parents: progress per subject, learning paths, badges, streak, weekly goal and what needs attention.
[Area("Parent")]
[Authorize(Roles = "Parent")]
public class ProgressController(KidContext kids, IServiceProvider services) : Controller
{
    private SafeLearnDbContext? Db => services.GetService<SafeLearnDbContext>();
    private string ParentId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    [HttpGet("progress")]
    public async Task<IActionResult> Index(int? child, CancellationToken cancellationToken)
    {
        if (Db is not { } db) return View("SqlOnly");
        var choices = await kids.ChoicesAsync(cancellationToken);
        if (choices.Count == 0) return Redirect("/onboarding");
        var chosen = choices.FirstOrDefault(item => item.Id == child) ?? choices[0];
        var subjects = await ProgressService.SubjectRowsAsync(db, chosen.Id, cancellationToken);
        var week = ProgressService.WeekStart(StudyTimeService.Today);
        // Quizzes whose best result is still below the pass mark.
        var weak = await db.QuizAttempts.AsNoTracking().Where(item => item.ChildProfileId == chosen.Id && item.SubmittedAt != null && item.ScorePercent != null)
            .GroupBy(item => new { item.QuizId, item.Quiz.Title, item.Quiz.PassPercent })
            .Select(group => new
            {
                group.Key.Title, group.Key.PassPercent, Best = group.Max(item => item.ScorePercent!.Value),
                Last = group.OrderByDescending(item => item.Id).Select(item => item.Id).First()
            })
            .ToListAsync(cancellationToken);
        var scored = subjects.Where(row => row.AvgQuiz is not null).Select(row => row.AvgQuiz!.Value).ToList();
        return View(new ProgressViewModel
        {
            Child = chosen,
            Children = choices,
            Subjects = subjects,
            Paths = await ProgressService.PathsAsync(db, chosen.Id, followedOnly: false, cancellationToken),
            Badges = await ProgressService.BadgesAsync(db, chosen.Id, cancellationToken),
            Streak = await ProgressService.StreakAsync(db, chosen.Id, cancellationToken),
            Week = await ProgressService.WeekAsync(db, chosen.Id, week, cancellationToken),
            LastWeek = await LastGoalAsync(db, chosen.Id, week, cancellationToken),
            ReviewCount = (await ProgressService.ReviewAsync(db, chosen.Id, cancellationToken)).Count,
            AvgQuiz = scored.Count == 0 ? null : Math.Round(scored.Average(), 1),
            CompletedLessons = (await ProgressService.CompletedLessonsAsync(db, chosen.Id, cancellationToken)).Count,
            Weak = weak.Where(item => item.Best < item.PassPercent).OrderBy(item => item.Best).Take(5).Select(item => (item.Title, item.Best, item.Last)).ToList()
        });
    }

    // US-45: one goal per child and week (Monday to Sunday); at least one target is needed.
    [HttpPost("progress/{child:int}/goal")]
    [EnableRateLimiting("account")]
    public async Task<IActionResult> Goal(int child, GoalForm form, CancellationToken cancellationToken)
    {
        if (Db is not { } db) return View("SqlOnly");
        if (!await MineAsync(db, child, cancellationToken)) return NotFound();
        var week = ProgressService.WeekStart(StudyTimeService.Today);
        var goal = await db.LearningGoals.FirstOrDefaultAsync(item => item.ChildProfileId == child && item.WeekStart == week, cancellationToken);
        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Số bài từ 0 đến 100, số phút từ 0 đến 3000.";
        }
        else if (form.TargetLessons is null && form.TargetMinutes is null)
        {
            if (goal is not null)
            {
                db.LearningGoals.Remove(goal);
                await db.SaveChangesAsync(cancellationToken);
                TempData["Info"] = "Đã bỏ mục tiêu tuần này.";
            }
            else TempData["Error"] = "Nhập số bài hoặc số phút cho mục tiêu tuần.";
        }
        else
        {
            if (goal is null)
            {
                goal = new LearningGoal { ChildProfileId = child, WeekStart = week, CreatedById = ParentId, CreatedAt = DateTime.UtcNow };
                db.LearningGoals.Add(goal);
            }
            goal.TargetLessons = form.TargetLessons;
            goal.TargetMinutes = form.TargetMinutes;
            try
            {
                await db.SaveChangesAsync(cancellationToken);
                TempData["Info"] = "Đã lưu mục tiêu tuần này.";
            }
            catch (DbUpdateException)
            {
                db.ChangeTracker.Clear();
                TempData["Error"] = "Mục tiêu vừa được lưu ở nơi khác. Hãy kiểm tra lại.";
            }
        }
        return Redirect($"/progress?child={child}#goal");
    }

    // US-42: a parent picks a path for the child; it must suit the child's grade and subjects.
    [HttpPost("progress/{child:int}/paths/{pathId:int}")]
    [EnableRateLimiting("account")]
    public async Task<IActionResult> Follow(int child, int pathId, CancellationToken cancellationToken)
    {
        if (Db is not { } db) return View("SqlOnly");
        if (!await MineAsync(db, child, cancellationToken)) return NotFound();
        var suits = await db.LearningPaths.AnyAsync(path => path.Id == pathId && path.IsActive && path.Subject.IsActive
            && db.ChildProfiles.Any(kid => kid.Id == child && kid.GradeLevelId == path.GradeLevelId && kid.ChildSubjects.Any(link => link.SubjectId == path.SubjectId)), cancellationToken);
        if (!suits) return NotFound();
        if (!await db.ChildPaths.AnyAsync(item => item.ChildProfileId == child && item.PathId == pathId, cancellationToken))
        {
            db.ChildPaths.Add(new ChildPath { ChildProfileId = child, PathId = pathId, Status = PathStatus.InProgress, StartedAt = DateTime.UtcNow });
            try
            {
                await db.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException)
            {
                // A double tap added it a moment earlier.
                db.ChangeTracker.Clear();
            }
            var name = await db.ChildProfiles.Where(item => item.Id == child).Select(item => item.DisplayName).FirstAsync(cancellationToken);
            // Lessons already finished count straight away.
            await ProgressService.AfterStudyAsync(db, child, ParentId, name, cancellationToken, studied: false);
        }
        TempData["Info"] = "Đã chọn lộ trình cho bé. Bé thấy bài tiếp theo ở trang học.";
        return Redirect($"/progress?child={child}#paths");
    }

    [HttpPost("progress/{child:int}/paths/{pathId:int}/stop")]
    [EnableRateLimiting("account")]
    public async Task<IActionResult> Stop(int child, int pathId, CancellationToken cancellationToken)
    {
        if (Db is not { } db) return View("SqlOnly");
        if (!await MineAsync(db, child, cancellationToken)) return NotFound();
        var followed = await db.ChildPaths.FirstOrDefaultAsync(item => item.ChildProfileId == child && item.PathId == pathId, cancellationToken);
        if (followed is not null)
        {
            db.ChildPaths.Remove(followed);
            await db.SaveChangesAsync(cancellationToken);
            TempData["Info"] = "Đã bỏ lộ trình. Bài bé đã học vẫn được giữ.";
        }
        return Redirect($"/progress?child={child}#paths");
    }

    private Task<bool> MineAsync(SafeLearnDbContext db, int child, CancellationToken cancellationToken) =>
        db.ChildProfiles.AnyAsync(item => item.Id == child && item.ParentId == ParentId && item.IsActive, cancellationToken);

    // The most recent earlier goal, offered as a starting point for this week.
    private static async Task<WeekGoalView?> LastGoalAsync(SafeLearnDbContext db, int child, DateOnly week, CancellationToken cancellationToken)
    {
        var last = await db.LearningGoals.AsNoTracking().Where(item => item.ChildProfileId == child && item.WeekStart < week)
            .OrderByDescending(item => item.WeekStart).FirstOrDefaultAsync(cancellationToken);
        return last is null ? null : await ProgressService.WeekAsync(db, child, last.WeekStart, cancellationToken);
    }
}
