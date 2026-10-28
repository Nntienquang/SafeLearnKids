using System;
using System.Collections.Generic;

namespace SafeLearnKids.Web.Models.Entities;

public partial class LearningSession
{
    public long Id { get; set; }

    public int ChildProfileId { get; set; }

    public DateTime StartedAt { get; set; }

    public DateTime LastHeartbeatAt { get; set; }

    public DateTime? EndedAt { get; set; }

    public int ActiveSeconds { get; set; }

    public string? EndReason { get; set; }

    public virtual ChildProfile ChildProfile { get; set; } = null!;

    public virtual ICollection<LessonActivity> LessonActivityLearningSessions { get; set; } = new List<LessonActivity>();

    public virtual ICollection<LessonActivity> LessonActivitySessions { get; set; } = new List<LessonActivity>();
}
