using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace SafeLearnKids.Web.Data;

// Reuse the existing Identity tables; SafeLearnDbContext maps business entities.
public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<ApplicationUser>(entity =>
        {
            entity.ToTable("Users", "dbo");
            entity.Property(user => user.Id).HasMaxLength(450);
            entity.Property(user => user.FullName).HasMaxLength(100);
            entity.Property(user => user.IsActive).HasDefaultValue(true);
            entity.Property(user => user.CreatedAt).HasDefaultValueSql("sysutcdatetime()");
        });
        modelBuilder.Entity<IdentityRole>(entity =>
        {
            entity.ToTable("Roles", "dbo");
            entity.Property(role => role.Id).HasMaxLength(450);
        });
        modelBuilder.Entity<IdentityUserRole<string>>().ToTable("UserRoles", "dbo");
        modelBuilder.Entity<IdentityUserClaim<string>>().ToTable("UserClaims", "dbo");
        modelBuilder.Entity<IdentityRoleClaim<string>>().ToTable("RoleClaims", "dbo");
        modelBuilder.Entity<IdentityUserLogin<string>>(entity =>
        {
            entity.ToTable("UserLogins", "dbo");
            entity.Property(login => login.LoginProvider).HasMaxLength(128);
            entity.Property(login => login.ProviderKey).HasMaxLength(128);
        });
        modelBuilder.Entity<IdentityUserToken<string>>(entity =>
        {
            entity.ToTable("UserTokens", "dbo");
            entity.Property(token => token.LoginProvider).HasMaxLength(128);
            entity.Property(token => token.Name).HasMaxLength(128);
        });
    }
}
