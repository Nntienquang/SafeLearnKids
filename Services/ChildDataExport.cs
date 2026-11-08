using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SafeLearnKids.Web.Data;

namespace SafeLearnKids.Web.Services;

// US-52: everything stored about one child, as one JSON document the parent can keep.
public static class ChildDataExport
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerOptions.Web) { WriteIndented = true };

    public static async Task<byte[]> BuildAsync(SafeLearnDbContext db, int childId, CancellationToken cancellationToken)
    {
        var child = await db.ChildProfiles.AsNoTracking().Where(item => item.Id == childId).Select(item => new
        {
            item.Id, item.DisplayName, Grade = item.GradeLevel.Level, item.BirthYear, item.IsActive, item.CreatedAt,
            Subjects = item.ChildSubjects.Select(link => link.Subject.Name).ToList()
        }).FirstAsync(cancellationToken);
        var document = new
        {
            exportedAt = DateTime.UtcNow,
            note = "Dữ liệu học tập của bé trên SafeLearn Kids. Thời gian theo giờ UTC.",
            profile = child,
            lessons = await db.LessonActivities.AsNoTracking().Where(item => item.ChildProfileId == childId).OrderBy(item => item.StartedAt)
                .Select(item => new { lesson = item.Lesson.Title, item.Status, item.ProgressPercent, item.WatchSeconds, item.StartedAt, item.CompletedAt, item.UpdatedAt })
                .ToListAsync(cancellationToken),
            favorites = await db.Favorites.AsNoTracking().Where(item => item.ChildProfileId == childId).Select(item => new { lesson = item.Lesson.Title, item.FavoritedAt }).ToListAsync(cancellationToken),
            assignments = await db.Assignments.AsNoTracking().Where(item => item.ChildProfileId == childId).OrderBy(item => item.AssignedAt)
                .Select(item => new { lesson = item.Lesson != null ? item.Lesson.Title : null, quiz = item.Quiz != null ? item.Quiz.Title : null, item.Status, item.DueDate, item.Note, item.AssignedAt, item.CompletedAt })
                .ToListAsync(cancellationToken),
            quizAttempts = await db.QuizAttempts.AsNoTracking().Where(item => item.ChildProfileId == childId).OrderBy(item => item.StartedAt)
                .Select(item => new
                {
                    quiz = item.Quiz.Title, item.StartedAt, item.SubmittedAt, item.ScorePercent, item.CorrectCount, item.TotalCount, item.Passed,
                    answers = item.QuizAttemptAnswerAttempts.Select(answer => new
                    {
                        question = answer.Question.Text, chosen = answer.SelectedOption != null ? answer.SelectedOption.Text : null, answer.IsCorrect, answer.AnsweredAt
                    }).ToList()
                }).ToListAsync(cancellationToken),
            dailyUsage = await db.DailyUsages.AsNoTracking().Where(item => item.ChildProfileId == childId).OrderBy(item => item.UsageDate)
                .Select(item => new { item.UsageDate, item.Seconds }).ToListAsync(cancellationToken),
            sessions = await db.LearningSessions.AsNoTracking().Where(item => item.ChildProfileId == childId).OrderBy(item => item.StartedAt)
                .Select(item => new { item.StartedAt, item.LastHeartbeatAt, item.EndedAt, item.EndReason }).ToListAsync(cancellationToken),
            searches = await db.SearchLogs.AsNoTracking().Where(item => item.ChildProfileId == childId).OrderBy(item => item.CreatedAt)
                .Select(item => new { item.Query, item.ResultCount, item.WasBlocked, item.CreatedAt }).ToListAsync(cancellationToken),
            timeLimits = await db.TimeLimitPolicies.AsNoTracking().Where(item => item.ChildProfileId == childId)
                .Select(item => new { item.DailyMinutes, item.StudyStart, item.StudyEnd, item.BreakEveryMinutes }).ToListAsync(cancellationToken),
            timeRequests = await db.TimeExtensionRequests.AsNoTracking().Where(item => item.ChildProfileId == childId).OrderBy(item => item.RequestedAt)
                .Select(item => new { item.ForDate, item.RequestedMinutes, item.GrantedMinutes, item.Reason, item.Status, item.RequestedAt, item.DecidedAt }).ToListAsync(cancellationToken),
            reports = await db.ContentReports.AsNoTracking().Where(item => item.ChildProfileId == childId).OrderBy(item => item.CreatedAt)
                .Select(item => new { lesson = item.Lesson != null ? item.Lesson.Title : null, video = item.SourceVideo != null ? item.SourceVideo.Title : null, item.Reason, item.Comment, item.Status, item.Resolution, item.CreatedAt })
                .ToListAsync(cancellationToken),
            badges = await db.ChildBadges.AsNoTracking().Where(item => item.ChildProfileId == childId).Select(item => new { badge = item.Badge.Name, item.AwardedAt }).ToListAsync(cancellationToken),
            streak = await db.Streaks.AsNoTracking().Where(item => item.ChildProfileId == childId).Select(item => new { item.CurrentDays, item.LongestDays, item.LastActiveDate }).FirstOrDefaultAsync(cancellationToken),
            goals = await db.LearningGoals.AsNoTracking().Where(item => item.ChildProfileId == childId).OrderBy(item => item.WeekStart)
                .Select(item => new { item.WeekStart, item.TargetLessons, item.TargetMinutes }).ToListAsync(cancellationToken),
            paths = await db.ChildPaths.AsNoTracking().Where(item => item.ChildProfileId == childId).Select(item => new { path = item.Path.Name, item.Status, item.StartedAt, item.CompletedAt }).ToListAsync(cancellationToken),
            subjectProgress = await db.SubjectProgresses.AsNoTracking().Where(item => item.ChildProfileId == childId)
                .Select(item => new { subject = item.Subject.Name, item.CompletedLessons, item.TotalLessons, item.AvgQuizPercent }).ToListAsync(cancellationToken),
            weeklyReports = await db.WeeklyReports.AsNoTracking().Where(item => item.ChildProfileId == childId).OrderBy(item => item.WeekStart)
                .Select(item => new { item.WeekStart, item.TotalMinutes, item.LessonsCompleted, item.QuizAttempts, item.AvgQuizPercent, item.Summary }).ToListAsync(cancellationToken),
            consents = await db.UserConsents.AsNoTracking().Where(item => item.ChildProfileId == childId)
                .Select(item => new { item.ConsentType, item.Version, item.Decision, item.DecidedAt }).ToListAsync(cancellationToken)
        };
        return JsonSerializer.SerializeToUtf8Bytes(document, Json);
    }
}
