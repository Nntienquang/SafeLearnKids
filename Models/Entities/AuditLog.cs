using System;
using System.Collections.Generic;

namespace SafeLearnKids.Web.Models.Entities;

public partial class AuditLog
{
    public long Id { get; set; }

    public string? ActorId { get; set; }

    public string Action { get; set; } = null!;

    public string EntityType { get; set; } = null!;

    public string? EntityId { get; set; }

    public string? Detail { get; set; }

    public string? IpAddress { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual User? Actor { get; set; }
}
