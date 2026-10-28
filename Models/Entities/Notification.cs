using System;
using System.Collections.Generic;

namespace SafeLearnKids.Web.Models.Entities;

public partial class Notification
{
    public int Id { get; set; }

    public string UserId { get; set; } = null!;

    public string NotificationType { get; set; } = null!;

    public string Title { get; set; } = null!;

    public string? Body { get; set; }

    public string? LinkUrl { get; set; }

    public bool IsRead { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual User User { get; set; } = null!;
}
