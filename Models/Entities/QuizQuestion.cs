using System;
using System.Collections.Generic;

namespace SafeLearnKids.Web.Models.Entities;

public partial class QuizQuestion
{
    public int Id { get; set; }

    public int QuizId { get; set; }

    public int QuestionId { get; set; }

    public int SortOrder { get; set; }

    public int Points { get; set; }

    public virtual Question Question { get; set; } = null!;

    public virtual Quiz Quiz { get; set; } = null!;

    public virtual ICollection<QuizAttemptAnswer> QuizAttemptAnswers { get; set; } = new List<QuizAttemptAnswer>();
}
