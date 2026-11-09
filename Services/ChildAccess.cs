using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SafeLearnKids.Web.Data;
using SafeLearnKids.Web.Models.Entities;

namespace SafeLearnKids.Web.Services;

// US-10: the parent's consent to process one child's learning data (dbo.UserConsents, ConsentType ChildDataPolicy).
// One row per parent, child and policy version; withdrawing turns the row to Revoked, agreeing again turns it back to Accepted.
// Without an accepted row the child cannot enter child mode (KidContext checks it on every request).
public static class ChildConsent
{
    public const string Type = "ChildDataPolicy";
    public const string Version = "v1.0";
    public const string Accepted = "Accepted";
    public const string Revoked = "Revoked";

    public static IQueryable<UserConsent> Active(SafeLearnDbContext db, string parentId) =>
        db.UserConsents.Where(item => item.UserId == parentId && item.ConsentType == Type && item.Version == Version
            && item.Decision == Accepted && item.ChildProfileId != null);

    public static async Task<Dictionary<int, DateTime>> GivenAsync(SafeLearnDbContext db, string parentId, CancellationToken cancellationToken) =>
        await Active(db, parentId).ToDictionaryAsync(item => item.ChildProfileId!.Value, item => item.AcceptedAt ?? item.DecidedAt, cancellationToken);

    // Saves the change. The caller has already checked that the child belongs to the parent.
    public static async Task GrantAsync(SafeLearnDbContext db, string parentId, int childId, string? ipAddress, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var row = await Row(db, parentId, childId, cancellationToken);
        if (row is null)
            db.UserConsents.Add(row = new UserConsent { UserId = parentId, ChildProfileId = childId, ConsentType = Type, Version = Version });
        row.Decision = Accepted;
        row.AcceptedAt = now;
        row.RevokedAt = null;
        row.DecidedAt = now;
        row.IpAddress = ipAddress;
        await db.SaveChangesAsync(cancellationToken);
    }

    // False when there was no accepted consent to withdraw.
    public static async Task<bool> RevokeAsync(SafeLearnDbContext db, string parentId, int childId, string? ipAddress, CancellationToken cancellationToken)
    {
        var row = await Row(db, parentId, childId, cancellationToken);
        if (row is not { Decision: Accepted }) return false;
        var now = DateTime.UtcNow;
        row.Decision = Revoked;
        row.RevokedAt = now;
        row.DecidedAt = now;
        row.IpAddress = ipAddress;
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    private static Task<UserConsent?> Row(SafeLearnDbContext db, string parentId, int childId, CancellationToken cancellationToken) =>
        db.UserConsents.FirstOrDefaultAsync(item => item.UserId == parentId && item.ChildProfileId == childId
            && item.ConsentType == Type && item.Version == Version, cancellationToken);
}

// US-08: an optional 4-digit PIN for each child, asked on the "who is learning" screen so brothers and sisters
// cannot open each other's profile. Stored hashed in dbo.ChildProfiles; 5 wrong tries lock that child for 5 minutes.
public static class ChildPins
{
    public const int MaxFailures = 5;
    public static readonly TimeSpan LockFor = TimeSpan.FromMinutes(5);
    private static readonly PasswordHasher<ChildProfile> Hasher = new();

    public sealed record Check(bool Ok, int Left, int? LockedMinutes);

    public static bool Valid(string? pin) => pin is { Length: 4 } && pin.All(char.IsAsciiDigit);

    public static void Set(ChildProfile child, string pin)
    {
        child.PinHash = Hasher.HashPassword(child, pin);
        child.PinChangedAt = DateTime.UtcNow;
        child.PinFailedAttempts = 0;
        child.PinLockedUntil = null;
    }

    public static void Clear(ChildProfile child)
    {
        child.PinHash = null;
        child.PinChangedAt = DateTime.UtcNow;
        child.PinFailedAttempts = 0;
        child.PinLockedUntil = null;
    }

    public static int? LockedMinutes(ChildProfile child, DateTime now) =>
        child.PinLockedUntil is { } until && until > now ? (int)Math.Ceiling((until - now).TotalMinutes) : null;

    // Updates the failure counters on the tracked entity; the caller saves.
    public static Check Verify(ChildProfile child, string? pin)
    {
        if (child.PinHash is null) return new(true, MaxFailures, null);
        var now = DateTime.UtcNow;
        if (LockedMinutes(child, now) is { } minutes) return new(false, 0, minutes);
        if (Valid(pin) && Hasher.VerifyHashedPassword(child, child.PinHash, pin!) != PasswordVerificationResult.Failed)
        {
            child.PinFailedAttempts = 0;
            child.PinLockedUntil = null;
            return new(true, MaxFailures, null);
        }
        child.PinFailedAttempts++;
        if (child.PinFailedAttempts < MaxFailures) return new(false, MaxFailures - child.PinFailedAttempts, null);
        child.PinFailedAttempts = 0;
        child.PinLockedUntil = now + LockFor;
        return new(false, 0, (int)LockFor.TotalMinutes);
    }
}
