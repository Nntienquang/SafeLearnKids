using System;
using System.Collections.Generic;

namespace SafeLearnKids.Web.Models.Entities;

public partial class SubjectProgress
{
    public int Id { get; set; }

    public int ChildProfileId { get; set; }

    public int SubjectId { get; set; }

    public int CompletedLessons { get; set; }

    public int TotalLessons { get; set; }

    public decimal? AvgQuizPercent { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual ChildProfile ChildProfile { get; set; } = null!;

    public virtual Subject Subject { get; set; } = null!;
}
