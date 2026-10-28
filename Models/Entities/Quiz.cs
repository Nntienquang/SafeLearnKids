using System;
using System.Collections.Generic;

namespace SafeLearnKids.Web.Models.Entities;

public partial class Quiz
{
    public int Id { get; set; }

    public int LessonId { get; set; }

    public string Title { get; set; } = null!;

    public int PassPercent { get; set; }

    public int? TimeLimitSec { get; set; }

    public bool ShuffleQuestions { get; set; }

    public string Status { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public virtual ICollection<Assignment> Assignments { get; set; } = new List<Assignment>();

    public virtual Lesson Lesson { get; set; } = null!;

    public virtual ICollection<QuizAttemptAnswer> QuizAttemptAnswers { get; set; } = new List<QuizAttemptAnswer>();

    public virtual ICollection<QuizAttempt> QuizAttempts { get; set; } = new List<QuizAttempt>();

    public virtual ICollection<QuizQuestion> QuizQuestions { get; set; } = new List<QuizQuestion>();
}
