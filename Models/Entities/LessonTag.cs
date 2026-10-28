using System;
using System.Collections.Generic;

namespace SafeLearnKids.Web.Models.Entities;

public partial class LessonTag
{
    public int Id { get; set; }

    public int LessonId { get; set; }

    public int TagId { get; set; }

    public virtual Lesson Lesson { get; set; } = null!;

    public virtual Tag Tag { get; set; } = null!;
}
