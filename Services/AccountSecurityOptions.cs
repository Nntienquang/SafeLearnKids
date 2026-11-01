namespace SafeLearnKids.Web.Services;

public sealed class AccountSecurityOptions
{
    public TimeSpan PasswordResetTokenLifetime { get; set; } = TimeSpan.FromMinutes(30);
    public TimeSpan EmailConfirmationTokenLifetime { get; set; } = TimeSpan.FromHours(24);
    public int RateLimitPermitLimit { get; set; } = 10;
    public int RateLimitWindowSeconds { get; set; } = 60;
}
