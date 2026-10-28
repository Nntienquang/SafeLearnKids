using System;
using System.Collections.Generic;

namespace SafeLearnKids.Web.Models.Entities;

public partial class QuizAttempt
{
    public long Id { get; set; }

    public int QuizId { get; set; }

    public int ChildProfileId { get; set; }

    public int? AssignmentId { get; set; }

    public DateTime StartedAt { get; set; }

    public DateTime? SubmittedAt { get; set; }

    public decimal? ScorePercent { get; set; }

    public int CorrectCount { get; set; }

    public int TotalCount { get; set; }

    public bool? Passed { get; set; }

    public virtual Assignment? Assignment { get; set; }

    public virtual Assignment? AssignmentNavigation { get; set; }

    public virtual ChildProfile ChildProfile { get; set; } = null!;

    public virtual Quiz Quiz { get; set; } = null!;

    public virtual ICollection<QuizAttemptAnswer> QuizAttemptAnswerAttempts { get; set; } = new List<QuizAttemptAnswer>();

    public virtual ICollection<QuizAttemptAnswer> QuizAttemptAnswerQuizAttempts { get; set; } = new List<QuizAttemptAnswer>();
}
