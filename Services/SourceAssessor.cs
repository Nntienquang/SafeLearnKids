using Microsoft.EntityFrameworkCore;
using SafeLearnKids.Web.Data;
using SafeLearnKids.Web.Models;
using SafeLearnKids.Web.Models.Entities;

namespace SafeLearnKids.Web.Services;

// A transparent rule-based first pass: it explains every point it adds or removes, and an admin decides.
public static class SourceAssessor
{
    public const string ModelName = "rule-v1";
    public const int ApproveAt = 75;
    public const int RejectBelow = 50;

    private static readonly string[] Risky =
    [
        "kinh dị", "kinh di", "horror", "bạo lực", "bao luc", "đánh nhau", "máu me", "18+", "người lớn", "sexy", "prank",
        "troll", "cá cược", "ca cuoc", "casino", "game bắn súng", "jumpscare", "ma quỷ", "rượu", "thuốc lá"
    ];

    private static readonly string[] Friendly =
    [
        "cho bé", "thiếu nhi", "thieu nhi", "trẻ em", "tre em", "mầm non", "tiểu học", "tieu hoc", "lớp ", "học", "hoc ", "kids",
        "children", "education", "toán", "tiếng việt", "tiếng anh", "khoa học", "kỹ năng"
    ];

    public static async Task<CurationSuggestion> AssessAsync(SafeLearnDbContext db, ContentSource source, CancellationToken cancellationToken)
    {
        var titles = await db.SourceVideoSources.Where(link => link.SourceId == source.Id)
            .OrderByDescending(link => link.DiscoveredAt).Take(50).Select(link => link.SourceVideo.Title).ToListAsync(cancellationToken);
        var keywords = await db.BlockedKeywords.Where(keyword => keyword.ParentId == null).Select(keyword => keyword.Keyword).ToListAsync(cancellationToken);
        var text = $"{source.Name}\n{source.Description}\n{string.Join('\n', titles)}";

        var score = 60;
        var notes = new List<string>();
        var flags = new List<string>();
        if (source.Platform == "Website")
        {
            score -= 10;
            notes.Add("Website không đồng bộ được video để xem trước (−10).");
        }
        else
        {
            score += 10;
            notes.Add("Nguồn YouTube phát qua trình phát nhúng (+10).");
        }
        foreach (var keyword in keywords.Where(keyword => Contains(text, keyword)).Take(3))
        {
            score -= 40;
            flags.Add("blocked-keyword");
            notes.Add($"Có từ khóa bị chặn \"{keyword}\" (−40).");
        }
        foreach (var word in Risky.Where(word => Contains(text, word)).Take(3))
        {
            score -= 25;
            flags.Add("risky-topic");
            notes.Add($"Có từ ngữ không phù hợp với trẻ: \"{word}\" (−25).");
        }
        var friendly = Friendly.Where(word => Contains(text, word)).Take(3).ToList();
        if (friendly.Count > 0)
        {
            score += 5 * friendly.Count;
            notes.Add($"Có dấu hiệu nội dung học tập cho trẻ: {string.Join(", ", friendly.Select(word => $"\"{word.Trim()}\""))} (+{5 * friendly.Count}).");
        }
        if (titles.Count == 0)
        {
            flags.Add("no-videos");
            notes.Add("Chưa có video nào để xem xét; hãy đồng bộ trước khi quyết định.");
        }
        else
        {
            notes.Add($"Đã xem tiêu đề của {titles.Count} video gần nhất.");
        }
        score = Math.Clamp(score, 0, 100);
        var verdict = score >= ApproveAt ? "Gợi ý: có thể duyệt." : score < RejectBelow ? "Gợi ý: nên từ chối." : "Gợi ý: cần xem kỹ vài video trước khi duyệt.";
        return new CurationSuggestion
        {
            SourceId = source.Id,
            Score = score,
            SafetyFlags = flags.Count == 0 ? "none" : string.Join(",", flags.Distinct()),
            Summary = verdict + "\n" + string.Join("\n", notes),
            ModelName = ModelName,
            Status = "Pending",
            CreatedAt = DateTime.UtcNow
        };
    }

    // Whether an admin's decision agrees with the suggestion, used to close it as Accepted or Dismissed.
    public static bool Agrees(decimal score, string decision) =>
        decision == SourceStatus.Approved ? score >= ApproveAt : score < ApproveAt;

    private static bool Contains(string text, string word) => text.Contains(word, StringComparison.OrdinalIgnoreCase);
}
