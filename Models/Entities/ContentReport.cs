using System;
using System.Collections.Generic;

namespace SafeLearnKids.Web.Models.Entities;

public partial class ContentReport
{
    public int Id { get; set; }

    public int? ChildProfileId { get; set; }

    public string? ReportedById { get; set; }

    public int? LessonId { get; set; }

    public int? SourceVideoId { get; set; }

    public string Reason { get; set; } = null!;

    public string? Comment { get; set; }

    public string Status { get; set; } = null!;

    public string? HandledById { get; set; }

    public string? Resolution { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? ResolvedAt { get; set; }

    public byte[] RowVersion { get; set; } = null!;

    public virtual ChildProfile? ChildProfile { get; set; }

    public virtual User? HandledBy { get; set; }

    public virtual Lesson? Lesson { get; set; }

    public virtual User? ReportedBy { get; set; }

    public virtual SourceVideo? SourceVideo { get; set; }
}
