using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using SafeLearnKids.Web.Data;
using SafeLearnKids.Web.Models;

namespace SafeLearnKids.Web.Controllers;

public class AccountController : Controller
{
    private readonly UserManager<ApplicationUser> _users;
    private readonly SignInManager<ApplicationUser> _signIn;
    private readonly IWebHostEnvironment _env;

    public AccountController(UserManager<ApplicationUser> users, SignInManager<ApplicationUser> signIn, IWebHostEnvironment env)
    {
        _users = users;
        _signIn = signIn;
        _env = env;
    }

    // ---------------- Login ----------------
    [AllowAnonymous]
    [HttpGet("login")]
    public IActionResult Login(string? returnUrl = null)
    {
        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    [AllowAnonymous]
    [HttpPost("login")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var user = await _users.FindByEmailAsync(model.Email);
        if (user is null)
        {
            ModelState.AddModelError(string.Empty, "Email hoặc mật khẩu không đúng.");
            return View(model);
        }

        var result = await _signIn.PasswordSignInAsync(user, model.Password, model.RememberMe, lockoutOnFailure: true);
        if (result.Succeeded)
        {
            if (!string.IsNullOrEmpty(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
            {
                return LocalRedirect(model.ReturnUrl);
            }

            return await _users.IsInRoleAsync(user, DemoSeeder.AdminRole)
                ? Redirect("/admin-home")
                : Redirect("/parent-home");
        }

        if (result.IsLockedOut)
        {
            ModelState.AddModelError(string.Empty, "Tài khoản bị khóa tạm thời 5 phút do nhập sai quá nhiều lần.");
        }
        else
        {
            var left = Math.Max(0, 5 - await _users.GetAccessFailedCountAsync(user));
            ModelState.AddModelError(string.Empty, $"Email hoặc mật khẩu không đúng. Còn {left} lần thử trước khi tài khoản bị khóa tạm thời.");
        }

        return View(model);
    }

    // ---------------- Register ----------------
    [AllowAnonymous]
    [HttpGet("register")]
    public IActionResult Register()
    {
        return View(new RegisterViewModel());
    }

    [AllowAnonymous]
    [HttpPost("register")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var user = new ApplicationUser
        {
            UserName = model.Email,
            Email = model.Email,
            FullName = model.FullName.Trim()
        };

        var result = await _users.CreateAsync(user, model.Password);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                var text = error.Code is "DuplicateUserName" or "DuplicateEmail"
                    ? "Email này đã được đăng ký. Hãy đăng nhập hoặc dùng email khác."
                    : error.Description;
                ModelState.AddModelError(error.Code.StartsWith("Duplicate") ? nameof(model.Email) : string.Empty, text);
            }

            return View(model);
        }

        await _users.AddToRoleAsync(user, DemoSeeder.ParentRole);
        await _signIn.SignInAsync(user, isPersistent: false);
        return Redirect("/onboarding");
    }

    // ---------------- Logout ----------------
    [Authorize]
    [HttpPost("logout")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await _signIn.SignOutAsync();
        return Redirect("/login");
    }

    // ---------------- Forgot / reset password ----------------
    [AllowAnonymous]
    [HttpGet("recover")]
    public IActionResult ForgotPassword()
    {
        return View(new ForgotPasswordViewModel());
    }

    [AllowAnonymous]
    [HttpPost("recover")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        // Same response whether or not the account exists (no account enumeration).
        var user = await _users.FindByEmailAsync(model.Email);
        if (user is not null && _env.IsDevelopment())
        {
            var token = await _users.GeneratePasswordResetTokenAsync(user);
            ViewData["DemoResetLink"] = Url.Action(nameof(ResetPassword), "Account", new { email = model.Email, token }, Request.Scheme);
        }

        ViewData["Sent"] = true;
        return View(model);
    }

    [AllowAnonymous]
    [HttpGet("reset-password")]
    public IActionResult ResetPassword(string? email, string? token)
    {
        if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(token))
        {
            return Redirect("/recover");
        }

        return View(new ResetPasswordViewModel { Email = email, Token = token });
    }

    [AllowAnonymous]
    [HttpPost("reset-password")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var user = await _users.FindByEmailAsync(model.Email);
        if (user is null)
        {
            return Redirect("/login");
        }

        var result = await _users.ResetPasswordAsync(user, model.Token, model.Password);
        if (result.Succeeded)
        {
            TempData["Info"] = "Đã đổi mật khẩu. Hãy đăng nhập lại.";
            return Redirect("/login");
        }

        foreach (var error in result.Errors)
        {
            ModelState.AddModelError(string.Empty, "Liên kết hết hạn hoặc đã được dùng. Hãy yêu cầu liên kết mới.");
            break;
        }

        return View(model);
    }
}
