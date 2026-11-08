using Microsoft.EntityFrameworkCore;
using SafeLearnKids.Web.Data;
using SafeLearnKids.Web.Models;
using SafeLearnKids.Web.Models.Entities;

namespace SafeLearnKids.Web.Services;

// The server's clock for child mode (US-30 to US-32). Days and study windows use the server's local time.
public static class StudyTimeService
{
    public const int DefaultDailyMinutes = 60;
    public const int MaxReportSeconds = 60;
    private static readonly TimeSpan SessionGap = TimeSpan.FromMinutes(5);

    public static DateOnly Today => DateOnly.FromDateTime(DateTime.Now);

    // dbo.TimeLimitOverrides.Weekday: 1 = Monday ... 7 = Sunday.
    public static int Weekday(DateOnly day) => ((int)day.DayOfWeek + 6) % 7 + 1;

    public static async Task<TimeStatus> StatusAsync(SafeLearnDbContext db, int childId, CancellationToken cancellationToken)
    {
        var today = Today;
        var weekday = Weekday(today);
        var policy = await db.TimeLimitPolicies.AsNoTracking().Where(item => item.ChildProfileId == childId).Select(item => new
        {
            item.DailyMinutes, item.StudyStart, item.StudyEnd, item.BreakEveryMinutes,
            Override = item.TimeLimitOverrides.Where(day => day.Weekday == weekday).Select(day => (int?)day.Minutes).FirstOrDefault()
        }).FirstOrDefaultAsync(cancellationToken);
        var limit = policy?.Override ?? policy?.DailyMinutes ?? DefaultDailyMinutes;
        var extra = await db.TimeExtensionRequests.Where(item => item.ChildProfileId == childId && item.ForDate == today && item.Status == RequestStatus.Approved)
            .SumAsync(item => item.GrantedMinutes ?? 0, cancellationToken);
        var used = await db.DailyUsages.Where(item => item.ChildProfileId == childId && item.UsageDate == today)
            .Select(item => item.Seconds).FirstOrDefaultAsync(cancellationToken);
        var now = TimeOnly.FromDateTime(DateTime.Now);
        string? blocked = null;
        if (policy?.StudyStart is { } start && policy.StudyEnd is { } end && !InWindow(now, start, end))
            blocked = $"Bây giờ ngoài giờ học. Con được học từ {start:HH\\:mm} đến {end:HH\\:mm}.";
        else if (used >= (limit + extra) * 60)
            blocked = limit + extra == 0 ? "Hôm nay là ngày nghỉ học." : "Con đã học hết giờ của hôm nay.";
        return new TimeStatus(limit, extra, used, policy?.StudyStart, policy?.StudyEnd, policy?.BreakEveryMinutes, blocked);
    }

    // Adds reported study time to today's usage and the open session. Returns whether a break reminder is due.
    public static async Task<bool> AddAsync(SafeLearnDbContext db, int childId, int seconds, CancellationToken cancellationToken)
    {
        seconds = Math.Clamp(seconds, 0, MaxReportSeconds);
        var today = Today;
        var now = DateTime.UtcNow;
        var usage = await db.DailyUsages.FirstOrDefaultAsync(item => item.ChildProfileId == childId && item.UsageDate == today, cancellationToken);
        if (usage is null) db.DailyUsages.Add(new DailyUsage { ChildProfileId = childId, UsageDate = today, Seconds = seconds });
        else usage.Seconds += seconds;

        var session = await db.LearningSessions.Where(item => item.ChildProfileId == childId && item.EndedAt == null)
            .OrderByDescending(item => item.LastHeartbeatAt).FirstOrDefaultAsync(cancellationToken);
        if (session is not null && now - session.LastHeartbeatAt > SessionGap)
        {
            // A long silence means the child left; that session ends where it last reported.
            session.EndedAt = session.LastHeartbeatAt;
            session.EndReason = "Timeout";
            session = null;
        }
        if (session is null)
        {
            session = new LearningSession { ChildProfileId = childId, StartedAt = now, LastHeartbeatAt = now };
            db.LearningSessions.Add(session);
        }
        var before = session.ActiveSeconds;
        session.ActiveSeconds += seconds;
        session.LastHeartbeatAt = now;
        var breakEvery = await db.TimeLimitPolicies.Where(item => item.ChildProfileId == childId).Select(item => item.BreakEveryMinutes).FirstOrDefaultAsync(cancellationToken);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Two tabs created today's usage row together; count this report on the row that won.
            db.ChangeTracker.Clear();
            await db.DailyUsages.Where(item => item.ChildProfileId == childId && item.UsageDate == today)
                .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.Seconds, item => item.Seconds + seconds), cancellationToken);
        }
        return breakEvery is > 0 && before / (breakEvery.Value * 60) < session.ActiveSeconds / (breakEvery.Value * 60);
    }

    public static async Task EndSessionAsync(SafeLearnDbContext db, int childId, string reason, CancellationToken cancellationToken) =>
        await db.LearningSessions.Where(item => item.ChildProfileId == childId && item.EndedAt == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.EndedAt, item => item.LastHeartbeatAt).SetProperty(item => item.EndReason, reason), cancellationToken);

    // Requests left pending from earlier days can no longer be granted.
    public static async Task ExpireAsync(SafeLearnDbContext db, CancellationToken cancellationToken)
    {
        var today = Today;
        await db.TimeExtensionRequests.Where(item => item.Status == RequestStatus.Pending && item.ForDate < today)
            .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.Status, RequestStatus.Expired), cancellationToken);
    }

    private static bool InWindow(TimeOnly now, TimeOnly start, TimeOnly end) =>
        start <= end ? now >= start && now < end : now >= start || now < end;
}
