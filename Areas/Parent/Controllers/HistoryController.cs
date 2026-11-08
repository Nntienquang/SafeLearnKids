using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SafeLearnKids.Web.Data;
using SafeLearnKids.Web.Models;
using SafeLearnKids.Web.Services;

namespace SafeLearnKids.Web.Areas.Parent.Controllers;

// US-29: what a child studied, for how long, what they searched and what they reported.
[Area("Parent")]
[Authorize(Roles = "Parent")]
public class HistoryController(KidContext kids, IServiceProvider services) : Controller
{
    private const int Days = 14;

    private SafeLearnDbContext? Db => services.GetService<SafeLearnDbContext>();

    [HttpGet("history")]
    public async Task<IActionResult> Index(int? child, CancellationToken cancellationToken)
    {
        if (Db is not { } db) return View("SqlOnly");
        var choices = await kids.ChoicesAsync(cancellationToken);
        if (choices.Count == 0) return Redirect("/onboarding");
        var chosen = choices.FirstOrDefault(item => item.Id == child) ?? choices[0];
        var since = StudyTimeService.Today.AddDays(-(Days - 1));
        var usage = await db.DailyUsages.AsNoTracking().Where(item => item.ChildProfileId == chosen.Id && item.UsageDate >= since)
            .ToDictionaryAsync(item => item.UsageDate, item => item.Seconds, cancellationToken);
        return View(new HistoryViewModel
        {
            Child = chosen,
            Children = choices,
            Lessons = await LessonsAsync(db, chosen.Id, 100, cancellationToken),
            Days = Enumerable.Range(0, Days).Select(offset => since.AddDays(offset)).Reverse()
                .Select(day => (day, usage.TryGetValue(day, out var seconds) ? seconds / 60 : 0)).ToList(),
            Searches = await db.SearchLogs.AsNoTracking().Where(item => item.ChildProfileId == chosen.Id).OrderByDescending(item => item.CreatedAt).Take(30)
                .Select(item => new HistorySearchRow(item.Query, item.ResultCount, item.WasBlocked, item.CreatedAt)).ToListAsync(cancellationToken),
            Reports = await db.ContentReports.AsNoTracking().Where(item => item.ChildProfileId == chosen.Id).OrderByDescending(item => item.CreatedAt).Take(20)
                .Select(item => new ReportRow(item.Id, item.Lesson != null ? item.Lesson.Title : item.SourceVideo != null ? item.SourceVideo.Title : "Nội dung đã xóa",
                    item.Reason, item.Comment, item.Status, item.Resolution, chosen.Name, item.CreatedAt)).ToListAsync(cancellationToken)
        });
    }

    [HttpGet("history/export")]
    public async Task<IActionResult> Export(int child, CancellationToken cancellationToken)
    {
        if (Db is not { } db) return View("SqlOnly");
        var chosen = (await kids.ChoicesAsync(cancellationToken)).FirstOrDefault(item => item.Id == child);
        if (chosen is null) return NotFound();
        var csv = new StringBuilder("Bài học,Môn,Trạng thái,Tiến độ (%),Thời gian xem (phút),Bắt đầu,Lần gần nhất\r\n");
        foreach (var row in await LessonsAsync(db, chosen.Id, 1000, cancellationToken))
            csv.AppendJoin(',', new[]
            {
                row.Title, row.Subject, row.Status == "Completed" ? "Hoàn thành" : "Đang học", row.Percent.ToString(), row.WatchMinutes.ToString(),
                row.StartedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm"), row.UpdatedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm")
            }.Select(Cell)).Append("\r\n");
        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv.ToString())).ToArray();
        return File(bytes, "text/csv; charset=utf-8", "lich-su-hoc.csv");
    }

    private static async Task<IReadOnlyList<HistoryLessonRow>> LessonsAsync(SafeLearnDbContext db, int childId, int take, CancellationToken cancellationToken) =>
        await db.LessonActivities.AsNoTracking().Where(item => item.ChildProfileId == childId).OrderByDescending(item => item.UpdatedAt).Take(take)
            .Select(item => new HistoryLessonRow(item.Lesson.Title, item.Lesson.Subject.Name, item.Status, item.ProgressPercent, item.WatchSeconds / 60, item.StartedAt, item.UpdatedAt))
            .ToListAsync(cancellationToken);

    // Titles come from admins; spreadsheets must not run them as formulas.
    private static string Cell(string value)
    {
        if (value.Length > 0 && "=+-@\t\r".Contains(value[0])) value = "'" + value;
        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }
}
