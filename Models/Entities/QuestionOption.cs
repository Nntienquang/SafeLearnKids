using System;
using System.Collections.Generic;

namespace SafeLearnKids.Web.Models.Entities;

public partial class QuestionOption
{
    public int Id { get; set; }

    public int QuestionId { get; set; }

    public string Text { get; set; } = null!;

    public bool IsCorrect { get; set; }

    public int SortOrder { get; set; }

    public virtual Question Question { get; set; } = null!;

    public virtual ICollection<QuizAttemptAnswer> QuizAttemptAnswerQuestionOptions { get; set; } = new List<QuizAttemptAnswer>();

    public virtual ICollection<QuizAttemptAnswer> QuizAttemptAnswerSelectedOptions { get; set; } = new List<QuizAttemptAnswer>();
}
