using System;
using System.Collections.Generic;

namespace SafeLearnKids.Web.Models.Entities;

public partial class ChildPath
{
    public int Id { get; set; }

    public int ChildProfileId { get; set; }

    public int PathId { get; set; }

    public string Status { get; set; } = null!;

    public int? CurrentItemId { get; set; }

    public DateTime? StartedAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    public virtual ChildProfile ChildProfile { get; set; } = null!;

    public virtual LearningPathItem? CurrentItem { get; set; }

    public virtual LearningPathItem? LearningPathItem { get; set; }

    public virtual LearningPath Path { get; set; } = null!;
}
