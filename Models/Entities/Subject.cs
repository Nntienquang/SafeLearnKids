using System;
using System.Collections.Generic;

namespace SafeLearnKids.Web.Models.Entities;

public partial class Subject
{
    public int Id { get; set; }

    public string Code { get; set; } = null!;

    public string Name { get; set; } = null!;

    public string? Description { get; set; }

    public string? IconKey { get; set; }

    public string? ColorKey { get; set; }

    public int SortOrder { get; set; }

    public bool IsActive { get; set; }

    public virtual ICollection<ChildSubject> ChildSubjects { get; set; } = new List<ChildSubject>();

    public virtual ICollection<LearningPath> LearningPaths { get; set; } = new List<LearningPath>();

    public virtual ICollection<Lesson> Lessons { get; set; } = new List<Lesson>();

    public virtual ICollection<SubjectGrade> SubjectGrades { get; set; } = new List<SubjectGrade>();

    public virtual ICollection<SubjectProgress> SubjectProgresses { get; set; } = new List<SubjectProgress>();
}
