using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SafeLearnKids.Web.Data;
using SafeLearnKids.Web.Models;
using SafeLearnKids.Web.Models.Entities;

namespace SafeLearnKids.Web.Services;

// Section "Jobs" in configuration.
public class JobOptions
{
    public bool Enabled { get; set; } = true;
    public int IntervalMinutes { get; set; } = 60;
    public int StartDelaySeconds { get; set; } = 20;
    public int MetricDays { get; set; } = 30;
}

// US-47/48: once an hour, records the daily numbers admins see and writes last week's report for every child.
// Each step only fills what is missing, so restarting the app or running two instances does no harm.
public sealed class DailyJobs(IServiceScopeFactory scopes, IOptions<JobOptions> options, AccountEmailQueue mail,
    IOptions<AccountEmailOptions> mailOptions, ILogger<DailyJobs> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.Enabled) return;
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(Math.Max(0, options.Value.StartDelaySeconds)), stoppingToken);
            using var timer = new PeriodicTimer(TimeSpan.FromMinutes(Math.Max(1, options.Value.IntervalMinutes)));
            do
            {
                await RunOnceAsync(stoppingToken);
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
    }

    public async Task RunOnceAsync(CancellationToken cancellationToken)
    {
        using var scope = scopes.CreateScope();
        if (scope.ServiceProvider.GetService<SafeLearnDbContext>() is not { } db) return;
        try
        {
            await MetricsAsync(db, cancellationToken);
            await WeeklyReportsAsync(db, cancellationToken);
        }
        catch (Exception error) when (error is DbUpdateException or InvalidOperationException or Microsoft.Data.SqlClient.SqlException)
        {
            logger.LogWarning(error, "Daily jobs stopped early; they run again at the next interval.");
        }
    }

    // US-48: one row per finished day in dbo.SystemMetricsDaily.
    private async Task MetricsAsync(SafeLearnDbContext db, CancellationToken cancellationToken)
    {
        var today = StudyTimeService.Today;
        var first = today.AddDays(-Math.Max(1, options.Value.MetricDays));
        var done = await db.SystemMetricsDailies.Where(item => item.MetricDate >= first).Select(item => item.MetricDate).ToListAsync(cancellationToken);
        for (var day = first; day < today; day = day.AddDays(1))
        {
            if (done.Contains(day)) continue;
            db.SystemMetricsDailies.Add(await MetricsForAsync(db, day, cancellationToken));
            try
            {
                await db.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException)
            {
                // Another instance recorded the same day.
                db.ChangeTracker.Clear();
            }
        }
    }

    public static async Task<SystemMetricsDaily> MetricsForAsync(SafeLearnDbContext db, DateOnly day, CancellationToken cancellationToken)
    {
        var from = day.ToDateTime(TimeOnly.MinValue).ToUniversalTime();
        var to = day.AddDays(1).ToDateTime(TimeOnly.MinValue).ToUniversalTime();
        return new SystemMetricsDaily
        {
            MetricDate = day,
            ActiveParents = await db.LoginHistories.Where(item => item.Succeeded && item.UserId != null && item.CreatedAt >= from && item.CreatedAt < to)
                .Select(item => item.UserId).Distinct().CountAsync(cancellationToken),
            ActiveChildren = await db.DailyUsages.CountAsync(item => item.UsageDate == day && item.Seconds > 0, cancellationToken),
            TotalMinutes = (await db.DailyUsages.Where(item => item.UsageDate == day).SumAsync(item => (long?)item.Seconds, cancellationToken) ?? 0) / 60,
            LessonsCompleted = await db.LessonActivities.CountAsync(item => item.Status == "Completed" && item.CompletedAt >= from && item.CompletedAt < to, cancellationToken),
            // Reports that were open at the end of that day.
            OpenReports = await db.ContentReports.CountAsync(item => item.CreatedAt < to && (item.ResolvedAt == null || item.ResolvedAt >= to), cancellationToken),
            NewSources = await db.ContentSources.CountAsync(item => item.CreatedAt >= from && item.CreatedAt < to, cancellationToken)
        };
    }

    // US-47: last week's report for each active child that existed then; the parent is told and, if they want it, emailed.
    private async Task WeeklyReportsAsync(SafeLearnDbContext db, CancellationToken cancellationToken)
    {
        var weekStart = ProgressService.WeekStart(StudyTimeService.Today).AddDays(-7);
        var weekEnd = weekStart.AddDays(7).ToDateTime(TimeOnly.MinValue).ToUniversalTime();
        var pending = await db.ChildProfiles.AsNoTracking()
            .Where(child => child.IsActive && child.CreatedAt < weekEnd && !child.WeeklyReports.Any(report => report.WeekStart == weekStart))
            .Select(child => new
            {
                child.Id, child.DisplayName, child.ParentId, child.Parent.Email, child.Parent.EmailConfirmed,
                WantsEmail = child.Parent.UserSetting == null || child.Parent.UserSetting.WeeklyEmail
            }).Take(500).ToListAsync(cancellationToken);
        foreach (var child in pending)
        {
            var data = await WeeklyReportService.ComputeAsync(db, child.Id, child.DisplayName, weekStart, cancellationToken);
            try
            {
                var (report, created) = await WeeklyReportService.SaveAsync(db, child.Id, weekStart, data, cancellationToken);
                if (!created) continue;
                var link = $"/weekly?child={child.Id}&week={weekStart:yyyy-MM-dd}";
                NotificationWriter.Add(db, child.ParentId, "WeeklyReport", $"Báo cáo tuần của {child.DisplayName}", data.Summary, link);
                if (child.WantsEmail && child.EmailConfirmed && !string.IsNullOrEmpty(child.Email))
                {
                    mail.Enqueue(child.Email, $"SafeLearn Kids · Báo cáo tuần {weekStart:dd/MM} của {child.DisplayName}", Email(child.DisplayName, weekStart, data, link));
                    report.EmailedAt = DateTime.UtcNow;
                }
                await db.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException)
            {
                db.ChangeTracker.Clear();
            }
        }
    }

    private string Email(string name, DateOnly weekStart, WeeklyReportService.WeekData data, string link)
    {
        var text = new StringBuilder();
        text.AppendLine($"Báo cáo tuần {weekStart:dd/MM} - {weekStart.AddDays(6):dd/MM/yyyy} của {name}");
        text.AppendLine();
        text.AppendLine(data.Summary);
        text.AppendLine();
        text.AppendLine($"Thời gian học: {data.Minutes} phút");
        text.AppendLine($"Bài hoàn thành: {data.Lessons}");
        text.AppendLine($"Lượt làm bài tập: {data.Attempts}" + (data.AvgQuiz is { } avg ? $" (trung bình {avg:0.#}%)" : ""));
        text.AppendLine();
        text.AppendLine("Xem chi tiết: " + mailOptions.Value.PublicBaseUrl.TrimEnd('/') + link);
        text.AppendLine("Tắt email này trong Cài đặt của tài khoản.");
        return text.ToString();
    }
}
