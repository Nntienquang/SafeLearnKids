using System;
using System.Collections.Generic;

namespace SafeLearnKids.Web.Models.Entities;

public partial class Tag
{
    public int Id { get; set; }

    public string Name { get; set; } = null!;

    public virtual ICollection<LessonTag> LessonTags { get; set; } = new List<LessonTag>();
}
