using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SafeLearnKids.Web.Data;
using SafeLearnKids.Web.Models;
using SafeLearnKids.Web.Models.Entities;
using SafeLearnKids.Web.Services;

namespace SafeLearnKids.Web.Areas.Admin.Controllers;

// Content sources and their videos (US-17 to US-20, US-22). Admins decide; the rule engine only suggests.
[Area("Admin")]
[Authorize(Roles = "Admin")]
public class CurationController(
    UserManager<ApplicationUser> users,
    AccountActivityLog activity,
    SourceSyncService sync,
    IServiceProvider services) : Controller
{
    private const int PageSize = 24;
    private const string SaveFailed = "Chưa thể lưu thay đổi. Vui lòng thử lại.";

    private SafeLearnDbContext? Db => services.GetService<SafeLearnDbContext>();
    private string ActorId => users.GetUserId(User)!;

    [HttpGet("admin-curate")]
    public async Task<IActionResult> Index(string? status, string? q, CancellationToken cancellationToken)
    {
        if (Db is not { } db) return View("SqlOnly");
        var query = db.ContentSources.AsNoTracking();
        if (!string.IsNullOrEmpty(status)) query = query.Where(source => source.Status == status);
        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            query = query.Where(source => source.Name.Contains(term) || source.Url.Contains(term));
        }
        var counts = await db.ContentSources.GroupBy(source => source.Status).Select(group => new { group.Key, Count = group.Count() })
            .ToDictionaryAsync(item => item.Key, item => item.Count, cancellationToken);
        return View(new SourcesViewModel
        {
            Status = status,
            Q = q,
            Counts = counts,
            // Suggestions waiting for a decision come first.
            Rows = await Rows(query.OrderBy(source => source.Status == SourceStatus.Suggested ? 0 : 1).ThenBy(source => source.Name)).ToListAsync(cancellationToken)
        });
    }

    [HttpGet("admin-sources/new")]
    public IActionResult Create() => Db is null ? View("SqlOnly") : View("SourceForm", new SourceFormViewModel());

    [HttpPost("admin-sources/new")]
    public async Task<IActionResult> Create(SourceFormViewModel model, CancellationToken cancellationToken)
    {
        if (Db is not { } db) return View("SqlOnly");
        model.Id = null;
        var parsed = SourceUrl.Parse(model.Url, out var problem);
        if (parsed is null) ModelState.AddModelError(nameof(model.Url), problem!);
        if (parsed is { } found && await db.ContentSources.AnyAsync(source => source.Platform == found.Platform && source.ExternalId == found.ExternalId, cancellationToken))
            ModelState.AddModelError(nameof(model.Url), "Nguồn này đã có trong danh sách.");
        if (!ModelState.IsValid) return View("SourceForm", model);

        var (platform, externalId, url) = parsed!.Value;
        var source = new ContentSource
        {
            Platform = platform,
            ExternalId = externalId,
            Url = url,
            Name = model.Name.Trim(),
            Description = string.IsNullOrWhiteSpace(model.Description) ? null : model.Description.Trim(),
            Status = SourceStatus.Suggested,
            SuggestedById = ActorId,
            CreatedAt = DateTime.UtcNow
        };
        db.ContentSources.Add(source);
        if (!await SaveAsync(db, cancellationToken))
        {
            ModelState.AddModelError(nameof(model.Url), "Nguồn này đã có trong danh sách.");
            return View("SourceForm", model);
        }
        await activity.ContentActionAsync(ActorId, "ContentSource", source.Id, "CreateSource", source.Name);
        TempData["Info"] = "Đã thêm nguồn ở trạng thái chờ duyệt. Hãy đồng bộ và xem video trước khi duyệt.";
        return RedirectToAction(nameof(Details), new { id = source.Id });
    }

    [HttpGet("admin-sources/{id:int}")]
    public async Task<IActionResult> Details(int id, CancellationToken cancellationToken)
    {
        if (Db is not { } db) return View("SqlOnly");
        var row = await Rows(db.ContentSources.AsNoTracking().Where(source => source.Id == id)).FirstOrDefaultAsync(cancellationToken);
        if (row is null) return NotFound();
        var source = await db.ContentSources.AsNoTracking().Where(item => item.Id == id).Select(item => new
        {
            item.Description, item.ExternalId, item.ReviewNote, item.ReviewedAt, item.CreatedAt,
            ReviewedBy = item.ReviewedBy == null ? null : item.ReviewedBy.Email,
            Blocking = item.ParentSources.Count(link => link.Status == "Blocked")
        }).FirstAsync(cancellationToken);
        return View("Source", new SourceDetailViewModel
        {
            Source = row,
            Description = source.Description,
            ExternalId = source.ExternalId,
            ReviewNote = source.ReviewNote,
            ReviewedBy = source.ReviewedBy,
            ReviewedAt = source.ReviewedAt,
            CreatedAt = source.CreatedAt,
            FamiliesBlocking = source.Blocking,
            Assessments = await db.CurationSuggestions.AsNoTracking().Where(item => item.SourceId == id).OrderByDescending(item => item.CreatedAt).Take(5)
                .Select(item => new AssessmentRow(item.Id, item.Score, item.SafetyFlags, item.Summary, item.ModelName, item.Status, item.CreatedAt,
                    item.DecidedBy == null ? null : item.DecidedBy.Email)).ToListAsync(cancellationToken),
            Videos = await VideoRows(db.SourceVideos.AsNoTracking().Where(video => video.SourceVideoSources.Any(link => link.SourceId == id))
                .OrderByDescending(video => video.PublishedAt).Take(30)).ToListAsync(cancellationToken)
        });
    }

    [HttpPost("admin-sources/{id:int}/edit")]
    public async Task<IActionResult> Edit(int id, [FromForm] string? name, [FromForm] string? description, CancellationToken cancellationToken)
    {
        if (Db is not { } db) return View("SqlOnly");
        var source = await db.ContentSources.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (source is null) return NotFound();
        name = name?.Trim();
        if (name is null || name.Length is < 2 or > 150) return Refuse(id, "Tên nguồn cần từ 2 đến 150 ký tự.");
        if (description?.Trim() is { Length: > 500 }) return Refuse(id, "Mô tả tối đa 500 ký tự.");
        source.Name = name;
        source.Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        if (!await SaveAsync(db, cancellationToken)) return Refuse(id, SaveFailed);
        await activity.ContentActionAsync(ActorId, "ContentSource", id, "UpdateSource", name);
        TempData["Info"] = "Đã lưu thông tin nguồn.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost("admin-sources/{id:int}/status")]
    public async Task<IActionResult> SetStatus(int id, [FromForm] string? to, [FromForm] string? note, CancellationToken cancellationToken)
    {
        if (Db is not { } db) return View("SqlOnly");
        var source = await db.ContentSources.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (source is null) return NotFound();
        var allowed = source.Status switch
        {
            SourceStatus.Suggested => new[] { SourceStatus.Approved, SourceStatus.Rejected },
            SourceStatus.Approved => [SourceStatus.Suspended],
            SourceStatus.Suspended => [SourceStatus.Approved, SourceStatus.Rejected],
            _ => [SourceStatus.Approved]
        };
        if (to is null || !allowed.Contains(to)) return Refuse(id, "Không thể chuyển nguồn sang trạng thái này.");
        note = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        if (to is SourceStatus.Rejected or SourceStatus.Suspended && note is null) return Refuse(id, "Hãy ghi lý do để phụ huynh và quản trị viên khác hiểu quyết định.");
        if (note is { Length: > 500 }) return Refuse(id, "Lý do tối đa 500 ký tự.");

        source.Status = to;
        source.ReviewNote = note;
        source.ReviewedById = ActorId;
        source.ReviewedAt = DateTime.UtcNow;
        // The admin's decision closes pending rule-engine suggestions for this source.
        foreach (var suggestion in await db.CurationSuggestions.Where(item => item.SourceId == id && item.Status == "Pending").ToListAsync(cancellationToken))
        {
            suggestion.Status = SourceAssessor.Agrees(suggestion.Score, to) ? "Accepted" : "Dismissed";
            suggestion.DecidedById = ActorId;
            suggestion.DecidedAt = source.ReviewedAt;
        }
        if (!await SaveAsync(db, cancellationToken)) return Refuse(id, SaveFailed);
        await activity.ContentActionAsync(ActorId, "ContentSource", id, "SetSourceStatus", to + (note is null ? "" : ": " + note));
        TempData["Info"] = to switch
        {
            SourceStatus.Approved => "Đã duyệt nguồn. Video đã duyệt của nguồn sẽ dùng được trong bài học.",
            SourceStatus.Suspended => "Đã tạm ngưng nguồn. Video chỉ thuộc nguồn này sẽ không còn phát cho bé.",
            _ => "Đã từ chối nguồn."
        };
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost("admin-sources/{id:int}/sync")]
    public async Task<IActionResult> Sync(int id, CancellationToken cancellationToken)
    {
        if (Db is not { } db) return View("SqlOnly");
        var source = await db.ContentSources.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (source is null) return NotFound();
        // Suggested sources may be synced so admins can look at the videos before deciding.
        if (source.Status is SourceStatus.Rejected or SourceStatus.Suspended) return Refuse(id, "Chỉ đồng bộ nguồn đang chờ duyệt hoặc đã duyệt.");
        var result = await sync.SyncAsync(db, source, cancellationToken);
        await activity.ContentActionAsync(ActorId, "ContentSource", id, "SyncSource", result.Error ?? $"{result.Found} video, {result.Added} mới");
        if (result.Error is not null) return Refuse(id, "Đồng bộ chưa thành công: " + result.Error);
        TempData["Info"] = $"Đã đồng bộ {result.Found} video, {result.Added} video mới chờ duyệt" + (result.Blocked > 0 ? $", {result.Blocked} video tự chặn do từ khóa." : ".");
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost("admin-sources/sync-approved")]
    public async Task<IActionResult> SyncApproved(CancellationToken cancellationToken)
    {
        if (Db is not { } db) return View("SqlOnly");
        var sources = await db.ContentSources.Where(source => source.Status == SourceStatus.Approved && source.Platform != "Website").ToListAsync(cancellationToken);
        int added = 0, failed = 0;
        foreach (var source in sources)
        {
            var result = await sync.SyncAsync(db, source, cancellationToken);
            added += result.Added;
            if (result.Error is not null) failed++;
        }
        await activity.ContentActionAsync(ActorId, "ContentSource", 0, "SyncApprovedSources", $"{sources.Count} nguồn, {added} video mới, {failed} lỗi");
        TempData[failed > 0 ? "Error" : "Info"] = $"Đã đồng bộ {sources.Count} nguồn đã duyệt: {added} video mới" + (failed > 0 ? $", {failed} nguồn lỗi (xem từng nguồn)." : ".");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("admin-sources/{id:int}/assess")]
    public async Task<IActionResult> Assess(int id, CancellationToken cancellationToken)
    {
        if (Db is not { } db) return View("SqlOnly");
        var source = await db.ContentSources.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (source is null) return NotFound();
        var suggestion = await SourceAssessor.AssessAsync(db, source, cancellationToken);
        db.CurationSuggestions.Add(suggestion);
        if (!await SaveAsync(db, cancellationToken)) return Refuse(id, SaveFailed);
        await activity.ContentActionAsync(ActorId, "ContentSource", id, "AssessSource", $"{suggestion.Score:0}/100");
        TempData["Info"] = $"Đánh giá tự động: {suggestion.Score:0}/100. Quyết định vẫn thuộc về bạn.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost("admin-sources/{id:int}/delete")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        if (Db is not { } db) return View("SqlOnly");
        var source = await db.ContentSources.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (source is null) return NotFound();
        if (source.Status == SourceStatus.Approved) return Refuse(id, "Không xóa nguồn đang được duyệt. Hãy tạm ngưng nguồn.");
        // Family choices and other sources keep the videos; only the links of this source go.
        await db.ParentSources.Where(link => link.SourceId == id).ExecuteDeleteAsync(cancellationToken);
        db.ContentSources.Remove(source);
        if (!await SaveAsync(db, cancellationToken)) return Refuse(id, SaveFailed);
        await activity.ContentActionAsync(ActorId, "ContentSource", id, "DeleteSource", source.Name);
        TempData["Info"] = "Đã xóa nguồn.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet("admin-videos")]
    public async Task<IActionResult> Videos(string? status, int? source, string? q, int page = 1, CancellationToken cancellationToken = default)
    {
        if (Db is not { } db) return View("SqlOnly");
        status ??= VideoStatus.Pending;
        var query = db.SourceVideos.AsNoTracking();
        if (status != "all") query = query.Where(video => video.Status == status);
        if (source is not null) query = query.Where(video => video.SourceVideoSources.Any(link => link.SourceId == source));
        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            query = query.Where(video => video.Title.Contains(term) || video.VideoId == term);
        }
        var matching = await query.CountAsync(cancellationToken);
        var totalPages = Math.Max(1, (int)Math.Ceiling(matching / (double)PageSize));
        page = Math.Clamp(page, 1, totalPages);
        return View(new VideosViewModel
        {
            Status = status,
            Source = source,
            Q = q,
            Page = page,
            TotalPages = totalPages,
            Matching = matching,
            Counts = await db.SourceVideos.GroupBy(video => video.Status).Select(group => new { group.Key, Count = group.Count() })
                .ToDictionaryAsync(item => item.Key, item => item.Count, cancellationToken),
            SourceOptions = (await db.ContentSources.AsNoTracking().OrderBy(item => item.Name).Select(item => new { item.Id, item.Name }).ToListAsync(cancellationToken))
                .Select(item => (item.Id, item.Name)).ToList(),
            Rows = await VideoRows(query.OrderByDescending(video => video.PublishedAt).ThenByDescending(video => video.Id).Skip((page - 1) * PageSize).Take(PageSize))
                .ToListAsync(cancellationToken)
        });
    }

    [HttpPost("admin-videos/{id:int}/status")]
    public async Task<IActionResult> SetVideoStatus(int id, [FromForm] string? to, [FromForm] string? note, [FromForm] string? back, CancellationToken cancellationToken)
    {
        if (Db is not { } db) return View("SqlOnly");
        var video = await db.SourceVideos.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (video is null) return NotFound();
        var target = Url.IsLocalUrl(back) ? back! : Url.Action(nameof(Videos))!;
        note = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        if (to is not (VideoStatus.Approved or VideoStatus.Blocked or VideoStatus.Pending)) return RefuseVideo(target, "Trạng thái không hợp lệ.");
        if (to == VideoStatus.Blocked && note is null) return RefuseVideo(target, "Hãy ghi lý do chặn video.");
        if (note is { Length: > 500 }) return RefuseVideo(target, "Lý do tối đa 500 ký tự.");
        video.Status = to;
        video.ReviewNote = note;
        video.ReviewedById = ActorId;
        video.ReviewedAt = DateTime.UtcNow;
        if (!await SaveAsync(db, cancellationToken)) return RefuseVideo(target, SaveFailed);
        var inLessons = await db.LessonContents.CountAsync(content => content.SourceVideoId == id && content.Lesson.Status == LessonStatus.Published, cancellationToken);
        await activity.ContentActionAsync(ActorId, "SourceVideo", id, "SetVideoStatus", to + (note is null ? "" : ": " + note));
        TempData["Info"] = to switch
        {
            VideoStatus.Approved => "Đã duyệt video.",
            VideoStatus.Blocked when inLessons > 0 => $"Đã chặn video. Video được gỡ khỏi {inLessons} bài đã xuất bản cho tới khi duyệt lại.",
            VideoStatus.Blocked => "Đã chặn video.",
            _ => "Đã đưa video về chờ duyệt."
        };
        return Redirect(target);
    }

    private static IQueryable<SourceRow> Rows(IQueryable<ContentSource> query) =>
        query.Select(source => new SourceRow(source.Id, source.Name, source.Platform, source.Url, source.Status,
            source.SourceVideoSources.Count, source.SourceVideoSources.Count(link => link.SourceVideo.Status == VideoStatus.Pending),
            source.CurationSuggestions.OrderByDescending(item => item.CreatedAt).Select(item => (decimal?)item.Score).FirstOrDefault(),
            source.LastSyncedAt, source.LastSyncError, source.SuggestedBy == null ? null : source.SuggestedBy.Email));

    private static IQueryable<VideoRow> VideoRows(IQueryable<SourceVideo> query) =>
        query.Select(video => new VideoRow(video.Id, video.VideoId, video.Title, video.Status, video.ThumbnailUrl, video.PublishedAt, video.ReviewNote,
            video.SourceVideoSources.Select(link => link.Source.Name).ToList(),
            video.LessonContents.Count(content => content.Lesson.Status == LessonStatus.Published)));

    private static async Task<bool> SaveAsync(SafeLearnDbContext db, CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException)
        {
            db.ChangeTracker.Clear();
            return false;
        }
    }

    private RedirectToActionResult Refuse(int id, string message)
    {
        TempData["Error"] = message;
        return RedirectToAction(nameof(Details), new { id });
    }

    private RedirectResult RefuseVideo(string target, string message)
    {
        TempData["Error"] = message;
        return Redirect(target);
    }
}
