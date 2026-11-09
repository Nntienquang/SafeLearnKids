using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace SafeLearnKids.Web.Services;

// Turns a pasted link into the (Platform, ExternalId) pair that dbo.ContentSources keys on.
public static partial class SourceUrl
{
    public const string Unsupported = "Hãy dán liên kết http(s) của kênh YouTube dạng youtube.com/channel/UC..., danh sách phát có list=..., hoặc một website.";
    public const string Handle = "Liên kết dạng @tên-kênh chưa dùng được. Hãy mở kênh, chọn Chia sẻ → Sao chép mã kênh, rồi dán youtube.com/channel/UC...";

    public static (string Platform, string ExternalId, string Url)? Parse(string text, out string? problem)
    {
        problem = Unsupported;
        if (!Uri.TryCreate(text.Trim(), UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https") || string.IsNullOrEmpty(uri.Host)
            || !string.IsNullOrEmpty(uri.UserInfo))
            return null;
        var host = uri.Host.ToLowerInvariant();
        var youTube = host is "youtube.com" or "www.youtube.com" or "m.youtube.com" or "music.youtube.com";
        if (youTube)
        {
            var list = System.Web.HttpUtility.ParseQueryString(uri.Query)["list"];
            if (list is not null && PlaylistId().IsMatch(list))
            {
                problem = null;
                return ("YouTubePlaylist", list, $"https://www.youtube.com/playlist?list={list}");
            }
            var channel = ChannelPath().Match(uri.AbsolutePath);
            if (channel.Success)
            {
                problem = null;
                return ("YouTubeChannel", channel.Groups[1].Value, $"https://www.youtube.com/channel/{channel.Groups[1].Value}");
            }
            problem = uri.AbsolutePath.StartsWith("/@", StringComparison.Ordinal) ? Handle : Unsupported;
            return null;
        }
        if (host == "youtu.be") return null;
        // Websites are listed for review only; the app never fetches them.
        var key = (host + uri.AbsolutePath.TrimEnd('/')).ToLowerInvariant();
        if (key.Length > 64) key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key))).ToLowerInvariant();
        var url = uri.GetLeftPart(UriPartial.Path);
        if (url.Length > 300) return null;
        problem = null;
        return ("Website", key, url);
    }

    public const string NotVideo = "Hãy dán liên kết của một video YouTube, ví dụ youtube.com/watch?v=..., youtu.be/... hoặc youtube.com/shorts/...";

    // A single video: watch?v=, youtu.be/, shorts/, embed/, live/ links, or the bare 11-character id. Returns the id or null.
    public static string? ParseVideo(string? text)
    {
        text = text?.Trim();
        if (string.IsNullOrEmpty(text)) return null;
        if (VideoId().IsMatch(text)) return text;
        if (!Uri.TryCreate(text, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https") || !string.IsNullOrEmpty(uri.UserInfo)) return null;
        var host = uri.Host.ToLowerInvariant();
        foreach (var prefix in new[] { "www.", "m.", "music." })
            if (host.StartsWith(prefix, StringComparison.Ordinal)) host = host[prefix.Length..];
        var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        string? id = host switch
        {
            "youtu.be" => segments.FirstOrDefault(),
            "youtube.com" or "youtube-nocookie.com" when uri.AbsolutePath == "/watch" => System.Web.HttpUtility.ParseQueryString(uri.Query)["v"],
            "youtube.com" or "youtube-nocookie.com" when segments.Length >= 2 && segments[0] is "shorts" or "embed" or "live" or "v" => segments[1],
            _ => null
        };
        return id is not null && VideoId().IsMatch(id) ? id : null;
    }

    [GeneratedRegex("^[A-Za-z0-9_-]{10,64}$")]
    public static partial Regex PlaylistId();

    [GeneratedRegex("^UC[A-Za-z0-9_-]{22}$")]
    public static partial Regex ChannelId();

    [GeneratedRegex("^/channel/(UC[A-Za-z0-9_-]{22})/?")]
    private static partial Regex ChannelPath();

    [GeneratedRegex("^[A-Za-z0-9_-]{11}$")]
    public static partial Regex VideoId();

    // Ids allowed into an embed URL: plain characters only, a little looser than YouTube's 11 for older stored rows.
    [GeneratedRegex("^[A-Za-z0-9_-]{6,20}$")]
    public static partial Regex EmbedId();
}
