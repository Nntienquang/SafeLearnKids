using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SafeLearnKids.Web.Data;
using SafeLearnKids.Web.Models;
using SafeLearnKids.Web.Services;

namespace SafeLearnKids.Web.Areas.Parent.Controllers;

// The parent's home: today, this week and what needs attention for one child, all from SQL.
[Area("Parent")]
[Authorize(Roles = "Parent")]
public class HomeController(KidContext kids, IServiceProvider services) : Controller
{
    [HttpGet("parent-home")]
    public async Task<IActionResult> Index(int? child, CancellationToken cancellationToken)
    {
        if (services.GetService<SafeLearnDbContext>() is not { } db) return View("SqlOnly");
        var choices = await kids.ChoicesAsync(cancellationToken);
        if (choices.Count == 0) return Redirect("/onboarding");
        var chosen = choices.FirstOrDefault(item => item.Id == child) ?? choices[0];
        var parentId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var today = StudyTimeService.Today;
        var since = today.AddDays(-6);
        var usage = await db.DailyUsages.AsNoTracking().Where(item => item.ChildProfileId == chosen.Id && item.UsageDate >= since)
            .ToDictionaryAsync(item => item.UsageDate, item => item.Seconds, cancellationToken);
        var subjects = await ProgressService.SubjectRowsAsync(db, chosen.Id, cancellationToken);
        var scored = subjects.Where(row => row.AvgQuiz is not null).Select(row => row.AvgQuiz!.Value).ToList();
        var pending = await db.TimeExtensionRequests.AsNoTracking()
            .Where(item => item.ChildProfileId == chosen.Id && item.Status == RequestStatus.Pending && item.ForDate == today)
            .Select(item => new { item.RequestedMinutes, item.Reason }).ToListAsync(cancellationToken);
        var weak = (await db.QuizAttempts.AsNoTracking().Where(item => item.ChildProfileId == chosen.Id && item.SubmittedAt != null && item.ScorePercent != null)
            .GroupBy(item => new { item.QuizId, item.Quiz.Title, item.Quiz.PassPercent })
            .Select(group => new { group.Key.Title, group.Key.PassPercent, Best = group.Max(item => item.ScorePercent!.Value), Last = group.Max(item => item.Id) })
            .ToListAsync(cancellationToken)).Where(item => item.Best < item.PassPercent).OrderBy(item => item.Best).Take(2).ToList();
        var notices = await db.Notifications.AsNoTracking().Where(item => item.UserId == parentId && !item.IsRead).OrderByDescending(item => item.CreatedAt).Take(3)
            .Select(item => new { item.NotificationType, item.Title, item.Body, item.LinkUrl }).ToListAsync(cancellationToken);
        var attention = new List<HomeAttention>();
        if (!await ChildConsent.Active(db, parentId).AnyAsync(item => item.ChildProfileId == chosen.Id, cancellationToken))
            attention.Add(new HomeAttention("shield", "t-r", $"Chưa đồng ý xử lý dữ liệu của {chosen.Name}", "Bé chưa vào học được cho tới khi ba mẹ đồng ý.", "/privacy", "Xem"));
        attention.AddRange(pending.Select(item => new HomeAttention("clock", "t-a", $"{chosen.Name} xin thêm {item.RequestedMinutes} phút", item.Reason ?? "Không ghi lời nhắn", $"/time?child={chosen.Id}", "Trả lời")));
        attention.AddRange(weak.Select(item => new HomeAttention("alert", "t-r", "Bài tập chưa đạt: " + item.Title, $"Điểm cao nhất {item.Best:0.#}% (cần {item.PassPercent}%)", $"/quiz-result/{item.Last}", "Xem bài")));
        attention.AddRange(notices.Select(item => new HomeAttention(NotificationKind.Icon(item.NotificationType), NotificationKind.Tone(item.NotificationType), item.Title, item.Body, item.LinkUrl ?? "/notifications", "Xem")));
        var status = await StudyTimeService.StatusAsync(db, chosen.Id, cancellationToken);
        var continuing = await db.LessonActivities.AsNoTracking().Where(item => item.ChildProfileId == chosen.Id && item.Status == "Started")
            .OrderByDescending(item => item.UpdatedAt)
            .Select(item => new HomeContinue(item.LessonId, item.Lesson.Title, item.Lesson.Subject.Name, item.ProgressPercent)).FirstOrDefaultAsync(cancellationToken);
        return View(new ParentHomeViewModel
        {
            Child = chosen,
            Children = choices,
            ParentName = User.FindFirstValue("FullName"),
            Today = status,
            Week = await ProgressService.WeekAsync(db, chosen.Id, ProgressService.WeekStart(today), cancellationToken),
            Streak = await ProgressService.StreakAsync(db, chosen.Id, cancellationToken),
            AvgQuiz = scored.Count == 0 ? null : Math.Round(scored.Average(), 1),
            OpenAssignments = await db.Assignments.CountAsync(item => item.ChildProfileId == chosen.Id
                && (item.Status == AssignmentStatus.Assigned || item.Status == AssignmentStatus.InProgress), cancellationToken),
            Days = Enumerable.Range(0, 7).Select(offset => since.AddDays(offset)).Select(day => (day, usage.TryGetValue(day, out var seconds) ? seconds / 60 : 0)).ToList(),
            Subjects = subjects,
            Attention = attention.Take(5).ToList(),
            Continue = continuing
        });
    }
}
