using System;
using System.Collections.Generic;

namespace SafeLearnKids.Web.Models.Entities;

public partial class QuizAttemptAnswer
{
    public long Id { get; set; }

    public long AttemptId { get; set; }

    public int QuizId { get; set; }

    public int QuestionId { get; set; }

    public int? SelectedOptionId { get; set; }

    public bool IsCorrect { get; set; }

    public DateTime AnsweredAt { get; set; }

    public virtual QuizAttempt Attempt { get; set; } = null!;

    public virtual Question Question { get; set; } = null!;

    public virtual QuestionOption? QuestionOption { get; set; }

    public virtual Quiz Quiz { get; set; } = null!;

    public virtual QuizAttempt QuizAttempt { get; set; } = null!;

    public virtual QuizQuestion QuizQuestion { get; set; } = null!;

    public virtual QuestionOption? SelectedOption { get; set; }
}
