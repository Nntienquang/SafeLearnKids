using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SafeLearnKids.Web.Data;
using SafeLearnKids.Web.Models;
using SafeLearnKids.Web.Services;

namespace SafeLearnKids.Web.Controllers;

[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[RequestSizeLimit(16_384)]
[EnableRateLimiting("account")]
public class AccountController(
    UserManager<ApplicationUser> users,
    SignInManager<ApplicationUser> signIn,
    ApplicationDbContext db,
    AccountEmailService email,
    AccountActivityLog activity) : Controller
{
    private const string LoginFailure = "Không thể đăng nhập. Hãy kiểm tra email, mật khẩu hoặc thử lại sau.";
    private const string InvalidLink = "Liên kết không hợp lệ, đã được dùng hoặc đã hết hạn. Hãy yêu cầu liên kết mới.";
    private static readonly ApplicationUser DummyUser = new();
    private static readonly PasswordHasher<ApplicationUser> DummyHasher = new();
    private static readonly string DummyHash = DummyHasher.HashPassword(DummyUser, Guid.NewGuid().ToString());

    private static void DummyPasswordCheck(string password) => DummyHasher.VerifyHashedPassword(DummyUser, DummyHash, password);

    [AllowAnonymous, HttpGet("login")]
    public IActionResult Login(string? returnUrl = null) => View(new LoginViewModel
    {
        ReturnUrl = returnUrl is not null && Url.IsLocalUrl(returnUrl) ? returnUrl : null
    });

    [AllowAnonymous, HttpPost("login")]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid) return View(model);
        var email = model.Email.Trim();
        var user = await users.FindByEmailAsync(email);
        if (user is not null && await signIn.CanSignInAsync(user) && !await users.IsLockedOutAsync(user))
        {
            Microsoft.AspNetCore.Identity.SignInResult result;
            try { result = await signIn.PasswordSignInAsync(user, model.Password, model.RememberMe, lockoutOnFailure: true); }
            catch (FormatException) { DummyPasswordCheck(model.Password); result = Microsoft.AspNetCore.Identity.SignInResult.Failed; }
            if (result.Succeeded)
            {
                user.LastLoginAt = DateTime.UtcNow;
                await users.UpdateAsync(user);
                await activity.LoginAsync(user.Id, email, true, null);
                if (model.ReturnUrl is not null && Url.IsLocalUrl(model.ReturnUrl)) return LocalRedirect(model.ReturnUrl);
                return Redirect(HomeUrl(await users.IsInRoleAsync(user, DemoSeeder.AdminRole), await users.IsInRoleAsync(user, DemoSeeder.ParentRole)));
            }
            await activity.LoginAsync(user.Id, email, false, result.IsLockedOut ? "LockedOut" : "InvalidCredentials");
        }
        else
        {
            DummyPasswordCheck(model.Password);
            await activity.LoginAsync(user?.Id, email, false, await BlockedReasonAsync(user));
        }
        ModelState.AddModelError(string.Empty, LoginFailure);
        return View(model);
    }

    [AllowAnonymous, HttpGet("register")]
    public IActionResult Register() => View(new RegisterViewModel());

    [AllowAnonymous, HttpPost("register")]
    public async Task<IActionResult> Register(RegisterViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return View(model);
        var fullName = model.FullName.Trim();
        if (fullName.Length < 2)
        {
            ModelState.AddModelError(nameof(model.FullName), "Họ và tên cần từ 2 đến 100 ký tự.");
            return View(model);
        }
        const int maxAttempts = 3;
        for (var attempt = 1; ; attempt++)
        {
            // SQL rollback leaves EF state behind; retry User + Parent with a fresh user.
            var user = new ApplicationUser { UserName = model.Email.Trim(), Email = model.Email.Trim(), FullName = fullName };
            try
            {
                // Dispose/rollback before catch clears EF state or retries.
                await using var transaction = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync(cancellationToken) : null;
                var result = await users.CreateAsync(user, model.Password);
                if (!result.Succeeded)
                {
                    // A duplicate address receives the same response as a new registration.
                    if (result.Errors.All(error => error.Code is "DuplicateUserName" or "DuplicateEmail"))
                        return RedirectToAction(nameof(CheckEmail));
                    AddPasswordErrors(result);
                    return View(model);
                }
                var roleResult = await users.AddToRoleAsync(user, DemoSeeder.ParentRole);
                if (!roleResult.Succeeded)
                {
                    if (transaction is null) await users.DeleteAsync(user);
                    ModelState.AddModelError(string.Empty, "Chưa thể tạo tài khoản. Vui lòng thử lại sau.");
                    return View(model);
                }
                if (transaction is not null) await transaction.CommitAsync(cancellationToken);
            }
            catch (Exception exception) when (IsSqlError(exception, 2601, 2627))
            {
                // Another registration won after Identity's duplicate check.
                db.ChangeTracker.Clear();
                return RedirectToAction(nameof(CheckEmail));
            }
            catch (Exception exception) when (IsSqlError(exception, 1205))
            {
                db.ChangeTracker.Clear();
                if (attempt >= maxAttempts)
                {
                    Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
                    ModelState.AddModelError(string.Empty, "Chưa thể tạo tài khoản. Vui lòng thử lại sau.");
                    return View(model);
                }
                await Task.Delay(TimeSpan.FromMilliseconds(Random.Shared.Next(100, 301) * attempt), cancellationToken);
                continue;
            }
            await activity.TermsAcceptedAsync(user.Id);
            // Queue email after commit, outside the transaction retry.
            await email.SendConfirmationAsync(user, cancellationToken);
            return RedirectToAction(nameof(CheckEmail));
        }
    }

    [AllowAnonymous, HttpGet("check-email")]
    public IActionResult CheckEmail() => View(new ForgotPasswordViewModel());

    [AllowAnonymous, HttpPost("resend-confirmation")]
    public async Task<IActionResult> ResendConfirmation(ForgotPasswordViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return View("CheckEmail", model);
        var user = await users.FindByEmailAsync(model.Email.Trim());
        if (user is { IsActive: true, EmailConfirmed: false }) await email.SendConfirmationAsync(user, cancellationToken);
        return RedirectToAction(nameof(CheckEmail));
    }

    [AllowAnonymous, HttpGet("confirm-email")]
    public IActionResult ConfirmEmail(string? userId, string? code)
    {
        // GET is a preview: mail scanners must not activate an account.
        return View(new EmailConfirmationViewModel { UserId = userId ?? string.Empty, Code = code ?? string.Empty });
    }

    [AllowAnonymous, HttpPost("confirm-email")]
    public async Task<IActionResult> ConfirmEmail(EmailConfirmationViewModel model)
    {
        if (ModelState.IsValid)
        {
            var user = await users.FindByIdAsync(model.UserId);
            var token = AccountEmailService.Decode(model.Code);
            if (user is { IsActive: true, EmailConfirmed: false } && token is not null)
            {
                var result = await users.ConfirmEmailAsync(user, token);
                if (result.Succeeded)
                {
                    TempData["Info"] = "Đã xác minh email. Bạn có thể đăng nhập.";
                    return RedirectToAction(nameof(Login));
                }
            }
        }
        ModelState.Clear();
        ModelState.AddModelError(string.Empty, InvalidLink);
        return View(model);
    }

    [Authorize, HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        var user = await users.GetUserAsync(User);
        if (user is not null && !(await users.UpdateSecurityStampAsync(user)).Succeeded)
            return StatusCode(503, "Chưa thể kết thúc phiên. Vui lòng thử lại.");
        await signIn.SignOutAsync();
        return RedirectToAction(nameof(Login));
    }

    [AllowAnonymous, HttpGet("recover")]
    public IActionResult ForgotPassword() => View(new ForgotPasswordViewModel());

    [AllowAnonymous, HttpPost("recover")]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return View(model);
        var user = await users.FindByEmailAsync(model.Email.Trim());
        if (user is { IsActive: true, EmailConfirmed: true }) await email.SendPasswordResetAsync(user, cancellationToken);
        ViewData["Sent"] = true;
        return View(model);
    }

    [AllowAnonymous, HttpGet("reset-password")]
    public IActionResult ResetPassword(string? email, string? token)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(token)) return RedirectToAction(nameof(ForgotPassword));
        return View(new ResetPasswordViewModel { Email = email, Token = token });
    }

    [AllowAnonymous, HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
    {
        if (!ModelState.IsValid) return View(model);
        var user = await users.FindByEmailAsync(model.Email.Trim());
        var token = AccountEmailService.Decode(model.Token);
        if (user is { IsActive: true, EmailConfirmed: true } && token is not null)
        {
            var result = await users.ResetPasswordAsync(user, token, model.Password);
            if (result.Succeeded)
            {
                await signIn.SignOutAsync();
                TempData["Info"] = "Đã đặt lại mật khẩu. Hãy đăng nhập lại.";
                return RedirectToAction(nameof(Login));
            }
            if (result.Errors.Any(error => error.Code.StartsWith("Password", StringComparison.Ordinal)))
            {
                AddPasswordErrors(result);
                return View(model);
            }
        }
        ModelState.AddModelError(string.Empty, InvalidLink);
        return View(model);
    }

    [Authorize, HttpGet("change-password")]
    public IActionResult ChangePassword() => View(new ChangePasswordViewModel());

    [Authorize, HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
    {
        if (!ModelState.IsValid) return View(model);
        var user = await users.GetUserAsync(User);
        if (user is null) return Challenge();
        var result = await users.ChangePasswordAsync(user, model.CurrentPassword, model.Password);
        if (!result.Succeeded)
        {
            if (result.Errors.Any(error => error.Code == "PasswordMismatch"))
                ModelState.AddModelError(nameof(model.CurrentPassword), "Mật khẩu hiện tại chưa đúng.");
            else AddPasswordErrors(result);
            return View(model);
        }
        await signIn.SignOutAsync();
        TempData["Info"] = "Đã đổi mật khẩu và kết thúc các phiên đăng nhập. Hãy đăng nhập lại.";
        return RedirectToAction(nameof(Login));
    }

    private static string HomeUrl(bool admin, bool parent) => admin ? "/admin-home" : parent ? "/parent-home" : "/";

    private async Task<string> BlockedReasonAsync(ApplicationUser? user)
    {
        if (user is null) return "InvalidCredentials";
        if (!user.IsActive) return "Disabled";
        if (await users.IsLockedOutAsync(user)) return "LockedOut";
        return user.EmailConfirmed ? "InvalidCredentials" : "EmailNotConfirmed";
    }

    private static bool IsSqlError(Exception exception, params int[] numbers)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is SqlException sqlException && sqlException.Errors.Cast<SqlError>().Any(error => numbers.Contains(error.Number)))
                return true;
        }
        return false;
    }

    private void AddPasswordErrors(IdentityResult result)
    {
        foreach (var error in result.Errors)
            ModelState.AddModelError(error.Code.StartsWith("Password", StringComparison.Ordinal) ? "Password" : string.Empty,
                error.Code.StartsWith("Password", StringComparison.Ordinal)
                    ? "Mật khẩu cần từ 8 đến 100 ký tự, có chữ thường và chữ số."
                    : "Chưa thể xử lý yêu cầu. Vui lòng thử lại sau.");
    }
}
