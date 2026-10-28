using System;
using System.Collections.Generic;

namespace SafeLearnKids.Web.Models.Entities;

public partial class UserSetting
{
    public int Id { get; set; }

    public string UserId { get; set; } = null!;

    public string Theme { get; set; } = null!;

    public string Language { get; set; } = null!;

    public string? ParentPinHash { get; set; }

    public bool WeeklyEmail { get; set; }

    public bool NotifyTimeRequest { get; set; }

    public bool NotifyContentReport { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual User User { get; set; } = null!;
}
