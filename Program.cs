using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using System.Threading.RateLimiting;
using SafeLearnKids.Web.Data;
using SafeLearnKids.Web.Services;

var builder = WebApplication.CreateBuilder(args);
var development = builder.Environment.IsDevelopment();
var security = builder.Configuration.GetSection("Auth").Get<AccountSecurityOptions>() ?? new();
builder.Services.AddOptions<AccountSecurityOptions>().Bind(builder.Configuration.GetSection("Auth"))
    .Validate(options => options.PasswordResetTokenLifetime >= TimeSpan.FromSeconds(1) && options.PasswordResetTokenLifetime <= TimeSpan.FromDays(1)
        && options.EmailConfirmationTokenLifetime >= TimeSpan.FromSeconds(1) && options.EmailConfirmationTokenLifetime <= TimeSpan.FromDays(7)
        && options.RateLimitPermitLimit is >= 1 and <= 1000 && options.RateLimitWindowSeconds is >= 1 and <= 3600,
        "Invalid authentication token lifetime or rate limit configuration.").ValidateOnStart();
builder.Services.AddOptions<AccountEmailOptions>().Bind(builder.Configuration.GetSection("Email"))
    .Validate(options => Uri.TryCreate(options.PublicBaseUrl, UriKind.Absolute, out var uri)
        && string.IsNullOrEmpty(uri.UserInfo) && string.IsNullOrEmpty(uri.Query) && string.IsNullOrEmpty(uri.Fragment)
        && (uri.Scheme == "https" || development && uri.IsLoopback && uri.Scheme == "http"),
        "Configure Email:PublicBaseUrl with a trusted HTTPS origin (HTTP loopback is allowed in Development).")
    .Validate(options => options.DeliveryMethod == "Pickup" && development
        || options.DeliveryMethod == "Smtp" && !string.IsNullOrWhiteSpace(options.Host) && options.Port is >= 1 and <= 65535,
        "Configure SMTP, or use private email pickup in Development only.")
    .Validate(options => System.Net.Mail.MailAddress.TryCreate(options.FromAddress, out _), "Configure a valid Email:FromAddress.")
    .ValidateOnStart();
builder.Services.AddScoped<AccountEmailService>();
builder.Services.AddSingleton<AccountEmailQueue>();
builder.Services.AddHostedService(provider => provider.GetRequiredService<AccountEmailQueue>());

#if DEBUG
// Resolve static assets when Debug launches use a different content root or environment.
builder.WebHost.UseStaticWebAssets();
#endif

var useInMemory = builder.Configuration.GetValue<bool>("Demo:UseInMemoryDatabase");
if (!development && (useInMemory || builder.Configuration.GetValue<bool>("Demo:SeedDemoAccounts")))
    throw new InvalidOperationException("InMemory and known demo accounts are allowed only in Development.");
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (!useInMemory && string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException("Configure ConnectionStrings:DefaultConnection for SQL Server.");
}

builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    if (useInMemory)
        options.UseInMemoryDatabase("SafeLearnKids");
    else
        options.UseSqlServer(connectionString);
});
if (!useInMemory)
{
    builder.Services.AddDbContext<SafeLearnDbContext>(options => options.UseSqlServer(connectionString));
}

builder.Services
    .AddIdentity<ApplicationUser, IdentityRole>(options =>
    {
        options.User.RequireUniqueEmail = true;
        options.SignIn.RequireConfirmedEmail = true;
        options.Tokens.EmailConfirmationTokenProvider = "EmailConfirmation";
        options.Password.RequiredLength = 8;
        options.Password.RequireNonAlphanumeric = false;
        options.Password.RequireUppercase = false;
        options.Lockout.AllowedForNewUsers = true;
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
    })
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders()
    .AddTokenProvider<EmailConfirmationTokenProvider>("EmailConfirmation")
    .AddSignInManager<AppSignInManager>();
builder.Services.Configure<DataProtectionTokenProviderOptions>(options => options.TokenLifespan = security.PasswordResetTokenLifetime);
builder.Services.Configure<EmailConfirmationTokenProviderOptions>(options => options.TokenLifespan = security.EmailConfirmationTokenLifetime);
// Validate the database security stamp on every request so revoked tickets cannot be replayed.
builder.Services.Configure<SecurityStampValidatorOptions>(options => options.ValidationInterval = TimeSpan.Zero);

builder.Services.AddScoped<IUserClaimsPrincipalFactory<ApplicationUser>, AppClaimsFactory>();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/login";
    options.AccessDeniedPath = "/error/403";
    options.LogoutPath = "/logout";
    options.Cookie.Name = useInMemory ? ".SafeLearnKids.DemoIdentity" : ".SafeLearnKids.Identity";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = development ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
    options.SlidingExpiration = true;
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
});

builder.Services.AddAntiforgery(options =>
{
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.Cookie.SecurePolicy = development ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
});
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("account", context =>
        HttpMethods.IsGet(context.Request.Method) || HttpMethods.IsHead(context.Request.Method)
            ? RateLimitPartition.GetNoLimiter(("read", "read"))
            : RateLimitPartition.GetFixedWindowLimiter(
                // Route aliases must share the same action's per-IP budget.
                (context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    context.GetEndpoint()?.Metadata.GetMetadata<ControllerActionDescriptor>()?.Id ?? "account"),
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = security.RateLimitPermitLimit, Window = TimeSpan.FromSeconds(security.RateLimitWindowSeconds),
                    QueueLimit = 0, AutoReplenishment = true
                }));
    options.OnRejected = async (context, cancellationToken) =>
    {
        context.HttpContext.Response.ContentType = "text/plain; charset=utf-8";
        context.HttpContext.Response.Headers.RetryAfter = security.RateLimitWindowSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture);
        await context.HttpContext.Response.WriteAsync("Bạn thao tác quá nhanh. Vui lòng thử lại sau.", cancellationToken);
    };
});
builder.Services.AddControllersWithViews(options => options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute()));

var app = builder.Build();

app.Logger.LogInformation(
    "Static assets: Environment={Environment}, ContentRoot={ContentRoot}, WebRoot={WebRoot}, ThemeCss={ThemeCss}, AuthCss={AuthCss}, UiJs={UiJs}",
    app.Environment.EnvironmentName,
    app.Environment.ContentRootPath,
    app.Environment.WebRootPath,
    app.Environment.WebRootFileProvider.GetFileInfo("css/theme.css").Exists,
    app.Environment.WebRootFileProvider.GetFileInfo("css/auth.css").Exists,
    app.Environment.WebRootFileProvider.GetFileInfo("js/ui.js").Exists);

// Keep exception details out of HTTP responses, including local account workflows.
app.UseExceptionHandler("/error/500");
if (!development) app.UseHsts();

app.UseStatusCodePagesWithReExecute("/error/{0}");
if (!development) app.UseHttpsRedirection();
app.Use(async (context, next) =>
{
    context.Response.Headers["Referrer-Policy"] = "no-referrer";
    context.Response.Headers.XContentTypeOptions = "nosniff";
    context.Response.Headers.XFrameOptions = "DENY";
    context.Response.Headers.ContentSecurityPolicy = "frame-ancestors 'none'; base-uri 'self'; form-action 'self'; object-src 'none'";
    context.Response.OnStarting(() =>
    {
        if (context.User.Identity?.IsAuthenticated == true) context.Response.Headers.CacheControl = "no-store";
        return Task.CompletedTask;
    });
    await next();
});
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseRateLimiter();
app.UseAuthorization();
app.MapControllers();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    if (useInMemory)
    {
        app.Logger.LogWarning("Database: InMemory demo. Accounts will be lost when the app stops.");
    }
    else
    {
        var connection = db.Database.GetDbConnection();
        await db.Database.OpenConnectionAsync();
        app.Logger.LogInformation("Database: SQL Server={Server}, Database={Database}", connection.DataSource, connection.Database);
        await db.Database.CloseConnectionAsync();
    }

    await DemoSeeder.SeedAsync(scope.ServiceProvider, useInMemory, app.Configuration.GetValue<bool>("Demo:SeedDemoAccounts"));
}

app.Run();
