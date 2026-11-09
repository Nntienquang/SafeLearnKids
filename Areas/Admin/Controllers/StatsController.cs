using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SafeLearnKids.Web.Data;
using SafeLearnKids.Web.Models;
using SafeLearnKids.Web.Services;

namespace SafeLearnKids.Web.Areas.Admin.Controllers;

// US-49: how the content is used over the last 7, 30 or 90 days. Only counts, never which child did what.
[Area("Admin")]
[Authorize(Roles = "Admin")]
public class StatsController(IServiceProvider services) : Controller
{
    private static readonly int[] Ranges = [7, 30, 90];
    private const int Top = 20;

    [HttpGet("admin-stats")]
    public async Task<IActionResult> Index(int days = 30, CancellationToken cancellationToken = default)
    {
        if (services.GetService<SafeLearnDbContext>() is not { } db) return View("SqlOnly");
        return View(await BuildAsync(db, days, cancellationToken));
    }

    [HttpGet("admin-stats/export")]
    public async Task<IActionResult> Export(int days = 30, CancellationToken cancellationToken = default)
    {
        if (services.GetService<SafeLearnDbContext>() is not { } db) return View("SqlOnly");
        var model = await BuildAsync(db, days, cancellationToken, take: 1000);
        var csv = new StringBuilder("Bài học,Môn,Lớp,Số bé học,Số bé hoàn thành,Tỉ lệ hoàn thành (%),Phút xem,Yêu thích,Báo cáo\r\n");
        foreach (var row in model.Lessons)
            csv.AppendJoin(',', new[] { row.Title, row.Subject, row.Grade.ToString(), row.Learners.ToString(), row.Completed.ToString(), row.CompletionRate.ToString(),
                row.WatchMinutes.ToString(), row.Favorites.ToString(), row.Reports.ToString() }.Select(Cell)).Append("\r\n");
        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv.ToString())).ToArray();
        return File(bytes, "text/csv; charset=utf-8", $"thong-ke-bai-hoc-{model.Days}-ngay.csv");
    }

    private static async Task<ContentStatsViewModel> BuildAsync(SafeLearnDbContext db, int days, CancellationToken cancellationToken, int take = Top)
    {
        days = Ranges.Contains(days) ? days : 30;
        var since = DateTime.UtcNow.AddDays(-days);
        // Anonymous projections first: EF can sort and filter those in SQL, then the rows become records.
        var lessons = (await LessonAccess.Public(db.Lessons.AsNoTracking()).Where(lesson => lesson.Status == LessonStatus.Published)
            .Select(lesson => new
            {
                lesson.Id, lesson.Title, Subject = lesson.Subject.Name, lesson.GradeLevel.Level,
                Learners = lesson.LessonActivities.Count(item => item.UpdatedAt >= since),
                Completed = lesson.LessonActivities.Count(item => item.Status == "Completed" && item.CompletedAt >= since),
                Seconds = lesson.LessonActivities.Where(item => item.UpdatedAt >= since).Sum(item => (int?)item.WatchSeconds) ?? 0,
                Favorites = lesson.Favorites.Count(item => item.FavoritedAt >= since),
                Reports = lesson.ContentReports.Count(item => item.CreatedAt >= since)
            })
            .OrderByDescending(row => row.Learners).ThenByDescending(row => row.Completed).ThenBy(row => row.Title).Take(take)
            .ToListAsync(cancellationToken))
            .Select(row => new LessonStatRow(row.Id, row.Title, row.Subject, row.Level, row.Learners, row.Completed, row.Seconds / 60, row.Favorites, row.Reports)).ToList();
        var subjects = (await db.Subjects.AsNoTracking().OrderBy(subject => subject.SortOrder)
            .Select(subject => new
            {
                subject.Name,
                Lessons = subject.Lessons.Count(lesson => lesson.Status == LessonStatus.Published && (lesson.OwnerParentId == null || lesson.ShareStatus == LessonAccess.Approved)),
                Learners = subject.Lessons.Where(lesson => lesson.OwnerParentId == null || lesson.ShareStatus == LessonAccess.Approved).SelectMany(lesson => lesson.LessonActivities).Where(item => item.UpdatedAt >= since).Select(item => item.ChildProfileId).Distinct().Count(),
                Completed = subject.Lessons.Where(lesson => lesson.OwnerParentId == null || lesson.ShareStatus == LessonAccess.Approved).SelectMany(lesson => lesson.LessonActivities).Count(item => item.Status == "Completed" && item.CompletedAt >= since),
                Seconds = subject.Lessons.Where(lesson => lesson.OwnerParentId == null || lesson.ShareStatus == LessonAccess.Approved).SelectMany(lesson => lesson.LessonActivities).Where(item => item.UpdatedAt >= since).Sum(item => (int?)item.WatchSeconds) ?? 0
            }).ToListAsync(cancellationToken))
            .Select(row => new SubjectStatRow(row.Name, row.Lessons, row.Learners, row.Completed, row.Seconds / 60)).ToList();
        var quizzes = (await db.Quizzes.AsNoTracking().Where(quiz => quiz.Lesson.OwnerParentId == null || quiz.Lesson.ShareStatus == LessonAccess.Approved)
            .Select(quiz => new
            {
                quiz.Id, quiz.Title, Lesson = quiz.Lesson.Title,
                Attempts = quiz.QuizAttempts.Count(item => item.SubmittedAt >= since),
                Children = quiz.QuizAttempts.Where(item => item.SubmittedAt >= since).Select(item => item.ChildProfileId).Distinct().Count(),
                Average = quiz.QuizAttempts.Where(item => item.SubmittedAt >= since).Average(item => item.ScorePercent),
                Passed = quiz.QuizAttempts.Count(item => item.SubmittedAt >= since && item.Passed == true)
            })
            .Where(row => row.Attempts > 0).OrderByDescending(row => row.Attempts).Take(take).ToListAsync(cancellationToken))
            .Select(row => new QuizStatRow(row.Id, row.Title, row.Lesson, row.Attempts, row.Children, row.Average, row.Passed)).ToList();
        // Questions most often answered wrong (at least 3 answers, so one child does not decide it).
        var hard = (await db.Questions.AsNoTracking().Where(question => question.Lesson.OwnerParentId == null || question.Lesson.ShareStatus == LessonAccess.Approved)
            .Select(question => new
            {
                question.Id, question.Text,
                Quiz = question.QuizQuestions.Select(link => link.Quiz.Title).FirstOrDefault(),
                Answers = question.QuizAttemptAnswers.Count(answer => answer.AnsweredAt >= since),
                Wrong = question.QuizAttemptAnswers.Count(answer => answer.AnsweredAt >= since && !answer.IsCorrect)
            })
            .Where(row => row.Answers >= 3).OrderByDescending(row => row.Wrong * 100 / row.Answers).ThenByDescending(row => row.Answers).Take(10)
            .ToListAsync(cancellationToken))
            .Select(row => new QuestionStatRow(row.Id, row.Text, row.Quiz ?? "", row.Answers, row.Wrong)).ToList();
        var searches = db.SearchLogs.AsNoTracking().Where(item => item.CreatedAt >= since);
        return new ContentStatsViewModel
        {
            Days = days,
            Lessons = lessons,
            Subjects = subjects,
            Quizzes = quizzes,
            HardQuestions = hard,
            SearchTotal = await searches.CountAsync(cancellationToken),
            SearchBlocked = await searches.CountAsync(item => item.WasBlocked, cancellationToken),
            Searches = (await searches.GroupBy(item => item.Query)
                .Select(group => new { Query = group.Key, Count = group.Count(), Blocked = group.Count(item => item.WasBlocked), Empty = group.Count(item => !item.WasBlocked && item.ResultCount == 0) })
                .OrderByDescending(row => row.Count).Take(15).ToListAsync(cancellationToken))
                .Select(row => new SearchStatRow(row.Query, row.Count, row.Blocked, row.Empty)).ToList()
        };
    }

    // Titles come from admins; spreadsheets must not run them as formulas.
    private static string Cell(string value)
    {
        if (value.Length > 0 && "=+-@\t\r".Contains(value[0])) value = "'" + value;
        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }
}
