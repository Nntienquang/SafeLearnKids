using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SafeLearnKids.Web.Data;
using SafeLearnKids.Web.Models;
using SafeLearnKids.Web.Services;

namespace SafeLearnKids.Web.Areas.Admin.Controllers;

// US-48: the admin home. Finished days come from dbo.SystemMetricsDaily (filled by DailyJobs); today is counted live.
[Area("Admin")]
[Authorize(Roles = "Admin")]
public class DashboardController(IServiceProvider services) : Controller
{
    private const int TrendDays = 14;

    [HttpGet("admin-home")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        if (services.GetService<SafeLearnDbContext>() is not { } db) return View("SqlOnly");
        var today = StudyTimeService.Today;
        var first = today.AddDays(-(TrendDays - 1));
        var stored = await db.SystemMetricsDailies.AsNoTracking().Where(item => item.MetricDate >= first && item.MetricDate < today)
            .Select(item => new DailyMetric(item.MetricDate, item.ActiveParents, item.ActiveChildren, item.TotalMinutes, item.LessonsCompleted, item.OpenReports, item.NewSources))
            .ToListAsync(cancellationToken);
        var live = await DailyJobs.MetricsForAsync(db, today, cancellationToken);
        var todayMetric = new DailyMetric(today, live.ActiveParents, live.ActiveChildren, live.TotalMinutes, live.LessonsCompleted, live.OpenReports, live.NewSources);
        // Days the job has not recorded yet show as empty rather than being guessed.
        var trend = Enumerable.Range(0, TrendDays - 1).Select(offset => first.AddDays(offset))
            .Select(day => stored.FirstOrDefault(item => item.Day == day) ?? new DailyMetric(day, 0, 0, 0, 0, 0, 0)).Append(todayMetric).ToList();
        var week = DateTime.UtcNow.AddDays(-7);
        var dayAgo = DateTime.UtcNow.AddDays(-1);
        var weekAgo = today.AddDays(-6);
        var parentRole = await db.Roles.Where(role => role.NormalizedName == "PARENT").Select(role => role.Id).FirstOrDefaultAsync(cancellationToken);
        var parents = db.Users.Where(user => user.Roles.Any(role => role.Id == parentRole));
        return View(new AdminDashboardViewModel
        {
            Parents = await parents.CountAsync(cancellationToken),
            NewParents7 = await parents.CountAsync(user => user.CreatedAt >= week, cancellationToken),
            Children = await db.ChildProfiles.CountAsync(child => child.IsActive, cancellationToken),
            ActiveChildren7 = await db.DailyUsages.Where(item => item.UsageDate >= weekAgo && item.Seconds > 0).Select(item => item.ChildProfileId).Distinct().CountAsync(cancellationToken),
            PublishedLessons = await db.Lessons.CountAsync(lesson => lesson.Status == LessonStatus.Published, cancellationToken),
            ApprovedVideos = await db.SourceVideos.CountAsync(video => video.Status == "Approved", cancellationToken),
            Today = todayMetric,
            Trend = trend,
            OpenReports = await db.ContentReports.CountAsync(report => report.Status == ReportStatus.Open || report.Status == ReportStatus.InReview, cancellationToken),
            PendingSources = await db.ContentSources.CountAsync(source => source.Status == "Suggested", cancellationToken),
            PendingVideos = await db.SourceVideos.CountAsync(video => video.Status == "Pending", cancellationToken),
            PendingQuestions = await db.Questions.CountAsync(question => question.Status == QuestionStatus.InReview, cancellationToken),
            PendingDataRequests = await db.DataRequests.CountAsync(request => request.Status == DataRequestStatus.Pending || request.Status == DataRequestStatus.Processing, cancellationToken),
            FailedLogins24h = await db.LoginHistories.CountAsync(login => !login.Succeeded && login.CreatedAt >= dayAgo, cancellationToken)
        });
    }
}
