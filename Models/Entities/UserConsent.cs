using System;
using System.Collections.Generic;

namespace SafeLearnKids.Web.Models.Entities;

public partial class UserConsent
{
    public int Id { get; set; }

    public string UserId { get; set; } = null!;

    public int? ChildProfileId { get; set; }

    public string ConsentType { get; set; } = null!;

    public string Version { get; set; } = null!;

    public string Decision { get; set; } = null!;

    public DateTime? AcceptedAt { get; set; }

    public DateTime DecidedAt { get; set; }

    public string? IpAddress { get; set; }

    public DateTime? RevokedAt { get; set; }

    public virtual User User { get; set; } = null!;
}
