using Microsoft.EntityFrameworkCore;
using SafeLearnKids.Web.Data;
using SafeLearnKids.Web.Models.Entities;

namespace SafeLearnKids.Web.Services;

// Business tables exist only on SQL, so InMemory demo runs skip these records.
public sealed class AccountActivityLog(
    IServiceProvider services,
    IHttpContextAccessor accessor,
    ILogger<AccountActivityLog> logger)
{
    public const string UserEntity = "User";
    private const string TermsVersion = "v1.0";

    public Task LoginAsync(string? userId, string email, bool succeeded, string? failReason) => SaveAsync(db =>
        db.LoginHistories.Add(new LoginHistory
        {
            UserId = userId,
            UserNameTried = Limit(email, 256)!,
            Succeeded = succeeded,
            FailReason = failReason,
            IpAddress = IpAddress,
            UserAgent = Limit(accessor.HttpContext?.Request.Headers.UserAgent.ToString(), 300),
            CreatedAt = DateTime.UtcNow
        }));

    public Task TermsAcceptedAsync(string userId) => SaveAsync(db =>
    {
        var now = DateTime.UtcNow;
        db.UserConsents.Add(new UserConsent
        {
            UserId = userId,
            ConsentType = "Terms",
            Version = TermsVersion,
            Decision = "Accepted",
            AcceptedAt = now,
            DecidedAt = now,
            IpAddress = IpAddress
        });
    });

    public Task AdminActionAsync(string actorId, string action, string? targetUserId, string? detail = null) => SaveAsync(db =>
        db.AuditLogs.Add(new AuditLog
        {
            ActorId = actorId,
            Action = action,
            EntityType = UserEntity,
            EntityId = Limit(targetUserId, 64),
            Detail = detail,
            IpAddress = IpAddress,
            CreatedAt = DateTime.UtcNow
        }));

    private string? IpAddress => Limit(accessor.HttpContext?.Connection.RemoteIpAddress?.ToString(), 45);

    private static string? Limit(string? value, int length) =>
        value?.Length > length ? value[..length] : value;

    private async Task SaveAsync(Action<SafeLearnDbContext> record)
    {
        var db = services.GetService<SafeLearnDbContext>();
        if (db is null) return;
        try
        {
            record(db);
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException exception)
        {
            // A logging fault must not undo a completed account action.
            db.ChangeTracker.Clear();
            logger.LogWarning(exception, "Could not save account activity.");
        }
    }
}
