using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using SafeLearnKids.Web.Models.Entities;

namespace SafeLearnKids.Web.Data;

public partial class SafeLearnDbContext : DbContext
{
    public SafeLearnDbContext(DbContextOptions<SafeLearnDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Assignment> Assignments { get; set; }

    public virtual DbSet<AuditLog> AuditLogs { get; set; }

    public virtual DbSet<Badge> Badges { get; set; }

    public virtual DbSet<BlockedKeyword> BlockedKeywords { get; set; }

    public virtual DbSet<ChildBadge> ChildBadges { get; set; }

    public virtual DbSet<ChildPath> ChildPaths { get; set; }

    public virtual DbSet<ChildProfile> ChildProfiles { get; set; }

    public virtual DbSet<ChildSubject> ChildSubjects { get; set; }

    public virtual DbSet<ContentReport> ContentReports { get; set; }

    public virtual DbSet<ContentSource> ContentSources { get; set; }

    public virtual DbSet<CurationSuggestion> CurationSuggestions { get; set; }

    public virtual DbSet<DailyUsage> DailyUsages { get; set; }

    public virtual DbSet<DataRequest> DataRequests { get; set; }

    public virtual DbSet<Favorite> Favorites { get; set; }

    public virtual DbSet<GradeLevel> GradeLevels { get; set; }

    public virtual DbSet<LearningGoal> LearningGoals { get; set; }

    public virtual DbSet<LearningPath> LearningPaths { get; set; }

    public virtual DbSet<LearningPathItem> LearningPathItems { get; set; }

    public virtual DbSet<LearningSession> LearningSessions { get; set; }

    public virtual DbSet<Lesson> Lessons { get; set; }

    public virtual DbSet<LessonActivity> LessonActivities { get; set; }

    public virtual DbSet<LessonContent> LessonContents { get; set; }

    public virtual DbSet<LessonTag> LessonTags { get; set; }

    public virtual DbSet<LoginHistory> LoginHistories { get; set; }

    public virtual DbSet<Notification> Notifications { get; set; }

    public virtual DbSet<ParentSource> ParentSources { get; set; }

    public virtual DbSet<Question> Questions { get; set; }

    public virtual DbSet<QuestionOption> QuestionOptions { get; set; }

    public virtual DbSet<Quiz> Quizzes { get; set; }

    public virtual DbSet<QuizAttempt> QuizAttempts { get; set; }

    public virtual DbSet<QuizAttemptAnswer> QuizAttemptAnswers { get; set; }

    public virtual DbSet<QuizQuestion> QuizQuestions { get; set; }

    public virtual DbSet<Role> Roles { get; set; }

    public virtual DbSet<SearchLog> SearchLogs { get; set; }

    public virtual DbSet<SourceVideo> SourceVideos { get; set; }

    public virtual DbSet<SourceVideoSource> SourceVideoSources { get; set; }

    public virtual DbSet<Streak> Streaks { get; set; }

    public virtual DbSet<Subject> Subjects { get; set; }

    public virtual DbSet<SubjectGrade> SubjectGrades { get; set; }

    public virtual DbSet<SubjectProgress> SubjectProgresses { get; set; }

    public virtual DbSet<SystemMetricsDaily> SystemMetricsDailies { get; set; }

    public virtual DbSet<Tag> Tags { get; set; }

    public virtual DbSet<TimeExtensionRequest> TimeExtensionRequests { get; set; }

    public virtual DbSet<TimeLimitOverride> TimeLimitOverrides { get; set; }

    public virtual DbSet<TimeLimitPolicy> TimeLimitPolicies { get; set; }

    public virtual DbSet<User> Users { get; set; }

    public virtual DbSet<UserConsent> UserConsents { get; set; }

    public virtual DbSet<UserSetting> UserSettings { get; set; }

    public virtual DbSet<WeeklyReport> WeeklyReports { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Assignment>(entity =>
        {
            entity.HasIndex(e => e.AssignedById, "IX_Assignments_AssignedById");

            entity.HasIndex(e => new { e.ChildProfileId, e.Status }, "IX_Assignments_ChildProfileId_Status");

            entity.HasIndex(e => e.LessonId, "IX_Assignments_LessonId");

            entity.HasIndex(e => e.QuizId, "IX_Assignments_QuizId");

            entity.HasIndex(e => new { e.Id, e.ChildProfileId, e.QuizId }, "UQ_Assignments_Id_ChildProfileId_QuizId").IsUnique();

            entity.HasIndex(e => new { e.ChildProfileId, e.LessonId }, "UX_Assignments_ChildProfileId_LessonId")
                .IsUnique()
                .HasFilter("([LessonId] IS NOT NULL AND ([Status] IN (N'Assigned', N'InProgress')))");

            entity.Property(e => e.AssignedAt).HasDefaultValueSql("(sysutcdatetime())", "DF_Assignments_AssignedAt");
            entity.Property(e => e.Note).HasMaxLength(300);
            entity.Property(e => e.QuizId).IsRequired();
            entity.Property(e => e.Status)
                .HasMaxLength(30)
                .HasDefaultValue("Assigned", "DF_Assignments_Status");

            entity.HasOne(d => d.AssignedBy).WithMany(p => p.Assignments)
                .HasForeignKey(d => d.AssignedById)
                .OnDelete(DeleteBehavior.ClientSetNull);

            entity.HasOne(d => d.ChildProfile).WithMany(p => p.Assignments).HasForeignKey(d => d.ChildProfileId);

            entity.HasOne(d => d.Lesson).WithMany(p => p.Assignments).HasForeignKey(d => d.LessonId);

            entity.HasOne(d => d.Quiz).WithMany(p => p.Assignments).HasForeignKey(d => d.QuizId);
        });

        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.HasIndex(e => new { e.ActorId, e.CreatedAt }, "IX_AuditLogs_ActorId_CreatedAt");

            entity.HasIndex(e => new { e.EntityType, e.EntityId }, "IX_AuditLogs_EntityType_EntityId");

            entity.Property(e => e.Action).HasMaxLength(60);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())", "DF_AuditLogs_CreatedAt");
            entity.Property(e => e.EntityId).HasMaxLength(64);
            entity.Property(e => e.EntityType).HasMaxLength(60);
            entity.Property(e => e.IpAddress).HasMaxLength(45);

            entity.HasOne(d => d.Actor).WithMany(p => p.AuditLogs)
                .HasForeignKey(d => d.ActorId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<Badge>(entity =>
        {
            entity.HasIndex(e => e.Code, "UQ_Badges_Code").IsUnique();

            entity.Property(e => e.Code).HasMaxLength(40);
            entity.Property(e => e.CriteriaType).HasMaxLength(30);
            entity.Property(e => e.Description).HasMaxLength(200);
            entity.Property(e => e.IconKey).HasMaxLength(30);
            entity.Property(e => e.IsActive).HasDefaultValue(true, "DF_Badges_IsActive");
            entity.Property(e => e.Name).HasMaxLength(80);
        });

        modelBuilder.Entity<BlockedKeyword>(entity =>
        {
            entity.HasIndex(e => e.CreatedById, "IX_BlockedKeywords_CreatedById");

            entity.HasIndex(e => e.ParentId, "IX_BlockedKeywords_ParentId");

            entity.HasIndex(e => new { e.Keyword, e.ParentId }, "UQ_BlockedKeywords_Keyword_ParentId").IsUnique();

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())", "DF_BlockedKeywords_CreatedAt");
            entity.Property(e => e.Keyword).HasMaxLength(60);

            entity.HasOne(d => d.CreatedBy).WithMany(p => p.BlockedKeywordCreatedBies).HasForeignKey(d => d.CreatedById);

            entity.HasOne(d => d.Parent).WithMany(p => p.BlockedKeywordParents)
                .HasForeignKey(d => d.ParentId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ChildBadge>(entity =>
        {
            entity.HasIndex(e => e.BadgeId, "IX_ChildBadges_BadgeId");

            entity.HasIndex(e => new { e.ChildProfileId, e.BadgeId }, "UQ_ChildBadges_ChildProfileId_BadgeId").IsUnique();

            entity.Property(e => e.AwardedAt).HasDefaultValueSql("(sysutcdatetime())", "DF_ChildBadges_AwardedAt");

            entity.HasOne(d => d.Badge).WithMany(p => p.ChildBadges)
                .HasForeignKey(d => d.BadgeId)
                .OnDelete(DeleteBehavior.ClientSetNull);

            entity.HasOne(d => d.ChildProfile).WithMany(p => p.ChildBadges).HasForeignKey(d => d.ChildProfileId);
        });

        modelBuilder.Entity<ChildPath>(entity =>
        {
            entity.HasIndex(e => e.CurrentItemId, "IX_ChildPaths_CurrentItemId");

            entity.HasIndex(e => e.PathId, "IX_ChildPaths_PathId");

            entity.HasIndex(e => new { e.ChildProfileId, e.PathId }, "UQ_ChildPaths_ChildProfileId_PathId").IsUnique();

            entity.Property(e => e.Status)
                .HasMaxLength(30)
                .HasDefaultValue("NotStarted", "DF_ChildPaths_Status");

            entity.HasOne(d => d.ChildProfile).WithMany(p => p.ChildPaths).HasForeignKey(d => d.ChildProfileId);

            entity.HasOne(d => d.CurrentItem).WithMany(p => p.ChildPathCurrentItems).HasForeignKey(d => d.CurrentItemId);

            entity.HasOne(d => d.Path).WithMany(p => p.ChildPaths)
                .HasForeignKey(d => d.PathId)
                .OnDelete(DeleteBehavior.ClientSetNull);

            entity.HasOne(d => d.LearningPathItem).WithMany(p => p.ChildPathLearningPathItems)
                .HasPrincipalKey(p => new { p.Id, p.PathId })
                .HasForeignKey(d => new { d.CurrentItemId, d.PathId });
        });

        modelBuilder.Entity<ChildProfile>(entity =>
        {
            entity.HasIndex(e => e.GradeLevelId, "IX_ChildProfiles_GradeLevelId");

            entity.HasIndex(e => new { e.ParentId, e.DisplayName }, "UQ_ChildProfiles_ParentId_DisplayName").IsUnique();

            entity.HasIndex(e => e.UserId, "UX_ChildProfiles_UserId")
                .IsUnique()
                .HasFilter("([UserId] IS NOT NULL)");

            entity.Property(e => e.AvatarKey).HasMaxLength(30);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())", "DF_ChildProfiles_CreatedAt");
            entity.Property(e => e.DisplayName).HasMaxLength(60);
            entity.Property(e => e.IsActive).HasDefaultValue(true, "DF_ChildProfiles_IsActive");
            entity.Property(e => e.PinHash).HasMaxLength(500);

            entity.HasOne(d => d.GradeLevel).WithMany(p => p.ChildProfiles)
                .HasForeignKey(d => d.GradeLevelId)
                .OnDelete(DeleteBehavior.ClientSetNull);

            entity.HasOne(d => d.Parent).WithMany(p => p.ChildProfileParents).HasForeignKey(d => d.ParentId);

            entity.HasOne(d => d.User).WithOne(p => p.ChildProfileUser).HasForeignKey<ChildProfile>(d => d.UserId);
        });

        modelBuilder.Entity<ChildSubject>(entity =>
        {
            entity.HasIndex(e => e.SubjectId, "IX_ChildSubjects_SubjectId");

            entity.HasIndex(e => new { e.ChildProfileId, e.SubjectId }, "UQ_ChildSubjects_ChildProfileId_SubjectId").IsUnique();

            entity.HasOne(d => d.ChildProfile).WithMany(p => p.ChildSubjects).HasForeignKey(d => d.ChildProfileId);

            entity.HasOne(d => d.Subject).WithMany(p => p.ChildSubjects)
                .HasForeignKey(d => d.SubjectId)
                .OnDelete(DeleteBehavior.ClientSetNull);
        });

        modelBuilder.Entity<ContentReport>(entity =>
        {
            entity.HasIndex(e => e.ChildProfileId, "IX_ContentReports_ChildProfileId");

            entity.HasIndex(e => e.HandledById, "IX_ContentReports_HandledById");

            entity.HasIndex(e => e.LessonId, "IX_ContentReports_LessonId");

            entity.HasIndex(e => e.ReportedById, "IX_ContentReports_ReportedById");

            entity.HasIndex(e => e.SourceVideoId, "IX_ContentReports_SourceVideoId");

            entity.HasIndex(e => new { e.Status, e.CreatedAt }, "IX_ContentReports_Status_CreatedAt");

            entity.Property(e => e.Comment).HasMaxLength(500);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())", "DF_ContentReports_CreatedAt");
            entity.Property(e => e.Reason)
                .HasMaxLength(30)
                .HasDefaultValue("Inappropriate", "DF_ContentReports_Reason");
            entity.Property(e => e.Resolution).HasMaxLength(500);
            entity.Property(e => e.RowVersion)
                .IsRowVersion()
                .IsConcurrencyToken();
            entity.Property(e => e.Status)
                .HasMaxLength(30)
                .HasDefaultValue("Open", "DF_ContentReports_Status");

            entity.HasOne(d => d.ChildProfile).WithMany(p => p.ContentReports)
                .HasForeignKey(d => d.ChildProfileId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(d => d.HandledBy).WithMany(p => p.ContentReportHandledBies).HasForeignKey(d => d.HandledById);

            entity.HasOne(d => d.Lesson).WithMany(p => p.ContentReports).HasForeignKey(d => d.LessonId);

            entity.HasOne(d => d.ReportedBy).WithMany(p => p.ContentReportReportedBies).HasForeignKey(d => d.ReportedById);

            entity.HasOne(d => d.SourceVideo).WithMany(p => p.ContentReports).HasForeignKey(d => d.SourceVideoId);
        });

        modelBuilder.Entity<ContentSource>(entity =>
        {
            entity.HasIndex(e => e.ReviewedById, "IX_ContentSources_ReviewedById");

            entity.HasIndex(e => e.Status, "IX_ContentSources_Status");

            entity.HasIndex(e => e.SuggestedById, "IX_ContentSources_SuggestedById");

            entity.HasIndex(e => new { e.Platform, e.ExternalId }, "UQ_ContentSources_Platform_ExternalId").IsUnique();

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())", "DF_ContentSources_CreatedAt");
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.Property(e => e.ExternalId).HasMaxLength(64);
            entity.Property(e => e.LastSyncError).HasMaxLength(300);
            entity.Property(e => e.Name).HasMaxLength(150);
            entity.Property(e => e.Platform)
                .HasMaxLength(30)
                .HasDefaultValue("YouTubeChannel", "DF_ContentSources_Platform");
            entity.Property(e => e.ReviewNote).HasMaxLength(500);
            entity.Property(e => e.RowVersion)
                .IsRowVersion()
                .IsConcurrencyToken();
            entity.Property(e => e.Status)
                .HasMaxLength(30)
                .HasDefaultValue("Suggested", "DF_ContentSources_Status");
            entity.Property(e => e.Url).HasMaxLength(300);

            entity.HasOne(d => d.ReviewedBy).WithMany(p => p.ContentSourceReviewedBies).HasForeignKey(d => d.ReviewedById);

            entity.HasOne(d => d.SuggestedBy).WithMany(p => p.ContentSourceSuggestedBies).HasForeignKey(d => d.SuggestedById);
        });

        modelBuilder.Entity<CurationSuggestion>(entity =>
        {
            entity.HasIndex(e => e.DecidedById, "IX_CurationSuggestions_DecidedById");

            entity.HasIndex(e => e.SourceId, "IX_CurationSuggestions_SourceId");

            entity.HasIndex(e => new { e.Status, e.CreatedAt }, "IX_CurationSuggestions_Status_CreatedAt");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())", "DF_CurationSuggestions_CreatedAt");
            entity.Property(e => e.ModelName).HasMaxLength(60);
            entity.Property(e => e.SafetyFlags).HasMaxLength(500);
            entity.Property(e => e.Score).HasColumnType("decimal(5, 2)");
            entity.Property(e => e.Status)
                .HasMaxLength(30)
                .HasDefaultValue("Pending", "DF_CurationSuggestions_Status");

            entity.HasOne(d => d.DecidedBy).WithMany(p => p.CurationSuggestions).HasForeignKey(d => d.DecidedById);

            entity.HasOne(d => d.Source).WithMany(p => p.CurationSuggestions).HasForeignKey(d => d.SourceId);
        });

        modelBuilder.Entity<DailyUsage>(entity =>
        {
            entity.HasIndex(e => new { e.ChildProfileId, e.UsageDate }, "UQ_DailyUsages_ChildProfileId_UsageDate").IsUnique();

            entity.HasOne(d => d.ChildProfile).WithMany(p => p.DailyUsages).HasForeignKey(d => d.ChildProfileId);
        });

        modelBuilder.Entity<DataRequest>(entity =>
        {
            entity.HasIndex(e => e.ParentId, "IX_DataRequests_ParentId");

            entity.HasIndex(e => e.ProcessedById, "IX_DataRequests_ProcessedById");

            entity.Property(e => e.ChildName).HasMaxLength(60);
            entity.Property(e => e.DecisionNote).HasMaxLength(500);
            entity.Property(e => e.RequestType).HasMaxLength(30);
            entity.Property(e => e.RequestedAt).HasDefaultValueSql("(sysutcdatetime())", "DF_DataRequests_RequestedAt");
            entity.Property(e => e.Status)
                .HasMaxLength(30)
                .HasDefaultValue("Pending", "DF_DataRequests_Status");

            entity.HasOne(d => d.Parent).WithMany(p => p.DataRequestParents).HasForeignKey(d => d.ParentId);

            entity.HasOne(d => d.ProcessedBy).WithMany(p => p.DataRequestProcessedBies).HasForeignKey(d => d.ProcessedById);
        });

        modelBuilder.Entity<Favorite>(entity =>
        {
            entity.HasIndex(e => e.LessonId, "IX_Favorites_LessonId");

            entity.HasIndex(e => new { e.ChildProfileId, e.LessonId }, "UQ_Favorites_ChildProfileId_LessonId").IsUnique();

            entity.Property(e => e.FavoritedAt).HasDefaultValueSql("(sysutcdatetime())", "DF_Favorites_FavoritedAt");

            entity.HasOne(d => d.ChildProfile).WithMany(p => p.Favorites).HasForeignKey(d => d.ChildProfileId);

            entity.HasOne(d => d.Lesson).WithMany(p => p.Favorites)
                .HasForeignKey(d => d.LessonId)
                .OnDelete(DeleteBehavior.ClientSetNull);
        });

        modelBuilder.Entity<GradeLevel>(entity =>
        {
            entity.HasIndex(e => e.Level, "UQ_GradeLevels_Level").IsUnique();

            entity.Property(e => e.Name).HasMaxLength(20);
        });

        modelBuilder.Entity<LearningGoal>(entity =>
        {
            entity.HasIndex(e => e.CreatedById, "IX_LearningGoals_CreatedById");

            entity.HasIndex(e => new { e.ChildProfileId, e.WeekStart }, "UQ_LearningGoals_ChildProfileId_WeekStart").IsUnique();

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())", "DF_LearningGoals_CreatedAt");

            entity.HasOne(d => d.ChildProfile).WithMany(p => p.LearningGoals).HasForeignKey(d => d.ChildProfileId);

            entity.HasOne(d => d.CreatedBy).WithMany(p => p.LearningGoals).HasForeignKey(d => d.CreatedById);
        });

        modelBuilder.Entity<LearningPath>(entity =>
        {
            entity.HasIndex(e => e.GradeLevelId, "IX_LearningPaths_GradeLevelId");

            entity.HasIndex(e => e.SubjectId, "IX_LearningPaths_SubjectId");

            entity.Property(e => e.Description).HasMaxLength(300);
            entity.Property(e => e.IsActive).HasDefaultValue(true, "DF_LearningPaths_IsActive");
            entity.Property(e => e.Name).HasMaxLength(120);

            entity.HasOne(d => d.GradeLevel).WithMany(p => p.LearningPaths)
                .HasForeignKey(d => d.GradeLevelId)
                .OnDelete(DeleteBehavior.ClientSetNull);

            entity.HasOne(d => d.Subject).WithMany(p => p.LearningPaths)
                .HasForeignKey(d => d.SubjectId)
                .OnDelete(DeleteBehavior.ClientSetNull);
        });

        modelBuilder.Entity<LearningPathItem>(entity =>
        {
            entity.HasIndex(e => e.LessonId, "IX_LearningPathItems_LessonId");

            entity.HasIndex(e => new { e.Id, e.PathId }, "UQ_LearningPathItems_Id_PathId").IsUnique();

            entity.HasIndex(e => new { e.PathId, e.LessonId }, "UQ_LearningPathItems_PathId_LessonId").IsUnique();

            entity.Property(e => e.IsRequired).HasDefaultValue(true, "DF_LearningPathItems_IsRequired");

            entity.HasOne(d => d.Lesson).WithMany(p => p.LearningPathItems)
                .HasForeignKey(d => d.LessonId)
                .OnDelete(DeleteBehavior.ClientSetNull);

            entity.HasOne(d => d.Path).WithMany(p => p.LearningPathItems).HasForeignKey(d => d.PathId);
        });

        modelBuilder.Entity<LearningSession>(entity =>
        {
            entity.HasIndex(e => new { e.ChildProfileId, e.StartedAt }, "IX_LearningSessions_ChildProfileId_StartedAt");

            entity.HasIndex(e => new { e.Id, e.ChildProfileId }, "UQ_LearningSessions_Id_ChildProfileId").IsUnique();

            entity.Property(e => e.EndReason).HasMaxLength(30);
            entity.Property(e => e.LastHeartbeatAt).HasDefaultValueSql("(sysutcdatetime())", "DF_LearningSessions_LastHeartbeatAt");
            entity.Property(e => e.StartedAt).HasDefaultValueSql("(sysutcdatetime())", "DF_LearningSessions_StartedAt");

            entity.HasOne(d => d.ChildProfile).WithMany(p => p.LearningSessions).HasForeignKey(d => d.ChildProfileId);
        });

        modelBuilder.Entity<Lesson>(entity =>
        {
            entity.HasIndex(e => e.ApprovedById, "IX_Lessons_ApprovedById");

            entity.HasIndex(e => e.CreatedById, "IX_Lessons_CreatedById");

            entity.HasIndex(e => e.GradeLevelId, "IX_Lessons_GradeLevelId");

            entity.HasIndex(e => new { e.SubjectId, e.GradeLevelId, e.Status }, "IX_Lessons_SubjectId_GradeLevelId_Status");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())", "DF_Lessons_CreatedAt");
            entity.Property(e => e.DurationMin).HasDefaultValue(10, "DF_Lessons_DurationMin");
            entity.Property(e => e.LessonType)
                .HasMaxLength(30)
                .HasDefaultValue("Video", "DF_Lessons_LessonType");
            entity.Property(e => e.RowVersion)
                .IsRowVersion()
                .IsConcurrencyToken();
            entity.Property(e => e.Status)
                .HasMaxLength(30)
                .HasDefaultValue("Draft", "DF_Lessons_Status");
            entity.Property(e => e.Summary).HasMaxLength(500);
            entity.Property(e => e.Title).HasMaxLength(200);
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("(sysutcdatetime())", "DF_Lessons_UpdatedAt");

            entity.HasOne(d => d.ApprovedBy).WithMany(p => p.LessonApprovedBies).HasForeignKey(d => d.ApprovedById);

            entity.HasOne(d => d.CreatedBy).WithMany(p => p.LessonCreatedBies)
                .HasForeignKey(d => d.CreatedById)
                .OnDelete(DeleteBehavior.ClientSetNull);

            entity.HasOne(d => d.GradeLevel).WithMany(p => p.Lessons)
                .HasForeignKey(d => d.GradeLevelId)
                .OnDelete(DeleteBehavior.ClientSetNull);

            entity.HasOne(d => d.Subject).WithMany(p => p.Lessons)
                .HasForeignKey(d => d.SubjectId)
                .OnDelete(DeleteBehavior.ClientSetNull);

            entity.HasOne(d => d.SubjectGrade).WithMany(p => p.Lessons)
                .HasPrincipalKey(p => new { p.SubjectId, p.GradeLevelId })
                .HasForeignKey(d => new { d.SubjectId, d.GradeLevelId })
                .OnDelete(DeleteBehavior.ClientSetNull);
        });

        modelBuilder.Entity<LessonActivity>(entity =>
        {
            entity.HasIndex(e => e.LessonId, "IX_LessonActivities_LessonId");

            entity.HasIndex(e => e.SessionId, "IX_LessonActivities_SessionId");

            entity.HasIndex(e => new { e.ChildProfileId, e.LessonId }, "UQ_LessonActivities_ChildProfileId_LessonId").IsUnique();

            entity.Property(e => e.StartedAt).HasDefaultValueSql("(sysutcdatetime())", "DF_LessonActivities_StartedAt");
            entity.Property(e => e.Status)
                .HasMaxLength(30)
                .HasDefaultValue("Started", "DF_LessonActivities_Status");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("(sysutcdatetime())", "DF_LessonActivities_UpdatedAt");

            entity.HasOne(d => d.ChildProfile).WithMany(p => p.LessonActivities).HasForeignKey(d => d.ChildProfileId);

            entity.HasOne(d => d.Lesson).WithMany(p => p.LessonActivities)
                .HasForeignKey(d => d.LessonId)
                .OnDelete(DeleteBehavior.ClientSetNull);

            entity.HasOne(d => d.Session).WithMany(p => p.LessonActivitySessions).HasForeignKey(d => d.SessionId);

            entity.HasOne(d => d.LearningSession).WithMany(p => p.LessonActivityLearningSessions)
                .HasPrincipalKey(p => new { p.Id, p.ChildProfileId })
                .HasForeignKey(d => new { d.SessionId, d.ChildProfileId });
        });

        modelBuilder.Entity<LessonContent>(entity =>
        {
            entity.HasIndex(e => new { e.LessonId, e.SortOrder }, "IX_LessonContents_LessonId_SortOrder");

            entity.HasIndex(e => e.SourceVideoId, "IX_LessonContents_SourceVideoId");

            entity.Property(e => e.ContentKind)
                .HasMaxLength(30)
                .HasDefaultValue("Video", "DF_LessonContents_ContentKind");
            entity.Property(e => e.FileName).HasMaxLength(150);
            entity.Property(e => e.FileUrl).HasMaxLength(300);
            entity.Property(e => e.Title).HasMaxLength(150);

            entity.HasOne(d => d.Lesson).WithMany(p => p.LessonContents).HasForeignKey(d => d.LessonId);

            entity.HasOne(d => d.SourceVideo).WithMany(p => p.LessonContents).HasForeignKey(d => d.SourceVideoId);
        });

        modelBuilder.Entity<LessonTag>(entity =>
        {
            entity.HasIndex(e => e.TagId, "IX_LessonTags_TagId");

            entity.HasIndex(e => new { e.LessonId, e.TagId }, "UQ_LessonTags_LessonId_TagId").IsUnique();

            entity.HasOne(d => d.Lesson).WithMany(p => p.LessonTags).HasForeignKey(d => d.LessonId);

            entity.HasOne(d => d.Tag).WithMany(p => p.LessonTags).HasForeignKey(d => d.TagId);
        });

        modelBuilder.Entity<LoginHistory>(entity =>
        {
            entity.HasIndex(e => new { e.IpAddress, e.CreatedAt }, "IX_LoginHistories_IpAddress_CreatedAt");

            entity.HasIndex(e => new { e.UserId, e.CreatedAt }, "IX_LoginHistories_UserId_CreatedAt");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())", "DF_LoginHistories_CreatedAt");
            entity.Property(e => e.FailReason).HasMaxLength(100);
            entity.Property(e => e.IpAddress).HasMaxLength(45);
            entity.Property(e => e.UserAgent).HasMaxLength(300);
            entity.Property(e => e.UserNameTried).HasMaxLength(256);

            entity.HasOne(d => d.User).WithMany(p => p.LoginHistories)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<Notification>(entity =>
        {
            entity.HasIndex(e => new { e.UserId, e.IsRead, e.CreatedAt }, "IX_Notifications_UserId_IsRead_CreatedAt");

            entity.Property(e => e.Body).HasMaxLength(500);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())", "DF_Notifications_CreatedAt");
            entity.Property(e => e.LinkUrl).HasMaxLength(200);
            entity.Property(e => e.NotificationType).HasMaxLength(30);
            entity.Property(e => e.Title).HasMaxLength(120);

            entity.HasOne(d => d.User).WithMany(p => p.Notifications).HasForeignKey(d => d.UserId);
        });

        modelBuilder.Entity<ParentSource>(entity =>
        {
            entity.HasIndex(e => e.SourceId, "IX_ParentSources_SourceId");

            entity.HasIndex(e => new { e.ParentId, e.SourceId }, "UQ_ParentSources_ParentId_SourceId").IsUnique();

            entity.Property(e => e.AddedAt).HasDefaultValueSql("(sysutcdatetime())", "DF_ParentSources_AddedAt");
            entity.Property(e => e.Status)
                .HasMaxLength(30)
                .HasDefaultValue("Allowed", "DF_ParentSources_Status");

            entity.HasOne(d => d.Parent).WithMany(p => p.ParentSources).HasForeignKey(d => d.ParentId);

            entity.HasOne(d => d.Source).WithMany(p => p.ParentSources)
                .HasForeignKey(d => d.SourceId)
                .OnDelete(DeleteBehavior.ClientSetNull);
        });

        modelBuilder.Entity<Question>(entity =>
        {
            entity.HasIndex(e => e.ApprovedById, "IX_Questions_ApprovedById");

            entity.HasIndex(e => e.CreatedById, "IX_Questions_CreatedById");

            entity.HasIndex(e => new { e.LessonId, e.Status }, "IX_Questions_LessonId_Status");

            entity.HasIndex(e => e.SourceVideoId, "IX_Questions_SourceVideoId");

            entity.Property(e => e.AiModelName).HasMaxLength(60);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())", "DF_Questions_CreatedAt");
            entity.Property(e => e.Difficulty).HasDefaultValue(1, "DF_Questions_Difficulty");
            entity.Property(e => e.Explanation).HasMaxLength(500);
            entity.Property(e => e.Origin)
                .HasMaxLength(30)
                .HasDefaultValue("Manual", "DF_Questions_Origin");
            entity.Property(e => e.QuestionType)
                .HasMaxLength(30)
                .HasDefaultValue("SingleChoice", "DF_Questions_QuestionType");
            entity.Property(e => e.Status)
                .HasMaxLength(30)
                .HasDefaultValue("Draft", "DF_Questions_Status");
            entity.Property(e => e.Text).HasMaxLength(500);

            entity.HasOne(d => d.ApprovedBy).WithMany(p => p.QuestionApprovedBies).HasForeignKey(d => d.ApprovedById);

            entity.HasOne(d => d.CreatedBy).WithMany(p => p.QuestionCreatedBies)
                .HasForeignKey(d => d.CreatedById)
                .OnDelete(DeleteBehavior.ClientSetNull);

            entity.HasOne(d => d.Lesson).WithMany(p => p.Questions)
                .HasForeignKey(d => d.LessonId)
                .OnDelete(DeleteBehavior.ClientSetNull);

            entity.HasOne(d => d.SourceVideo).WithMany(p => p.Questions).HasForeignKey(d => d.SourceVideoId);
        });

        modelBuilder.Entity<QuestionOption>(entity =>
        {
            entity.HasIndex(e => e.QuestionId, "IX_QuestionOptions_QuestionId");

            entity.HasIndex(e => new { e.Id, e.QuestionId }, "UQ_QuestionOptions_Id_QuestionId").IsUnique();

            entity.Property(e => e.Text).HasMaxLength(300);

            entity.HasOne(d => d.Question).WithMany(p => p.QuestionOptions).HasForeignKey(d => d.QuestionId);
        });

        modelBuilder.Entity<Quiz>(entity =>
        {
            entity.HasIndex(e => e.LessonId, "IX_Quizzes_LessonId");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())", "DF_Quizzes_CreatedAt");
            entity.Property(e => e.PassPercent).HasDefaultValue(70, "DF_Quizzes_PassPercent");
            entity.Property(e => e.ShuffleQuestions).HasDefaultValue(true, "DF_Quizzes_ShuffleQuestions");
            entity.Property(e => e.Status)
                .HasMaxLength(30)
                .HasDefaultValue("Draft", "DF_Quizzes_Status");
            entity.Property(e => e.Title).HasMaxLength(150);

            entity.HasOne(d => d.Lesson).WithMany(p => p.Quizzes)
                .HasForeignKey(d => d.LessonId)
                .OnDelete(DeleteBehavior.ClientSetNull);
        });

        modelBuilder.Entity<QuizAttempt>(entity =>
        {
            entity.HasIndex(e => e.AssignmentId, "IX_QuizAttempts_AssignmentId");

            entity.HasIndex(e => new { e.ChildProfileId, e.QuizId }, "IX_QuizAttempts_ChildProfileId_QuizId");

            entity.HasIndex(e => e.QuizId, "IX_QuizAttempts_QuizId");

            entity.HasIndex(e => new { e.Id, e.QuizId }, "UQ_QuizAttempts_Id_QuizId").IsUnique();

            entity.Property(e => e.ScorePercent).HasColumnType("decimal(5, 2)");
            entity.Property(e => e.StartedAt).HasDefaultValueSql("(sysutcdatetime())", "DF_QuizAttempts_StartedAt");

            entity.HasOne(d => d.Assignment).WithMany(p => p.QuizAttemptAssignments).HasForeignKey(d => d.AssignmentId);

            entity.HasOne(d => d.ChildProfile).WithMany(p => p.QuizAttempts).HasForeignKey(d => d.ChildProfileId);

            entity.HasOne(d => d.Quiz).WithMany(p => p.QuizAttempts)
                .HasForeignKey(d => d.QuizId)
                .OnDelete(DeleteBehavior.ClientSetNull);

            entity.HasOne(d => d.AssignmentNavigation).WithMany(p => p.QuizAttemptAssignmentNavigations)
                .HasPrincipalKey(p => new { p.Id, p.ChildProfileId, p.QuizId })
                .HasForeignKey(d => new { d.AssignmentId, d.ChildProfileId, d.QuizId });
        });

        modelBuilder.Entity<QuizAttemptAnswer>(entity =>
        {
            entity.HasIndex(e => e.AttemptId, "IX_QuizAttemptAnswers_AttemptId");

            entity.HasIndex(e => e.QuestionId, "IX_QuizAttemptAnswers_QuestionId");

            entity.HasIndex(e => e.QuizId, "IX_QuizAttemptAnswers_QuizId");

            entity.HasIndex(e => e.SelectedOptionId, "IX_QuizAttemptAnswers_SelectedOptionId");

            entity.HasIndex(e => new { e.AttemptId, e.QuestionId }, "UQ_QuizAttemptAnswers_AttemptId_QuestionId").IsUnique();

            entity.Property(e => e.AnsweredAt).HasDefaultValueSql("(sysutcdatetime())", "DF_QuizAttemptAnswers_AnsweredAt");

            entity.HasOne(d => d.Attempt).WithMany(p => p.QuizAttemptAnswerAttempts).HasForeignKey(d => d.AttemptId);

            entity.HasOne(d => d.Question).WithMany(p => p.QuizAttemptAnswers)
                .HasForeignKey(d => d.QuestionId)
                .OnDelete(DeleteBehavior.ClientSetNull);

            entity.HasOne(d => d.Quiz).WithMany(p => p.QuizAttemptAnswers)
                .HasForeignKey(d => d.QuizId)
                .OnDelete(DeleteBehavior.ClientSetNull);

            entity.HasOne(d => d.SelectedOption).WithMany(p => p.QuizAttemptAnswerSelectedOptions).HasForeignKey(d => d.SelectedOptionId);

            entity.HasOne(d => d.QuizAttempt).WithMany(p => p.QuizAttemptAnswerQuizAttempts)
                .HasPrincipalKey(p => new { p.Id, p.QuizId })
                .HasForeignKey(d => new { d.AttemptId, d.QuizId })
                .OnDelete(DeleteBehavior.ClientSetNull);

            entity.HasOne(d => d.QuizQuestion).WithMany(p => p.QuizAttemptAnswers)
                .HasPrincipalKey(p => new { p.QuizId, p.QuestionId })
                .HasForeignKey(d => new { d.QuizId, d.QuestionId })
                .OnDelete(DeleteBehavior.ClientSetNull);

            entity.HasOne(d => d.QuestionOption).WithMany(p => p.QuizAttemptAnswerQuestionOptions)
                .HasPrincipalKey(p => new { p.Id, p.QuestionId })
                .HasForeignKey(d => new { d.SelectedOptionId, d.QuestionId });
        });

        modelBuilder.Entity<QuizQuestion>(entity =>
        {
            entity.HasIndex(e => e.QuestionId, "IX_QuizQuestions_QuestionId");

            entity.HasIndex(e => new { e.QuizId, e.QuestionId }, "UQ_QuizQuestions_QuizId_QuestionId").IsUnique();

            entity.Property(e => e.Points).HasDefaultValue(1, "DF_QuizQuestions_Points");

            entity.HasOne(d => d.Question).WithMany(p => p.QuizQuestions)
                .HasForeignKey(d => d.QuestionId)
                .OnDelete(DeleteBehavior.ClientSetNull);

            entity.HasOne(d => d.Quiz).WithMany(p => p.QuizQuestions).HasForeignKey(d => d.QuizId);
        });

        modelBuilder.Entity<Role>(entity =>
        {
            entity.HasIndex(e => e.NormalizedName, "RoleNameIndex")
                .IsUnique()
                .HasFilter("([NormalizedName] IS NOT NULL)");

            entity.Property(e => e.Name).HasMaxLength(256);
            entity.Property(e => e.NormalizedName).HasMaxLength(256);
        });

        modelBuilder.Entity<SearchLog>(entity =>
        {
            entity.HasIndex(e => new { e.ChildProfileId, e.CreatedAt }, "IX_SearchLogs_ChildProfileId_CreatedAt");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())", "DF_SearchLogs_CreatedAt");
            entity.Property(e => e.Query).HasMaxLength(200);

            entity.HasOne(d => d.ChildProfile).WithMany(p => p.SearchLogs).HasForeignKey(d => d.ChildProfileId);
        });

        modelBuilder.Entity<SourceVideo>(entity =>
        {
            entity.HasIndex(e => e.ReviewedById, "IX_SourceVideos_ReviewedById");

            entity.HasIndex(e => e.Status, "IX_SourceVideos_Status");

            entity.HasIndex(e => new { e.Platform, e.VideoId }, "UQ_SourceVideos_Platform_VideoId").IsUnique();

            entity.Property(e => e.Platform)
                .HasMaxLength(30)
                .HasDefaultValue("YouTube", "DF_SourceVideos_Platform");
            entity.Property(e => e.ReviewNote).HasMaxLength(500);
            entity.Property(e => e.RowVersion)
                .IsRowVersion()
                .IsConcurrencyToken();
            entity.Property(e => e.Status)
                .HasMaxLength(30)
                .HasDefaultValue("Pending", "DF_SourceVideos_Status");
            entity.Property(e => e.ThumbnailUrl).HasMaxLength(300);
            entity.Property(e => e.Title).HasMaxLength(250);
            entity.Property(e => e.VideoId).HasMaxLength(32);

            entity.HasOne(d => d.ReviewedBy).WithMany(p => p.SourceVideos).HasForeignKey(d => d.ReviewedById);
        });

        modelBuilder.Entity<SourceVideoSource>(entity =>
        {
            entity.HasIndex(e => e.SourceVideoId, "IX_SourceVideoSources_SourceVideoId");

            entity.HasIndex(e => new { e.SourceId, e.SourceVideoId }, "UQ_SourceVideoSources_SourceId_SourceVideoId").IsUnique();

            entity.Property(e => e.DiscoveredAt).HasDefaultValueSql("(sysutcdatetime())", "DF_SourceVideoSources_DiscoveredAt");

            entity.HasOne(d => d.Source).WithMany(p => p.SourceVideoSources).HasForeignKey(d => d.SourceId);

            entity.HasOne(d => d.SourceVideo).WithMany(p => p.SourceVideoSources).HasForeignKey(d => d.SourceVideoId);
        });

        modelBuilder.Entity<Streak>(entity =>
        {
            entity.HasIndex(e => e.ChildProfileId, "UQ_Streaks_ChildProfileId").IsUnique();

            entity.HasOne(d => d.ChildProfile).WithOne(p => p.Streak).HasForeignKey<Streak>(d => d.ChildProfileId);
        });

        modelBuilder.Entity<Subject>(entity =>
        {
            entity.HasIndex(e => e.Code, "UQ_Subjects_Code").IsUnique();

            entity.Property(e => e.Code).HasMaxLength(30);
            entity.Property(e => e.ColorKey).HasMaxLength(20);
            entity.Property(e => e.Description).HasMaxLength(300);
            entity.Property(e => e.IconKey).HasMaxLength(30);
            entity.Property(e => e.IsActive).HasDefaultValue(true, "DF_Subjects_IsActive");
            entity.Property(e => e.Name).HasMaxLength(80);
        });

        modelBuilder.Entity<SubjectGrade>(entity =>
        {
            entity.HasIndex(e => e.GradeLevelId, "IX_SubjectGrades_GradeLevelId");

            entity.HasIndex(e => new { e.SubjectId, e.GradeLevelId }, "UQ_SubjectGrades_SubjectId_GradeLevelId").IsUnique();

            entity.HasOne(d => d.GradeLevel).WithMany(p => p.SubjectGrades)
                .HasForeignKey(d => d.GradeLevelId)
                .OnDelete(DeleteBehavior.ClientSetNull);

            entity.HasOne(d => d.Subject).WithMany(p => p.SubjectGrades).HasForeignKey(d => d.SubjectId);
        });

        modelBuilder.Entity<SubjectProgress>(entity =>
        {
            entity.HasIndex(e => e.SubjectId, "IX_SubjectProgresses_SubjectId");

            entity.HasIndex(e => new { e.ChildProfileId, e.SubjectId }, "UQ_SubjectProgresses_ChildProfileId_SubjectId").IsUnique();

            entity.Property(e => e.AvgQuizPercent).HasColumnType("decimal(5, 2)");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("(sysutcdatetime())", "DF_SubjectProgresses_UpdatedAt");

            entity.HasOne(d => d.ChildProfile).WithMany(p => p.SubjectProgresses).HasForeignKey(d => d.ChildProfileId);

            entity.HasOne(d => d.Subject).WithMany(p => p.SubjectProgresses)
                .HasForeignKey(d => d.SubjectId)
                .OnDelete(DeleteBehavior.ClientSetNull);
        });

        modelBuilder.Entity<SystemMetricsDaily>(entity =>
        {
            entity.ToTable("SystemMetricsDaily");

            entity.HasIndex(e => e.MetricDate, "UQ_SystemMetricsDaily_MetricDate").IsUnique();
        });

        modelBuilder.Entity<Tag>(entity =>
        {
            entity.HasIndex(e => e.Name, "UQ_Tags_Name").IsUnique();

            entity.Property(e => e.Name).HasMaxLength(40);
        });

        modelBuilder.Entity<TimeExtensionRequest>(entity =>
        {
            entity.HasIndex(e => new { e.ChildProfileId, e.Status }, "IX_TimeExtensionRequests_ChildProfileId_Status");

            entity.HasIndex(e => e.DecidedById, "IX_TimeExtensionRequests_DecidedById");

            entity.Property(e => e.Reason).HasMaxLength(200);
            entity.Property(e => e.RequestedAt).HasDefaultValueSql("(sysutcdatetime())", "DF_TimeExtensionRequests_RequestedAt");
            entity.Property(e => e.RowVersion)
                .IsRowVersion()
                .IsConcurrencyToken();
            entity.Property(e => e.Status)
                .HasMaxLength(30)
                .HasDefaultValue("Pending", "DF_TimeExtensionRequests_Status");

            entity.HasOne(d => d.ChildProfile).WithMany(p => p.TimeExtensionRequests).HasForeignKey(d => d.ChildProfileId);

            entity.HasOne(d => d.DecidedBy).WithMany(p => p.TimeExtensionRequests).HasForeignKey(d => d.DecidedById);
        });

        modelBuilder.Entity<TimeLimitOverride>(entity =>
        {
            entity.HasIndex(e => new { e.PolicyId, e.Weekday }, "UQ_TimeLimitOverrides_PolicyId_Weekday").IsUnique();

            entity.HasOne(d => d.Policy).WithMany(p => p.TimeLimitOverrides).HasForeignKey(d => d.PolicyId);
        });

        modelBuilder.Entity<TimeLimitPolicy>(entity =>
        {
            entity.HasIndex(e => e.UpdatedById, "IX_TimeLimitPolicies_UpdatedById");

            entity.HasIndex(e => e.ChildProfileId, "UQ_TimeLimitPolicies_ChildProfileId").IsUnique();

            entity.Property(e => e.DailyMinutes).HasDefaultValue(60, "DF_TimeLimitPolicies_DailyMinutes");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("(sysutcdatetime())", "DF_TimeLimitPolicies_UpdatedAt");

            entity.HasOne(d => d.ChildProfile).WithOne(p => p.TimeLimitPolicy).HasForeignKey<TimeLimitPolicy>(d => d.ChildProfileId);

            entity.HasOne(d => d.UpdatedBy).WithMany(p => p.TimeLimitPolicies).HasForeignKey(d => d.UpdatedById);
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasIndex(e => e.NormalizedEmail, "EmailIndex");

            entity.HasIndex(e => e.NormalizedUserName, "UserNameIndex")
                .IsUnique()
                .HasFilter("([NormalizedUserName] IS NOT NULL)");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())", "DF_Users_CreatedAt");
            entity.Property(e => e.Email).HasMaxLength(256);
            entity.Property(e => e.FullName).HasMaxLength(100);
            entity.Property(e => e.IsActive).HasDefaultValue(true, "DF_Users_IsActive");
            entity.Property(e => e.LockoutEnabled).HasDefaultValue(true, "DF_Users_LockoutEnabled");
            entity.Property(e => e.NormalizedEmail).HasMaxLength(256);
            entity.Property(e => e.NormalizedUserName).HasMaxLength(256);
            entity.Property(e => e.UserName).HasMaxLength(256);

            entity.HasMany(d => d.Roles).WithMany(p => p.Users)
                .UsingEntity<Dictionary<string, object>>(
                    "UserRole",
                    r => r.HasOne<Role>().WithMany()
                        .HasForeignKey("RoleId")
                        .HasConstraintName("FK_UserRoles_Roles"),
                    l => l.HasOne<User>().WithMany()
                        .HasForeignKey("UserId")
                        .HasConstraintName("FK_UserRoles_Users"),
                    j =>
                    {
                        j.HasKey("UserId", "RoleId");
                        j.ToTable("UserRoles");
                        j.HasIndex(new[] { "RoleId" }, "IX_UserRoles_RoleId");
                    });
        });

        modelBuilder.Entity<UserConsent>(entity =>
        {
            entity.HasIndex(e => new { e.UserId, e.ChildProfileId, e.ConsentType, e.Version }, "UQ_UserConsents_UserId_ChildProfileId_ConsentType_Version").IsUnique();

            entity.Property(e => e.ConsentType).HasMaxLength(30);
            entity.Property(e => e.DecidedAt).HasDefaultValueSql("(sysutcdatetime())", "DF_UserConsents_DecidedAt");
            entity.Property(e => e.Decision).HasMaxLength(20);
            entity.Property(e => e.IpAddress).HasMaxLength(45);
            entity.Property(e => e.Version).HasMaxLength(20);

            entity.HasOne(d => d.User).WithMany(p => p.UserConsents).HasForeignKey(d => d.UserId);
        });

        modelBuilder.Entity<UserSetting>(entity =>
        {
            entity.HasIndex(e => e.UserId, "UQ_UserSettings_UserId").IsUnique();

            entity.Property(e => e.Language)
                .HasMaxLength(5)
                .HasDefaultValue("vi", "DF_UserSettings_Language");
            entity.Property(e => e.NotifyContentReport).HasDefaultValue(true, "DF_UserSettings_NotifyContentReport");
            entity.Property(e => e.NotifyTimeRequest).HasDefaultValue(true, "DF_UserSettings_NotifyTimeRequest");
            entity.Property(e => e.ParentPinHash).HasMaxLength(200);
            entity.Property(e => e.Theme)
                .HasMaxLength(30)
                .HasDefaultValue("Auto", "DF_UserSettings_Theme");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("(sysutcdatetime())", "DF_UserSettings_UpdatedAt");
            entity.Property(e => e.WeeklyEmail).HasDefaultValue(true, "DF_UserSettings_WeeklyEmail");

            entity.HasOne(d => d.User).WithOne(p => p.UserSetting).HasForeignKey<UserSetting>(d => d.UserId);
        });

        modelBuilder.Entity<WeeklyReport>(entity =>
        {
            entity.HasIndex(e => new { e.ChildProfileId, e.WeekStart }, "UQ_WeeklyReports_ChildProfileId_WeekStart").IsUnique();

            entity.Property(e => e.AvgQuizPercent).HasColumnType("decimal(5, 2)");
            entity.Property(e => e.GeneratedAt).HasDefaultValueSql("(sysutcdatetime())", "DF_WeeklyReports_GeneratedAt");
            entity.Property(e => e.NeedsAttentionSubject).HasMaxLength(80);
            entity.Property(e => e.StrongSubject).HasMaxLength(80);

            entity.HasOne(d => d.ChildProfile).WithMany(p => p.WeeklyReports).HasForeignKey(d => d.ChildProfileId);
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
