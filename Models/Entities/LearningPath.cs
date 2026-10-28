using System;
using System.Collections.Generic;

namespace SafeLearnKids.Web.Models.Entities;

public partial class LearningPath
{
    public int Id { get; set; }

    public string Name { get; set; } = null!;

    public int SubjectId { get; set; }

    public int GradeLevelId { get; set; }

    public string? Description { get; set; }

    public bool IsActive { get; set; }

    public virtual ICollection<ChildPath> ChildPaths { get; set; } = new List<ChildPath>();

    public virtual GradeLevel GradeLevel { get; set; } = null!;

    public virtual ICollection<LearningPathItem> LearningPathItems { get; set; } = new List<LearningPathItem>();

    public virtual Subject Subject { get; set; } = null!;
}
