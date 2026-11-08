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
}
