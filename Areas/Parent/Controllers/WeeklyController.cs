using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SafeLearnKids.Web.Data;
using SafeLearnKids.Web.Models;
using SafeLearnKids.Web.Services;

namespace SafeLearnKids.Web.Areas.Parent.Controllers;

// US-47: the weekly report of one child. Finished weeks are kept in dbo.WeeklyReports; the current week is shown live.
[Area("Parent")]
[Authorize(Roles = "Parent")]
public class WeeklyController(KidContext kids, IServiceProvider services) : Controller
{
    private const int WeeksBack = 12;

    private SafeLearnDbContext? Db => services.GetService<SafeLearnDbContext>();

    [HttpGet("weekly")]
    public async Task<IActionResult> Index(int? child, DateOnly? week, CancellationToken cancellationToken)
    {
        if (Db is not { } db) return View("SqlOnly");
        var choices = await kids.ChoicesAsync(cancellationToken);
        if (choices.Count == 0) return Redirect("/onboarding");
        var chosen = choices.FirstOrDefault(item => item.Id == child) ?? choices[0];
        var current = ProgressService.WeekStart(StudyTimeService.Today);
        var weeks = Enumerable.Range(0, WeeksBack).Select(offset => current.AddDays(-7 * offset)).ToList();
        // Default to the last finished week; any Monday within the range is accepted.
        var start = week is { } asked && weeks.Contains(ProgressService.WeekStart(asked)) ? ProgressService.WeekStart(asked) : current.AddDays(-7);
        var data = await WeeklyReportService.ComputeAsync(db, chosen.Id, chosen.Name, start, cancellationToken);
        DateTime generated = DateTime.UtcNow;
        DateTime? emailed = null;
        if (start < current)
        {
            // Opening a finished week stores (or refreshes) its report.
            try
            {
                var (report, _) = await WeeklyReportService.SaveAsync(db, chosen.Id, start, data, cancellationToken);
                generated = report.GeneratedAt;
                emailed = report.EmailedAt;
            }
            catch (DbUpdateException)
            {
                db.ChangeTracker.Clear();
            }
        }
        var previous = await db.DailyUsages.AsNoTracking()
            .Where(item => item.ChildProfileId == chosen.Id && item.UsageDate >= start.AddDays(-7) && item.UsageDate < start)
            .SumAsync(item => (int?)item.Seconds, cancellationToken);
        var goal = await ProgressService.WeekAsync(db, chosen.Id, start, cancellationToken);
        return View(new WeeklyViewModel
        {
            Child = chosen,
            Children = choices,
            WeekStart = start,
            Weeks = weeks,
            CurrentWeek = start == current,
            TotalMinutes = data.Minutes,
            LessonsCompleted = data.Lessons,
            QuizAttempts = data.Attempts,
            AvgQuizPercent = data.AvgQuiz,
            StrongSubject = data.Strong,
            NeedsAttentionSubject = data.Attention,
            Summary = data.Summary,
            Days = data.Days,
            Subjects = data.Subjects,
            Badges = data.Badges,
            Goal = goal.HasGoal ? goal : null,
            PreviousMinutes = previous is null ? null : previous / 60,
            GeneratedAt = generated,
            EmailedAt = emailed
        });
    }

    [HttpGet("weekly/export")]
    public async Task<IActionResult> Export(int child, CancellationToken cancellationToken)
    {
        if (Db is not { } db) return View("SqlOnly");
        var chosen = (await kids.ChoicesAsync(cancellationToken)).FirstOrDefault(item => item.Id == child);
        if (chosen is null) return NotFound();
        var rows = await db.WeeklyReports.AsNoTracking().Where(item => item.ChildProfileId == chosen.Id).OrderByDescending(item => item.WeekStart).ToListAsync(cancellationToken);
        var csv = new StringBuilder("Tuần bắt đầu,Số phút,Bài hoàn thành,Lượt bài tập,Điểm trung bình (%),Môn tốt nhất,Môn cần chú ý,Tóm tắt\r\n");
        foreach (var row in rows)
            csv.AppendJoin(',', new[]
            {
                row.WeekStart.ToString("yyyy-MM-dd"), row.TotalMinutes.ToString(), row.LessonsCompleted.ToString(), row.QuizAttempts.ToString(),
                row.AvgQuizPercent?.ToString("0.##") ?? "", row.StrongSubject ?? "", row.NeedsAttentionSubject ?? "", row.Summary ?? ""
            }.Select(Cell)).Append("\r\n");
        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv.ToString())).ToArray();
        return File(bytes, "text/csv; charset=utf-8", "bao-cao-tuan.csv");
    }

    // Subject names come from admins; spreadsheets must not run them as formulas.
    private static string Cell(string value)
    {
        if (value.Length > 0 && "=+-@\t\r".Contains(value[0])) value = "'" + value;
        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }
}
