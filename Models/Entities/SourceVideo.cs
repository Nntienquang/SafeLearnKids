using System;
using System.Collections.Generic;

namespace SafeLearnKids.Web.Models.Entities;

public partial class SourceVideo
{
    public int Id { get; set; }

    public string Platform { get; set; } = null!;

    public string VideoId { get; set; } = null!;

    public string Title { get; set; } = null!;

    public int? DurationSec { get; set; }

    public string? ThumbnailUrl { get; set; }

    public string Status { get; set; } = null!;

    public DateTime? PublishedAt { get; set; }

    public DateTime? CheckedAt { get; set; }

    public string? ReviewedById { get; set; }

    public DateTime? ReviewedAt { get; set; }

    public string? ReviewNote { get; set; }

    public byte[] RowVersion { get; set; } = null!;

    public virtual ICollection<ContentReport> ContentReports { get; set; } = new List<ContentReport>();

    public virtual ICollection<LessonContent> LessonContents { get; set; } = new List<LessonContent>();

    public virtual ICollection<Question> Questions { get; set; } = new List<Question>();

    public virtual User? ReviewedBy { get; set; }

    public virtual ICollection<SourceVideoSource> SourceVideoSources { get; set; } = new List<SourceVideoSource>();
}
