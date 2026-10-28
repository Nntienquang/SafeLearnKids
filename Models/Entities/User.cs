using System;
using System.Collections.Generic;

namespace SafeLearnKids.Web.Models.Entities;

public partial class User
{
    public string Id { get; set; } = null!;

    public string? UserName { get; set; }

    public string? NormalizedUserName { get; set; }

    public string? Email { get; set; }

    public string? NormalizedEmail { get; set; }

    public bool EmailConfirmed { get; set; }

    public string? PasswordHash { get; set; }

    public string? SecurityStamp { get; set; }

    public string? ConcurrencyStamp { get; set; }

    public string? PhoneNumber { get; set; }

    public bool PhoneNumberConfirmed { get; set; }

    public bool TwoFactorEnabled { get; set; }

    public DateTimeOffset? LockoutEnd { get; set; }

    public bool LockoutEnabled { get; set; }

    public int AccessFailedCount { get; set; }

    public string? FullName { get; set; }

    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? LastLoginAt { get; set; }

    public virtual ICollection<Assignment> Assignments { get; set; } = new List<Assignment>();

    public virtual ICollection<AuditLog> AuditLogs { get; set; } = new List<AuditLog>();

    public virtual ICollection<BlockedKeyword> BlockedKeywordCreatedBies { get; set; } = new List<BlockedKeyword>();

    public virtual ICollection<BlockedKeyword> BlockedKeywordParents { get; set; } = new List<BlockedKeyword>();

    public virtual ICollection<ChildProfile> ChildProfileParents { get; set; } = new List<ChildProfile>();

    public virtual ChildProfile? ChildProfileUser { get; set; }

    public virtual ICollection<ContentReport> ContentReportHandledBies { get; set; } = new List<ContentReport>();

    public virtual ICollection<ContentReport> ContentReportReportedBies { get; set; } = new List<ContentReport>();

    public virtual ICollection<ContentSource> ContentSourceReviewedBies { get; set; } = new List<ContentSource>();

    public virtual ICollection<ContentSource> ContentSourceSuggestedBies { get; set; } = new List<ContentSource>();

    public virtual ICollection<CurationSuggestion> CurationSuggestions { get; set; } = new List<CurationSuggestion>();

    public virtual ICollection<DataRequest> DataRequestParents { get; set; } = new List<DataRequest>();

    public virtual ICollection<DataRequest> DataRequestProcessedBies { get; set; } = new List<DataRequest>();

    public virtual ICollection<LearningGoal> LearningGoals { get; set; } = new List<LearningGoal>();

    public virtual ICollection<Lesson> LessonApprovedBies { get; set; } = new List<Lesson>();

    public virtual ICollection<Lesson> LessonCreatedBies { get; set; } = new List<Lesson>();

    public virtual ICollection<LoginHistory> LoginHistories { get; set; } = new List<LoginHistory>();

    public virtual ICollection<Notification> Notifications { get; set; } = new List<Notification>();

    public virtual ICollection<ParentSource> ParentSources { get; set; } = new List<ParentSource>();

    public virtual ICollection<Question> QuestionApprovedBies { get; set; } = new List<Question>();

    public virtual ICollection<Question> QuestionCreatedBies { get; set; } = new List<Question>();

    public virtual ICollection<SourceVideo> SourceVideos { get; set; } = new List<SourceVideo>();

    public virtual ICollection<TimeExtensionRequest> TimeExtensionRequests { get; set; } = new List<TimeExtensionRequest>();

    public virtual ICollection<TimeLimitPolicy> TimeLimitPolicies { get; set; } = new List<TimeLimitPolicy>();

    public virtual ICollection<UserConsent> UserConsents { get; set; } = new List<UserConsent>();

    public virtual UserSetting? UserSetting { get; set; }

    public virtual ICollection<Role> Roles { get; set; } = new List<Role>();
}
