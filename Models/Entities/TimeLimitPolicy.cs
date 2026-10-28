using System;
using System.Collections.Generic;

namespace SafeLearnKids.Web.Models.Entities;

public partial class TimeLimitPolicy
{
    public int Id { get; set; }

    public int ChildProfileId { get; set; }

    public int DailyMinutes { get; set; }

    public TimeOnly? StudyStart { get; set; }

    public TimeOnly? StudyEnd { get; set; }

    public int? BreakEveryMinutes { get; set; }

    public string? UpdatedById { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual ChildProfile ChildProfile { get; set; } = null!;

    public virtual ICollection<TimeLimitOverride> TimeLimitOverrides { get; set; } = new List<TimeLimitOverride>();

    public virtual User? UpdatedBy { get; set; }
}
