using System;
using System.Collections.Generic;

namespace SafeLearnKids.Web.Models.Entities;

public partial class DailyUsage
{
    public long Id { get; set; }

    public int ChildProfileId { get; set; }

    public DateOnly UsageDate { get; set; }

    public int Seconds { get; set; }

    public virtual ChildProfile ChildProfile { get; set; } = null!;
}
