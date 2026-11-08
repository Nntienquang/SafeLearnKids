using Microsoft.EntityFrameworkCore;
using SafeLearnKids.Web.Data;
using SafeLearnKids.Web.Models.Entities;

namespace SafeLearnKids.Web.Services;

// Adds rows to dbo.Notifications; the caller saves them with its own changes.
public static class NotificationWriter
{
    public static void Add(SafeLearnDbContext db, string userId, string type, string title, string? body, string? link) =>
        db.Notifications.Add(new Notification
        {
            UserId = userId,
            NotificationType = type,
            Title = title.Length > 120 ? title[..120] : title,
            Body = body is { Length: > 500 } ? body[..500] : body,
            LinkUrl = link,
            IsRead = false,
            CreatedAt = DateTime.UtcNow
        });

    // US-50: time requests and content reports can be switched off in the parent's settings (on when never set).
    public static async Task AddAsync(SafeLearnDbContext db, string userId, string type, string title, string? body, string? link, CancellationToken cancellationToken)
    {
        if (type is "TimeRequest" or "ContentReport")
        {
            var wanted = await db.UserSettings.AsNoTracking().Where(item => item.UserId == userId)
                .Select(item => (bool?)(type == "TimeRequest" ? item.NotifyTimeRequest : item.NotifyContentReport)).FirstOrDefaultAsync(cancellationToken);
            if (wanted == false) return;
        }
        Add(db, userId, type, title, body, link);
    }
}
