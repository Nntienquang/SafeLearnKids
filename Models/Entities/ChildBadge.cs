using System;
using System.Collections.Generic;

namespace SafeLearnKids.Web.Models.Entities;

public partial class ChildBadge
{
    public int Id { get; set; }

    public int ChildProfileId { get; set; }

    public int BadgeId { get; set; }

    public DateTime AwardedAt { get; set; }

    public virtual Badge Badge { get; set; } = null!;

    public virtual ChildProfile ChildProfile { get; set; } = null!;
}
