using System;
using System.Collections.Generic;

namespace SafeLearnKids.Web.Models.Entities;

public partial class DataRequest
{
    public int Id { get; set; }

    public string ParentId { get; set; } = null!;

    public int? ChildProfileId { get; set; }

    public string RequestType { get; set; } = null!;

    public string Status { get; set; } = null!;

    public string? ProcessedById { get; set; }

    public string? ChildName { get; set; }

    public string? DecisionNote { get; set; }

    public DateTime RequestedAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    public virtual User Parent { get; set; } = null!;

    public virtual User? ProcessedBy { get; set; }
}
