using System.Net;
using System.Text.Json;
using System.Xml;
using System.Xml.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SafeLearnKids.Web.Data;
using SafeLearnKids.Web.Models;
using SafeLearnKids.Web.Models.Entities;

namespace SafeLearnKids.Web.Services;

public sealed class YouTubeOptions
{
    // Leave empty to use the public feed; set it with user-secrets or YouTube__ApiKey, never in Git.
    public string? ApiKey { get; set; }
    public string ApiBaseUrl { get; set; } = "https://www.googleapis.com/youtube/v3";
    // YouTube's public Atom feed lists a channel's or playlist's latest 15 videos without a key.
    public string FeedBaseUrl { get; set; } = "https://www.youtube.com/feeds/videos.xml";
    public int MaxVideosPerSync { get; set; } = 200;
    // Without a key, a single video's title comes from YouTube's public oEmbed endpoint.
    public string OEmbedUrl { get; set; } = "https://www.youtube.com/oembed";
}

public record SyncResult(int Found, int Added, int Blocked, string? Error);

// One video looked up by id. Embeddable is false when the owner does not allow it to play inside other sites.
public record VideoLookup(string? Title, string? Channel, int? DurationSec, DateTime? PublishedAt, bool Embeddable, string? Error);

public sealed class SourceSyncService(IHttpClientFactory http, IOptions<YouTubeOptions> options, ILogger<SourceSyncService> logger)
{
    public const string ClientName = "youtube";
    private const int MaxResponseBytes = 2 * 1024 * 1024;
    private static readonly XNamespace Atom = "http://www.w3.org/2005/Atom";
    private static readonly XNamespace Yt = "http://www.youtube.com/xml/schemas/2015";

    private record Entry(string Id, string Title, DateTime? Published, int? DurationSec = null, bool Embeddable = true);

    private sealed class SyncFailure(string message) : Exception(message);

    public bool UsesApi => !string.IsNullOrWhiteSpace(options.Value.ApiKey);

    public async Task<SyncResult> SyncAsync(SafeLearnDbContext db, ContentSource source, CancellationToken cancellationToken)
    {
        SyncResult result;
        try
        {
            var entries = await FetchAsync(source, cancellationToken);
            result = await StoreAsync(db, source, entries, cancellationToken);
        }
        catch (SyncFailure failure)
        {
            result = new SyncResult(0, 0, 0, failure.Message);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or XmlException or JsonException)
        {
            // The request URL carries the API key, so only the exception type is logged.
            logger.LogWarning("Could not sync content source {SourceId}: {Error}", source.Id, exception.GetType().Name);
            result = new SyncResult(0, 0, 0, "Không kết nối được tới YouTube hoặc dữ liệu không đọc được. Hãy thử lại sau.");
        }
        source.LastSyncedAt = DateTime.UtcNow;
        source.LastSyncError = result.Error;
        await db.SaveChangesAsync(cancellationToken);
        return result;
    }

    // A single video added by an admin. With a key: title, channel, duration and the embed flag from the Data API.
    // Without a key: title and channel from oEmbed, which answers 401/403 for videos that cannot be embedded.
    public async Task<VideoLookup> LookupVideoAsync(string videoId, CancellationToken cancellationToken)
    {
        if (!SourceUrl.VideoId().IsMatch(videoId)) return new(null, null, null, null, false, SourceUrl.NotVideo);
        try
        {
            if (UsesApi)
            {
                using var document = await JsonAsync($"videos?part=snippet,contentDetails,status&id={videoId}", cancellationToken);
                var items = document.RootElement.GetProperty("items");
                if (items.GetArrayLength() == 0) return new(null, null, null, null, false, NotFoundVideo);
                var video = items[0];
                var snippet = video.TryGetProperty("snippet", out var part) ? part : default;
                int? seconds = null;
                if (video.TryGetProperty("contentDetails", out var details) && details.TryGetProperty("duration", out var duration))
                {
                    try
                    {
                        seconds = (int)XmlConvert.ToTimeSpan(duration.GetString() ?? "").TotalSeconds;
                    }
                    catch (FormatException)
                    {
                    }
                }
                var embeddable = !video.TryGetProperty("status", out var status) || !status.TryGetProperty("embeddable", out var flag) || flag.GetBoolean();
                return new(Text(snippet, "title"), Text(snippet, "channelTitle"), seconds, Date(Text(snippet, "publishedAt")), embeddable, null);
            }
            var watch = Uri.EscapeDataString($"https://www.youtube.com/watch?v={videoId}");
            using var response = await http.CreateClient(ClientName).GetAsync($"{options.Value.OEmbedUrl}?url={watch}&format=json", HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden) return new(null, null, null, null, false, null);
            if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.BadRequest) return new(null, null, null, null, false, NotFoundVideo);
            if (!response.IsSuccessStatusCode) return new(null, null, null, null, false, $"YouTube trả về lỗi {(int)response.StatusCode}. Hãy thử lại sau.");
            await using var body = await ReadLimitedAsync(response, cancellationToken);
            using var oembed = await JsonDocument.ParseAsync(body, cancellationToken: cancellationToken);
            return new(Text(oembed.RootElement, "title"), Text(oembed.RootElement, "author_name"), null, null, true, null);
        }
        catch (SyncFailure failure)
        {
            return new(null, null, null, null, false, failure.Message);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or JsonException or InvalidOperationException or KeyNotFoundException)
        {
            // The request URL may carry the API key, so only the exception type is logged.
            logger.LogWarning("Could not look up a YouTube video: {Error}", exception.GetType().Name);
            return new(null, null, null, null, false, "Không kết nối được tới YouTube hoặc dữ liệu không đọc được. Hãy thử lại sau.");
        }
    }

    private const string NotFoundVideo = "YouTube không tìm thấy video này (có thể đã bị xóa hoặc để riêng tư).";

    private static string? Text(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString()?.Trim() : null;

    private async Task<List<Entry>> FetchAsync(ContentSource source, CancellationToken cancellationToken)
    {
        // Only fixed YouTube addresses are requested, with ids the URL parser already validated.
        var valid = source.Platform switch
        {
            "YouTubeChannel" => SourceUrl.ChannelId().IsMatch(source.ExternalId),
            "YouTubePlaylist" => SourceUrl.PlaylistId().IsMatch(source.ExternalId),
            _ => throw new SyncFailure("Nguồn website không đồng bộ tự động. Hãy dùng nguồn YouTube để lấy video.")
        };
        if (!valid) throw new SyncFailure("Mã kênh hoặc danh sách phát không hợp lệ.");
        return UsesApi ? await FromApiAsync(source, cancellationToken) : await FromFeedAsync(source, cancellationToken);
    }

    private async Task<List<Entry>> FromFeedAsync(ContentSource source, CancellationToken cancellationToken)
    {
        var parameter = source.Platform == "YouTubeChannel" ? "channel_id" : "playlist_id";
        await using var body = await GetAsync($"{options.Value.FeedBaseUrl}?{parameter}={Uri.EscapeDataString(source.ExternalId)}", cancellationToken);
        using var reader = XmlReader.Create(body, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null });
        var feed = XDocument.Load(reader);
        return feed.Root?.Elements(Atom + "entry").Select(entry => new Entry(
                (string?)entry.Element(Yt + "videoId") ?? "",
                ((string?)entry.Element(Atom + "title") ?? "").Trim(),
                Date((string?)entry.Element(Atom + "published"))))
            .ToList() ?? [];
    }

    private async Task<List<Entry>> FromApiAsync(ContentSource source, CancellationToken cancellationToken)
    {
        var playlist = source.ExternalId;
        if (source.Platform == "YouTubeChannel")
        {
            // A channel's uploads are a playlist of their own.
            using var channel = await JsonAsync($"channels?part=contentDetails&id={Uri.EscapeDataString(source.ExternalId)}", cancellationToken);
            playlist = channel.RootElement.TryGetProperty("items", out var items) && items.GetArrayLength() > 0
                ? items[0].GetProperty("contentDetails").GetProperty("relatedPlaylists").GetProperty("uploads").GetString() ?? ""
                : throw new SyncFailure("YouTube không tìm thấy kênh này.");
        }
        var entries = new List<Entry>();
        string? page = null;
        do
        {
            using var list = await JsonAsync($"playlistItems?part=snippet,contentDetails&maxResults=50&playlistId={Uri.EscapeDataString(playlist)}"
                + (page is null ? "" : "&pageToken=" + Uri.EscapeDataString(page)), cancellationToken);
            foreach (var item in list.RootElement.GetProperty("items").EnumerateArray())
            {
                var content = item.GetProperty("contentDetails");
                entries.Add(new Entry(
                    content.TryGetProperty("videoId", out var id) ? id.GetString() ?? "" : "",
                    item.GetProperty("snippet").TryGetProperty("title", out var title) ? (title.GetString() ?? "").Trim() : "",
                    Date(content.TryGetProperty("videoPublishedAt", out var at) ? at.GetString() : null)));
            }
            page = list.RootElement.TryGetProperty("nextPageToken", out var next) ? next.GetString() : null;
        } while (page is not null && entries.Count < options.Value.MaxVideosPerSync);

        // Durations and whether YouTube allows the video to play inside our page.
        var details = new Dictionary<string, (int? Duration, bool Embeddable)>();
        foreach (var chunk in entries.Select(entry => entry.Id).Where(id => SourceUrl.VideoId().IsMatch(id)).Distinct().Chunk(50))
        {
            using var videos = await JsonAsync($"videos?part=contentDetails,status&id={string.Join(',', chunk)}", cancellationToken);
            foreach (var video in videos.RootElement.GetProperty("items").EnumerateArray())
            {
                int? seconds = null;
                try
                {
                    seconds = (int)XmlConvert.ToTimeSpan(video.GetProperty("contentDetails").GetProperty("duration").GetString() ?? "").TotalSeconds;
                }
                catch (FormatException)
                {
                }
                var embeddable = !video.TryGetProperty("status", out var status) || !status.TryGetProperty("embeddable", out var flag) || flag.GetBoolean();
                details[video.GetProperty("id").GetString() ?? ""] = (seconds, embeddable);
            }
        }
        return entries.Select(entry => details.TryGetValue(entry.Id, out var extra) ? entry with { DurationSec = extra.Duration, Embeddable = extra.Embeddable } : entry).ToList();
    }

    private async Task<JsonDocument> JsonAsync(string path, CancellationToken cancellationToken)
    {
        await using var body = await GetAsync($"{options.Value.ApiBaseUrl}/{path}&key={Uri.EscapeDataString(options.Value.ApiKey!)}", cancellationToken);
        return await JsonDocument.ParseAsync(body, cancellationToken: cancellationToken);
    }

    private async Task<Stream> GetAsync(string url, CancellationToken cancellationToken)
    {
        using var response = await http.CreateClient(ClientName).GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new SyncFailure(response.StatusCode switch
            {
                HttpStatusCode.NotFound => "YouTube không tìm thấy kênh hoặc danh sách phát này (có thể đã xóa hoặc để riêng tư).",
                HttpStatusCode.Forbidden when UsesApi => "YouTube từ chối yêu cầu: API key hết hạn mức trong ngày hoặc chưa bật YouTube Data API v3.",
                HttpStatusCode.BadRequest when UsesApi => "API key YouTube không hợp lệ.",
                _ => $"YouTube trả về lỗi {(int)response.StatusCode}. Hãy thử lại sau."
            });
        return await ReadLimitedAsync(response, cancellationToken);
    }

    private static async Task<Stream> ReadLimitedAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var limited = new MemoryStream();
        var buffer = new byte[81920];
        int read;
        while ((read = await stream.ReadAsync(buffer, cancellationToken)) > 0)
        {
            if (limited.Length + read > MaxResponseBytes) throw new SyncFailure("Dữ liệu YouTube trả về quá lớn.");
            limited.Write(buffer, 0, read);
        }
        limited.Position = 0;
        return limited;
    }

    private static DateTime? Date(string? text) =>
        DateTime.TryParse(text, null, System.Globalization.DateTimeStyles.AdjustToUniversal, out var at) ? at : null;

    private static async Task<SyncResult> StoreAsync(SafeLearnDbContext db, ContentSource source, List<Entry> found, CancellationToken cancellationToken)
    {
        var entries = found.Where(entry => SourceUrl.VideoId().IsMatch(entry.Id) && entry.Title.Length > 0).DistinctBy(entry => entry.Id).ToList();
        var ids = entries.Select(entry => entry.Id).ToList();
        var known = await db.SourceVideos.Include(video => video.SourceVideoSources)
            .Where(video => video.Platform == "YouTube" && ids.Contains(video.VideoId)).ToListAsync(cancellationToken);
        var keywords = await db.BlockedKeywords.Where(keyword => keyword.ParentId == null).Select(keyword => keyword.Keyword).ToListAsync(cancellationToken);
        int added = 0, blocked = 0;
        var now = DateTime.UtcNow;
        foreach (var entry in entries)
        {
            var video = known.FirstOrDefault(item => item.VideoId == entry.Id);
            if (video is null)
            {
                var hit = keywords.FirstOrDefault(keyword => entry.Title.Contains(keyword, StringComparison.OrdinalIgnoreCase));
                var reason = hit is not null ? $"Tự chặn khi đồng bộ: tiêu đề chứa từ khóa bị chặn \"{hit}\"."
                    : !entry.Embeddable ? "Tự chặn khi đồng bộ: YouTube không cho phát video này trong trang khác." : null;
                video = new SourceVideo
                {
                    Platform = "YouTube",
                    VideoId = entry.Id,
                    Title = entry.Title.Length > 250 ? entry.Title[..250] : entry.Title,
                    // Thumbnails come from YouTube's own image host, never from a URL in the response.
                    ThumbnailUrl = $"https://i.ytimg.com/vi/{entry.Id}/hqdefault.jpg",
                    DurationSec = entry.DurationSec,
                    PublishedAt = entry.Published,
                    CheckedAt = now,
                    // New videos wait for a person; flagged ones never reach children.
                    Status = reason is null ? VideoStatus.Pending : VideoStatus.Blocked,
                    ReviewNote = reason
                };
                db.SourceVideos.Add(video);
                added++;
                if (reason is not null) blocked++;
            }
            else
            {
                video.CheckedAt = now;
                video.DurationSec ??= entry.DurationSec;
            }
            if (video.SourceVideoSources.All(link => link.SourceId != source.Id))
                video.SourceVideoSources.Add(new SourceVideoSource { SourceId = source.Id, DiscoveredAt = now });
        }
        await db.SaveChangesAsync(cancellationToken);
        return new SyncResult(entries.Count, added, blocked, null);
    }
}
