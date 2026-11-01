using Microsoft.AspNetCore.Identity;

namespace SafeLearnKids.Web.Data;

public static class DemoSeeder
{
    public const string ParentRole = "Parent";
    public const string AdminRole = "Admin";

    public static async Task SeedAsync(IServiceProvider services, bool inMemory, bool seedAccounts)
    {
        var db = services.GetRequiredService<ApplicationDbContext>();
        if (inMemory)
        {
            await db.Database.EnsureCreatedAsync();
        }

        var roles = services.GetRequiredService<RoleManager<IdentityRole>>();
        foreach (var role in new[] { ParentRole, AdminRole })
        {
            if (!await roles.RoleExistsAsync(role))
            {
                await roles.CreateAsync(new IdentityRole(role));
            }
        }

        await EnsureBootstrapAdminAsync(services);

        if (!seedAccounts)
        {
            return;
        }

        var users = services.GetRequiredService<UserManager<ApplicationUser>>();
        var configuration = services.GetRequiredService<IConfiguration>();
        var parentPassword = configuration["Demo:ParentPassword"];
        var adminPassword = configuration["Demo:AdminPassword"];
        if (string.IsNullOrWhiteSpace(parentPassword) || string.IsNullOrWhiteSpace(adminPassword))
            throw new InvalidOperationException("Set Demo:ParentPassword and Demo:AdminPassword privately before seeding demo accounts.");
        await EnsureUserAsync(users, "ha.nguyen@gmail.com", "Nguyễn Thu Hà", parentPassword, ParentRole);
        await EnsureUserAsync(users, "admin@safelearnkids.vn", "Quản trị SafeLearn Kids", adminPassword, AdminRole);
    }

    // Registration only creates parents, so the first admin comes from private configuration.
    private static async Task EnsureBootstrapAdminAsync(IServiceProvider services)
    {
        var configuration = services.GetRequiredService<IConfiguration>();
        var email = configuration["Bootstrap:AdminEmail"]?.Trim();
        var password = configuration["Bootstrap:AdminPassword"];
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password)) return;

        var users = services.GetRequiredService<UserManager<ApplicationUser>>();
        // Runs only while no admin can sign in, so later demotions stay in force.
        var admins = await users.GetUsersInRoleAsync(AdminRole);
        if (admins.Any(admin => admin.IsActive && admin.EmailConfirmed && admin.PasswordHash is not null)) return;

        var user = await users.FindByEmailAsync(email);
        if (user is null)
        {
            user = new ApplicationUser { UserName = email, Email = email, FullName = "Quản trị viên" };
            Require(await users.CreateAsync(user, password));
        }
        else
        {
            // Drop any password set before this address was proven to be the operator's.
            if (user.PasswordHash is not null) Require(await users.RemovePasswordAsync(user));
            Require(await users.AddPasswordAsync(user, password));
        }

        user.EmailConfirmed = true;
        user.IsActive = true;
        user.LockoutEnd = null;
        Require(await users.UpdateAsync(user));
        if (!await users.IsInRoleAsync(user, AdminRole)) Require(await users.AddToRoleAsync(user, AdminRole));
    }

    private static void Require(IdentityResult result)
    {
        if (!result.Succeeded)
            throw new InvalidOperationException("Bootstrap admin creation failed. Check the configured password policy and role setup.");
    }

    private static async Task EnsureUserAsync(UserManager<ApplicationUser> users, string email, string fullName, string password, string role)
    {
        if (await users.FindByEmailAsync(email) is not null)
        {
            return;
        }

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            FullName = fullName
        };

        var result = await users.CreateAsync(user, password);
        if (!result.Succeeded || !(await users.AddToRoleAsync(user, role)).Succeeded)
            throw new InvalidOperationException("Demo account creation failed. Check the configured password policy and role setup.");
    }
}
