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

// US-30 and US-32: daily limits per child and the parent's answer to extra-time requests.
[Area("Parent")]
[Authorize(Roles = "Parent")]
public class TimeLimitsController(KidContext kids, IServiceProvider services) : Controller
{
    private SafeLearnDbContext? Db => services.GetService<SafeLearnDbContext>();
    private string ParentId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    [HttpGet("time")]
    public async Task<IActionResult> Index(int? child, CancellationToken cancellationToken)
    {
        if (Db is not { } db) return View("SqlOnly");
        var choices = await kids.ChoicesAsync(cancellationToken);
        if (choices.Count == 0) return Redirect("/onboarding");
        var chosen = choices.FirstOrDefault(item => item.Id == child) ?? choices[0];
        var policy = await db.TimeLimitPolicies.AsNoTracking().Include(item => item.TimeLimitOverrides)
            .FirstOrDefaultAsync(item => item.ChildProfileId == chosen.Id, cancellationToken);
        var form = new TimeLimitForm
        {
            DailyMinutes = policy?.DailyMinutes ?? StudyTimeService.DefaultDailyMinutes,
            StudyStart = policy?.StudyStart,
            StudyEnd = policy?.StudyEnd,
            BreakEveryMinutes = policy?.BreakEveryMinutes,
            Weekdays = Enumerable.Range(1, 7).Select(day => policy?.TimeLimitOverrides.FirstOrDefault(item => item.Weekday == day)?.Minutes).ToList()
        };
        return View(await ModelAsync(db, chosen, choices, form, cancellationToken));
    }

    [HttpPost("time/{childId:int}")]
    public async Task<IActionResult> Save(int childId, TimeLimitForm form, CancellationToken cancellationToken)
    {
        if (Db is not { } db) return View("SqlOnly");
        var choices = await kids.ChoicesAsync(cancellationToken);
        if (choices.FirstOrDefault(item => item.Id == childId) is not { } chosen) return NotFound();
        while (form.Weekdays.Count < 7) form.Weekdays.Add(null);
        if (form.Weekdays.Take(7).Any(minutes => minutes is < 0 or > 600)) ModelState.AddModelError(nameof(form.Weekdays), "Mỗi thứ từ 0 đến 600 phút, để trống nếu giống mọi ngày.");
        if ((form.StudyStart is null) != (form.StudyEnd is null)) ModelState.AddModelError(nameof(form.StudyStart), "Nhập cả giờ bắt đầu và giờ kết thúc, hoặc để trống cả hai.");
        else if (form.StudyStart is { } start && form.StudyStart == form.StudyEnd) ModelState.AddModelError(nameof(form.StudyStart), "Giờ bắt đầu và kết thúc phải khác nhau.");
        if (!ModelState.IsValid) return View("Index", await ModelAsync(db, chosen, choices, form, cancellationToken));

        var policy = await db.TimeLimitPolicies.Include(item => item.TimeLimitOverrides).FirstOrDefaultAsync(item => item.ChildProfileId == childId, cancellationToken);
        if (policy is null)
        {
            policy = new TimeLimitPolicy { ChildProfileId = childId };
            db.TimeLimitPolicies.Add(policy);
        }
        policy.DailyMinutes = form.DailyMinutes;
        policy.StudyStart = form.StudyStart;
        policy.StudyEnd = form.StudyEnd;
        policy.BreakEveryMinutes = form.BreakEveryMinutes;
        policy.UpdatedById = ParentId;
        policy.UpdatedAt = DateTime.UtcNow;
        for (var day = 1; day <= 7; day++)
        {
            var minutes = form.Weekdays[day - 1];
            var existing = policy.TimeLimitOverrides.FirstOrDefault(item => item.Weekday == day);
            if (minutes is null && existing is not null) db.TimeLimitOverrides.Remove(existing);
            else if (minutes is { } value && existing is not null) existing.Minutes = value;
            else if (minutes is { } added) policy.TimeLimitOverrides.Add(new TimeLimitOverride { Weekday = day, Minutes = added });
        }
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            db.ChangeTracker.Clear();
            TempData["Error"] = "Chưa thể lưu cài đặt. Vui lòng thử lại.";
            return RedirectToAction(nameof(Index), new { child = childId });
        }
        TempData["Info"] = $"Đã lưu giới hạn giờ học của {chosen.Name}. Áp dụng ngay từ bây giờ.";
        return RedirectToAction(nameof(Index), new { child = childId });
    }

    [HttpPost("time/requests/{id:int}")]
    [EnableRateLimiting("account")]
    public async Task<IActionResult> Decide(int id, [FromForm] string? decision, [FromForm] int? minutes, [FromForm] string? rowVersion, CancellationToken cancellationToken)
    {
        if (Db is not { } db) return View("SqlOnly");
        var request = await db.TimeExtensionRequests.Include(item => item.ChildProfile)
            .FirstOrDefaultAsync(item => item.Id == id && item.ChildProfile.ParentId == ParentId, cancellationToken);
        if (request is null) return NotFound();
        await StudyTimeService.ExpireAsync(db, cancellationToken);
        await db.Entry(request).ReloadAsync(cancellationToken);
        if (request.Status != RequestStatus.Pending)
            return Back(request.ChildProfileId, request.Status == RequestStatus.Expired ? "Yêu cầu từ hôm trước đã hết hạn." : "Yêu cầu này đã được trả lời.");
        if (decision == "approve")
        {
            var granted = minutes ?? request.RequestedMinutes;
            if (granted < 1 || granted > request.RequestedMinutes) return Back(request.ChildProfileId, $"Cho thêm từ 1 đến {request.RequestedMinutes} phút.");
            request.Status = RequestStatus.Approved;
            request.GrantedMinutes = granted;
        }
        else if (decision == "deny") request.Status = RequestStatus.Denied;
        else return Back(request.ChildProfileId, "Hãy chọn đồng ý hoặc từ chối.");
        request.DecidedById = ParentId;
        request.DecidedAt = DateTime.UtcNow;
        try
        {
            if (!string.IsNullOrEmpty(rowVersion)) db.Entry(request).Property(item => item.RowVersion).OriginalValue = Convert.FromBase64String(rowVersion);
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is DbUpdateConcurrencyException or FormatException)
        {
            // Another device answered first.
            return Back(request.ChildProfileId, "Yêu cầu này vừa được trả lời ở nơi khác.");
        }
        TempData["Info"] = request.Status == RequestStatus.Approved
            ? $"Đã cho {request.ChildProfile.DisplayName} thêm {request.GrantedMinutes} phút hôm nay."
            : $"Đã từ chối yêu cầu của {request.ChildProfile.DisplayName}.";
        return RedirectToAction(nameof(Index), new { child = request.ChildProfileId });
    }

    private RedirectToActionResult Back(int childId, string message)
    {
        TempData["Error"] = message;
        return RedirectToAction(nameof(Index), new { child = childId });
    }

    private static async Task<TimePageViewModel> ModelAsync(SafeLearnDbContext db, KidChoice chosen, IReadOnlyList<KidChoice> choices, TimeLimitForm form, CancellationToken cancellationToken)
    {
        await StudyTimeService.ExpireAsync(db, cancellationToken);
        var ids = choices.Select(item => item.Id).ToList();
        var since = StudyTimeService.Today.AddDays(-6);
        var requests = await db.TimeExtensionRequests.AsNoTracking().Where(item => ids.Contains(item.ChildProfileId) && item.ForDate >= since)
            .OrderBy(item => item.Status == RequestStatus.Pending ? 0 : 1).ThenByDescending(item => item.RequestedAt).Take(20)
            .Select(item => new { item.Id, item.ChildProfileId, item.ChildProfile.DisplayName, item.RequestedMinutes, item.GrantedMinutes, item.Reason, item.Status, item.RequestedAt, item.RowVersion })
            .ToListAsync(cancellationToken);
        return new TimePageViewModel
        {
            Child = chosen,
            Children = choices,
            Form = form,
            Today = await StudyTimeService.StatusAsync(db, chosen.Id, cancellationToken),
            Requests = requests.Select(item => new ExtensionRow(item.Id, item.ChildProfileId, item.DisplayName, item.RequestedMinutes, item.GrantedMinutes, item.Reason,
                item.Status, item.RequestedAt, Convert.ToBase64String(item.RowVersion))).ToList()
        };
    }
}
