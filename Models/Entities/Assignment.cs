using System;
using System.Collections.Generic;

namespace SafeLearnKids.Web.Models.Entities;

public partial class Assignment
{
    public int Id { get; set; }

    public int ChildProfileId { get; set; }

    public int? LessonId { get; set; }

    public int? QuizId { get; set; }

    public string AssignedById { get; set; } = null!;

    public DateOnly? DueDate { get; set; }

    public string Status { get; set; } = null!;

    public string? Note { get; set; }

    public DateTime AssignedAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    public virtual User AssignedBy { get; set; } = null!;

    public virtual ChildProfile ChildProfile { get; set; } = null!;

    public virtual Lesson? Lesson { get; set; }

    public virtual Quiz? Quiz { get; set; }

    public virtual ICollection<QuizAttempt> QuizAttemptAssignmentNavigations { get; set; } = new List<QuizAttempt>();

    public virtual ICollection<QuizAttempt> QuizAttemptAssignments { get; set; } = new List<QuizAttempt>();
}
