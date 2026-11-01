using Microsoft.AspNetCore.Identity;

namespace SafeLearnKids.Web.Data;

public class ApplicationUser : IdentityUser
{
    public string? FullName { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastLoginAt { get; set; }
}
