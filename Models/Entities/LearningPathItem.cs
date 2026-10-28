using System;
using System.Collections.Generic;

namespace SafeLearnKids.Web.Models.Entities;

public partial class LearningPathItem
{
    public int Id { get; set; }

    public int PathId { get; set; }

    public int LessonId { get; set; }

    public int SortOrder { get; set; }

    public bool IsRequired { get; set; }

    public virtual ICollection<ChildPath> ChildPathCurrentItems { get; set; } = new List<ChildPath>();

    public virtual ICollection<ChildPath> ChildPathLearningPathItems { get; set; } = new List<ChildPath>();

    public virtual Lesson Lesson { get; set; } = null!;

    public virtual LearningPath Path { get; set; } = null!;
}
