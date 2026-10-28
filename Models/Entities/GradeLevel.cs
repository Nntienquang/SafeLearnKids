using System;
using System.Collections.Generic;

namespace SafeLearnKids.Web.Models.Entities;

public partial class GradeLevel
{
    public int Id { get; set; }

    public int Level { get; set; }

    public string Name { get; set; } = null!;

    public virtual ICollection<ChildProfile> ChildProfiles { get; set; } = new List<ChildProfile>();

    public virtual ICollection<LearningPath> LearningPaths { get; set; } = new List<LearningPath>();

    public virtual ICollection<Lesson> Lessons { get; set; } = new List<Lesson>();

    public virtual ICollection<SubjectGrade> SubjectGrades { get; set; } = new List<SubjectGrade>();
}
