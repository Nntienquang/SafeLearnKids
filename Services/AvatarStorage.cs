using Microsoft.AspNetCore.Identity;
using SafeLearnKids.Web.Data;

namespace SafeLearnKids.Web.Services;

public sealed class AvatarStorage(IWebHostEnvironment environment, UserManager<ApplicationUser> users)
{
    public const int MaxBytes = 2 * 1024 * 1024;
    public const int MaxRequestBytes = MaxBytes * 2;
    public const string Rejected = "Chọn ảnh JPG, PNG hoặc WebP, tối đa 2 MB.";
    private const string PublicPath = "/uploads/avatars/";

    private string Directory => Path.Combine(environment.WebRootPath, "uploads", "avatars");

    public async Task<bool> AcceptsAsync(IFormFile? file, CancellationToken cancellationToken) =>
        await ReadImageAsync(file, cancellationToken) is not null;

    public async Task<bool> ReplaceAsync(ApplicationUser user, IFormFile? file, CancellationToken cancellationToken)
    {
        if (await ReadImageAsync(file, cancellationToken) is not { } image) return false;

        System.IO.Directory.CreateDirectory(Directory);
        var name = Guid.NewGuid().ToString("N") + image.Extension;
        await File.WriteAllBytesAsync(Path.Combine(Directory, name), image.Bytes, cancellationToken);
        var previous = user.AvatarUrl;
        user.AvatarUrl = PublicPath + name;
        if ((await users.UpdateAsync(user)).Succeeded)
        {
            Delete(previous);
            return true;
        }
        Delete(user.AvatarUrl);
        user.AvatarUrl = previous;
        return false;
    }

    public async Task<bool> RemoveAsync(ApplicationUser user)
    {
        var previous = user.AvatarUrl;
        user.AvatarUrl = null;
        if (!(await users.UpdateAsync(user)).Succeeded) return false;
        Delete(previous);
        return true;
    }

    public void Delete(string? url)
    {
        if (url?.StartsWith(PublicPath, StringComparison.Ordinal) == true)
            File.Delete(Path.Combine(Directory, Path.GetFileName(url)));
    }

    private static async Task<(byte[] Bytes, string Extension)?> ReadImageAsync(IFormFile? file, CancellationToken cancellationToken)
    {
        if (file is null || file.Length is 0 or > MaxBytes) return null;
        using var buffer = new MemoryStream();
        await file.CopyToAsync(buffer, cancellationToken);
        var bytes = buffer.ToArray();
        // Trust the bytes only: the browser's file name and type are attacker-controlled.
        return ImageExtension(bytes) is { } extension ? (bytes, extension) : null;
    }

    private static string? ImageExtension(ReadOnlySpan<byte> bytes)
    {
        if (bytes.StartsWith<byte>([0xFF, 0xD8, 0xFF])) return ".jpg";
        if (bytes.StartsWith<byte>([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A])) return ".png";
        if (bytes.Length > 12 && bytes.StartsWith("RIFF"u8) && bytes[8..12].SequenceEqual("WEBP"u8)) return ".webp";
        return null;
    }
}
