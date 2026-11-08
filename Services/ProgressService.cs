using Microsoft.EntityFrameworkCore;
using SafeLearnKids.Web.Data;
using SafeLearnKids.Web.Models;
using SafeLearnKids.Web.Models.Entities;

namespace SafeLearnKids.Web.Services;

// Sprint 7: subject progress (US-41), learning paths (US-42), badges (US-43), streaks (US-44),
// weekly goals (US-45) and questions to review (US-46). Everything is computed from what the child really did.
public static class ProgressService
{
    private const int ReviewLimit = 20;

    public static DateOnly WeekStart(DateOnly day) => day.AddDays(-(StudyTimeService.Weekday(day) - 1));

    // Runs after the child studied (lesson time, a finished lesson, a submitted quiz). Saves its own changes and
    // returns the names of badges earned just now. A race with another tab is harmless: the next call catches up.
    // studied is false when only the plan changed (a parent chose a path), so the streak is not touched.
    public static async Task<IReadOnlyList<string>> AfterStudyAsync(SafeLearnDbContext db, int kidId, string parentId, string kidName, CancellationToken cancellationToken, bool studied = true)
    {
        try
        {
            if (studied) await TouchStreakAsync(db, kidId, cancellationToken);
            await RefreshSubjectsAsync(db, kidId, cancellationToken);
            await AdvancePathsAsync(db, kidId, cancellationToken);
            var earned = await AwardAsync(db, kidId, parentId, kidName, cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
            return earned;
        }
        catch (DbUpdateException)
        {
            db.ChangeTracker.Clear();
            return [];
        }
    }

    // US-44: one day counts once; missing a day starts again at 1.
    private static async Task TouchStreakAsync(SafeLearnDbContext db, int kidId, CancellationToken cancellationToken)
    {
        var today = StudyTimeService.Today;
        var streak = await db.Streaks.FirstOrDefaultAsync(item => item.ChildProfileId == kidId, cancellationToken);
        if (streak is null)
        {
            db.Streaks.Add(new Streak { ChildProfileId = kidId, CurrentDays = 1, LongestDays = 1, LastActiveDate = today });
            return;
        }
        if (streak.LastActiveDate >= today) return;
        streak.CurrentDays = streak.LastActiveDate == today.AddDays(-1) ? streak.CurrentDays + 1 : 1;
        streak.LongestDays = Math.Max(streak.LongestDays, streak.CurrentDays);
        streak.LastActiveDate = today;
    }

    public static async Task<StreakView> StreakAsync(SafeLearnDbContext db, int kidId, CancellationToken cancellationToken)
    {
        var streak = await db.Streaks.AsNoTracking().FirstOrDefaultAsync(item => item.ChildProfileId == kidId, cancellationToken);
        if (streak is null) return new StreakView(0, 0, false);
        var today = StudyTimeService.Today;
        // A streak that missed yesterday is already broken, even before the next study day resets it.
        var alive = streak.LastActiveDate is { } last && last >= today.AddDays(-1);
        return new StreakView(alive ? streak.CurrentDays : 0, streak.LongestDays, streak.LastActiveDate >= today);
    }

    // US-41: one row per subject the child studies at their grade, refreshed from lesson activity and quiz scores.
    private static async Task RefreshSubjectsAsync(SafeLearnDbContext db, int kidId, CancellationToken cancellationToken)
    {
        var rows = await SubjectRowsAsync(db, kidId, cancellationToken);
        var stored = await db.SubjectProgresses.Where(item => item.ChildProfileId == kidId).ToListAsync(cancellationToken);
        var now = DateTime.UtcNow;
        foreach (var row in rows)
        {
            var progress = stored.FirstOrDefault(item => item.SubjectId == row.SubjectId);
            if (progress is null)
            {
                progress = new SubjectProgress { ChildProfileId = kidId, SubjectId = row.SubjectId };
                db.SubjectProgresses.Add(progress);
            }
            else if (progress.CompletedLessons == row.Completed && progress.TotalLessons == row.Total && progress.AvgQuizPercent == row.AvgQuiz) continue;
            progress.TotalLessons = row.Total;
            progress.CompletedLessons = Math.Min(row.Completed, row.Total);
            progress.AvgQuizPercent = row.AvgQuiz;
            progress.UpdatedAt = now;
        }
    }

    public static async Task<IReadOnlyList<SubjectProgressRow>> SubjectRowsAsync(SafeLearnDbContext db, int kidId, CancellationToken cancellationToken)
    {
        var child = await db.ChildProfiles.AsNoTracking().Where(item => item.Id == kidId)
            .Select(item => new { item.GradeLevelId, Subjects = item.ChildSubjects.Select(link => link.SubjectId).ToList() }).FirstAsync(cancellationToken);
        var subjects = await db.Subjects.AsNoTracking().Where(subject => subject.IsActive && child.Subjects.Contains(subject.Id))
            .OrderBy(subject => subject.SortOrder).ThenBy(subject => subject.Name)
            .Select(subject => new
            {
                subject.Id, subject.Code, subject.Name, subject.ColorKey, subject.IconKey,
                Total = subject.Lessons.Count(lesson => lesson.Status == LessonStatus.Published && lesson.GradeLevelId == child.GradeLevelId),
                Completed = subject.Lessons.Count(lesson => lesson.Status == LessonStatus.Published && lesson.GradeLevelId == child.GradeLevelId
                    && lesson.LessonActivities.Any(item => item.ChildProfileId == kidId && item.Status == "Completed"))
            }).ToListAsync(cancellationToken);
        // Average of the best score per quiz, by subject.
        var best = await db.QuizAttempts.AsNoTracking().Where(item => item.ChildProfileId == kidId && item.SubmittedAt != null && item.ScorePercent != null)
            .GroupBy(item => new { item.QuizId, item.Quiz.Lesson.SubjectId })
            .Select(group => new { group.Key.SubjectId, Best = group.Max(item => item.ScorePercent!.Value) }).ToListAsync(cancellationToken);
        return subjects.Select(subject =>
        {
            var look = SubjectCatalog.Look(subject.Code, subject.Name, subject.ColorKey, subject.IconKey, true);
            var scores = best.Where(item => item.SubjectId == subject.Id).Select(item => item.Best).ToList();
            return new SubjectProgressRow(subject.Id, subject.Name, look.Key, look.C, subject.Completed, subject.Total,
                scores.Count == 0 ? null : Math.Round(scores.Average(), 2));
        }).ToList();
    }

    // US-42: the current step is the first lesson not done yet; the path is complete when every required lesson is done.
    private static async Task AdvancePathsAsync(SafeLearnDbContext db, int kidId, CancellationToken cancellationToken)
    {
        var paths = await db.ChildPaths.Where(item => item.ChildProfileId == kidId && item.Status != PathStatus.Completed).ToListAsync(cancellationToken);
        if (paths.Count == 0) return;
        var done = await CompletedLessonsAsync(db, kidId, cancellationToken);
        var pathIds = paths.Select(item => item.PathId).ToList();
        var items = await db.LearningPathItems.AsNoTracking().Where(item => pathIds.Contains(item.PathId))
            .OrderBy(item => item.SortOrder).ThenBy(item => item.Id).ToListAsync(cancellationToken);
        var now = DateTime.UtcNow;
        foreach (var path in paths)
        {
            var steps = items.Where(item => item.PathId == path.PathId).ToList();
            var current = steps.FirstOrDefault(item => !done.Contains(item.LessonId));
            var complete = steps.Count > 0 && steps.Where(item => item.IsRequired).All(item => done.Contains(item.LessonId));
            path.CurrentItemId = complete ? null : current?.Id;
            if (complete)
            {
                path.Status = PathStatus.Completed;
                path.StartedAt ??= now;
                path.CompletedAt = now < path.StartedAt ? path.StartedAt : now;
            }
            else if (path.Status == PathStatus.NotStarted && steps.Any(item => done.Contains(item.LessonId)))
            {
                path.Status = PathStatus.InProgress;
                path.StartedAt ??= now;
            }
        }
    }

    public static async Task<HashSet<int>> CompletedLessonsAsync(SafeLearnDbContext db, int kidId, CancellationToken cancellationToken) =>
        (await db.LessonActivities.AsNoTracking().Where(item => item.ChildProfileId == kidId && item.Status == "Completed")
            .Select(item => item.LessonId).ToListAsync(cancellationToken)).ToHashSet();

    // Paths shown to parents and children: those this child follows, plus active ones for the child's grade and subjects.
    public static async Task<IReadOnlyList<PathView>> PathsAsync(SafeLearnDbContext db, int kidId, bool followedOnly, CancellationToken cancellationToken)
    {
        var child = await db.ChildProfiles.AsNoTracking().Where(item => item.Id == kidId)
            .Select(item => new { item.GradeLevelId, Subjects = item.ChildSubjects.Select(link => link.SubjectId).ToList() }).FirstAsync(cancellationToken);
        var rows = await db.LearningPaths.AsNoTracking()
            .Where(path => path.ChildPaths.Any(item => item.ChildProfileId == kidId)
                || (!followedOnly && path.IsActive && path.Subject.IsActive && path.GradeLevelId == child.GradeLevelId && child.Subjects.Contains(path.SubjectId)))
            .OrderBy(path => path.Subject.SortOrder).ThenBy(path => path.Name)
            .Select(path => new
            {
                path.Id, path.Name, path.Description, Subject = path.Subject.Name, path.GradeLevel.Level,
                Followed = path.ChildPaths.Where(item => item.ChildProfileId == kidId).Select(item => new { item.Id, item.Status }).FirstOrDefault(),
                Items = path.LearningPathItems.OrderBy(item => item.SortOrder).ThenBy(item => item.Id)
                    .Select(item => new { item.Id, item.LessonId, item.Lesson.Title, item.IsRequired, Published = item.Lesson.Status == LessonStatus.Published }).ToList()
            }).ToListAsync(cancellationToken);
        var done = await CompletedLessonsAsync(db, kidId, cancellationToken);
        return rows.Where(row => row.Followed is not null || row.Items.Count > 0).Select(row =>
        {
            // Lessons that are no longer published are left out of the steps.
            var items = row.Items.Where(item => item.Published).ToList();
            var currentId = items.FirstOrDefault(item => !done.Contains(item.LessonId))?.Id;
            var status = row.Followed?.Status ?? PathStatus.NotStarted;
            return new PathView(row.Id, row.Name, row.Description, row.Subject, row.Level, status,
                items.Select(item => new PathStep(item.Id, item.LessonId, item.Title, item.IsRequired, done.Contains(item.LessonId),
                    status != PathStatus.Completed && row.Followed is not null && item.Id == currentId)).ToList(), row.Followed?.Id);
        }).ToList();
    }

    // US-43: compares every active badge with the child's numbers and awards what is new. The parent is told.
    private static async Task<IReadOnlyList<string>> AwardAsync(SafeLearnDbContext db, int kidId, string parentId, string kidName, CancellationToken cancellationToken)
    {
        var badges = await BadgesAsync(db, kidId, cancellationToken);
        var fresh = badges.Where(badge => !badge.Earned && badge.Current >= badge.Threshold).ToList();
        var now = DateTime.UtcNow;
        foreach (var badge in fresh)
        {
            db.ChildBadges.Add(new ChildBadge { ChildProfileId = kidId, BadgeId = badge.Id, AwardedAt = now });
            NotificationWriter.Add(db, parentId, "Badge", $"{kidName} nhận huy hiệu {badge.Name}", badge.Description, $"/progress?child={kidId}");
        }
        return fresh.Select(badge => badge.Name).ToList();
    }

    public static async Task<IReadOnlyList<BadgeView>> BadgesAsync(SafeLearnDbContext db, int kidId, CancellationToken cancellationToken)
    {
        var badges = await db.Badges.AsNoTracking()
            .Where(badge => badge.IsActive || badge.ChildBadges.Any(item => item.ChildProfileId == kidId))
            .OrderBy(badge => badge.CriteriaType).ThenBy(badge => badge.Threshold).ThenBy(badge => badge.Id)
            .Select(badge => new
            {
                badge.Id, badge.Name, badge.Description, badge.IconKey, badge.CriteriaType, badge.Threshold,
                AwardedAt = badge.ChildBadges.Where(item => item.ChildProfileId == kidId).Select(item => (DateTime?)item.AwardedAt).FirstOrDefault()
            }).ToListAsync(cancellationToken);
        var metrics = await MetricsAsync(db, kidId, cancellationToken);
        return badges.Select(badge => new BadgeView(badge.Id, badge.Name, badge.Description, BadgeCriteria.Icon(badge.IconKey, badge.CriteriaType),
            badge.CriteriaType, badge.Threshold, metrics.TryGetValue(badge.CriteriaType, out var value) ? value : 0, badge.AwardedAt)).ToList();
    }

    private static async Task<Dictionary<string, int>> MetricsAsync(SafeLearnDbContext db, int kidId, CancellationToken cancellationToken)
    {
        var lessons = await db.LessonActivities.CountAsync(item => item.ChildProfileId == kidId && item.Status == "Completed", cancellationToken);
        // Tracked changes from this request (a streak touched just now) count too.
        var streak = db.Streaks.Local.FirstOrDefault(item => item.ChildProfileId == kidId)?.LongestDays
            ?? await db.Streaks.Where(item => item.ChildProfileId == kidId).Select(item => (int?)item.LongestDays).FirstOrDefaultAsync(cancellationToken) ?? 0;
        var perfect = await db.QuizAttempts.Where(item => item.ChildProfileId == kidId && item.SubmittedAt != null && item.ScorePercent >= 100)
            .Select(item => item.QuizId).Distinct().CountAsync(cancellationToken);
        var seconds = await db.DailyUsages.Where(item => item.ChildProfileId == kidId).SumAsync(item => (int?)item.Seconds, cancellationToken) ?? 0;
        return new()
        {
            [BadgeCriteria.LessonsCompleted] = lessons,
            [BadgeCriteria.StreakDays] = streak,
            [BadgeCriteria.QuizPerfect] = perfect,
            [BadgeCriteria.MinutesTotal] = seconds / 60
        };
    }

    // Stars on the child's badge page: the best result of each quiz, 0–5 stars as on the result page.
    public static async Task<int> StarsAsync(SafeLearnDbContext db, int kidId, CancellationToken cancellationToken)
    {
        var best = await db.QuizAttempts.AsNoTracking().Where(item => item.ChildProfileId == kidId && item.SubmittedAt != null && item.ScorePercent != null)
            .GroupBy(item => item.QuizId).Select(group => group.Max(item => item.ScorePercent!.Value)).ToListAsync(cancellationToken);
        return best.Sum(QuizRules.Stars);
    }

    // US-45: lessons finished and minutes studied in the week that starts on the given Monday.
    public static async Task<WeekGoalView> WeekAsync(SafeLearnDbContext db, int kidId, DateOnly weekStart, CancellationToken cancellationToken)
    {
        var goal = await db.LearningGoals.AsNoTracking().FirstOrDefaultAsync(item => item.ChildProfileId == kidId && item.WeekStart == weekStart, cancellationToken);
        var weekEnd = weekStart.AddDays(7);
        // CompletedAt is stored in UTC; the week is in server local time like the rest of study time.
        var from = weekStart.ToDateTime(TimeOnly.MinValue).ToUniversalTime();
        var to = weekEnd.ToDateTime(TimeOnly.MinValue).ToUniversalTime();
        var lessons = await db.LessonActivities.CountAsync(item => item.ChildProfileId == kidId && item.Status == "Completed"
            && item.CompletedAt >= from && item.CompletedAt < to, cancellationToken);
        var seconds = await db.DailyUsages.Where(item => item.ChildProfileId == kidId && item.UsageDate >= weekStart && item.UsageDate < weekEnd)
            .SumAsync(item => (int?)item.Seconds, cancellationToken) ?? 0;
        return new WeekGoalView(weekStart, goal?.TargetLessons, goal?.TargetMinutes, lessons, seconds / 60);
    }

    // US-46: questions whose latest answer by this child was wrong (or left empty), still in use.
    public static async Task<IReadOnlyList<ReviewQuestion>> ReviewAsync(SafeLearnDbContext db, int kidId, CancellationToken cancellationToken)
    {
        var answers = await db.QuizAttemptAnswers.AsNoTracking()
            .Where(item => item.Attempt.ChildProfileId == kidId && item.Attempt.SubmittedAt != null)
            .Select(item => new { item.QuestionId, item.QuizId, item.IsCorrect, item.SelectedOptionId, item.AttemptId, item.AnsweredAt })
            .ToListAsync(cancellationToken);
        var wrong = answers.GroupBy(item => item.QuestionId)
            .Select(group => group.OrderByDescending(item => item.AttemptId).First())
            .Where(item => !item.IsCorrect).OrderByDescending(item => item.AnsweredAt).Take(ReviewLimit).ToList();
        if (wrong.Count == 0) return [];
        var ids = wrong.Select(item => item.QuestionId).ToList();
        var questions = await db.Questions.AsNoTracking().Where(item => ids.Contains(item.Id) && item.Status == QuestionStatus.Published)
            .Select(item => new
            {
                item.Id, item.Text, item.Explanation, Lesson = item.Lesson.Title,
                Options = item.QuestionOptions.OrderBy(option => option.SortOrder).ThenBy(option => option.Id).Select(option => new QuizOptionView(option.Id, option.Text)).ToList()
            }).ToListAsync(cancellationToken);
        var quizIds = wrong.Select(item => item.QuizId).Distinct().ToList();
        var quizzes = await db.Quizzes.AsNoTracking().Where(item => quizIds.Contains(item.Id)).ToDictionaryAsync(item => item.Id, item => item.Title, cancellationToken);
        return wrong.Select(item => (item, question: questions.FirstOrDefault(row => row.Id == item.QuestionId)))
            .Where(pair => pair.question is not null)
            .Select(pair => new ReviewQuestion(pair.question!.Id, pair.question.Text, pair.question.Explanation, quizzes.GetValueOrDefault(pair.item.QuizId, ""),
                pair.item.QuizId, pair.question.Lesson, pair.question.Options, pair.item.SelectedOptionId, pair.item.AnsweredAt)).ToList();
    }
}
