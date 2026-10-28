using System;
using System.Collections.Generic;

namespace SafeLearnKids.Web.Models.Entities;

public partial class SearchLog
{
    public long Id { get; set; }

    public int ChildProfileId { get; set; }

    public string Query { get; set; } = null!;

    public int ResultCount { get; set; }

    public bool WasBlocked { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual ChildProfile ChildProfile { get; set; } = null!;
}
