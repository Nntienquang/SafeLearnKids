using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using SafeLearnKids.Web.Data;

namespace SafeLearnKids.Web.Services;

public sealed class AccountEmailOptions
{
    public string PublicBaseUrl { get; set; } = string.Empty;
    public string DeliveryMethod { get; set; } = "Smtp";
    public string FromAddress { get; set; } = "no-reply@safelearnkids.local";
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 587;
    public string? UserName { get; set; }
    public string? Password { get; set; }
    public string? PickupDirectory { get; set; }
}

public sealed class AccountEmailService(
    UserManager<ApplicationUser> users,
    IOptions<AccountEmailOptions> options,
    AccountEmailQueue queue)
{
    public async Task SendConfirmationAsync(ApplicationUser user, CancellationToken cancellationToken)
    {
        var token = await users.GenerateEmailConfirmationTokenAsync(user);
        var url = Link("/confirm-email", new Dictionary<string, string?>
        {
            ["userId"] = user.Id, ["code"] = Encode(token)
        });
        await SendAsync(user.Email!, "Xác minh tài khoản SafeLearn Kids",
            $"Để xác minh email, hãy mở liên kết và chọn Xác minh tài khoản:\n{url}\n\nNếu bạn không đăng ký, hãy bỏ qua email này.", cancellationToken);
    }

    public async Task SendInvitationAsync(ApplicationUser user, CancellationToken cancellationToken)
    {
        var token = await users.GenerateEmailConfirmationTokenAsync(user);
        var url = Link("/confirm-email", new Dictionary<string, string?>
        {
            ["userId"] = user.Id, ["code"] = Encode(token)
        });
        await SendAsync(user.Email!, "Lời mời tham gia SafeLearn Kids",
            $"Quản trị viên đã tạo tài khoản SafeLearn Kids cho bạn. Hãy mở liên kết, chọn Xác minh tài khoản rồi đặt mật khẩu:\n{url}\n\nNếu bạn không mong đợi lời mời này, hãy bỏ qua email.", cancellationToken);
    }

    public async Task<string> PasswordSetupTokenAsync(ApplicationUser user) =>
        Encode(await users.GeneratePasswordResetTokenAsync(user));

    public async Task SendPasswordResetAsync(ApplicationUser user, CancellationToken cancellationToken)
    {
        var url = Link("/reset-password", new Dictionary<string, string?>
        {
            ["email"] = user.Email, ["token"] = await PasswordSetupTokenAsync(user)
        });
        await SendAsync(user.Email!, "Đặt lại mật khẩu SafeLearn Kids",
            $"Để đặt lại mật khẩu, hãy mở liên kết:\n{url}\n\nNếu bạn không yêu cầu, hãy bỏ qua email này.", cancellationToken);
    }

    private string Link(string path, Dictionary<string, string?> parameters) =>
        QueryHelpers.AddQueryString(options.Value.PublicBaseUrl.TrimEnd('/') + path, parameters);

    private static string Encode(string token) => WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));

    public static string? Decode(string code)
    {
        try { return Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(code)); }
        catch (FormatException) { return null; }
    }

    private Task SendAsync(string recipient, string subject, string body, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        queue.Enqueue(recipient, subject, body);
        return Task.CompletedTask;
    }
}
