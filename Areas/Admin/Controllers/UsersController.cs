using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SafeLearnKids.Web.Data;
using SafeLearnKids.Web.Models;
using SafeLearnKids.Web.Services;

namespace SafeLearnKids.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Admin")]
public class UsersController(
    UserManager<ApplicationUser> users,
    ApplicationDbContext db,
    AccountEmailService email,
    AccountActivityLog activity,
    AvatarStorage avatars,
    IServiceProvider services) : Controller
{
    private const int PageSize = 20;
    private const string SaveFailed = "Chưa thể lưu thay đổi. Vui lòng thử lại.";
    private const string EmailTaken = "Email này đã có tài khoản.";
    private const string EmailUnusable = "Không thể dùng email này cho tài khoản.";
    private const string InUse = "Tài khoản đã có hồ sơ bé hoặc dữ liệu liên quan nên không xóa được. Hãy khóa tài khoản.";
    private static readonly string[] ManagedRoles = [DemoSeeder.ParentRole, DemoSeeder.AdminRole];

    [HttpGet("admin-users")]
    public async Task<IActionResult> Index(string? q, string? status, string? role, int page = 1, CancellationToken cancellationToken = default)
    {
        var matching = await Filter(q, status, role).CountAsync(cancellationToken);
        var totalPages = Math.Max(1, (int)Math.Ceiling(matching / (double)PageSize));
        page = Math.Clamp(page, 1, totalPages);
        var pageUsers = await Filter(q, status, role)
            .OrderByDescending(user => user.CreatedAt).ThenBy(user => user.Id)
            .Skip((page - 1) * PageSize).Take(PageSize)
            .ToListAsync(cancellationToken);
        return View(new AdminUsersViewModel
        {
            Q = q,
            Status = status,
            Role = role,
            Page = page,
            TotalPages = totalPages,
            Matching = matching,
            Total = await Filter(null, null, null).CountAsync(cancellationToken),
            Active = await Filter(null, AdminUserStatus.Active, null).CountAsync(cancellationToken),
            Locked = await Filter(null, AdminUserStatus.Locked, null).CountAsync(cancellationToken),
            Pending = await Filter(null, AdminUserStatus.Pending, null).CountAsync(cancellationToken),
            RoleOptions = await db.Roles.Select(item => item.Name!).OrderBy(name => name).ToListAsync(cancellationToken),
            Rows = await ToRowsAsync(pageUsers, cancellationToken)
        });
    }

    [HttpGet("admin-users/export")]
    public async Task<IActionResult> Export(string? q, string? status, string? role, CancellationToken cancellationToken)
    {
        var matches = await Filter(q, status, role).OrderByDescending(user => user.CreatedAt).ToListAsync(cancellationToken);
        var csv = new StringBuilder("Họ tên,Email,Vai trò,Số bé,Tham gia,Đăng nhập gần nhất,Trạng thái\r\n");
        foreach (var row in await ToRowsAsync(matches, cancellationToken))
        {
            csv.AppendJoin(',', new[]
            {
                row.Name, row.Email, string.Join(" ", row.Roles), row.Children.ToString(), row.CreatedAt.ToString("yyyy-MM-dd"),
                row.LastLoginAt?.ToString("yyyy-MM-dd HH:mm") ?? string.Empty, row.Status
            }.Select(CsvCell)).Append("\r\n");
        }
        await activity.AdminActionAsync(ActorId, "ExportUsers", null, $"{matches.Count} tài khoản");
        var content = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv.ToString())).ToArray();
        return File(content, "text/csv; charset=utf-8", "nguoi-dung.csv");
    }

    [HttpGet("admin-users/new")]
    public IActionResult Create() => View("Form", new AdminUserFormViewModel());

    [HttpPost("admin-users/new")]
    public async Task<IActionResult> Create(AdminUserFormViewModel model, CancellationToken cancellationToken)
    {
        model.Id = null;
        if (!ManagedRoles.Contains(model.Role)) ModelState.AddModelError(nameof(model.Role), "Vai trò không hợp lệ.");
        var address = model.Email.Trim();
        var fullName = model.FullName.Trim();
        if (fullName.Length < 2) ModelState.AddModelError(nameof(model.FullName), "Họ và tên cần từ 2 đến 100 ký tự.");
        if (ModelState.IsValid && await users.FindByEmailAsync(address) is not null) ModelState.AddModelError(nameof(model.Email), EmailTaken);
        if (!ModelState.IsValid) return View("Form", model);

        var user = new ApplicationUser { UserName = address, Email = address, FullName = fullName };
        try
        {
            // Dispose rolls back a user left without a role.
            await using var transaction = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync(cancellationToken) : null;
            var result = await users.CreateAsync(user);
            if (result.Succeeded) result = await users.AddToRoleAsync(user, model.Role);
            if (!result.Succeeded)
            {
                ModelState.AddModelError(nameof(model.Email), EmailUnusable);
                return View("Form", model);
            }
            if (transaction is not null) await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Another request took the address after the check above.
            db.ChangeTracker.Clear();
            ModelState.AddModelError(nameof(model.Email), EmailTaken);
            return View("Form", model);
        }
        await email.SendInvitationAsync(user, cancellationToken);
        return await FinishAsync(user.Id, IdentityResult.Success, "CreateUser", "Đã tạo tài khoản và gửi email mời.", model.Role);
    }

    [HttpGet("admin-users/{id}/edit")]
    public async Task<IActionResult> Edit(string id)
    {
        var user = await users.FindByIdAsync(id);
        if (user is null) return NotFound();
        return View("Form", new AdminUserFormViewModel { Id = id, IsSelf = id == ActorId, FullName = user.FullName ?? string.Empty, Email = user.Email ?? string.Empty });
    }

    [HttpPost("admin-users/{id}/edit")]
    public async Task<IActionResult> Edit(string id, AdminUserFormViewModel model, CancellationToken cancellationToken)
    {
        var user = await users.FindByIdAsync(id);
        if (user is null) return NotFound();
        model.Id = id;
        model.IsSelf = id == ActorId;
        var address = model.Email.Trim();
        var fullName = model.FullName.Trim();
        var emailChanged = !string.Equals(address, user.Email, StringComparison.OrdinalIgnoreCase);
        if (fullName.Length < 2) ModelState.AddModelError(nameof(model.FullName), "Họ và tên cần từ 2 đến 100 ký tự.");
        // Changing it would unconfirm the address and lock the acting admin out.
        if (emailChanged && model.IsSelf) ModelState.AddModelError(nameof(model.Email), "Bạn không thể tự đổi email của mình tại đây.");
        if (ModelState.IsValid && emailChanged && await users.FindByEmailAsync(address) is not null) ModelState.AddModelError(nameof(model.Email), EmailTaken);
        if (!ModelState.IsValid) return View("Form", model);

        user.FullName = fullName;
        IdentityResult result;
        try
        {
            await using var transaction = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync(cancellationToken) : null;
            result = emailChanged ? await users.SetUserNameAsync(user, address) : await users.UpdateAsync(user);
            // SetEmailAsync clears the confirmation and ends the account's sessions.
            if (result.Succeeded && emailChanged) result = await users.SetEmailAsync(user, address);
            if (result.Succeeded && transaction is not null) await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            db.ChangeTracker.Clear();
            ModelState.AddModelError(nameof(model.Email), EmailTaken);
            return View("Form", model);
        }
        if (!result.Succeeded)
        {
            db.ChangeTracker.Clear();
            ModelState.AddModelError(emailChanged ? nameof(model.Email) : string.Empty, emailChanged ? EmailUnusable : SaveFailed);
            return View("Form", model);
        }
        if (emailChanged && user.IsActive) await SendAccessEmailAsync(user, cancellationToken);
        return await FinishAsync(id, result, "UpdateUser", emailChanged ? "Đã lưu thay đổi. Địa chỉ mới cần được xác minh." : "Đã lưu thay đổi.", emailChanged ? "Đổi email" : null);
    }

    [HttpPost("admin-users/{id}/delete")]
    public async Task<IActionResult> Delete(string id, CancellationToken cancellationToken)
    {
        var user = await users.FindByIdAsync(id);
        if (user is null) return NotFound();
        // The acting admin cannot be deleted, so one admin always remains.
        if (id == ActorId) return Refuse(id, "Bạn không thể tự xóa tài khoản của mình.");
        var business = services.GetService<SafeLearnDbContext>();
        // Children's learning data follows the parent privacy flow, not an admin click.
        if (business is not null && await business.ChildProfiles.AnyAsync(child => child.ParentId == id || child.UserId == id, cancellationToken))
            return Refuse(id, InUse);
        var roles = string.Join(", ", await users.GetRolesAsync(user));
        var avatar = user.AvatarUrl;
        // The foreign key is cleared on delete, but these rows keep the typed email.
        var history = business is null ? [] : await business.LoginHistories.Where(item => item.UserId == id).Select(item => item.Id).ToListAsync(cancellationToken);
        try
        {
            if (!(await users.DeleteAsync(user)).Succeeded) return Refuse(id, SaveFailed);
        }
        catch (DbUpdateException)
        {
            // Lessons, reviews and other shared records still reference this account.
            db.ChangeTracker.Clear();
            return Refuse(id, InUse);
        }
        avatars.Delete(avatar);
        if (business is not null) await business.LoginHistories.Where(item => history.Contains(item.Id)).ExecuteDeleteAsync(cancellationToken);
        await activity.AdminActionAsync(ActorId, "DeleteUser", id, roles);
        TempData["Info"] = "Đã xóa tài khoản.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("admin-users/{id}/avatar")]
    [RequestSizeLimit(AvatarStorage.MaxRequestBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = AvatarStorage.MaxRequestBytes)]
    public async Task<IActionResult> Avatar(string id, IFormFile? file, CancellationToken cancellationToken)
    {
        var user = await users.FindByIdAsync(id);
        if (user is null) return NotFound();
        if (!await avatars.ReplaceAsync(user, file, cancellationToken)) return Refuse(id, AvatarStorage.Rejected);
        return await FinishAsync(id, IdentityResult.Success, "ChangeAvatar", "Đã cập nhật ảnh đại diện.");
    }

    [HttpPost("admin-users/{id}/avatar/remove")]
    public async Task<IActionResult> RemoveAvatar(string id)
    {
        var user = await users.FindByIdAsync(id);
        if (user is null) return NotFound();
        if (!await avatars.RemoveAsync(user)) return Refuse(id, SaveFailed);
        return await FinishAsync(id, IdentityResult.Success, "RemoveAvatar", "Đã gỡ ảnh đại diện.");
    }

    [HttpGet("admin-users/{id}")]
    public async Task<IActionResult> Details(string id, CancellationToken cancellationToken)
    {
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (user is null) return NotFound();
        var row = (await ToRowsAsync([user], cancellationToken))[0];
        var logins = new List<AdminLoginRow>();
        var audit = new List<AdminAuditRow>();
        if (services.GetService<SafeLearnDbContext>() is { } business)
        {
            logins = await business.LoginHistories.AsNoTracking().Where(item => item.UserId == id)
                .OrderByDescending(item => item.CreatedAt).Take(10)
                .Select(item => new AdminLoginRow(item.CreatedAt, item.Succeeded, item.FailReason, item.IpAddress))
                .ToListAsync(cancellationToken);
            audit = await business.AuditLogs.AsNoTracking()
                .Where(item => item.EntityType == AccountActivityLog.UserEntity && item.EntityId == id)
                .OrderByDescending(item => item.CreatedAt).Take(10)
                .Select(item => new AdminAuditRow(item.CreatedAt, item.Action, item.Detail, item.Actor == null ? null : item.Actor.Email))
                .ToListAsync(cancellationToken);
        }
        return View(new AdminUserDetailViewModel
        {
            User = row,
            EmailConfirmed = user.EmailConfirmed,
            Disabled = !user.IsActive,
            LockedOutUntil = user.LockoutEnd > DateTimeOffset.UtcNow ? user.LockoutEnd : null,
            IsSelf = id == ActorId,
            RoleEditable = RoleEditable(row.Roles),
            Logins = logins,
            Audit = audit
        });
    }

    [HttpPost("admin-users/{id}/lock")]
    public async Task<IActionResult> Lock(string id)
    {
        var user = await users.FindByIdAsync(id);
        if (user is null) return NotFound();
        // An admin can only be locked by another active admin, so one always remains.
        if (id == ActorId) return Refuse(id, "Bạn không thể tự khóa tài khoản của mình.");
        user.IsActive = false;
        return await FinishAsync(id, await users.UpdateSecurityStampAsync(user), "LockUser", "Đã khóa tài khoản và kết thúc mọi phiên đăng nhập.");
    }

    [HttpPost("admin-users/{id}/unlock")]
    public async Task<IActionResult> Unlock(string id)
    {
        var user = await users.FindByIdAsync(id);
        if (user is null) return NotFound();
        user.IsActive = true;
        user.LockoutEnd = null;
        user.AccessFailedCount = 0;
        return await FinishAsync(id, await users.UpdateAsync(user), "UnlockUser", "Đã mở khóa tài khoản.");
    }

    [HttpPost("admin-users/{id}/role")]
    public async Task<IActionResult> SetRole(string id, [FromForm] string? role)
    {
        var user = await users.FindByIdAsync(id);
        if (user is null) return NotFound();
        if (role is null || !ManagedRoles.Contains(role)) return Refuse(id, "Vai trò không hợp lệ.");
        if (id == ActorId) return Refuse(id, "Bạn không thể tự đổi vai trò của mình.");
        var current = await users.GetRolesAsync(user);
        if (!RoleEditable(current)) return Refuse(id, "Vai trò của tài khoản này không chỉnh được tại đây.");
        if (role == DemoSeeder.AdminRole && user is not { IsActive: true, EmailConfirmed: true })
            return Refuse(id, "Chỉ cấp quyền Admin cho tài khoản đang hoạt động và đã xác minh email.");
        if (current.Contains(role)) return Refuse(id, "Tài khoản đã có vai trò này.");

        // Dispose rolls back a partial role swap.
        await using var transaction = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync() : null;
        var result = IdentityResult.Success;
        foreach (var previous in current)
            if (result.Succeeded) result = await users.RemoveFromRoleAsync(user, previous);
        if (result.Succeeded) result = await users.AddToRoleAsync(user, role);
        // Roles live in the cookie, so sessions holding the old role must end.
        if (result.Succeeded) result = await users.UpdateSecurityStampAsync(user);
        if (result.Succeeded && transaction is not null) await transaction.CommitAsync();
        return await FinishAsync(id, result, "ChangeRole", "Đã đổi vai trò và kết thúc các phiên đăng nhập cũ.", role);
    }

    [HttpPost("admin-users/{id}/resend-confirmation")]
    public async Task<IActionResult> ResendConfirmation(string id, CancellationToken cancellationToken)
    {
        var user = await users.FindByIdAsync(id);
        if (user is null) return NotFound();
        if (user is not { IsActive: true, EmailConfirmed: false }) return Refuse(id, "Chỉ gửi xác minh cho tài khoản đang chờ xác minh.");
        await SendAccessEmailAsync(user, cancellationToken);
        return await FinishAsync(id, IdentityResult.Success, "ResendConfirmation", "Đã gửi lại email xác minh.");
    }

    [HttpPost("admin-users/{id}/send-reset")]
    public async Task<IActionResult> SendReset(string id, CancellationToken cancellationToken)
    {
        var user = await users.FindByIdAsync(id);
        if (user is null) return NotFound();
        if (user is not { IsActive: true, EmailConfirmed: true }) return Refuse(id, "Chỉ gửi đặt lại mật khẩu cho tài khoản đang hoạt động.");
        await email.SendPasswordResetAsync(user, cancellationToken);
        return await FinishAsync(id, IdentityResult.Success, "SendPasswordReset", "Đã gửi email đặt lại mật khẩu.");
    }

    private string ActorId => users.GetUserId(User)!;

    private async Task SendAccessEmailAsync(ApplicationUser user, CancellationToken cancellationToken)
    {
        if (await users.HasPasswordAsync(user)) await email.SendConfirmationAsync(user, cancellationToken);
        else await email.SendInvitationAsync(user, cancellationToken);
    }

    private static bool RoleEditable(IEnumerable<string> roles) => roles.All(ManagedRoles.Contains);

    private IQueryable<ApplicationUser> Filter(string? q, string? status, string? role)
    {
        var now = DateTimeOffset.UtcNow;
        var query = db.Users.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            query = query.Where(user => user.Email!.Contains(term) || user.FullName!.Contains(term));
        }
        if (!string.IsNullOrWhiteSpace(role))
            query = query.Where(user => db.UserRoles.Any(link => link.UserId == user.Id
                && db.Roles.Any(item => item.Id == link.RoleId && item.Name == role)));
        return status switch
        {
            AdminUserStatus.Locked => query.Where(user => !user.IsActive || user.LockoutEnd > now),
            AdminUserStatus.Pending => query.Where(user => user.IsActive && !(user.LockoutEnd > now) && !user.EmailConfirmed),
            AdminUserStatus.Active => query.Where(user => user.IsActive && !(user.LockoutEnd > now) && user.EmailConfirmed),
            _ => query
        };
    }

    private async Task<IReadOnlyList<AdminUserRow>> ToRowsAsync(IReadOnlyCollection<ApplicationUser> page, CancellationToken cancellationToken)
    {
        var ids = page.Select(user => user.Id).ToList();
        var roles = (await (from link in db.UserRoles
                            join role in db.Roles on link.RoleId equals role.Id
                            where ids.Contains(link.UserId)
                            select new { link.UserId, role.Name }).ToListAsync(cancellationToken))
            .ToLookup(item => item.UserId, item => item.Name!);
        var children = services.GetService<SafeLearnDbContext>() is { } business
            ? await business.ChildProfiles.AsNoTracking().Where(child => child.IsActive && ids.Contains(child.ParentId))
                .GroupBy(child => child.ParentId).Select(group => new { group.Key, Count = group.Count() })
                .ToDictionaryAsync(item => item.Key, item => item.Count, cancellationToken)
            : [];
        var now = DateTimeOffset.UtcNow;
        return page.Select(user => new AdminUserRow(
            user.Id,
            user.FullName ?? user.UserName ?? string.Empty,
            user.Email ?? string.Empty,
            roles[user.Id].OrderBy(name => name).ToList(),
            children.GetValueOrDefault(user.Id),
            user.CreatedAt,
            user.LastLoginAt,
            !user.IsActive || user.LockoutEnd > now ? AdminUserStatus.Locked
                : user.EmailConfirmed ? AdminUserStatus.Active : AdminUserStatus.Pending,
            user.AvatarUrl)).ToList();
    }

    // Names come from the sign-up form; spreadsheets must not run them as formulas.
    private static string CsvCell(string value)
    {
        if (value.Length > 0 && "=+-@\t\r".Contains(value[0])) value = "'" + value;
        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }

    private async Task<IActionResult> FinishAsync(string id, IdentityResult result, string action, string message, string? detail = null)
    {
        if (!result.Succeeded) return Refuse(id, SaveFailed);
        await activity.AdminActionAsync(ActorId, action, id, detail);
        TempData["Info"] = message;
        return RedirectToAction(nameof(Details), new { id });
    }

    private RedirectToActionResult Refuse(string id, string message)
    {
        TempData["Error"] = message;
        return RedirectToAction(nameof(Details), new { id });
    }
}
