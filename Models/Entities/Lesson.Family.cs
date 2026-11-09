namespace SafeLearnKids.Web.Models.Entities;

// Family lessons and the community library (added after the scaffold; columns created by Services/SchemaUpdater.cs).
public partial class Lesson
{
    // Null for the standard library written by admins; the parent's account for a family lesson.
    public string? OwnerParentId { get; set; }

    // Null when the family keeps the lesson to itself; Pending, Approved or Rejected once shared with the community.
    public string? ShareStatus { get; set; }

    public string? ShareNote { get; set; }

    public DateTime? SharedAt { get; set; }

    public DateTime? ShareReviewedAt { get; set; }

    public virtual User? OwnerParent { get; set; }
}

public class LessonRequest
{
    public int Id { get; set; }
    public string ParentId { get; set; } = null!;
    public int SubjectId { get; set; }
    public int GradeLevelId { get; set; }
    public string Topic { get; set; } = null!;
    public string? Note { get; set; }
    public string Status { get; set; } = null!;
    public string? Reply { get; set; }
    public int? LessonId { get; set; }
    public string? HandledById { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? HandledAt { get; set; }

    public virtual User Parent { get; set; } = null!;
    public virtual Subject Subject { get; set; } = null!;
    public virtual GradeLevel GradeLevel { get; set; } = null!;
    public virtual Lesson? Lesson { get; set; }
    public virtual ICollection<LessonRequestVote> Votes { get; set; } = new List<LessonRequestVote>();
}

public class LessonRequestVote
{
    public int RequestId { get; set; }
    public string ParentId { get; set; } = null!;
    public DateTime CreatedAt { get; set; }

    public virtual LessonRequest Request { get; set; } = null!;
}
