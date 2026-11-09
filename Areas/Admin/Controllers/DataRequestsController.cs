using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using SafeLearnKids.Web.Data;
using SafeLearnKids.Web.Models;
using SafeLearnKids.Web.Services;

namespace SafeLearnKids.Web.Areas.Admin.Controllers;

// US-52: an admin carries out (or declines, with a reason) a parent's request to delete a child's data.
// Deleting the profile removes the learning data through the database's cascades.
[Area("Admin")]
[Authorize(Roles = "Admin")]
public class DataRequestsController(UserManager<ApplicationUser> users, AccountActivityLog activity, IServiceProvider services) : Controller
{
    [HttpGet("admin-data-requests")]
    public async Task<IActionResult> Index(string status = "open", CancellationToken cancellationToken = default)
    {
        if (services.GetService<SafeLearnDbContext>() is not { } db) return View("SqlOnly");
        var open = status != "done";
        var rows = db.DataRequests.AsNoTracking().Where(item => item.RequestType == "Delete");
        rows = open ? rows.Where(item => item.Status == DataRequestStatus.Pending || item.Status == DataRequestStatus.Processing)
            : rows.Where(item => item.Status == DataRequestStatus.Done || item.Status == DataRequestStatus.Rejected);
        return View(new AdminDataRequestsViewModel
        {
            Status = open ? "open" : "done",
            Open = await db.DataRequests.CountAsync(item => item.RequestType == "Delete" && (item.Status == DataRequestStatus.Pending || item.Status == DataRequestStatus.Processing), cancellationToken),
            Rows = await rows.OrderBy(item => item.RequestedAt).Take(100)
                .Select(item => new DataRequestRow(item.Id, item.RequestType, item.Status, item.ChildName, item.ChildProfileId, item.DecisionNote, item.RequestedAt, item.CompletedAt,
                    item.Parent.Email, item.ProcessedBy != null ? item.ProcessedBy.Email : null))
                .ToListAsync(cancellationToken)
        });
    }

    [HttpPost("admin-data-requests/{id:int}")]
    [EnableRateLimiting("account")]
    public async Task<IActionResult> Decide(int id, [FromForm] string? decision, [FromForm] string? note, CancellationToken cancellationToken)
    {
        if (services.GetService<SafeLearnDbContext>() is not { } db) return View("SqlOnly");
        var request = await db.DataRequests.FirstOrDefaultAsync(item => item.Id == id && item.RequestType == "Delete", cancellationToken);
        if (request is null) return NotFound();
        if (request.Status is not (DataRequestStatus.Pending or DataRequestStatus.Processing))
        {
            TempData["Error"] = "Yêu cầu này đã được xử lý.";
            return RedirectToAction(nameof(Index));
        }
        note = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        if (note is { Length: > 500 } || (decision == "reject" && note is null) || decision is not ("approve" or "reject"))
        {
            TempData["Error"] = decision == "reject" ? "Ghi lý do từ chối (tối đa 500 ký tự) để gửi cho phụ huynh." : "Chọn xóa hoặc từ chối.";
            return RedirectToAction(nameof(Index));
        }
        var adminId = users.GetUserId(User)!;
        var now = DateTime.UtcNow;
        if (decision == "approve")
        {
            var child = request.ChildProfileId is { } childId
                ? await db.ChildProfiles.FirstOrDefaultAsync(item => item.Id == childId && item.ParentId == request.ParentId, cancellationToken)
                : null;
            if (child is not null)
            {
                db.UserConsents.RemoveRange(db.UserConsents.Where(item => item.ChildProfileId == child.Id && item.UserId == request.ParentId));
                db.ChildProfiles.Remove(child);
            }
            request.Status = DataRequestStatus.Done;
            request.DecisionNote = note ?? "Đã xóa hồ sơ và toàn bộ dữ liệu học của bé.";
        }
        else
        {
            request.Status = DataRequestStatus.Rejected;
            request.DecisionNote = note;
        }
        request.ProcessedById = adminId;
        request.CompletedAt = now < request.RequestedAt ? request.RequestedAt : now;
        NotificationWriter.Add(db, request.ParentId, "System",
            decision == "approve" ? $"Đã xóa dữ liệu của {request.ChildName}" : $"Yêu cầu xóa dữ liệu của {request.ChildName} bị từ chối",
            request.DecisionNote, "/privacy");
        await db.SaveChangesAsync(cancellationToken);
        await activity.ContentActionAsync(adminId, "DataRequest", id, decision == "approve" ? "ChildDataDeleted" : "ChildDataDeleteRejected",
            $"{request.ChildName} · {request.DecisionNote}");
        TempData["Info"] = decision == "approve" ? $"Đã xóa dữ liệu của {request.ChildName}." : "Đã từ chối yêu cầu và báo cho phụ huynh.";
        return RedirectToAction(nameof(Index));
    }
}
