using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using SafeLearnKids.Web.Data;
using SafeLearnKids.Web.Models;
using SafeLearnKids.Web.Models.Entities;
using SafeLearnKids.Web.Services;

namespace SafeLearnKids.Web.Areas.Parent.Controllers;

// Phase B: parents tell the admins what their children need; other families add "we need this too".
[Area("Parent")]
[Authorize(Roles = "Parent")]
public class LessonRequestsController(AccountActivityLog activity, IServiceProvider services) : Controller
{
    private const int OthersShown = 50;

    private SafeLearnDbContext? Db => services.GetService<SafeLearnDbContext>();
    private string ParentId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    [HttpGet("lesson-requests")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        if (Db is not { } db) return View("SqlOnly");
        return View(await ModelAsync(db, new LessonRequestForm(), cancellationToken));
    }

    [HttpPost("lesson-requests")]
    [EnableRateLimiting("account")]
    public async Task<IActionResult> Create(LessonRequestForm form, CancellationToken cancellationToken)
    {
        if (Db is not { } db) return View("SqlOnly");
        form.Topic = form.Topic?.Trim() ?? "";
        form.Note = string.IsNullOrWhiteSpace(form.Note) ? null : form.Note.Trim();
        var gradeId = await db.GradeLevels.Where(item => item.Level == form.Grade).Select(item => (int?)item.Id).FirstOrDefaultAsync(cancellationToken);
        if (gradeId is null || !await db.Subjects.AnyAsync(item => item.Id == form.SubjectId && item.IsActive, cancellationToken))
            ModelState.AddModelError(nameof(form.SubjectId), "Chọn môn và lớp.");
        if (ModelState.IsValid && await db.LessonRequests.CountAsync(item => item.ParentId == ParentId && item.Status == RequestState.Open, cancellationToken) >= FamilyRules.MaxOpenRequests)
            ModelState.AddModelError("", $"Mỗi gia đình có tối đa {FamilyRules.MaxOpenRequests} đề xuất đang chờ.");
        if (ModelState.IsValid && await db.LessonRequests.AnyAsync(item => item.ParentId == ParentId && item.Status == RequestState.Open
                && item.SubjectId == form.SubjectId && item.GradeLevelId == gradeId && item.Topic == form.Topic, cancellationToken))
            ModelState.AddModelError(nameof(form.Topic), "Bạn đã gửi đề xuất này rồi.");
        if (ModelState.IsValid && await FamilyLessonService.BlockedWordAsync(db, ParentId, [form.Topic, form.Note], cancellationToken) is { } word)
            ModelState.AddModelError("", FamilyLessonService.BlockedMessage(word));
        if (!ModelState.IsValid)
        {
            Response.StatusCode = StatusCodes.Status400BadRequest;
            return View("Index", await ModelAsync(db, form, cancellationToken));
        }
        var request = new LessonRequest
        {
            ParentId = ParentId, SubjectId = form.SubjectId, GradeLevelId = gradeId!.Value, Topic = form.Topic, Note = form.Note,
            Status = RequestState.Open, CreatedAt = DateTime.UtcNow
        };
        db.LessonRequests.Add(request);
        await db.SaveChangesAsync(cancellationToken);
        await activity.ContentActionAsync(ParentId, "LessonRequest", request.Id, "LessonRequestCreate", request.Topic);
        TempData["Info"] = "Đã gửi đề xuất. Quản trị viên sẽ xem và báo lại cho bạn.";
        return RedirectToAction(nameof(Index));
    }

    // "We need this too": one vote per family; voting again takes it back. A family cannot vote for its own request.
    [HttpPost("lesson-requests/{id:int}/vote")]
    [EnableRateLimiting("account")]
    public async Task<IActionResult> Vote(int id, CancellationToken cancellationToken)
    {
        if (Db is not { } db) return View("SqlOnly");
        var request = await db.LessonRequests.Where(item => item.Id == id && (item.Status == RequestState.Open || item.Status == RequestState.Planned))
            .Select(item => new { item.ParentId }).FirstOrDefaultAsync(cancellationToken);
        if (request is null || request.ParentId == ParentId) return NotFound();
        var vote = await db.LessonRequestVotes.FirstOrDefaultAsync(item => item.RequestId == id && item.ParentId == ParentId, cancellationToken);
        if (vote is null) db.LessonRequestVotes.Add(new LessonRequestVote { RequestId = id, ParentId = ParentId, CreatedAt = DateTime.UtcNow });
        else db.LessonRequestVotes.Remove(vote);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // A double tap voted a moment earlier.
            db.ChangeTracker.Clear();
        }
        return Redirect(Url.Action(nameof(Index)) + "#others");
    }

    [HttpPost("lesson-requests/{id:int}/withdraw")]
    [EnableRateLimiting("account")]
    public async Task<IActionResult> Withdraw(int id, CancellationToken cancellationToken)
    {
        if (Db is not { } db) return View("SqlOnly");
        var request = await db.LessonRequests.FirstOrDefaultAsync(item => item.Id == id && item.ParentId == ParentId, cancellationToken);
        if (request is null) return NotFound();
        if (request.Status != RequestState.Open)
        {
            TempData["Error"] = "Đề xuất đã được quản trị viên xử lý nên không rút được.";
            return RedirectToAction(nameof(Index));
        }
        db.LessonRequests.Remove(request);
        await db.SaveChangesAsync(cancellationToken);
        TempData["Info"] = "Đã rút đề xuất.";
        return RedirectToAction(nameof(Index));
    }

    private async Task<LessonRequestsViewModel> ModelAsync(SafeLearnDbContext db, LessonRequestForm form, CancellationToken cancellationToken)
    {
        var parentId = ParentId;
        // Filter and sort on the entity, then project: EF cannot translate filters on the record.
        IQueryable<LessonRequestRow> Rows(IQueryable<LessonRequest> query) => query.Select(item => new LessonRequestRow(item.Id, item.Subject.Name, item.GradeLevel.Level,
            item.Topic, item.Note, item.Status, item.Reply, item.LessonId, item.Votes.Count, item.ParentId == parentId, item.Votes.Any(vote => vote.ParentId == parentId),
            item.CreatedAt, null));
        var requests = db.LessonRequests.AsNoTracking();
        var subjects = await db.Subjects.AsNoTracking().Where(item => item.IsActive).OrderBy(item => item.SortOrder).ThenBy(item => item.Name)
            .Select(item => new SubjectRowOption(item.Id, item.Name)).ToListAsync(cancellationToken);
        if (form.SubjectId == 0) form.SubjectId = subjects.FirstOrDefault()?.Id ?? 0;
        return new LessonRequestsViewModel
        {
            Form = form,
            Subjects = subjects,
            Mine = await Rows(requests.Where(item => item.ParentId == parentId).OrderByDescending(item => item.CreatedAt)).ToListAsync(cancellationToken),
            // Other families' open requests, most wanted first; who asked is not shown.
            Others = await Rows(requests.Where(item => item.ParentId != parentId && (item.Status == RequestState.Open || item.Status == RequestState.Planned))
                .OrderByDescending(item => item.Votes.Count).ThenByDescending(item => item.CreatedAt).Take(OthersShown)).ToListAsync(cancellationToken)
        };
    }
}
