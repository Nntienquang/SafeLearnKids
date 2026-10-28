using System;
using System.Collections.Generic;

namespace SafeLearnKids.Web.Models.Entities;

public partial class LessonContent
{
    public int Id { get; set; }

    public int LessonId { get; set; }

    public string ContentKind { get; set; } = null!;

    public string? Title { get; set; }

    public int? SourceVideoId { get; set; }

    public string? Body { get; set; }

    public string? FileUrl { get; set; }

    public string? FileName { get; set; }

    public int SortOrder { get; set; }

    public virtual Lesson Lesson { get; set; } = null!;

    public virtual SourceVideo? SourceVideo { get; set; }
}
