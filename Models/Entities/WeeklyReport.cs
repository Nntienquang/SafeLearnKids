using System;
using System.Collections.Generic;

namespace SafeLearnKids.Web.Models.Entities;

public partial class WeeklyReport
{
    public int Id { get; set; }

    public int ChildProfileId { get; set; }

    public DateOnly WeekStart { get; set; }

    public int TotalMinutes { get; set; }

    public int LessonsCompleted { get; set; }

    public int QuizAttempts { get; set; }

    public decimal? AvgQuizPercent { get; set; }

    public string? StrongSubject { get; set; }

    public string? NeedsAttentionSubject { get; set; }

    public string? Summary { get; set; }

    public DateTime GeneratedAt { get; set; }

    public DateTime? EmailedAt { get; set; }

    public virtual ChildProfile ChildProfile { get; set; } = null!;
}
