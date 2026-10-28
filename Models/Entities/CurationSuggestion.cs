using System;
using System.Collections.Generic;

namespace SafeLearnKids.Web.Models.Entities;

public partial class CurationSuggestion
{
    public int Id { get; set; }

    public int SourceId { get; set; }

    public decimal Score { get; set; }

    public string? SafetyFlags { get; set; }

    public string? Summary { get; set; }

    public string ModelName { get; set; } = null!;

    public string Status { get; set; } = null!;

    public string? DecidedById { get; set; }

    public DateTime? DecidedAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual User? DecidedBy { get; set; }

    public virtual ContentSource Source { get; set; } = null!;
}
