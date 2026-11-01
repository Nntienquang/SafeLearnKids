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
