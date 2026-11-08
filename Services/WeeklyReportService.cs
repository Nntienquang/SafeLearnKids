using Microsoft.EntityFrameworkCore;
using SafeLearnKids.Web.Data;
using SafeLearnKids.Web.Models;
using SafeLearnKids.Web.Models.Entities;

namespace SafeLearnKids.Web.Services;

// US-47: what one child did in one week (Monday to Sunday, server local time), stored in dbo.WeeklyReports.
public static class WeeklyReportService
{
    private const int AttentionScore = 70;

    public sealed record WeekData(int Minutes, IReadOnlyList<(DateOnly Day, int Minutes)> Days, int Lessons, int Attempts, decimal? AvgQuiz,
        IReadOnlyList<WeeklySubjectRow> Subjects, string? Strong, string? Attention, IReadOnlyList<string> Badges, string Summary);

    public static async Task<WeekData> ComputeAsync(SafeLearnDbContext db, int kidId, string kidName, DateOnly weekStart, CancellationToken cancellationToken)
    {
        var weekEnd = weekStart.AddDays(7);
        var from = weekStart.ToDateTime(TimeOnly.MinValue).ToUniversalTime();
        var to = weekEnd.ToDateTime(TimeOnly.MinValue).ToUniversalTime();
        var usage = await db.DailyUsages.AsNoTracking().Where(item => item.ChildProfileId == kidId && item.UsageDate >= weekStart && item.UsageDate < weekEnd)
            .ToDictionaryAsync(item => item.UsageDate, item => item.Seconds, cancellationToken);
        var days = Enumerable.Range(0, 7).Select(offset => weekStart.AddDays(offset))
            .Select(day => (day, usage.TryGetValue(day, out var seconds) ? seconds / 60 : 0)).ToList();
        var lessons = await db.LessonActivities.AsNoTracking()
            .Where(item => item.ChildProfileId == kidId && item.Status == "Completed" && item.CompletedAt >= from && item.CompletedAt < to)
            .Select(item => new { item.Lesson.SubjectId }).ToListAsync(cancellationToken);
        var attempts = await db.QuizAttempts.AsNoTracking()
            .Where(item => item.ChildProfileId == kidId && item.SubmittedAt >= from && item.SubmittedAt < to && item.ScorePercent != null)
            .Select(item => new { item.Quiz.Lesson.SubjectId, Score = item.ScorePercent!.Value }).ToListAsync(cancellationToken);
        var subjectIds = lessons.Select(item => item.SubjectId).Concat(attempts.Select(item => item.SubjectId)).Distinct().ToList();
        var subjects = await db.Subjects.AsNoTracking().Where(item => subjectIds.Contains(item.Id))
            .Select(item => new { item.Id, item.Code, item.Name, item.ColorKey, item.IconKey }).ToListAsync(cancellationToken);
        var rows = subjects.Select(subject =>
        {
            var scores = attempts.Where(item => item.SubjectId == subject.Id).Select(item => item.Score).ToList();
            return new WeeklySubjectRow(subject.Name, SubjectCatalog.Look(subject.Code, subject.Name, subject.ColorKey, subject.IconKey, true).C,
                lessons.Count(item => item.SubjectId == subject.Id), scores.Count, scores.Count == 0 ? null : Math.Round(scores.Average(), 2));
        }).OrderByDescending(row => row.Lessons + row.Attempts).ToList();
        var badges = await db.ChildBadges.AsNoTracking().Where(item => item.ChildProfileId == kidId && item.AwardedAt >= from && item.AwardedAt < to)
            .OrderBy(item => item.AwardedAt).Select(item => item.Badge.Name).ToListAsync(cancellationToken);

        // Strongest: best quiz average, otherwise most lessons. Needs attention: a quiz average below 70%.
        var strong = rows.Where(row => row.AvgQuiz is not null).OrderByDescending(row => row.AvgQuiz).ThenByDescending(row => row.Lessons).FirstOrDefault()
            ?? rows.Where(row => row.Lessons > 0).OrderByDescending(row => row.Lessons).FirstOrDefault();
        var attention = rows.Where(row => row.AvgQuiz < AttentionScore).OrderBy(row => row.AvgQuiz).FirstOrDefault();
        if (attention is not null && attention == strong) strong = null;
        var minutes = days.Sum(day => day.Item2);
        decimal? average = attempts.Count == 0 ? null : Math.Round(attempts.Average(item => item.Score), 2);
        var activeDays = days.Count(day => day.Item2 > 0);
        var summary = minutes == 0 && lessons.Count == 0 && attempts.Count == 0
            ? $"Trong tuần, {kidName} chưa học bài nào. Ba mẹ có thể giao một bài ngắn để bé bắt đầu lại."
            : $"{kidName} học {minutes} phút trong {activeDays} ngày, hoàn thành {lessons.Count} bài và làm {attempts.Count} lượt bài tập"
              + (average is { } avg ? $" (điểm trung bình {avg:0.#}%)" : "") + "."
              + (strong is not null ? $" Bé làm tốt nhất ở môn {strong.Name}." : "")
              + (attention is not null ? $" Môn {attention.Name} cần ôn thêm." : "")
              + (badges.Count > 0 ? $" Bé nhận {badges.Count} huy hiệu mới." : "");
        return new WeekData(minutes, days, lessons.Count, attempts.Count, average, rows, Cut(strong?.Name), Cut(attention?.Name), badges, summary);
    }

    // Creates or refreshes the stored row. Returns it and whether it is new.
    public static async Task<(WeeklyReport Report, bool Created)> SaveAsync(SafeLearnDbContext db, int kidId, DateOnly weekStart, WeekData data, CancellationToken cancellationToken)
    {
        var report = await db.WeeklyReports.FirstOrDefaultAsync(item => item.ChildProfileId == kidId && item.WeekStart == weekStart, cancellationToken);
        var created = report is null;
        if (report is null)
        {
            report = new WeeklyReport { ChildProfileId = kidId, WeekStart = weekStart };
            db.WeeklyReports.Add(report);
        }
        report.TotalMinutes = data.Minutes;
        report.LessonsCompleted = data.Lessons;
        report.QuizAttempts = data.Attempts;
        report.AvgQuizPercent = data.AvgQuiz;
        report.StrongSubject = data.Strong;
        report.NeedsAttentionSubject = data.Attention;
        report.Summary = data.Summary;
        report.GeneratedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return (report, created);
    }

    private static string? Cut(string? name) => name is { Length: > 80 } ? name[..80] : name;
}
