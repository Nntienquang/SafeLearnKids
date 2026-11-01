using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace SafeLearnKids.Web.Models;

public class MustBeTrueAttribute : ValidationAttribute, IClientModelValidator
{
    public MustBeTrueAttribute()
    {
        ErrorMessage = "Hãy xác nhận bạn là phụ huynh và đồng ý điều khoản.";
    }

    public override bool IsValid(object? value)
    {
        return value is true;
    }

    public void AddValidation(ClientModelValidationContext context)
    {
        MergeAttribute(context.Attributes, "data-val", "true");
        MergeAttribute(context.Attributes, "data-val-mustbetrue", FormatErrorMessage(context.ModelMetadata.GetDisplayName()));
    }

    private static bool MergeAttribute(IDictionary<string, string> attributes, string key, string value)
    {
        if (attributes.ContainsKey(key)) return false;
        attributes.Add(key, value);
        return true;
    }
}

public record AvatarViewModel(string? Url, string Name, string Size, string Background)
{
    public string Initial => Name.Split(' ', StringSplitOptions.RemoveEmptyEntries)
        .LastOrDefault(word => char.IsLetter(word[0]))?.Substring(0, 1).ToUpper() ?? "?";
}

public class LoginViewModel
{
    [Required(ErrorMessage = "Nhập email của bạn.")]
    [EmailAddress(ErrorMessage = "Email chưa đúng định dạng.")]
    [StringLength(256, ErrorMessage = "Email không được vượt quá 256 ký tự.")]
    [Display(Name = "Email")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Nhập mật khẩu.")]
    [DataType(DataType.Password)]
    [Display(Name = "Mật khẩu")]
    [StringLength(100, ErrorMessage = "Mật khẩu không được vượt quá 100 ký tự.")]
    public string Password { get; set; } = string.Empty;

    [Display(Name = "Ghi nhớ trên máy này")]
    public bool RememberMe { get; set; }

    public string? ReturnUrl { get; set; }
}

public class RegisterViewModel
{
    [Required(ErrorMessage = "Nhập họ và tên.")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "Họ và tên cần từ 2 đến 100 ký tự.")]
    [Display(Name = "Họ và tên")]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Nhập email.")]
    [EmailAddress(ErrorMessage = "Email chưa đúng định dạng.")]
    [StringLength(256, ErrorMessage = "Email không được vượt quá 256 ký tự.")]
    [Display(Name = "Email")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Nhập mật khẩu.")]
    [StringLength(100, MinimumLength = 8, ErrorMessage = "Mật khẩu cần từ 8 đến 100 ký tự.")]
    [DataType(DataType.Password)]
    [Display(Name = "Mật khẩu")]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "Nhập lại mật khẩu.")]
    [DataType(DataType.Password)]
    [Display(Name = "Nhập lại mật khẩu")]
    [Compare(nameof(Password), ErrorMessage = "Hai mật khẩu chưa khớp nhau.")]
    public string ConfirmPassword { get; set; } = string.Empty;

    [MustBeTrue(ErrorMessage = "Hãy xác nhận bạn là phụ huynh và đồng ý điều khoản.")]
    public bool Agree { get; set; }
}

public class ForgotPasswordViewModel
{
    [Required(ErrorMessage = "Nhập email đã đăng ký.")]
    [EmailAddress(ErrorMessage = "Email chưa đúng định dạng.")]
    [StringLength(256, ErrorMessage = "Email không được vượt quá 256 ký tự.")]
    [Display(Name = "Email")]
    public string Email { get; set; } = string.Empty;
}

public class ResetPasswordViewModel
{
    [Required(ErrorMessage = "Nhập email.")]
    [EmailAddress(ErrorMessage = "Email chưa đúng định dạng.")]
    [StringLength(256, ErrorMessage = "Email không được vượt quá 256 ký tự.")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Liên kết không hợp lệ. Hãy yêu cầu liên kết mới.")]
    [StringLength(8192, ErrorMessage = "Liên kết không hợp lệ. Hãy yêu cầu liên kết mới.")]
    public string Token { get; set; } = string.Empty;

    [Required(ErrorMessage = "Nhập mật khẩu mới.")]
    [StringLength(100, MinimumLength = 8, ErrorMessage = "Mật khẩu mới cần từ 8 đến 100 ký tự.")]
    [DataType(DataType.Password)]
    [Display(Name = "Mật khẩu mới")]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "Nhập lại mật khẩu mới.")]
    [DataType(DataType.Password)]
    [Display(Name = "Nhập lại mật khẩu mới")]
    [Compare(nameof(Password), ErrorMessage = "Hai mật khẩu chưa khớp nhau.")]
    public string ConfirmPassword { get; set; } = string.Empty;
}

public class ChangePasswordViewModel
{
    [Required(ErrorMessage = "Nhập mật khẩu hiện tại.")]
    [DataType(DataType.Password)]
    [StringLength(100, ErrorMessage = "Mật khẩu hiện tại không được vượt quá 100 ký tự.")]
    [Display(Name = "Mật khẩu hiện tại")]
    public string CurrentPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "Nhập mật khẩu mới.")]
    [StringLength(100, MinimumLength = 8, ErrorMessage = "Mật khẩu mới cần từ 8 đến 100 ký tự.")]
    [DataType(DataType.Password)]
    [Display(Name = "Mật khẩu mới")]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "Nhập lại mật khẩu mới.")]
    [DataType(DataType.Password)]
    [Compare(nameof(Password), ErrorMessage = "Hai mật khẩu chưa khớp nhau.")]
    [Display(Name = "Nhập lại mật khẩu mới")]
    public string ConfirmPassword { get; set; } = string.Empty;
}

public class EmailConfirmationViewModel
{
    [Required(ErrorMessage = "Liên kết không hợp lệ. Hãy yêu cầu liên kết mới.")]
    [StringLength(450, ErrorMessage = "Liên kết không hợp lệ. Hãy yêu cầu liên kết mới.")]
    public string UserId { get; set; } = string.Empty;

    [Required(ErrorMessage = "Liên kết không hợp lệ. Hãy yêu cầu liên kết mới.")]
    [StringLength(8192, ErrorMessage = "Liên kết không hợp lệ. Hãy yêu cầu liên kết mới.")]
    public string Code { get; set; } = string.Empty;
}
