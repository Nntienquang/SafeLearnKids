using System.ComponentModel.DataAnnotations;

namespace SafeLearnKids.Web.Models;

public static class AdminUserStatus
{
    public const string Active = "active";
    public const string Locked = "locked";
    public const string Pending = "pending";
}

public record AdminUserRow(
    string Id,
    string Name,
    string Email,
    IReadOnlyList<string> Roles,
    int Children,
    DateTime CreatedAt,
    DateTime? LastLoginAt,
    string Status,
    string? AvatarUrl);

public class AdminUserFormViewModel
{
    public string? Id { get; set; }
    public bool IsSelf { get; set; }

    [Required(ErrorMessage = "Nhập họ và tên.")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "Họ và tên cần từ 2 đến 100 ký tự.")]
    [Display(Name = "Họ và tên")]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Nhập email.")]
    [EmailAddress(ErrorMessage = "Email chưa đúng định dạng.")]
    [StringLength(256, ErrorMessage = "Email không được vượt quá 256 ký tự.")]
    [Display(Name = "Email")]
    public string Email { get; set; } = string.Empty;

    [Display(Name = "Vai trò")]
    public string Role { get; set; } = "Parent";
}

public class AdminUsersViewModel
{
    public string? Q { get; init; }
    public string? Status { get; init; }
    public string? Role { get; init; }
    public int Page { get; init; }
    public int TotalPages { get; init; }
    public int Matching { get; init; }
    public int Total { get; init; }
    public int Active { get; init; }
    public int Locked { get; init; }
    public int Pending { get; init; }
    public IReadOnlyList<string> RoleOptions { get; init; } = [];
    public IReadOnlyList<AdminUserRow> Rows { get; init; } = [];
}

public record AdminLoginRow(DateTime At, bool Succeeded, string? FailReason, string? IpAddress);

public record AdminAuditRow(DateTime At, string Action, string? Detail, string? Actor);

public class AdminUserDetailViewModel
{
    public required AdminUserRow User { get; init; }
    public bool EmailConfirmed { get; init; }
    public bool Disabled { get; init; }
    public DateTimeOffset? LockedOutUntil { get; init; }
    public bool IsSelf { get; init; }
    public bool RoleEditable { get; init; }
    public IReadOnlyList<AdminLoginRow> Logins { get; init; } = [];
    public IReadOnlyList<AdminAuditRow> Audit { get; init; } = [];
}
