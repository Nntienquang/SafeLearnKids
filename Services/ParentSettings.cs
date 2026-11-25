using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using SafeLearnKids.Web.Data;
using SafeLearnKids.Web.Models;
using SafeLearnKids.Web.Models.Entities;

namespace SafeLearnKids.Web.Services;

// US-50: one dbo.UserSettings row per account, created on first save. The parent PIN is stored hashed (Identity's
// PBKDF2 hasher); wrong PINs are counted per account and lock the keypad for a few minutes.
public sealed class ParentSettings(IServiceProvider services, IPasswordHasher<ApplicationUser> hasher, IMemoryCache cache)
{
    public const int MaxFailures = 5;
    public static readonly TimeSpan LockFor = TimeSpan.FromMinutes(5);

    private SafeLearnDbContext? Db => services.GetService<SafeLearnDbContext>();

    public async Task<UserSetting?> FindAsync(string userId, CancellationToken cancellationToken) =>
        Db is { } db ? await db.UserSettings.AsNoTracking().FirstOrDefaultAsync(item => item.UserId == userId, cancellationToken) : null;

    public async Task<SettingsForm> FormAsync(string userId, CancellationToken cancellationToken)
    {
        var row = await FindAsync(userId, cancellationToken);
        // Same defaults as the table's DEFAULT constraints.
        return row is null ? new SettingsForm { Theme = ThemeChoice.Auto } : new SettingsForm
        {
            Theme = ThemeChoice.All.Contains(row.Theme) ? row.Theme : ThemeChoice.Auto,
            WeeklyEmail = row.WeeklyEmail, NotifyTimeRequest = row.NotifyTimeRequest, NotifyContentReport = row.NotifyContentReport
        };
    }

    public async Task<bool> SaveAsync(string userId, Action<UserSetting> change, CancellationToken cancellationToken)
    {
        if (Db is not { } db) return false;
        var row = await db.UserSettings.FirstOrDefaultAsync(item => item.UserId == userId, cancellationToken);
        if (row is null)
        {
            row = new UserSetting { UserId = userId, Theme = ThemeChoice.Auto, Language = "vi", WeeklyEmail = true, NotifyTimeRequest = true, NotifyContentReport = true };
            db.UserSettings.Add(row);
        }
        change(row);
        row.UpdatedAt = DateTime.UtcNow;
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException)
        {
            // Created a moment earlier by another tab.
            db.ChangeTracker.Clear();
            return false;
        }
    }

    // A new PIN also clears the exit lock and the failure count.
    public Task<bool> SetPinAsync(ApplicationUser user, string pin, CancellationToken cancellationToken)
    {
        var hash = hasher.HashPassword(user, pin);
        cache.Remove(Key(user.Id));
        return SaveAsync(user.Id, row =>
        {
            row.ParentPinHash = hash;
            row.PinFailedAttempts = 0;
            row.PinLockedUntil = null;
        }, cancellationToken);
    }

    // Minutes left on the exit lock, or null when it is open. Kept in dbo.UserSettings, so a restart or a second
    // server does not reset it; the in-memory count is only the fallback when the app runs without SQL (demo mode).
    public async Task<int?> LockedMinutesAsync(string userId, CancellationToken cancellationToken)
    {
        DateTime? until;
        if (Db is { } db)
            until = await db.UserSettings.AsNoTracking().Where(item => item.UserId == userId).Select(item => item.PinLockedUntil).FirstOrDefaultAsync(cancellationToken);
        else
            until = cache.TryGetValue(Key(userId), out Attempts? attempts) ? attempts!.Until : null;
        var now = DateTime.UtcNow;
        return until is { } end && end > now ? (int)Math.Ceiling((end - now).TotalMinutes) : null;
    }

    public async Task<PinCheck> CheckPinAsync(ApplicationUser user, string? pin, CancellationToken cancellationToken)
    {
        if (await LockedMinutesAsync(user.Id, cancellationToken) is { } minutes) return new(false, 0, minutes);
        var hash = (await FindAsync(user.Id, cancellationToken))?.ParentPinHash;
        var right = hash is not null && pin is { Length: 4 } && hasher.VerifyHashedPassword(user, hash, pin) != PasswordVerificationResult.Failed;
        return await RecordAsync(user.Id, right, cancellationToken);
    }

    // A parent without a PIN (or who forgot it) leaves child mode with the account password; failures count the same way.
    public Task<PinCheck> RecordAsync(string userId, bool right, CancellationToken cancellationToken) =>
        Db is { } db ? (right ? SucceededAsync(db, userId, cancellationToken) : FailedAsync(db, userId, cancellationToken))
            : Task.FromResult(right ? Succeeded(userId) : Failed(userId));

    private static async Task<PinCheck> SucceededAsync(SafeLearnDbContext db, string userId, CancellationToken cancellationToken)
    {
        await db.UserSettings.Where(item => item.UserId == userId && (item.PinFailedAttempts != 0 || item.PinLockedUntil != null))
            .ExecuteUpdateAsync(set => set.SetProperty(item => item.PinFailedAttempts, 0).SetProperty(item => item.PinLockedUntil, (DateTime?)null), cancellationToken);
        return new(true, MaxFailures, null);
    }

    // Atomic in SQL: concurrent wrong tries from two tabs cannot both slip under the limit.
    private async Task<PinCheck> FailedAsync(SafeLearnDbContext db, string userId, CancellationToken cancellationToken)
    {
        if (!await db.UserSettings.AnyAsync(item => item.UserId == userId, cancellationToken))
            await SaveAsync(userId, _ => { }, cancellationToken);
        await db.UserSettings.Where(item => item.UserId == userId)
            .ExecuteUpdateAsync(set => set.SetProperty(item => item.PinFailedAttempts, item => item.PinFailedAttempts + 1), cancellationToken);
        var until = DateTime.UtcNow + LockFor;
        var locked = await db.UserSettings.Where(item => item.UserId == userId && item.PinFailedAttempts >= MaxFailures)
            .ExecuteUpdateAsync(set => set.SetProperty(item => item.PinFailedAttempts, 0).SetProperty(item => item.PinLockedUntil, until), cancellationToken);
        if (locked > 0) return new(false, 0, (int)LockFor.TotalMinutes);
        var failures = await db.UserSettings.AsNoTracking().Where(item => item.UserId == userId).Select(item => item.PinFailedAttempts).FirstAsync(cancellationToken);
        return new(false, Math.Max(1, MaxFailures - failures), null);
    }

    private PinCheck Succeeded(string userId)
    {
        cache.Remove(Key(userId));
        return new(true, MaxFailures, null);
    }

    private PinCheck Failed(string userId)
    {
        var attempts = cache.GetOrCreate(Key(userId), entry =>
        {
            entry.SlidingExpiration = TimeSpan.FromHours(1);
            return new Attempts();
        })!;
        lock (attempts)
        {
            attempts.Failures++;
            if (attempts.Failures < MaxFailures) return new(false, MaxFailures - attempts.Failures, null);
            attempts.Failures = 0;
            attempts.Until = DateTime.UtcNow + LockFor;
            return new(false, 0, (int)LockFor.TotalMinutes);
        }
    }

    private static string Key(string userId) => "pin-fail:" + userId;

    private sealed class Attempts
    {
        public int Failures { get; set; }
        public DateTime? Until { get; set; }
    }

    public sealed record PinCheck(bool Ok, int Left, int? LockedMinutes);
}
