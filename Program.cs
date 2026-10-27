using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SafeLearnKids.Web.Data;

var builder = WebApplication.CreateBuilder(args);

// ---- Data: SQL Server in production, in-memory for the front-end demo (Demo:UseInMemoryDatabase) ----
var useInMemory = builder.Configuration.GetValue<bool>("Demo:UseInMemoryDatabase");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    if (useInMemory)
        options.UseInMemoryDatabase("SafeLearnKids");
    else
        options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"));
});

// ---- ASP.NET Core Identity (roles: Parent, Admin) ----
builder.Services
    .AddIdentity<ApplicationUser, IdentityRole>(options =>
    {
        options.User.RequireUniqueEmail = true;
        options.Password.RequiredLength = 8;
        options.Password.RequireNonAlphanumeric = false;
        options.Password.RequireUppercase = false;
        options.Lockout.AllowedForNewUsers = true;
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
    })
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

builder.Services.AddScoped<IUserClaimsPrincipalFactory<ApplicationUser>, AppClaimsFactory>();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/login";
    options.AccessDeniedPath = "/login";
    options.LogoutPath = "/logout";
    options.SlidingExpiration = true;
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
});

builder.Services.AddControllersWithViews();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/error/500");
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/error/{0}");
app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

using (var scope = app.Services.CreateScope())
{
    await DemoSeeder.SeedAsync(scope.ServiceProvider, useInMemory, app.Configuration.GetValue<bool>("Demo:SeedDemoAccounts"));
}

app.Run();
