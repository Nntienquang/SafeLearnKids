using System;
using System.Collections.Generic;

namespace SafeLearnKids.Web.Models.Entities;

public partial class Question
{
    public int Id { get; set; }

    public int LessonId { get; set; }

    public string Text { get; set; } = null!;

    public string? Explanation { get; set; }

    public string QuestionType { get; set; } = null!;

    public int Difficulty { get; set; }

    public string Origin { get; set; } = null!;

    public int? SourceVideoId { get; set; }

    public string? AiModelName { get; set; }

    public string Status { get; set; } = null!;

    public string CreatedById { get; set; } = null!;

    public string? ApprovedById { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? ApprovedAt { get; set; }

    public virtual User? ApprovedBy { get; set; }

    public virtual User CreatedBy { get; set; } = null!;

    public virtual Lesson Lesson { get; set; } = null!;

    public virtual ICollection<QuestionOption> QuestionOptions { get; set; } = new List<QuestionOption>();

    public virtual ICollection<QuizAttemptAnswer> QuizAttemptAnswers { get; set; } = new List<QuizAttemptAnswer>();

    public virtual ICollection<QuizQuestion> QuizQuestions { get; set; } = new List<QuizQuestion>();

    public virtual SourceVideo? SourceVideo { get; set; }
}
