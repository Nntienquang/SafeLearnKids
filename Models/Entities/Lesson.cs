using System;
using System.Collections.Generic;

namespace SafeLearnKids.Web.Models.Entities;

public partial class Lesson
{
    public int Id { get; set; }

    public int SubjectId { get; set; }

    public int GradeLevelId { get; set; }

    public string Title { get; set; } = null!;

    public string? Summary { get; set; }

    public string LessonType { get; set; } = null!;

    public int DurationMin { get; set; }

    public int SortOrder { get; set; }

    public string Status { get; set; } = null!;

    public string CreatedById { get; set; } = null!;

    public string? ApprovedById { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public DateTime? PublishedAt { get; set; }

    public byte[] RowVersion { get; set; } = null!;

    public virtual User? ApprovedBy { get; set; }

    public virtual ICollection<Assignment> Assignments { get; set; } = new List<Assignment>();

    public virtual ICollection<ContentReport> ContentReports { get; set; } = new List<ContentReport>();

    public virtual User CreatedBy { get; set; } = null!;

    public virtual ICollection<Favorite> Favorites { get; set; } = new List<Favorite>();

    public virtual GradeLevel GradeLevel { get; set; } = null!;

    public virtual ICollection<LearningPathItem> LearningPathItems { get; set; } = new List<LearningPathItem>();

    public virtual ICollection<LessonActivity> LessonActivities { get; set; } = new List<LessonActivity>();

    public virtual ICollection<LessonContent> LessonContents { get; set; } = new List<LessonContent>();

    public virtual ICollection<LessonTag> LessonTags { get; set; } = new List<LessonTag>();

    public virtual ICollection<Question> Questions { get; set; } = new List<Question>();

    public virtual ICollection<Quiz> Quizzes { get; set; } = new List<Quiz>();

    public virtual Subject Subject { get; set; } = null!;

    public virtual SubjectGrade SubjectGrade { get; set; } = null!;
}
