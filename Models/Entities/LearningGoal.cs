using System;
using System.Collections.Generic;

namespace SafeLearnKids.Web.Models.Entities;

public partial class LearningGoal
{
    public int Id { get; set; }

    public int ChildProfileId { get; set; }

    public DateOnly WeekStart { get; set; }

    public int? TargetLessons { get; set; }

    public int? TargetMinutes { get; set; }

    public string? CreatedById { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual ChildProfile ChildProfile { get; set; } = null!;

    public virtual User? CreatedBy { get; set; }
}
