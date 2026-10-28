using System;
using System.Collections.Generic;

namespace SafeLearnKids.Web.Models.Entities;

public partial class LessonActivity
{
    public long Id { get; set; }

    public int ChildProfileId { get; set; }

    public int LessonId { get; set; }

    public long? SessionId { get; set; }

    public int ProgressPercent { get; set; }

    public int LastPositionSec { get; set; }

    public int WatchSeconds { get; set; }

    public string Status { get; set; } = null!;

    public DateTime StartedAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual ChildProfile ChildProfile { get; set; } = null!;

    public virtual LearningSession? LearningSession { get; set; }

    public virtual Lesson Lesson { get; set; } = null!;

    public virtual LearningSession? Session { get; set; }
}
