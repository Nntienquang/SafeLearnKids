using System;
using System.Collections.Generic;

namespace SafeLearnKids.Web.Models.Entities;

public partial class SourceVideoSource
{
    public int Id { get; set; }

    public int SourceVideoId { get; set; }

    public int SourceId { get; set; }

    public DateTime DiscoveredAt { get; set; }

    public virtual ContentSource Source { get; set; } = null!;

    public virtual SourceVideo SourceVideo { get; set; } = null!;
}
