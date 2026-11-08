using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SafeLearnKids.Web.Data;
using SafeLearnKids.Web.Models;
using SafeLearnKids.Web.Models.Entities;

namespace SafeLearnKids.Web.Areas.Admin.Controllers;

// US-51 ("type" filters the action name: a parameter called "action" would bind the MVC route value instead).
// Who did what (dbo.AuditLogs) and every sign-in attempt (dbo.LoginHistories), filterable and exportable.
[Area("Admin")]
[Authorize(Roles = "Admin")]
public class AuditController(IServiceProvider services) : Controller
{
    private const int PageSize = 50;
    private const int ExportLimit = 5000;

    [HttpGet("admin-audit")]
    public async Task<IActionResult> Index(string tab = "actions", string? q = null, string? type = null, string? result = null,
        DateOnly? from = null, DateOnly? to = null, int page = 1, CancellationToken cancellationToken = default)
    {
        if (services.GetService<SafeLearnDbContext>() is not { } db) return View("SqlOnly");
        tab = tab == "logins" ? "logins" : "actions";
        var term = string.IsNullOrWhiteSpace(q) ? null : q.Trim();
        int matching;
        IReadOnlyList<AuditRow> audits = [];
        IReadOnlyList<LoginHistoryRow> logins = [];
        if (tab == "actions")
        {
            var rows = Audits(db, term, type, from, to);
            matching = await rows.CountAsync(cancellationToken);
            page = Math.Clamp(page, 1, Math.Max(1, (int)Math.Ceiling(matching / (double)PageSize)));
            audits = await Project(db, rows).Skip((page - 1) * PageSize).Take(PageSize).ToListAsync(cancellationToken);
        }
        else
        {
            var rows = Logins(db, term, result, from, to);
            matching = await rows.CountAsync(cancellationToken);
            page = Math.Clamp(page, 1, Math.Max(1, (int)Math.Ceiling(matching / (double)PageSize)));
            logins = await Project(db, rows).Skip((page - 1) * PageSize).Take(PageSize).ToListAsync(cancellationToken);
        }
        return View(new AuditViewModel
        {
            Tab = tab, Q = term, Action = type, Result = result, From = from, To = to, Page = page, Matching = matching,
            TotalPages = Math.Max(1, (int)Math.Ceiling(matching / (double)PageSize)),
            Actions = await db.AuditLogs.AsNoTracking().Select(item => item.Action).Distinct().OrderBy(item => item).ToListAsync(cancellationToken),
            Audits = audits,
            Logins = logins
        });
    }

    [HttpGet("admin-audit/export")]
    public async Task<IActionResult> Export(string tab = "actions", string? q = null, string? type = null, string? result = null,
        DateOnly? from = null, DateOnly? to = null, CancellationToken cancellationToken = default)
    {
        if (services.GetService<SafeLearnDbContext>() is not { } db) return View("SqlOnly");
        var term = string.IsNullOrWhiteSpace(q) ? null : q.Trim();
        var csv = new StringBuilder();
        if (tab == "logins")
        {
            csv.Append("Thời gian,Email thử,Tài khoản,Kết quả,Lý do,IP,Trình duyệt\r\n");
            foreach (var row in await Project(db, Logins(db, term, result, from, to)).Take(ExportLimit).ToListAsync(cancellationToken))
                csv.AppendJoin(',', new[] { row.At.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"), row.Tried, row.User ?? "", row.Succeeded ? "Thành công" : "Thất bại",
                    row.Reason ?? "", row.Ip ?? "", row.Agent ?? "" }.Select(Cell)).Append("\r\n");
        }
        else
        {
            csv.Append("Thời gian,Người thực hiện,Thao tác,Đối tượng,Mã,Chi tiết,IP\r\n");
            foreach (var row in await Project(db, Audits(db, term, type, from, to)).Take(ExportLimit).ToListAsync(cancellationToken))
                csv.AppendJoin(',', new[] { row.At.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"), row.Actor ?? "hệ thống", row.Action, row.EntityType, row.EntityId ?? "",
                    row.Detail ?? "", row.Ip ?? "" }.Select(Cell)).Append("\r\n");
        }
        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv.ToString())).ToArray();
        return File(bytes, "text/csv; charset=utf-8", tab == "logins" ? "nhat-ky-dang-nhap.csv" : "nhat-ky-thao-tac.csv");
    }

    private static IQueryable<AuditLog> Audits(SafeLearnDbContext db, string? term, string? action, DateOnly? from, DateOnly? to)
    {
        var rows = db.AuditLogs.AsNoTracking().AsQueryable();
        if (!string.IsNullOrEmpty(action)) rows = rows.Where(item => item.Action == action);
        if (term is not null)
        {
            var actors = db.Users.Where(user => user.Email!.Contains(term) || (user.FullName != null && user.FullName.Contains(term))).Select(user => user.Id);
            rows = rows.Where(item => (item.ActorId != null && actors.Contains(item.ActorId)) || (item.Detail != null && item.Detail.Contains(term))
                || item.EntityType.Contains(term) || item.EntityId == term);
        }
        if (from is { } start) rows = rows.Where(item => item.CreatedAt >= start.ToDateTime(TimeOnly.MinValue).ToUniversalTime());
        if (to is { } end) rows = rows.Where(item => item.CreatedAt < end.AddDays(1).ToDateTime(TimeOnly.MinValue).ToUniversalTime());
        return rows;
    }

    private static IQueryable<LoginHistory> Logins(SafeLearnDbContext db, string? term, string? result, DateOnly? from, DateOnly? to)
    {
        var rows = db.LoginHistories.AsNoTracking().AsQueryable();
        if (result == "failed") rows = rows.Where(item => !item.Succeeded);
        else if (result == "success") rows = rows.Where(item => item.Succeeded);
        if (term is not null) rows = rows.Where(item => item.UserNameTried.Contains(term) || (item.IpAddress != null && item.IpAddress.Contains(term)));
        if (from is { } start) rows = rows.Where(item => item.CreatedAt >= start.ToDateTime(TimeOnly.MinValue).ToUniversalTime());
        if (to is { } end) rows = rows.Where(item => item.CreatedAt < end.AddDays(1).ToDateTime(TimeOnly.MinValue).ToUniversalTime());
        return rows;
    }

    private static IQueryable<AuditRow> Project(SafeLearnDbContext db, IQueryable<AuditLog> rows) =>
        rows.OrderByDescending(item => item.CreatedAt).ThenByDescending(item => item.Id).Select(item => new AuditRow(item.Id,
            db.Users.Where(user => user.Id == item.ActorId).Select(user => user.Email).FirstOrDefault(),
            item.Action, item.EntityType, item.EntityId, item.Detail, item.IpAddress, item.CreatedAt));

    private static IQueryable<LoginHistoryRow> Project(SafeLearnDbContext db, IQueryable<LoginHistory> rows) =>
        rows.OrderByDescending(item => item.CreatedAt).ThenByDescending(item => item.Id).Select(item => new LoginHistoryRow(item.Id,
            db.Users.Where(user => user.Id == item.UserId).Select(user => user.Email).FirstOrDefault(),
            item.UserNameTried, item.Succeeded, item.FailReason, item.IpAddress, item.UserAgent, item.CreatedAt));

    // Details may contain user text; spreadsheets must not run it as formulas.
    private static string Cell(string value)
    {
        if (value.Length > 0 && "=+-@\t\r".Contains(value[0])) value = "'" + value;
        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }
}
