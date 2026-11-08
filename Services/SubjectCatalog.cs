using Microsoft.EntityFrameworkCore;
using SafeLearnKids.Web.Data;
using SafeLearnKids.Web.Models;

namespace SafeLearnKids.Web.Services;

// One place maps dbo.Subjects to the keys, colours and icons the pages use.
public static class SubjectCatalog
{
    // store.js and its demo lessons predate the SQL catalog and use these keys.
    private static readonly Dictionary<string, string> LegacyKeys = new()
    {
        ["MATH"] = "toan",
        ["VIET"] = "tiengviet",
        ["SCIENCE"] = "khoahoc",
        ["ENGLISH"] = "tienganh"
    };

    public static readonly IReadOnlyDictionary<string, (string Tone, string From, string To, string Label)> Colors =
        new Dictionary<string, (string, string, string, string)>
        {
            ["blue"] = ("b", "#4F46E5", "#38BDF8", "Xanh dương"),
            ["purple"] = ("p", "#6A4CFF", "#C084FC", "Tím"),
            ["green"] = ("g", "#14B8A6", "#22C55E", "Xanh lá"),
            ["orange"] = ("a", "#F59E0B", "#FB7185", "Cam"),
            ["teal"] = ("t", "#0EA5E9", "#14B8A6", "Xanh ngọc"),
            ["red"] = ("r", "#F43F5E", "#FB923C", "Đỏ")
        };

    public static readonly IReadOnlyList<(string Key, string Label)> Icons =
    [
        ("calc", "Máy tính"), ("pen", "Bút"), ("flask", "Ống nghiệm"), ("globe", "Quả địa cầu"), ("book", "Sách"),
        ("heart", "Trái tim"), ("star", "Ngôi sao"), ("smile", "Mặt cười"), ("shield", "Lá chắn"), ("layers", "Tầng lớp"),
        ("target", "Mục tiêu"), ("zap", "Tia sét")
    ];

    public static string Key(string code) => LegacyKeys.TryGetValue(code, out var key) ? key : code.ToLowerInvariant();

    // A new code must not take a key that already names another subject.
    public static bool Reserved(string code) =>
        !LegacyKeys.ContainsKey(code) && LegacyKeys.Values.Contains(code.ToLowerInvariant());

    public static SubjectLook Look(string code, string name, string? colorKey, string? iconKey, bool active)
    {
        var color = Colors.TryGetValue(colorKey ?? "", out var found) ? found : Colors["blue"];
        var icon = Icons.Any(item => item.Key == iconKey) ? iconKey! : "book";
        return new SubjectLook(Key(code), name, color.Tone, icon, [color.From, color.To], active);
    }

    public static async Task<IReadOnlyList<SubjectLook>> LooksAsync(SafeLearnDbContext db, CancellationToken cancellationToken)
    {
        var rows = await db.Subjects.AsNoTracking().OrderBy(subject => subject.SortOrder).ThenBy(subject => subject.Name)
            .Select(subject => new { subject.Code, subject.Name, subject.ColorKey, subject.IconKey, subject.IsActive })
            .ToListAsync(cancellationToken);
        return rows.Select(row => Look(row.Code, row.Name, row.ColorKey, row.IconKey, row.IsActive)).ToList();
    }
}
