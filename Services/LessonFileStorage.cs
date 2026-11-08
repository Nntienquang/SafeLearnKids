namespace SafeLearnKids.Web.Services;

public sealed class LessonFileStorage(IWebHostEnvironment environment)
{
    public const int MaxBytes = 5 * 1024 * 1024;
    public const int MaxRequestBytes = MaxBytes + 64 * 1024;
    public const string Rejected = "Chọn tệp PDF (tài liệu) hoặc ảnh JPG, PNG, WebP (hình ảnh), tối đa 5 MB.";
    private const string PublicPath = "/uploads/lessons/";

    private string Directory => Path.Combine(environment.WebRootPath, "uploads", "lessons");

    // Returns the public URL, or null when the bytes do not match the content kind.
    public async Task<string?> SaveAsync(IFormFile? file, string kind, CancellationToken cancellationToken)
    {
        if (file is null || file.Length is 0 or > MaxBytes) return null;
        using var buffer = new MemoryStream();
        await file.CopyToAsync(buffer, cancellationToken);
        var bytes = buffer.ToArray();
        // Trust the bytes only: the browser's file name and type are attacker-controlled.
        var extension = kind switch
        {
            "Document" => bytes.AsSpan().StartsWith("%PDF-"u8) ? ".pdf" : null,
            "Image" => ImageExtension(bytes),
            _ => null
        };
        if (extension is null) return null;
        System.IO.Directory.CreateDirectory(Directory);
        var name = Guid.NewGuid().ToString("N") + extension;
        await File.WriteAllBytesAsync(Path.Combine(Directory, name), bytes, cancellationToken);
        return PublicPath + name;
    }

    public void Delete(string? url)
    {
        if (url?.StartsWith(PublicPath, StringComparison.Ordinal) == true)
            File.Delete(Path.Combine(Directory, Path.GetFileName(url)));
    }

    private static string? ImageExtension(ReadOnlySpan<byte> bytes)
    {
        if (bytes.StartsWith<byte>([0xFF, 0xD8, 0xFF])) return ".jpg";
        if (bytes.StartsWith<byte>([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A])) return ".png";
        if (bytes.Length > 12 && bytes.StartsWith("RIFF"u8) && bytes[8..12].SequenceEqual("WEBP"u8)) return ".webp";
        return null;
    }
}
