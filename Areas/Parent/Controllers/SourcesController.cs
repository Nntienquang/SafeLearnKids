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

// A family can only narrow what the admins approved (US-21); new sources go to the admins as suggestions.
[Area("Parent")]
[Authorize(Roles = "Parent")]
[EnableRateLimiting("account")]
public class SourcesController(AccountActivityLog activity, IServiceProvider services) : Controller
{
    private const int MaxPendingSuggestions = 5;

    private SafeLearnDbContext? Db => services.GetService<SafeLearnDbContext>();
    private string ParentId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    [HttpGet("sources")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        if (Db is not { } db) return View("SqlOnly");
        return View(await ModelAsync(db, new SourceFormViewModel(), cancellationToken));
    }

    [HttpPost("sources/{id:int}/block")]
    public Task<IActionResult> Block(int id, CancellationToken cancellationToken) => ChooseAsync(id, "Blocked", cancellationToken);

    [HttpPost("sources/{id:int}/allow")]
    public Task<IActionResult> Allow(int id, CancellationToken cancellationToken) => ChooseAsync(id, "Allowed", cancellationToken);

    [HttpPost("sources/suggest")]
    public async Task<IActionResult> Suggest([Bind(Prefix = "Suggest")] SourceFormViewModel model, CancellationToken cancellationToken)
    {
        if (Db is not { } db) return View("SqlOnly");
        var parsed = SourceUrl.Parse(model.Url, out var problem);
        if (parsed is null) ModelState.AddModelError("Suggest.Url", problem!);
        if (ModelState.IsValid && parsed is { } found)
        {
            var existing = await db.ContentSources.AsNoTracking().FirstOrDefaultAsync(source => source.Platform == found.Platform && source.ExternalId == found.ExternalId, cancellationToken);
            if (existing is not null)
                ModelState.AddModelError("Suggest.Url", existing.Status switch
                {
                    SourceStatus.Approved => "Nguồn này đã được duyệt và có trong danh sách bên dưới.",
                    SourceStatus.Suggested => "Nguồn này đã được đề xuất và đang chờ quản trị viên duyệt.",
                    _ => "Nguồn này đã được quản trị viên xem xét và chưa phù hợp với trẻ."
                });
            else if (await db.ContentSources.CountAsync(source => source.SuggestedById == ParentId && source.Status == SourceStatus.Suggested, cancellationToken) >= MaxPendingSuggestions)
                ModelState.AddModelError("Suggest.Url", $"Bạn đang có {MaxPendingSuggestions} đề xuất chờ duyệt. Hãy chờ quản trị viên xem xét trước khi đề xuất thêm.");
        }
        if (!ModelState.IsValid) return View("Index", await ModelAsync(db, model, cancellationToken));

        var (platform, externalId, url) = parsed!.Value;
        var created = new ContentSource
        {
            Platform = platform,
            ExternalId = externalId,
            Url = url,
            Name = model.Name.Trim(),
            Description = string.IsNullOrWhiteSpace(model.Description) ? null : model.Description.Trim(),
            Status = SourceStatus.Suggested,
            SuggestedById = ParentId,
            CreatedAt = DateTime.UtcNow
        };
        db.ContentSources.Add(created);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Another family suggested the same source a moment earlier.
            db.ChangeTracker.Clear();
            ModelState.AddModelError("Suggest.Url", "Nguồn này đã được đề xuất và đang chờ quản trị viên duyệt.");
            return View("Index", await ModelAsync(db, model, cancellationToken));
        }
        await activity.ContentActionAsync(ParentId, "ContentSource", created.Id, "SuggestSource", created.Name);
        TempData["Info"] = "Đã gửi đề xuất. Nguồn chỉ đến với bé sau khi quản trị viên duyệt.";
        return RedirectToAction(nameof(Index));
    }

    private async Task<IActionResult> ChooseAsync(int id, string status, CancellationToken cancellationToken)
    {
        if (Db is not { } db) return View("SqlOnly");
        if (!await db.ContentSources.AnyAsync(source => source.Id == id && source.Status == SourceStatus.Approved, cancellationToken)) return NotFound();
        var choice = await db.ParentSources.FirstOrDefaultAsync(link => link.ParentId == ParentId && link.SourceId == id, cancellationToken);
        if (choice is null) db.ParentSources.Add(new ParentSource { ParentId = ParentId, SourceId = id, Status = status, AddedAt = DateTime.UtcNow });
        else
        {
            choice.Status = status;
            choice.AddedAt = DateTime.UtcNow;
        }
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            db.ChangeTracker.Clear();
            TempData["Error"] = "Chưa thể lưu lựa chọn. Vui lòng thử lại.";
            return RedirectToAction(nameof(Index));
        }
        TempData["Info"] = status == "Blocked" ? "Đã chặn nguồn cho gia đình. Video của nguồn này không còn phát cho bé." : "Đã cho phép lại nguồn cho gia đình.";
        return RedirectToAction(nameof(Index));
    }

    private async Task<FamilySourcesViewModel> ModelAsync(SafeLearnDbContext db, SourceFormViewModel suggest, CancellationToken cancellationToken)
    {
        var parentId = ParentId;
        return new FamilySourcesViewModel
        {
            Suggest = suggest,
            Sources = await db.ContentSources.AsNoTracking().Where(source => source.Status == SourceStatus.Approved).OrderBy(source => source.Name)
                .Select(source => new FamilySourceRow(source.Id, source.Name, source.Platform, source.Url,
                    source.SourceVideoSources.Count(link => link.SourceVideo.Status == VideoStatus.Approved),
                    source.ParentSources.Any(link => link.ParentId == parentId && link.Status == "Blocked")))
                .ToListAsync(cancellationToken),
            Suggestions = await db.ContentSources.AsNoTracking().Where(source => source.SuggestedById == parentId).OrderByDescending(source => source.CreatedAt).Take(20)
                .Select(source => new SuggestionRow(source.Name, source.Platform, source.Url, source.Status, source.ReviewNote, source.CreatedAt))
                .ToListAsync(cancellationToken)
        };
    }
}
