using System;
using System.Collections.Generic;

namespace SafeLearnKids.Web.Models.Entities;

public partial class ChildProfile
{
    public int Id { get; set; }

    public string ParentId { get; set; } = null!;

    public string? UserId { get; set; }

    public string DisplayName { get; set; } = null!;

    public int GradeLevelId { get; set; }

    public int? BirthYear { get; set; }

    public string? AvatarKey { get; set; }

    public bool IsActive { get; set; }

    public DateTime? PinChangedAt { get; set; }

    public DateTime? PinLockedUntil { get; set; }

    public int PinFailedAttempts { get; set; }

    public string? PinHash { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual ICollection<Assignment> Assignments { get; set; } = new List<Assignment>();

    public virtual ICollection<ChildBadge> ChildBadges { get; set; } = new List<ChildBadge>();

    public virtual ICollection<ChildPath> ChildPaths { get; set; } = new List<ChildPath>();

    public virtual ICollection<ChildSubject> ChildSubjects { get; set; } = new List<ChildSubject>();

    public virtual ICollection<ContentReport> ContentReports { get; set; } = new List<ContentReport>();

    public virtual ICollection<DailyUsage> DailyUsages { get; set; } = new List<DailyUsage>();

    public virtual ICollection<Favorite> Favorites { get; set; } = new List<Favorite>();

    public virtual GradeLevel GradeLevel { get; set; } = null!;

    public virtual ICollection<LearningGoal> LearningGoals { get; set; } = new List<LearningGoal>();

    public virtual ICollection<LearningSession> LearningSessions { get; set; } = new List<LearningSession>();

    public virtual ICollection<LessonActivity> LessonActivities { get; set; } = new List<LessonActivity>();

    public virtual User Parent { get; set; } = null!;

    public virtual ICollection<QuizAttempt> QuizAttempts { get; set; } = new List<QuizAttempt>();

    public virtual ICollection<SearchLog> SearchLogs { get; set; } = new List<SearchLog>();

    public virtual Streak? Streak { get; set; }

    public virtual ICollection<SubjectProgress> SubjectProgresses { get; set; } = new List<SubjectProgress>();

    public virtual ICollection<TimeExtensionRequest> TimeExtensionRequests { get; set; } = new List<TimeExtensionRequest>();

    public virtual TimeLimitPolicy? TimeLimitPolicy { get; set; }

    public virtual User? User { get; set; }

    public virtual ICollection<WeeklyReport> WeeklyReports { get; set; } = new List<WeeklyReport>();
}
