using System;
using System.Collections.Generic;

namespace SafeLearnKids.Web.Models.Entities;

public partial class TimeExtensionRequest
{
    public int Id { get; set; }

    public int ChildProfileId { get; set; }

    public DateOnly ForDate { get; set; }

    public int RequestedMinutes { get; set; }

    public string? Reason { get; set; }

    public string Status { get; set; } = null!;

    public int? GrantedMinutes { get; set; }

    public string? DecidedById { get; set; }

    public DateTime RequestedAt { get; set; }

    public DateTime? DecidedAt { get; set; }

    public byte[] RowVersion { get; set; } = null!;

    public virtual ChildProfile ChildProfile { get; set; } = null!;

    public virtual User? DecidedBy { get; set; }
}
