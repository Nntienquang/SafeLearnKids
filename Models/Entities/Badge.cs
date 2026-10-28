using System;
using System.Collections.Generic;

namespace SafeLearnKids.Web.Models.Entities;

public partial class Badge
{
    public int Id { get; set; }

    public string Code { get; set; } = null!;

    public string Name { get; set; } = null!;

    public string Description { get; set; } = null!;

    public string IconKey { get; set; } = null!;

    public string CriteriaType { get; set; } = null!;

    public int Threshold { get; set; }

    public bool IsActive { get; set; }

    public virtual ICollection<ChildBadge> ChildBadges { get; set; } = new List<ChildBadge>();
}
