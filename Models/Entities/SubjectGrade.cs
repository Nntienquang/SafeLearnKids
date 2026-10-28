using System;
using System.Collections.Generic;

namespace SafeLearnKids.Web.Models.Entities;

public partial class SubjectGrade
{
    public int Id { get; set; }

    public int SubjectId { get; set; }

    public int GradeLevelId { get; set; }

    public virtual GradeLevel GradeLevel { get; set; } = null!;

    public virtual ICollection<Lesson> Lessons { get; set; } = new List<Lesson>();

    public virtual Subject Subject { get; set; } = null!;
}
