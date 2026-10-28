using System;
using System.Collections.Generic;

namespace SafeLearnKids.Web.Models.Entities;

public partial class ParentSource
{
    public int Id { get; set; }

    public string ParentId { get; set; } = null!;

    public int SourceId { get; set; }

    public string Status { get; set; } = null!;

    public DateTime AddedAt { get; set; }

    public virtual User Parent { get; set; } = null!;

    public virtual ContentSource Source { get; set; } = null!;
}
