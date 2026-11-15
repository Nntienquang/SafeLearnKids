/*
SafeLearn Kids: full database schema (56 tables, keys, CHECK constraints, indexes).
Generated with sqlpackage from the development database "hoctapchotre"; do not edit by hand, regenerate instead.

Run once on an EMPTY database, for example:
    sqlcmd -S <server> -d hoctapchotre -U <user> -P <password> -C -I -b -i database/01-schema.sql
Then run database/02-reference-data.sql. The app adds later columns itself at startup (Services/SchemaUpdater.cs).
*/
GO
SET ANSI_NULLS, ANSI_PADDING, ANSI_WARNINGS, ARITHABORT, CONCAT_NULL_YIELDS_NULL, QUOTED_IDENTIFIER ON;

SET NUMERIC_ROUNDABORT OFF;


GO



GO
PRINT N'Creating Table [dbo].[Assignments]...';


GO
CREATE TABLE [dbo].[Assignments] (
    [Id]             INT            IDENTITY (1, 1) NOT NULL,
    [ChildProfileId] INT            NOT NULL,
    [LessonId]       INT            NULL,
    [QuizId]         INT            NULL,
    [AssignedById]   NVARCHAR (450) NOT NULL,
    [DueDate]        DATE           NULL,
    [Status]         NVARCHAR (30)  NOT NULL,
    [Note]           NVARCHAR (300) NULL,
    [AssignedAt]     DATETIME2 (7)  NOT NULL,
    [CompletedAt]    DATETIME2 (7)  NULL,
    CONSTRAINT [PK_Assignments] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [UQ_Assignments_Id_ChildProfileId_QuizId] UNIQUE NONCLUSTERED ([Id] ASC, [ChildProfileId] ASC, [QuizId] ASC)
);


GO
PRINT N'Creating Index [dbo].[Assignments].[IX_Assignments_ChildProfileId_Status]...';


GO
CREATE NONCLUSTERED INDEX [IX_Assignments_ChildProfileId_Status]
    ON [dbo].[Assignments]([ChildProfileId] ASC, [Status] ASC);


GO
PRINT N'Creating Index [dbo].[Assignments].[IX_Assignments_QuizId]...';


GO
CREATE NONCLUSTERED INDEX [IX_Assignments_QuizId]
    ON [dbo].[Assignments]([QuizId] ASC);


GO
PRINT N'Creating Index [dbo].[Assignments].[UX_Assignments_ChildProfileId_LessonId]...';


GO
CREATE UNIQUE NONCLUSTERED INDEX [UX_Assignments_ChildProfileId_LessonId]
    ON [dbo].[Assignments]([ChildProfileId] ASC, [LessonId] ASC) WHERE ([LessonId] IS NOT NULL AND ([Status] IN (N'Assigned', N'InProgress')));


GO
PRINT N'Creating Index [dbo].[Assignments].[IX_Assignments_LessonId]...';


GO
CREATE NONCLUSTERED INDEX [IX_Assignments_LessonId]
    ON [dbo].[Assignments]([LessonId] ASC);


GO
PRINT N'Creating Index [dbo].[Assignments].[IX_Assignments_AssignedById]...';


GO
CREATE NONCLUSTERED INDEX [IX_Assignments_AssignedById]
    ON [dbo].[Assignments]([AssignedById] ASC);


GO
PRINT N'Creating Table [dbo].[AuditLogs]...';


GO
CREATE TABLE [dbo].[AuditLogs] (
    [Id]         BIGINT         IDENTITY (1, 1) NOT NULL,
    [ActorId]    NVARCHAR (450) NULL,
    [Action]     NVARCHAR (60)  NOT NULL,
    [EntityType] NVARCHAR (60)  NOT NULL,
    [EntityId]   NVARCHAR (64)  NULL,
    [Detail]     NVARCHAR (MAX) NULL,
    [IpAddress]  NVARCHAR (45)  NULL,
    [CreatedAt]  DATETIME2 (7)  NOT NULL,
    CONSTRAINT [PK_AuditLogs] PRIMARY KEY CLUSTERED ([Id] ASC)
);


GO
PRINT N'Creating Index [dbo].[AuditLogs].[IX_AuditLogs_ActorId_CreatedAt]...';


GO
CREATE NONCLUSTERED INDEX [IX_AuditLogs_ActorId_CreatedAt]
    ON [dbo].[AuditLogs]([ActorId] ASC, [CreatedAt] ASC);


GO
PRINT N'Creating Index [dbo].[AuditLogs].[IX_AuditLogs_EntityType_EntityId]...';


GO
CREATE NONCLUSTERED INDEX [IX_AuditLogs_EntityType_EntityId]
    ON [dbo].[AuditLogs]([EntityType] ASC, [EntityId] ASC);


GO
PRINT N'Creating Table [dbo].[Badges]...';


GO
CREATE TABLE [dbo].[Badges] (
    [Id]           INT            IDENTITY (1, 1) NOT NULL,
    [Code]         NVARCHAR (40)  NOT NULL,
    [Name]         NVARCHAR (80)  NOT NULL,
    [Description]  NVARCHAR (200) NOT NULL,
    [IconKey]      NVARCHAR (30)  NOT NULL,
    [CriteriaType] NVARCHAR (30)  NOT NULL,
    [Threshold]    INT            NOT NULL,
    [IsActive]     BIT            NOT NULL,
    CONSTRAINT [PK_Badges] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [UQ_Badges_Code] UNIQUE NONCLUSTERED ([Code] ASC)
);


GO
PRINT N'Creating Table [dbo].[BlockedKeywords]...';


GO
CREATE TABLE [dbo].[BlockedKeywords] (
    [Id]          INT            IDENTITY (1, 1) NOT NULL,
    [Keyword]     NVARCHAR (60)  NOT NULL,
    [ParentId]    NVARCHAR (450) NULL,
    [CreatedById] NVARCHAR (450) NULL,
    [CreatedAt]   DATETIME2 (7)  NOT NULL,
    CONSTRAINT [PK_BlockedKeywords] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [UQ_BlockedKeywords_Keyword_ParentId] UNIQUE NONCLUSTERED ([Keyword] ASC, [ParentId] ASC)
);


GO
PRINT N'Creating Index [dbo].[BlockedKeywords].[IX_BlockedKeywords_CreatedById]...';


GO
CREATE NONCLUSTERED INDEX [IX_BlockedKeywords_CreatedById]
    ON [dbo].[BlockedKeywords]([CreatedById] ASC);


GO
PRINT N'Creating Index [dbo].[BlockedKeywords].[IX_BlockedKeywords_ParentId]...';


GO
CREATE NONCLUSTERED INDEX [IX_BlockedKeywords_ParentId]
    ON [dbo].[BlockedKeywords]([ParentId] ASC);


GO
PRINT N'Creating Table [dbo].[ChildBadges]...';


GO
CREATE TABLE [dbo].[ChildBadges] (
    [Id]             INT           IDENTITY (1, 1) NOT NULL,
    [ChildProfileId] INT           NOT NULL,
    [BadgeId]        INT           NOT NULL,
    [AwardedAt]      DATETIME2 (7) NOT NULL,
    CONSTRAINT [PK_ChildBadges] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [UQ_ChildBadges_ChildProfileId_BadgeId] UNIQUE NONCLUSTERED ([ChildProfileId] ASC, [BadgeId] ASC)
);


GO
PRINT N'Creating Index [dbo].[ChildBadges].[IX_ChildBadges_BadgeId]...';


GO
CREATE NONCLUSTERED INDEX [IX_ChildBadges_BadgeId]
    ON [dbo].[ChildBadges]([BadgeId] ASC);


GO
PRINT N'Creating Table [dbo].[ChildPaths]...';


GO
CREATE TABLE [dbo].[ChildPaths] (
    [Id]             INT           IDENTITY (1, 1) NOT NULL,
    [ChildProfileId] INT           NOT NULL,
    [PathId]         INT           NOT NULL,
    [Status]         NVARCHAR (30) NOT NULL,
    [CurrentItemId]  INT           NULL,
    [StartedAt]      DATETIME2 (7) NULL,
    [CompletedAt]    DATETIME2 (7) NULL,
    CONSTRAINT [PK_ChildPaths] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [UQ_ChildPaths_ChildProfileId_PathId] UNIQUE NONCLUSTERED ([ChildProfileId] ASC, [PathId] ASC)
);


GO
PRINT N'Creating Index [dbo].[ChildPaths].[IX_ChildPaths_PathId]...';


GO
CREATE NONCLUSTERED INDEX [IX_ChildPaths_PathId]
    ON [dbo].[ChildPaths]([PathId] ASC);


GO
PRINT N'Creating Index [dbo].[ChildPaths].[IX_ChildPaths_CurrentItemId]...';


GO
CREATE NONCLUSTERED INDEX [IX_ChildPaths_CurrentItemId]
    ON [dbo].[ChildPaths]([CurrentItemId] ASC);


GO
PRINT N'Creating Table [dbo].[ChildProfiles]...';


GO
CREATE TABLE [dbo].[ChildProfiles] (
    [Id]                INT            IDENTITY (1, 1) NOT NULL,
    [ParentId]          NVARCHAR (450) NOT NULL,
    [UserId]            NVARCHAR (450) NULL,
    [DisplayName]       NVARCHAR (60)  NOT NULL,
    [GradeLevelId]      INT            NOT NULL,
    [BirthYear]         INT            NULL,
    [AvatarKey]         NVARCHAR (30)  NULL,
    [IsActive]          BIT            NOT NULL,
    [PinChangedAt]      DATETIME2 (7)  NULL,
    [PinLockedUntil]    DATETIME2 (7)  NULL,
    [PinFailedAttempts] INT            NOT NULL,
    [PinHash]           NVARCHAR (500) NULL,
    [CreatedAt]         DATETIME2 (7)  NOT NULL,
    CONSTRAINT [PK_ChildProfiles] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [UQ_ChildProfiles_ParentId_DisplayName] UNIQUE NONCLUSTERED ([ParentId] ASC, [DisplayName] ASC)
);


GO
PRINT N'Creating Index [dbo].[ChildProfiles].[UX_ChildProfiles_UserId]...';


GO
CREATE UNIQUE NONCLUSTERED INDEX [UX_ChildProfiles_UserId]
    ON [dbo].[ChildProfiles]([UserId] ASC) WHERE ([UserId] IS NOT NULL);


GO
PRINT N'Creating Index [dbo].[ChildProfiles].[IX_ChildProfiles_GradeLevelId]...';


GO
CREATE NONCLUSTERED INDEX [IX_ChildProfiles_GradeLevelId]
    ON [dbo].[ChildProfiles]([GradeLevelId] ASC);


GO
PRINT N'Creating Table [dbo].[ChildSubjects]...';


GO
CREATE TABLE [dbo].[ChildSubjects] (
    [Id]             INT IDENTITY (1, 1) NOT NULL,
    [ChildProfileId] INT NOT NULL,
    [SubjectId]      INT NOT NULL,
    CONSTRAINT [PK_ChildSubjects] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [UQ_ChildSubjects_ChildProfileId_SubjectId] UNIQUE NONCLUSTERED ([ChildProfileId] ASC, [SubjectId] ASC)
);


GO
PRINT N'Creating Index [dbo].[ChildSubjects].[IX_ChildSubjects_SubjectId]...';


GO
CREATE NONCLUSTERED INDEX [IX_ChildSubjects_SubjectId]
    ON [dbo].[ChildSubjects]([SubjectId] ASC);


GO
PRINT N'Creating Table [dbo].[ContentReports]...';


GO
CREATE TABLE [dbo].[ContentReports] (
    [Id]             INT            IDENTITY (1, 1) NOT NULL,
    [ChildProfileId] INT            NULL,
    [ReportedById]   NVARCHAR (450) NULL,
    [LessonId]       INT            NULL,
    [SourceVideoId]  INT            NULL,
    [Reason]         NVARCHAR (30)  NOT NULL,
    [Comment]        NVARCHAR (500) NULL,
    [Status]         NVARCHAR (30)  NOT NULL,
    [HandledById]    NVARCHAR (450) NULL,
    [Resolution]     NVARCHAR (500) NULL,
    [CreatedAt]      DATETIME2 (7)  NOT NULL,
    [ResolvedAt]     DATETIME2 (7)  NULL,
    [RowVersion]     ROWVERSION     NOT NULL,
    CONSTRAINT [PK_ContentReports] PRIMARY KEY CLUSTERED ([Id] ASC)
);


GO
PRINT N'Creating Index [dbo].[ContentReports].[IX_ContentReports_ReportedById]...';


GO
CREATE NONCLUSTERED INDEX [IX_ContentReports_ReportedById]
    ON [dbo].[ContentReports]([ReportedById] ASC);


GO
PRINT N'Creating Index [dbo].[ContentReports].[IX_ContentReports_HandledById]...';


GO
CREATE NONCLUSTERED INDEX [IX_ContentReports_HandledById]
    ON [dbo].[ContentReports]([HandledById] ASC);


GO
PRINT N'Creating Index [dbo].[ContentReports].[IX_ContentReports_SourceVideoId]...';


GO
CREATE NONCLUSTERED INDEX [IX_ContentReports_SourceVideoId]
    ON [dbo].[ContentReports]([SourceVideoId] ASC);


GO
PRINT N'Creating Index [dbo].[ContentReports].[IX_ContentReports_Status_CreatedAt]...';


GO
CREATE NONCLUSTERED INDEX [IX_ContentReports_Status_CreatedAt]
    ON [dbo].[ContentReports]([Status] ASC, [CreatedAt] ASC);


GO
PRINT N'Creating Index [dbo].[ContentReports].[IX_ContentReports_LessonId]...';


GO
CREATE NONCLUSTERED INDEX [IX_ContentReports_LessonId]
    ON [dbo].[ContentReports]([LessonId] ASC);


GO
PRINT N'Creating Index [dbo].[ContentReports].[IX_ContentReports_ChildProfileId]...';


GO
CREATE NONCLUSTERED INDEX [IX_ContentReports_ChildProfileId]
    ON [dbo].[ContentReports]([ChildProfileId] ASC);


GO
PRINT N'Creating Table [dbo].[ContentSources]...';


GO
CREATE TABLE [dbo].[ContentSources] (
    [Id]            INT            IDENTITY (1, 1) NOT NULL,
    [Platform]      NVARCHAR (30)  NOT NULL,
    [ExternalId]    NVARCHAR (64)  NOT NULL,
    [Name]          NVARCHAR (150) NOT NULL,
    [Url]           NVARCHAR (300) NOT NULL,
    [Description]   NVARCHAR (500) NULL,
    [Status]        NVARCHAR (30)  NOT NULL,
    [SuggestedById] NVARCHAR (450) NULL,
    [ReviewedById]  NVARCHAR (450) NULL,
    [ReviewedAt]    DATETIME2 (7)  NULL,
    [ReviewNote]    NVARCHAR (500) NULL,
    [LastSyncedAt]  DATETIME2 (7)  NULL,
    [LastSyncError] NVARCHAR (300) NULL,
    [CreatedAt]     DATETIME2 (7)  NOT NULL,
    [RowVersion]    ROWVERSION     NOT NULL,
    CONSTRAINT [PK_ContentSources] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [UQ_ContentSources_Platform_ExternalId] UNIQUE NONCLUSTERED ([Platform] ASC, [ExternalId] ASC)
);


GO
PRINT N'Creating Index [dbo].[ContentSources].[IX_ContentSources_SuggestedById]...';


GO
CREATE NONCLUSTERED INDEX [IX_ContentSources_SuggestedById]
    ON [dbo].[ContentSources]([SuggestedById] ASC);


GO
PRINT N'Creating Index [dbo].[ContentSources].[IX_ContentSources_ReviewedById]...';


GO
CREATE NONCLUSTERED INDEX [IX_ContentSources_ReviewedById]
    ON [dbo].[ContentSources]([ReviewedById] ASC);


GO
PRINT N'Creating Index [dbo].[ContentSources].[IX_ContentSources_Status]...';


GO
CREATE NONCLUSTERED INDEX [IX_ContentSources_Status]
    ON [dbo].[ContentSources]([Status] ASC);


GO
PRINT N'Creating Table [dbo].[CurationSuggestions]...';


GO
CREATE TABLE [dbo].[CurationSuggestions] (
    [Id]          INT            IDENTITY (1, 1) NOT NULL,
    [SourceId]    INT            NOT NULL,
    [Score]       DECIMAL (5, 2) NOT NULL,
    [SafetyFlags] NVARCHAR (500) NULL,
    [Summary]     NVARCHAR (MAX) NULL,
    [ModelName]   NVARCHAR (60)  NOT NULL,
    [Status]      NVARCHAR (30)  NOT NULL,
    [DecidedById] NVARCHAR (450) NULL,
    [DecidedAt]   DATETIME2 (7)  NULL,
    [CreatedAt]   DATETIME2 (7)  NOT NULL,
    CONSTRAINT [PK_CurationSuggestions] PRIMARY KEY CLUSTERED ([Id] ASC)
);


GO
PRINT N'Creating Index [dbo].[CurationSuggestions].[IX_CurationSuggestions_Status_CreatedAt]...';


GO
CREATE NONCLUSTERED INDEX [IX_CurationSuggestions_Status_CreatedAt]
    ON [dbo].[CurationSuggestions]([Status] ASC, [CreatedAt] ASC);


GO
PRINT N'Creating Index [dbo].[CurationSuggestions].[IX_CurationSuggestions_SourceId]...';


GO
CREATE NONCLUSTERED INDEX [IX_CurationSuggestions_SourceId]
    ON [dbo].[CurationSuggestions]([SourceId] ASC);


GO
PRINT N'Creating Index [dbo].[CurationSuggestions].[IX_CurationSuggestions_DecidedById]...';


GO
CREATE NONCLUSTERED INDEX [IX_CurationSuggestions_DecidedById]
    ON [dbo].[CurationSuggestions]([DecidedById] ASC);


GO
PRINT N'Creating Table [dbo].[DailyUsages]...';


GO
CREATE TABLE [dbo].[DailyUsages] (
    [Id]             BIGINT IDENTITY (1, 1) NOT NULL,
    [ChildProfileId] INT    NOT NULL,
    [UsageDate]      DATE   NOT NULL,
    [Seconds]        INT    NOT NULL,
    CONSTRAINT [PK_DailyUsages] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [UQ_DailyUsages_ChildProfileId_UsageDate] UNIQUE NONCLUSTERED ([ChildProfileId] ASC, [UsageDate] ASC)
);


GO
PRINT N'Creating Table [dbo].[DataRequests]...';


GO
CREATE TABLE [dbo].[DataRequests] (
    [Id]             INT            IDENTITY (1, 1) NOT NULL,
    [ParentId]       NVARCHAR (450) NOT NULL,
    [ChildProfileId] INT            NULL,
    [RequestType]    NVARCHAR (30)  NOT NULL,
    [Status]         NVARCHAR (30)  NOT NULL,
    [ProcessedById]  NVARCHAR (450) NULL,
    [ChildName]      NVARCHAR (60)  NULL,
    [DecisionNote]   NVARCHAR (500) NULL,
    [RequestedAt]    DATETIME2 (7)  NOT NULL,
    [CompletedAt]    DATETIME2 (7)  NULL,
    CONSTRAINT [PK_DataRequests] PRIMARY KEY CLUSTERED ([Id] ASC)
);


GO
PRINT N'Creating Index [dbo].[DataRequests].[IX_DataRequests_ParentId]...';


GO
CREATE NONCLUSTERED INDEX [IX_DataRequests_ParentId]
    ON [dbo].[DataRequests]([ParentId] ASC);


GO
PRINT N'Creating Index [dbo].[DataRequests].[IX_DataRequests_ProcessedById]...';


GO
CREATE NONCLUSTERED INDEX [IX_DataRequests_ProcessedById]
    ON [dbo].[DataRequests]([ProcessedById] ASC);


GO
PRINT N'Creating Table [dbo].[Favorites]...';


GO
CREATE TABLE [dbo].[Favorites] (
    [Id]             INT           IDENTITY (1, 1) NOT NULL,
    [ChildProfileId] INT           NOT NULL,
    [LessonId]       INT           NOT NULL,
    [FavoritedAt]    DATETIME2 (7) NOT NULL,
    CONSTRAINT [PK_Favorites] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [UQ_Favorites_ChildProfileId_LessonId] UNIQUE NONCLUSTERED ([ChildProfileId] ASC, [LessonId] ASC)
);


GO
PRINT N'Creating Index [dbo].[Favorites].[IX_Favorites_LessonId]...';


GO
CREATE NONCLUSTERED INDEX [IX_Favorites_LessonId]
    ON [dbo].[Favorites]([LessonId] ASC);


GO
PRINT N'Creating Table [dbo].[GradeLevels]...';


GO
CREATE TABLE [dbo].[GradeLevels] (
    [Id]    INT           IDENTITY (1, 1) NOT NULL,
    [Level] INT           NOT NULL,
    [Name]  NVARCHAR (20) NOT NULL,
    CONSTRAINT [PK_GradeLevels] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [UQ_GradeLevels_Level] UNIQUE NONCLUSTERED ([Level] ASC)
);


GO
PRINT N'Creating Table [dbo].[LearningGoals]...';


GO
CREATE TABLE [dbo].[LearningGoals] (
    [Id]             INT            IDENTITY (1, 1) NOT NULL,
    [ChildProfileId] INT            NOT NULL,
    [WeekStart]      DATE           NOT NULL,
    [TargetLessons]  INT            NULL,
    [TargetMinutes]  INT            NULL,
    [CreatedById]    NVARCHAR (450) NULL,
    [CreatedAt]      DATETIME2 (7)  NOT NULL,
    CONSTRAINT [PK_LearningGoals] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [UQ_LearningGoals_ChildProfileId_WeekStart] UNIQUE NONCLUSTERED ([ChildProfileId] ASC, [WeekStart] ASC)
);


GO
PRINT N'Creating Index [dbo].[LearningGoals].[IX_LearningGoals_CreatedById]...';


GO
CREATE NONCLUSTERED INDEX [IX_LearningGoals_CreatedById]
    ON [dbo].[LearningGoals]([CreatedById] ASC);


GO
PRINT N'Creating Table [dbo].[LearningPathItems]...';


GO
CREATE TABLE [dbo].[LearningPathItems] (
    [Id]         INT IDENTITY (1, 1) NOT NULL,
    [PathId]     INT NOT NULL,
    [LessonId]   INT NOT NULL,
    [SortOrder]  INT NOT NULL,
    [IsRequired] BIT NOT NULL,
    CONSTRAINT [PK_LearningPathItems] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [UQ_LearningPathItems_Id_PathId] UNIQUE NONCLUSTERED ([Id] ASC, [PathId] ASC),
    CONSTRAINT [UQ_LearningPathItems_PathId_LessonId] UNIQUE NONCLUSTERED ([PathId] ASC, [LessonId] ASC)
);


GO
PRINT N'Creating Index [dbo].[LearningPathItems].[IX_LearningPathItems_LessonId]...';


GO
CREATE NONCLUSTERED INDEX [IX_LearningPathItems_LessonId]
    ON [dbo].[LearningPathItems]([LessonId] ASC);


GO
PRINT N'Creating Table [dbo].[LearningPaths]...';


GO
CREATE TABLE [dbo].[LearningPaths] (
    [Id]           INT            IDENTITY (1, 1) NOT NULL,
    [Name]         NVARCHAR (120) NOT NULL,
    [SubjectId]    INT            NOT NULL,
    [GradeLevelId] INT            NOT NULL,
    [Description]  NVARCHAR (300) NULL,
    [IsActive]     BIT            NOT NULL,
    CONSTRAINT [PK_LearningPaths] PRIMARY KEY CLUSTERED ([Id] ASC)
);


GO
PRINT N'Creating Index [dbo].[LearningPaths].[IX_LearningPaths_GradeLevelId]...';


GO
CREATE NONCLUSTERED INDEX [IX_LearningPaths_GradeLevelId]
    ON [dbo].[LearningPaths]([GradeLevelId] ASC);


GO
PRINT N'Creating Index [dbo].[LearningPaths].[IX_LearningPaths_SubjectId]...';


GO
CREATE NONCLUSTERED INDEX [IX_LearningPaths_SubjectId]
    ON [dbo].[LearningPaths]([SubjectId] ASC);


GO
PRINT N'Creating Table [dbo].[LearningSessions]...';


GO
CREATE TABLE [dbo].[LearningSessions] (
    [Id]              BIGINT        IDENTITY (1, 1) NOT NULL,
    [ChildProfileId]  INT           NOT NULL,
    [StartedAt]       DATETIME2 (7) NOT NULL,
    [LastHeartbeatAt] DATETIME2 (7) NOT NULL,
    [EndedAt]         DATETIME2 (7) NULL,
    [ActiveSeconds]   INT           NOT NULL,
    [EndReason]       NVARCHAR (30) NULL,
    CONSTRAINT [PK_LearningSessions] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [UQ_LearningSessions_Id_ChildProfileId] UNIQUE NONCLUSTERED ([Id] ASC, [ChildProfileId] ASC)
);


GO
PRINT N'Creating Index [dbo].[LearningSessions].[IX_LearningSessions_ChildProfileId_StartedAt]...';


GO
CREATE NONCLUSTERED INDEX [IX_LearningSessions_ChildProfileId_StartedAt]
    ON [dbo].[LearningSessions]([ChildProfileId] ASC, [StartedAt] ASC);


GO
PRINT N'Creating Table [dbo].[LessonActivities]...';


GO
CREATE TABLE [dbo].[LessonActivities] (
    [Id]              BIGINT        IDENTITY (1, 1) NOT NULL,
    [ChildProfileId]  INT           NOT NULL,
    [LessonId]        INT           NOT NULL,
    [SessionId]       BIGINT        NULL,
    [ProgressPercent] INT           NOT NULL,
    [LastPositionSec] INT           NOT NULL,
    [WatchSeconds]    INT           NOT NULL,
    [Status]          NVARCHAR (30) NOT NULL,
    [StartedAt]       DATETIME2 (7) NOT NULL,
    [CompletedAt]     DATETIME2 (7) NULL,
    [UpdatedAt]       DATETIME2 (7) NOT NULL,
    CONSTRAINT [PK_LessonActivities] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [UQ_LessonActivities_ChildProfileId_LessonId] UNIQUE NONCLUSTERED ([ChildProfileId] ASC, [LessonId] ASC)
);


GO
PRINT N'Creating Index [dbo].[LessonActivities].[IX_LessonActivities_LessonId]...';


GO
CREATE NONCLUSTERED INDEX [IX_LessonActivities_LessonId]
    ON [dbo].[LessonActivities]([LessonId] ASC);


GO
PRINT N'Creating Index [dbo].[LessonActivities].[IX_LessonActivities_SessionId]...';


GO
CREATE NONCLUSTERED INDEX [IX_LessonActivities_SessionId]
    ON [dbo].[LessonActivities]([SessionId] ASC);


GO
PRINT N'Creating Table [dbo].[LessonContents]...';


GO
CREATE TABLE [dbo].[LessonContents] (
    [Id]            INT            IDENTITY (1, 1) NOT NULL,
    [LessonId]      INT            NOT NULL,
    [ContentKind]   NVARCHAR (30)  NOT NULL,
    [Title]         NVARCHAR (150) NULL,
    [SourceVideoId] INT            NULL,
    [Body]          NVARCHAR (MAX) NULL,
    [FileUrl]       NVARCHAR (300) NULL,
    [FileName]      NVARCHAR (150) NULL,
    [SortOrder]     INT            NOT NULL,
    CONSTRAINT [PK_LessonContents] PRIMARY KEY CLUSTERED ([Id] ASC)
);


GO
PRINT N'Creating Index [dbo].[LessonContents].[IX_LessonContents_SourceVideoId]...';


GO
CREATE NONCLUSTERED INDEX [IX_LessonContents_SourceVideoId]
    ON [dbo].[LessonContents]([SourceVideoId] ASC);


GO
PRINT N'Creating Index [dbo].[LessonContents].[IX_LessonContents_LessonId_SortOrder]...';


GO
CREATE NONCLUSTERED INDEX [IX_LessonContents_LessonId_SortOrder]
    ON [dbo].[LessonContents]([LessonId] ASC, [SortOrder] ASC);


GO
PRINT N'Creating Table [dbo].[LessonRequests]...';


GO
CREATE TABLE [dbo].[LessonRequests] (
    [Id]           INT            IDENTITY (1, 1) NOT NULL,
    [ParentId]     NVARCHAR (450) NOT NULL,
    [SubjectId]    INT            NOT NULL,
    [GradeLevelId] INT            NOT NULL,
    [Topic]        NVARCHAR (200) NOT NULL,
    [Note]         NVARCHAR (500) NULL,
    [Status]       NVARCHAR (30)  NOT NULL,
    [Reply]        NVARCHAR (500) NULL,
    [LessonId]     INT            NULL,
    [HandledById]  NVARCHAR (450) NULL,
    [CreatedAt]    DATETIME2 (7)  NOT NULL,
    [HandledAt]    DATETIME2 (7)  NULL,
    CONSTRAINT [PK_LessonRequests] PRIMARY KEY CLUSTERED ([Id] ASC)
);


GO
PRINT N'Creating Index [dbo].[LessonRequests].[IX_LessonRequests_Status_SubjectId_GradeLevelId]...';


GO
CREATE NONCLUSTERED INDEX [IX_LessonRequests_Status_SubjectId_GradeLevelId]
    ON [dbo].[LessonRequests]([Status] ASC, [SubjectId] ASC, [GradeLevelId] ASC);


GO
PRINT N'Creating Table [dbo].[LessonRequestVotes]...';


GO
CREATE TABLE [dbo].[LessonRequestVotes] (
    [RequestId] INT            NOT NULL,
    [ParentId]  NVARCHAR (450) NOT NULL,
    [CreatedAt] DATETIME2 (7)  NOT NULL,
    CONSTRAINT [PK_LessonRequestVotes] PRIMARY KEY CLUSTERED ([RequestId] ASC, [ParentId] ASC)
);


GO
PRINT N'Creating Table [dbo].[Lessons]...';


GO
CREATE TABLE [dbo].[Lessons] (
    [Id]              INT            IDENTITY (1, 1) NOT NULL,
    [SubjectId]       INT            NOT NULL,
    [GradeLevelId]    INT            NOT NULL,
    [Title]           NVARCHAR (200) NOT NULL,
    [Summary]         NVARCHAR (500) NULL,
    [LessonType]      NVARCHAR (30)  NOT NULL,
    [DurationMin]     INT            NOT NULL,
    [SortOrder]       INT            NOT NULL,
    [Status]          NVARCHAR (30)  NOT NULL,
    [CreatedById]     NVARCHAR (450) NOT NULL,
    [ApprovedById]    NVARCHAR (450) NULL,
    [CreatedAt]       DATETIME2 (7)  NOT NULL,
    [UpdatedAt]       DATETIME2 (7)  NOT NULL,
    [PublishedAt]     DATETIME2 (7)  NULL,
    [RowVersion]      ROWVERSION     NOT NULL,
    [OwnerParentId]   NVARCHAR (450) NULL,
    [ShareStatus]     NVARCHAR (30)  NULL,
    [ShareNote]       NVARCHAR (500) NULL,
    [SharedAt]        DATETIME2 (7)  NULL,
    [ShareReviewedAt] DATETIME2 (7)  NULL,
    CONSTRAINT [PK_Lessons] PRIMARY KEY CLUSTERED ([Id] ASC)
);


GO
PRINT N'Creating Index [dbo].[Lessons].[IX_Lessons_CreatedById]...';


GO
CREATE NONCLUSTERED INDEX [IX_Lessons_CreatedById]
    ON [dbo].[Lessons]([CreatedById] ASC);


GO
PRINT N'Creating Index [dbo].[Lessons].[IX_Lessons_OwnerParentId]...';


GO
CREATE NONCLUSTERED INDEX [IX_Lessons_OwnerParentId]
    ON [dbo].[Lessons]([OwnerParentId] ASC);


GO
PRINT N'Creating Index [dbo].[Lessons].[IX_Lessons_ApprovedById]...';


GO
CREATE NONCLUSTERED INDEX [IX_Lessons_ApprovedById]
    ON [dbo].[Lessons]([ApprovedById] ASC);


GO
PRINT N'Creating Index [dbo].[Lessons].[IX_Lessons_SubjectId_GradeLevelId_Status]...';


GO
CREATE NONCLUSTERED INDEX [IX_Lessons_SubjectId_GradeLevelId_Status]
    ON [dbo].[Lessons]([SubjectId] ASC, [GradeLevelId] ASC, [Status] ASC);


GO
PRINT N'Creating Index [dbo].[Lessons].[IX_Lessons_GradeLevelId]...';


GO
CREATE NONCLUSTERED INDEX [IX_Lessons_GradeLevelId]
    ON [dbo].[Lessons]([GradeLevelId] ASC);


GO
PRINT N'Creating Table [dbo].[LessonTags]...';


GO
CREATE TABLE [dbo].[LessonTags] (
    [Id]       INT IDENTITY (1, 1) NOT NULL,
    [LessonId] INT NOT NULL,
    [TagId]    INT NOT NULL,
    CONSTRAINT [PK_LessonTags] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [UQ_LessonTags_LessonId_TagId] UNIQUE NONCLUSTERED ([LessonId] ASC, [TagId] ASC)
);


GO
PRINT N'Creating Index [dbo].[LessonTags].[IX_LessonTags_TagId]...';


GO
CREATE NONCLUSTERED INDEX [IX_LessonTags_TagId]
    ON [dbo].[LessonTags]([TagId] ASC);


GO
PRINT N'Creating Table [dbo].[LoginHistories]...';


GO
CREATE TABLE [dbo].[LoginHistories] (
    [Id]            BIGINT         IDENTITY (1, 1) NOT NULL,
    [UserId]        NVARCHAR (450) NULL,
    [UserNameTried] NVARCHAR (256) NOT NULL,
    [Succeeded]     BIT            NOT NULL,
    [FailReason]    NVARCHAR (100) NULL,
    [IpAddress]     NVARCHAR (45)  NULL,
    [UserAgent]     NVARCHAR (300) NULL,
    [CreatedAt]     DATETIME2 (7)  NOT NULL,
    CONSTRAINT [PK_LoginHistories] PRIMARY KEY CLUSTERED ([Id] ASC)
);


GO
PRINT N'Creating Index [dbo].[LoginHistories].[IX_LoginHistories_UserId_CreatedAt]...';


GO
CREATE NONCLUSTERED INDEX [IX_LoginHistories_UserId_CreatedAt]
    ON [dbo].[LoginHistories]([UserId] ASC, [CreatedAt] ASC);


GO
PRINT N'Creating Index [dbo].[LoginHistories].[IX_LoginHistories_IpAddress_CreatedAt]...';


GO
CREATE NONCLUSTERED INDEX [IX_LoginHistories_IpAddress_CreatedAt]
    ON [dbo].[LoginHistories]([IpAddress] ASC, [CreatedAt] ASC);


GO
PRINT N'Creating Table [dbo].[Notifications]...';


GO
CREATE TABLE [dbo].[Notifications] (
    [Id]               INT            IDENTITY (1, 1) NOT NULL,
    [UserId]           NVARCHAR (450) NOT NULL,
    [NotificationType] NVARCHAR (30)  NOT NULL,
    [Title]            NVARCHAR (120) NOT NULL,
    [Body]             NVARCHAR (500) NULL,
    [LinkUrl]          NVARCHAR (200) NULL,
    [IsRead]           BIT            NOT NULL,
    [CreatedAt]        DATETIME2 (7)  NOT NULL,
    CONSTRAINT [PK_Notifications] PRIMARY KEY CLUSTERED ([Id] ASC)
);


GO
PRINT N'Creating Index [dbo].[Notifications].[IX_Notifications_UserId_IsRead_CreatedAt]...';


GO
CREATE NONCLUSTERED INDEX [IX_Notifications_UserId_IsRead_CreatedAt]
    ON [dbo].[Notifications]([UserId] ASC, [IsRead] ASC, [CreatedAt] ASC);


GO
PRINT N'Creating Table [dbo].[ParentSources]...';


GO
CREATE TABLE [dbo].[ParentSources] (
    [Id]       INT            IDENTITY (1, 1) NOT NULL,
    [ParentId] NVARCHAR (450) NOT NULL,
    [SourceId] INT            NOT NULL,
    [Status]   NVARCHAR (30)  NOT NULL,
    [AddedAt]  DATETIME2 (7)  NOT NULL,
    CONSTRAINT [PK_ParentSources] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [UQ_ParentSources_ParentId_SourceId] UNIQUE NONCLUSTERED ([ParentId] ASC, [SourceId] ASC)
);


GO
PRINT N'Creating Index [dbo].[ParentSources].[IX_ParentSources_SourceId]...';


GO
CREATE NONCLUSTERED INDEX [IX_ParentSources_SourceId]
    ON [dbo].[ParentSources]([SourceId] ASC);


GO
PRINT N'Creating Table [dbo].[QuestionOptions]...';


GO
CREATE TABLE [dbo].[QuestionOptions] (
    [Id]         INT            IDENTITY (1, 1) NOT NULL,
    [QuestionId] INT            NOT NULL,
    [Text]       NVARCHAR (300) NOT NULL,
    [IsCorrect]  BIT            NOT NULL,
    [SortOrder]  INT            NOT NULL,
    CONSTRAINT [PK_QuestionOptions] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [UQ_QuestionOptions_Id_QuestionId] UNIQUE NONCLUSTERED ([Id] ASC, [QuestionId] ASC)
);


GO
PRINT N'Creating Index [dbo].[QuestionOptions].[IX_QuestionOptions_QuestionId]...';


GO
CREATE NONCLUSTERED INDEX [IX_QuestionOptions_QuestionId]
    ON [dbo].[QuestionOptions]([QuestionId] ASC);


GO
PRINT N'Creating Table [dbo].[Questions]...';


GO
CREATE TABLE [dbo].[Questions] (
    [Id]            INT            IDENTITY (1, 1) NOT NULL,
    [LessonId]      INT            NOT NULL,
    [Text]          NVARCHAR (500) NOT NULL,
    [Explanation]   NVARCHAR (500) NULL,
    [QuestionType]  NVARCHAR (30)  NOT NULL,
    [Difficulty]    INT            NOT NULL,
    [Origin]        NVARCHAR (30)  NOT NULL,
    [SourceVideoId] INT            NULL,
    [AiModelName]   NVARCHAR (60)  NULL,
    [Status]        NVARCHAR (30)  NOT NULL,
    [CreatedById]   NVARCHAR (450) NOT NULL,
    [ApprovedById]  NVARCHAR (450) NULL,
    [CreatedAt]     DATETIME2 (7)  NOT NULL,
    [ApprovedAt]    DATETIME2 (7)  NULL,
    CONSTRAINT [PK_Questions] PRIMARY KEY CLUSTERED ([Id] ASC)
);


GO
PRINT N'Creating Index [dbo].[Questions].[IX_Questions_LessonId_Status]...';


GO
CREATE NONCLUSTERED INDEX [IX_Questions_LessonId_Status]
    ON [dbo].[Questions]([LessonId] ASC, [Status] ASC);


GO
PRINT N'Creating Index [dbo].[Questions].[IX_Questions_CreatedById]...';


GO
CREATE NONCLUSTERED INDEX [IX_Questions_CreatedById]
    ON [dbo].[Questions]([CreatedById] ASC);


GO
PRINT N'Creating Index [dbo].[Questions].[IX_Questions_ApprovedById]...';


GO
CREATE NONCLUSTERED INDEX [IX_Questions_ApprovedById]
    ON [dbo].[Questions]([ApprovedById] ASC);


GO
PRINT N'Creating Index [dbo].[Questions].[IX_Questions_SourceVideoId]...';


GO
CREATE NONCLUSTERED INDEX [IX_Questions_SourceVideoId]
    ON [dbo].[Questions]([SourceVideoId] ASC);


GO
PRINT N'Creating Table [dbo].[QuizAttemptAnswers]...';


GO
CREATE TABLE [dbo].[QuizAttemptAnswers] (
    [Id]               BIGINT        IDENTITY (1, 1) NOT NULL,
    [AttemptId]        BIGINT        NOT NULL,
    [QuizId]           INT           NOT NULL,
    [QuestionId]       INT           NOT NULL,
    [SelectedOptionId] INT           NULL,
    [IsCorrect]        BIT           NOT NULL,
    [AnsweredAt]       DATETIME2 (7) NOT NULL,
    CONSTRAINT [PK_QuizAttemptAnswers] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [UQ_QuizAttemptAnswers_AttemptId_QuestionId] UNIQUE NONCLUSTERED ([AttemptId] ASC, [QuestionId] ASC)
);


GO
PRINT N'Creating Index [dbo].[QuizAttemptAnswers].[IX_QuizAttemptAnswers_QuizId]...';


GO
CREATE NONCLUSTERED INDEX [IX_QuizAttemptAnswers_QuizId]
    ON [dbo].[QuizAttemptAnswers]([QuizId] ASC);


GO
PRINT N'Creating Index [dbo].[QuizAttemptAnswers].[IX_QuizAttemptAnswers_QuestionId]...';


GO
CREATE NONCLUSTERED INDEX [IX_QuizAttemptAnswers_QuestionId]
    ON [dbo].[QuizAttemptAnswers]([QuestionId] ASC);


GO
PRINT N'Creating Index [dbo].[QuizAttemptAnswers].[IX_QuizAttemptAnswers_SelectedOptionId]...';


GO
CREATE NONCLUSTERED INDEX [IX_QuizAttemptAnswers_SelectedOptionId]
    ON [dbo].[QuizAttemptAnswers]([SelectedOptionId] ASC);


GO
PRINT N'Creating Index [dbo].[QuizAttemptAnswers].[IX_QuizAttemptAnswers_AttemptId]...';


GO
CREATE NONCLUSTERED INDEX [IX_QuizAttemptAnswers_AttemptId]
    ON [dbo].[QuizAttemptAnswers]([AttemptId] ASC);


GO
PRINT N'Creating Table [dbo].[QuizAttempts]...';


GO
CREATE TABLE [dbo].[QuizAttempts] (
    [Id]             BIGINT         IDENTITY (1, 1) NOT NULL,
    [QuizId]         INT            NOT NULL,
    [ChildProfileId] INT            NOT NULL,
    [AssignmentId]   INT            NULL,
    [StartedAt]      DATETIME2 (7)  NOT NULL,
    [SubmittedAt]    DATETIME2 (7)  NULL,
    [ScorePercent]   DECIMAL (5, 2) NULL,
    [CorrectCount]   INT            NOT NULL,
    [TotalCount]     INT            NOT NULL,
    [Passed]         BIT            NULL,
    CONSTRAINT [PK_QuizAttempts] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [UQ_QuizAttempts_Id_QuizId] UNIQUE NONCLUSTERED ([Id] ASC, [QuizId] ASC)
);


GO
PRINT N'Creating Index [dbo].[QuizAttempts].[IX_QuizAttempts_ChildProfileId_QuizId]...';


GO
CREATE NONCLUSTERED INDEX [IX_QuizAttempts_ChildProfileId_QuizId]
    ON [dbo].[QuizAttempts]([ChildProfileId] ASC, [QuizId] ASC);


GO
PRINT N'Creating Index [dbo].[QuizAttempts].[IX_QuizAttempts_AssignmentId]...';


GO
CREATE NONCLUSTERED INDEX [IX_QuizAttempts_AssignmentId]
    ON [dbo].[QuizAttempts]([AssignmentId] ASC);


GO
PRINT N'Creating Index [dbo].[QuizAttempts].[IX_QuizAttempts_QuizId]...';


GO
CREATE NONCLUSTERED INDEX [IX_QuizAttempts_QuizId]
    ON [dbo].[QuizAttempts]([QuizId] ASC);


GO
PRINT N'Creating Table [dbo].[QuizQuestions]...';


GO
CREATE TABLE [dbo].[QuizQuestions] (
    [Id]         INT IDENTITY (1, 1) NOT NULL,
    [QuizId]     INT NOT NULL,
    [QuestionId] INT NOT NULL,
    [SortOrder]  INT NOT NULL,
    [Points]     INT NOT NULL,
    CONSTRAINT [PK_QuizQuestions] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [UQ_QuizQuestions_QuizId_QuestionId] UNIQUE NONCLUSTERED ([QuizId] ASC, [QuestionId] ASC)
);


GO
PRINT N'Creating Index [dbo].[QuizQuestions].[IX_QuizQuestions_QuestionId]...';


GO
CREATE NONCLUSTERED INDEX [IX_QuizQuestions_QuestionId]
    ON [dbo].[QuizQuestions]([QuestionId] ASC);


GO
PRINT N'Creating Table [dbo].[Quizzes]...';


GO
CREATE TABLE [dbo].[Quizzes] (
    [Id]               INT            IDENTITY (1, 1) NOT NULL,
    [LessonId]         INT            NOT NULL,
    [Title]            NVARCHAR (150) NOT NULL,
    [PassPercent]      INT            NOT NULL,
    [TimeLimitSec]     INT            NULL,
    [ShuffleQuestions] BIT            NOT NULL,
    [Status]           NVARCHAR (30)  NOT NULL,
    [CreatedAt]        DATETIME2 (7)  NOT NULL,
    CONSTRAINT [PK_Quizzes] PRIMARY KEY CLUSTERED ([Id] ASC)
);


GO
PRINT N'Creating Index [dbo].[Quizzes].[IX_Quizzes_LessonId]...';


GO
CREATE NONCLUSTERED INDEX [IX_Quizzes_LessonId]
    ON [dbo].[Quizzes]([LessonId] ASC);


GO
PRINT N'Creating Table [dbo].[RoleClaims]...';


GO
CREATE TABLE [dbo].[RoleClaims] (
    [Id]         INT            IDENTITY (1, 1) NOT NULL,
    [RoleId]     NVARCHAR (450) NOT NULL,
    [ClaimType]  NVARCHAR (MAX) NULL,
    [ClaimValue] NVARCHAR (MAX) NULL,
    CONSTRAINT [PK_RoleClaims] PRIMARY KEY CLUSTERED ([Id] ASC)
);


GO
PRINT N'Creating Index [dbo].[RoleClaims].[IX_RoleClaims_RoleId]...';


GO
CREATE NONCLUSTERED INDEX [IX_RoleClaims_RoleId]
    ON [dbo].[RoleClaims]([RoleId] ASC);


GO
PRINT N'Creating Table [dbo].[Roles]...';


GO
CREATE TABLE [dbo].[Roles] (
    [Id]               NVARCHAR (450) NOT NULL,
    [Name]             NVARCHAR (256) NULL,
    [NormalizedName]   NVARCHAR (256) NULL,
    [ConcurrencyStamp] NVARCHAR (MAX) NULL,
    CONSTRAINT [PK_Roles] PRIMARY KEY CLUSTERED ([Id] ASC)
);


GO
PRINT N'Creating Index [dbo].[Roles].[RoleNameIndex]...';


GO
CREATE UNIQUE NONCLUSTERED INDEX [RoleNameIndex]
    ON [dbo].[Roles]([NormalizedName] ASC) WHERE ([NormalizedName] IS NOT NULL);


GO
PRINT N'Creating Table [dbo].[SearchLogs]...';


GO
CREATE TABLE [dbo].[SearchLogs] (
    [Id]             BIGINT         IDENTITY (1, 1) NOT NULL,
    [ChildProfileId] INT            NOT NULL,
    [Query]          NVARCHAR (200) NOT NULL,
    [ResultCount]    INT            NOT NULL,
    [WasBlocked]     BIT            NOT NULL,
    [CreatedAt]      DATETIME2 (7)  NOT NULL,
    CONSTRAINT [PK_SearchLogs] PRIMARY KEY CLUSTERED ([Id] ASC)
);


GO
PRINT N'Creating Index [dbo].[SearchLogs].[IX_SearchLogs_ChildProfileId_CreatedAt]...';


GO
CREATE NONCLUSTERED INDEX [IX_SearchLogs_ChildProfileId_CreatedAt]
    ON [dbo].[SearchLogs]([ChildProfileId] ASC, [CreatedAt] ASC);


GO
PRINT N'Creating Table [dbo].[SourceVideos]...';


GO
CREATE TABLE [dbo].[SourceVideos] (
    [Id]           INT            IDENTITY (1, 1) NOT NULL,
    [Platform]     NVARCHAR (30)  NOT NULL,
    [VideoId]      NVARCHAR (32)  NOT NULL,
    [Title]        NVARCHAR (250) NOT NULL,
    [DurationSec]  INT            NULL,
    [ThumbnailUrl] NVARCHAR (300) NULL,
    [Status]       NVARCHAR (30)  NOT NULL,
    [PublishedAt]  DATETIME2 (7)  NULL,
    [CheckedAt]    DATETIME2 (7)  NULL,
    [ReviewedById] NVARCHAR (450) NULL,
    [ReviewedAt]   DATETIME2 (7)  NULL,
    [ReviewNote]   NVARCHAR (500) NULL,
    [RowVersion]   ROWVERSION     NOT NULL,
    CONSTRAINT [PK_SourceVideos] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [UQ_SourceVideos_Platform_VideoId] UNIQUE NONCLUSTERED ([Platform] ASC, [VideoId] ASC)
);


GO
PRINT N'Creating Index [dbo].[SourceVideos].[IX_SourceVideos_Status]...';


GO
CREATE NONCLUSTERED INDEX [IX_SourceVideos_Status]
    ON [dbo].[SourceVideos]([Status] ASC);


GO
PRINT N'Creating Index [dbo].[SourceVideos].[IX_SourceVideos_ReviewedById]...';


GO
CREATE NONCLUSTERED INDEX [IX_SourceVideos_ReviewedById]
    ON [dbo].[SourceVideos]([ReviewedById] ASC);


GO
PRINT N'Creating Table [dbo].[SourceVideoSources]...';


GO
CREATE TABLE [dbo].[SourceVideoSources] (
    [Id]            INT           IDENTITY (1, 1) NOT NULL,
    [SourceVideoId] INT           NOT NULL,
    [SourceId]      INT           NOT NULL,
    [DiscoveredAt]  DATETIME2 (7) NOT NULL,
    CONSTRAINT [PK_SourceVideoSources] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [UQ_SourceVideoSources_SourceId_SourceVideoId] UNIQUE NONCLUSTERED ([SourceId] ASC, [SourceVideoId] ASC)
);


GO
PRINT N'Creating Index [dbo].[SourceVideoSources].[IX_SourceVideoSources_SourceVideoId]...';


GO
CREATE NONCLUSTERED INDEX [IX_SourceVideoSources_SourceVideoId]
    ON [dbo].[SourceVideoSources]([SourceVideoId] ASC);


GO
PRINT N'Creating Table [dbo].[Streaks]...';


GO
CREATE TABLE [dbo].[Streaks] (
    [Id]             INT  IDENTITY (1, 1) NOT NULL,
    [ChildProfileId] INT  NOT NULL,
    [CurrentDays]    INT  NOT NULL,
    [LongestDays]    INT  NOT NULL,
    [LastActiveDate] DATE NULL,
    CONSTRAINT [PK_Streaks] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [UQ_Streaks_ChildProfileId] UNIQUE NONCLUSTERED ([ChildProfileId] ASC)
);


GO
PRINT N'Creating Table [dbo].[SubjectGrades]...';


GO
CREATE TABLE [dbo].[SubjectGrades] (
    [Id]           INT IDENTITY (1, 1) NOT NULL,
    [SubjectId]    INT NOT NULL,
    [GradeLevelId] INT NOT NULL,
    CONSTRAINT [PK_SubjectGrades] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [UQ_SubjectGrades_SubjectId_GradeLevelId] UNIQUE NONCLUSTERED ([SubjectId] ASC, [GradeLevelId] ASC)
);


GO
PRINT N'Creating Index [dbo].[SubjectGrades].[IX_SubjectGrades_GradeLevelId]...';


GO
CREATE NONCLUSTERED INDEX [IX_SubjectGrades_GradeLevelId]
    ON [dbo].[SubjectGrades]([GradeLevelId] ASC);


GO
PRINT N'Creating Table [dbo].[SubjectProgresses]...';


GO
CREATE TABLE [dbo].[SubjectProgresses] (
    [Id]               INT            IDENTITY (1, 1) NOT NULL,
    [ChildProfileId]   INT            NOT NULL,
    [SubjectId]        INT            NOT NULL,
    [CompletedLessons] INT            NOT NULL,
    [TotalLessons]     INT            NOT NULL,
    [AvgQuizPercent]   DECIMAL (5, 2) NULL,
    [UpdatedAt]        DATETIME2 (7)  NOT NULL,
    CONSTRAINT [PK_SubjectProgresses] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [UQ_SubjectProgresses_ChildProfileId_SubjectId] UNIQUE NONCLUSTERED ([ChildProfileId] ASC, [SubjectId] ASC)
);


GO
PRINT N'Creating Index [dbo].[SubjectProgresses].[IX_SubjectProgresses_SubjectId]...';


GO
CREATE NONCLUSTERED INDEX [IX_SubjectProgresses_SubjectId]
    ON [dbo].[SubjectProgresses]([SubjectId] ASC);


GO
PRINT N'Creating Table [dbo].[Subjects]...';


GO
CREATE TABLE [dbo].[Subjects] (
    [Id]          INT            IDENTITY (1, 1) NOT NULL,
    [Code]        NVARCHAR (30)  NOT NULL,
    [Name]        NVARCHAR (80)  NOT NULL,
    [Description] NVARCHAR (300) NULL,
    [IconKey]     NVARCHAR (30)  NULL,
    [ColorKey]    NVARCHAR (20)  NULL,
    [SortOrder]   INT            NOT NULL,
    [IsActive]    BIT            NOT NULL,
    CONSTRAINT [PK_Subjects] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [UQ_Subjects_Code] UNIQUE NONCLUSTERED ([Code] ASC)
);


GO
PRINT N'Creating Table [dbo].[SystemMetricsDaily]...';


GO
CREATE TABLE [dbo].[SystemMetricsDaily] (
    [Id]               INT    IDENTITY (1, 1) NOT NULL,
    [MetricDate]       DATE   NOT NULL,
    [ActiveParents]    INT    NOT NULL,
    [ActiveChildren]   INT    NOT NULL,
    [TotalMinutes]     BIGINT NOT NULL,
    [LessonsCompleted] INT    NOT NULL,
    [OpenReports]      INT    NOT NULL,
    [NewSources]       INT    NOT NULL,
    CONSTRAINT [PK_SystemMetricsDaily] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [UQ_SystemMetricsDaily_MetricDate] UNIQUE NONCLUSTERED ([MetricDate] ASC)
);


GO
PRINT N'Creating Table [dbo].[Tags]...';


GO
CREATE TABLE [dbo].[Tags] (
    [Id]   INT           IDENTITY (1, 1) NOT NULL,
    [Name] NVARCHAR (40) NOT NULL,
    CONSTRAINT [PK_Tags] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [UQ_Tags_Name] UNIQUE NONCLUSTERED ([Name] ASC)
);


GO
PRINT N'Creating Table [dbo].[TimeExtensionRequests]...';


GO
CREATE TABLE [dbo].[TimeExtensionRequests] (
    [Id]               INT            IDENTITY (1, 1) NOT NULL,
    [ChildProfileId]   INT            NOT NULL,
    [ForDate]          DATE           NOT NULL,
    [RequestedMinutes] INT            NOT NULL,
    [Reason]           NVARCHAR (200) NULL,
    [Status]           NVARCHAR (30)  NOT NULL,
    [GrantedMinutes]   INT            NULL,
    [DecidedById]      NVARCHAR (450) NULL,
    [RequestedAt]      DATETIME2 (7)  NOT NULL,
    [DecidedAt]        DATETIME2 (7)  NULL,
    [RowVersion]       ROWVERSION     NOT NULL,
    CONSTRAINT [PK_TimeExtensionRequests] PRIMARY KEY CLUSTERED ([Id] ASC)
);


GO
PRINT N'Creating Index [dbo].[TimeExtensionRequests].[IX_TimeExtensionRequests_DecidedById]...';


GO
CREATE NONCLUSTERED INDEX [IX_TimeExtensionRequests_DecidedById]
    ON [dbo].[TimeExtensionRequests]([DecidedById] ASC);


GO
PRINT N'Creating Index [dbo].[TimeExtensionRequests].[IX_TimeExtensionRequests_ChildProfileId_Status]...';


GO
CREATE NONCLUSTERED INDEX [IX_TimeExtensionRequests_ChildProfileId_Status]
    ON [dbo].[TimeExtensionRequests]([ChildProfileId] ASC, [Status] ASC);


GO
PRINT N'Creating Table [dbo].[TimeLimitOverrides]...';


GO
CREATE TABLE [dbo].[TimeLimitOverrides] (
    [Id]       INT IDENTITY (1, 1) NOT NULL,
    [PolicyId] INT NOT NULL,
    [Weekday]  INT NOT NULL,
    [Minutes]  INT NOT NULL,
    CONSTRAINT [PK_TimeLimitOverrides] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [UQ_TimeLimitOverrides_PolicyId_Weekday] UNIQUE NONCLUSTERED ([PolicyId] ASC, [Weekday] ASC)
);


GO
PRINT N'Creating Table [dbo].[TimeLimitPolicies]...';


GO
CREATE TABLE [dbo].[TimeLimitPolicies] (
    [Id]                INT            IDENTITY (1, 1) NOT NULL,
    [ChildProfileId]    INT            NOT NULL,
    [DailyMinutes]      INT            NOT NULL,
    [StudyStart]        TIME (7)       NULL,
    [StudyEnd]          TIME (7)       NULL,
    [BreakEveryMinutes] INT            NULL,
    [UpdatedById]       NVARCHAR (450) NULL,
    [UpdatedAt]         DATETIME2 (7)  NOT NULL,
    CONSTRAINT [PK_TimeLimitPolicies] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [UQ_TimeLimitPolicies_ChildProfileId] UNIQUE NONCLUSTERED ([ChildProfileId] ASC)
);


GO
PRINT N'Creating Index [dbo].[TimeLimitPolicies].[IX_TimeLimitPolicies_UpdatedById]...';


GO
CREATE NONCLUSTERED INDEX [IX_TimeLimitPolicies_UpdatedById]
    ON [dbo].[TimeLimitPolicies]([UpdatedById] ASC);


GO
PRINT N'Creating Table [dbo].[UserClaims]...';


GO
CREATE TABLE [dbo].[UserClaims] (
    [Id]         INT            IDENTITY (1, 1) NOT NULL,
    [UserId]     NVARCHAR (450) NOT NULL,
    [ClaimType]  NVARCHAR (MAX) NULL,
    [ClaimValue] NVARCHAR (MAX) NULL,
    CONSTRAINT [PK_UserClaims] PRIMARY KEY CLUSTERED ([Id] ASC)
);


GO
PRINT N'Creating Index [dbo].[UserClaims].[IX_UserClaims_UserId]...';


GO
CREATE NONCLUSTERED INDEX [IX_UserClaims_UserId]
    ON [dbo].[UserClaims]([UserId] ASC);


GO
PRINT N'Creating Table [dbo].[UserConsents]...';


GO
CREATE TABLE [dbo].[UserConsents] (
    [Id]             INT            IDENTITY (1, 1) NOT NULL,
    [UserId]         NVARCHAR (450) NOT NULL,
    [ChildProfileId] INT            NULL,
    [ConsentType]    NVARCHAR (30)  NOT NULL,
    [Version]        NVARCHAR (20)  NOT NULL,
    [Decision]       NVARCHAR (20)  NOT NULL,
    [AcceptedAt]     DATETIME2 (7)  NULL,
    [DecidedAt]      DATETIME2 (7)  NOT NULL,
    [IpAddress]      NVARCHAR (45)  NULL,
    [RevokedAt]      DATETIME2 (7)  NULL,
    CONSTRAINT [PK_UserConsents] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [UQ_UserConsents_UserId_ChildProfileId_ConsentType_Version] UNIQUE NONCLUSTERED ([UserId] ASC, [ChildProfileId] ASC, [ConsentType] ASC, [Version] ASC)
);


GO
PRINT N'Creating Table [dbo].[UserLogins]...';


GO
CREATE TABLE [dbo].[UserLogins] (
    [LoginProvider]       NVARCHAR (128) NOT NULL,
    [ProviderKey]         NVARCHAR (128) NOT NULL,
    [ProviderDisplayName] NVARCHAR (MAX) NULL,
    [UserId]              NVARCHAR (450) NOT NULL,
    CONSTRAINT [PK_UserLogins] PRIMARY KEY CLUSTERED ([LoginProvider] ASC, [ProviderKey] ASC)
);


GO
PRINT N'Creating Index [dbo].[UserLogins].[IX_UserLogins_UserId]...';


GO
CREATE NONCLUSTERED INDEX [IX_UserLogins_UserId]
    ON [dbo].[UserLogins]([UserId] ASC);


GO
PRINT N'Creating Table [dbo].[UserRoles]...';


GO
CREATE TABLE [dbo].[UserRoles] (
    [UserId] NVARCHAR (450) NOT NULL,
    [RoleId] NVARCHAR (450) NOT NULL,
    CONSTRAINT [PK_UserRoles] PRIMARY KEY CLUSTERED ([UserId] ASC, [RoleId] ASC)
);


GO
PRINT N'Creating Index [dbo].[UserRoles].[IX_UserRoles_RoleId]...';


GO
CREATE NONCLUSTERED INDEX [IX_UserRoles_RoleId]
    ON [dbo].[UserRoles]([RoleId] ASC);


GO
PRINT N'Creating Table [dbo].[Users]...';


GO
CREATE TABLE [dbo].[Users] (
    [Id]                   NVARCHAR (450)     NOT NULL,
    [UserName]             NVARCHAR (256)     NULL,
    [NormalizedUserName]   NVARCHAR (256)     NULL,
    [Email]                NVARCHAR (256)     NULL,
    [NormalizedEmail]      NVARCHAR (256)     NULL,
    [EmailConfirmed]       BIT                NOT NULL,
    [PasswordHash]         NVARCHAR (MAX)     NULL,
    [SecurityStamp]        NVARCHAR (MAX)     NULL,
    [ConcurrencyStamp]     NVARCHAR (MAX)     NULL,
    [PhoneNumber]          NVARCHAR (MAX)     NULL,
    [PhoneNumberConfirmed] BIT                NOT NULL,
    [TwoFactorEnabled]     BIT                NOT NULL,
    [LockoutEnd]           DATETIMEOFFSET (7) NULL,
    [LockoutEnabled]       BIT                NOT NULL,
    [AccessFailedCount]    INT                NOT NULL,
    [FullName]             NVARCHAR (100)     NULL,
    [IsActive]             BIT                NOT NULL,
    [CreatedAt]            DATETIME2 (7)      NOT NULL,
    [LastLoginAt]          DATETIME2 (7)      NULL,
    [AvatarUrl]            NVARCHAR (300)     NULL,
    CONSTRAINT [PK_Users] PRIMARY KEY CLUSTERED ([Id] ASC)
);


GO
PRINT N'Creating Index [dbo].[Users].[EmailIndex]...';


GO
CREATE NONCLUSTERED INDEX [EmailIndex]
    ON [dbo].[Users]([NormalizedEmail] ASC);


GO
PRINT N'Creating Index [dbo].[Users].[UserNameIndex]...';


GO
CREATE UNIQUE NONCLUSTERED INDEX [UserNameIndex]
    ON [dbo].[Users]([NormalizedUserName] ASC) WHERE ([NormalizedUserName] IS NOT NULL);


GO
PRINT N'Creating Table [dbo].[UserSettings]...';


GO
CREATE TABLE [dbo].[UserSettings] (
    [Id]                  INT            IDENTITY (1, 1) NOT NULL,
    [UserId]              NVARCHAR (450) NOT NULL,
    [Theme]               NVARCHAR (30)  NOT NULL,
    [Language]            NVARCHAR (5)   NOT NULL,
    [ParentPinHash]       NVARCHAR (200) NULL,
    [WeeklyEmail]         BIT            NOT NULL,
    [NotifyTimeRequest]   BIT            NOT NULL,
    [NotifyContentReport] BIT            NOT NULL,
    [UpdatedAt]           DATETIME2 (7)  NOT NULL,
    CONSTRAINT [PK_UserSettings] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [UQ_UserSettings_UserId] UNIQUE NONCLUSTERED ([UserId] ASC)
);


GO
PRINT N'Creating Table [dbo].[UserTokens]...';


GO
CREATE TABLE [dbo].[UserTokens] (
    [UserId]        NVARCHAR (450) NOT NULL,
    [LoginProvider] NVARCHAR (128) NOT NULL,
    [Name]          NVARCHAR (128) NOT NULL,
    [Value]         NVARCHAR (MAX) NULL,
    CONSTRAINT [PK_UserTokens] PRIMARY KEY NONCLUSTERED ([UserId] ASC, [LoginProvider] ASC, [Name] ASC)
);


GO
PRINT N'Creating Table [dbo].[WeeklyReports]...';


GO
CREATE TABLE [dbo].[WeeklyReports] (
    [Id]                    INT            IDENTITY (1, 1) NOT NULL,
    [ChildProfileId]        INT            NOT NULL,
    [WeekStart]             DATE           NOT NULL,
    [TotalMinutes]          INT            NOT NULL,
    [LessonsCompleted]      INT            NOT NULL,
    [QuizAttempts]          INT            NOT NULL,
    [AvgQuizPercent]        DECIMAL (5, 2) NULL,
    [StrongSubject]         NVARCHAR (80)  NULL,
    [NeedsAttentionSubject] NVARCHAR (80)  NULL,
    [Summary]               NVARCHAR (MAX) NULL,
    [GeneratedAt]           DATETIME2 (7)  NOT NULL,
    [EmailedAt]             DATETIME2 (7)  NULL,
    CONSTRAINT [PK_WeeklyReports] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [UQ_WeeklyReports_ChildProfileId_WeekStart] UNIQUE NONCLUSTERED ([ChildProfileId] ASC, [WeekStart] ASC)
);


GO
PRINT N'Creating Default Constraint [dbo].[DF_Assignments_Status]...';


GO
ALTER TABLE [dbo].[Assignments]
    ADD CONSTRAINT [DF_Assignments_Status] DEFAULT (N'Assigned') FOR [Status];


GO
PRINT N'Creating Default Constraint [dbo].[DF_Assignments_AssignedAt]...';


GO
ALTER TABLE [dbo].[Assignments]
    ADD CONSTRAINT [DF_Assignments_AssignedAt] DEFAULT (sysutcdatetime()) FOR [AssignedAt];


GO
PRINT N'Creating Default Constraint [dbo].[DF_AuditLogs_CreatedAt]...';


GO
ALTER TABLE [dbo].[AuditLogs]
    ADD CONSTRAINT [DF_AuditLogs_CreatedAt] DEFAULT (sysutcdatetime()) FOR [CreatedAt];


GO
PRINT N'Creating Default Constraint [dbo].[DF_Badges_IsActive]...';


GO
ALTER TABLE [dbo].[Badges]
    ADD CONSTRAINT [DF_Badges_IsActive] DEFAULT ((1)) FOR [IsActive];


GO
PRINT N'Creating Default Constraint [dbo].[DF_BlockedKeywords_CreatedAt]...';


GO
ALTER TABLE [dbo].[BlockedKeywords]
    ADD CONSTRAINT [DF_BlockedKeywords_CreatedAt] DEFAULT (sysutcdatetime()) FOR [CreatedAt];


GO
PRINT N'Creating Default Constraint [dbo].[DF_ChildBadges_AwardedAt]...';


GO
ALTER TABLE [dbo].[ChildBadges]
    ADD CONSTRAINT [DF_ChildBadges_AwardedAt] DEFAULT (sysutcdatetime()) FOR [AwardedAt];


GO
PRINT N'Creating Default Constraint [dbo].[DF_ChildPaths_Status]...';


GO
ALTER TABLE [dbo].[ChildPaths]
    ADD CONSTRAINT [DF_ChildPaths_Status] DEFAULT (N'NotStarted') FOR [Status];


GO
PRINT N'Creating Default Constraint [dbo].[DF_ChildProfiles_CreatedAt]...';


GO
ALTER TABLE [dbo].[ChildProfiles]
    ADD CONSTRAINT [DF_ChildProfiles_CreatedAt] DEFAULT (sysutcdatetime()) FOR [CreatedAt];


GO
PRINT N'Creating Default Constraint [dbo].[DF_ChildProfiles_IsActive]...';


GO
ALTER TABLE [dbo].[ChildProfiles]
    ADD CONSTRAINT [DF_ChildProfiles_IsActive] DEFAULT ((1)) FOR [IsActive];


GO
PRINT N'Creating Default Constraint [dbo].[DF_ChildProfiles_PinFailedAttempts]...';


GO
ALTER TABLE [dbo].[ChildProfiles]
    ADD CONSTRAINT [DF_ChildProfiles_PinFailedAttempts] DEFAULT ((0)) FOR [PinFailedAttempts];


GO
PRINT N'Creating Default Constraint [dbo].[DF_ContentReports_CreatedAt]...';


GO
ALTER TABLE [dbo].[ContentReports]
    ADD CONSTRAINT [DF_ContentReports_CreatedAt] DEFAULT (sysutcdatetime()) FOR [CreatedAt];


GO
PRINT N'Creating Default Constraint [dbo].[DF_ContentReports_Reason]...';


GO
ALTER TABLE [dbo].[ContentReports]
    ADD CONSTRAINT [DF_ContentReports_Reason] DEFAULT (N'Inappropriate') FOR [Reason];


GO
PRINT N'Creating Default Constraint [dbo].[DF_ContentReports_Status]...';


GO
ALTER TABLE [dbo].[ContentReports]
    ADD CONSTRAINT [DF_ContentReports_Status] DEFAULT (N'Open') FOR [Status];


GO
PRINT N'Creating Default Constraint [dbo].[DF_ContentSources_CreatedAt]...';


GO
ALTER TABLE [dbo].[ContentSources]
    ADD CONSTRAINT [DF_ContentSources_CreatedAt] DEFAULT (sysutcdatetime()) FOR [CreatedAt];


GO
PRINT N'Creating Default Constraint [dbo].[DF_ContentSources_Platform]...';


GO
ALTER TABLE [dbo].[ContentSources]
    ADD CONSTRAINT [DF_ContentSources_Platform] DEFAULT (N'YouTubeChannel') FOR [Platform];


GO
PRINT N'Creating Default Constraint [dbo].[DF_ContentSources_Status]...';


GO
ALTER TABLE [dbo].[ContentSources]
    ADD CONSTRAINT [DF_ContentSources_Status] DEFAULT (N'Suggested') FOR [Status];


GO
PRINT N'Creating Default Constraint [dbo].[DF_CurationSuggestions_CreatedAt]...';


GO
ALTER TABLE [dbo].[CurationSuggestions]
    ADD CONSTRAINT [DF_CurationSuggestions_CreatedAt] DEFAULT (sysutcdatetime()) FOR [CreatedAt];


GO
PRINT N'Creating Default Constraint [dbo].[DF_CurationSuggestions_Status]...';


GO
ALTER TABLE [dbo].[CurationSuggestions]
    ADD CONSTRAINT [DF_CurationSuggestions_Status] DEFAULT (N'Pending') FOR [Status];


GO
PRINT N'Creating Default Constraint [dbo].[DF_DailyUsages_Seconds]...';


GO
ALTER TABLE [dbo].[DailyUsages]
    ADD CONSTRAINT [DF_DailyUsages_Seconds] DEFAULT ((0)) FOR [Seconds];


GO
PRINT N'Creating Default Constraint [dbo].[DF_DataRequests_RequestedAt]...';


GO
ALTER TABLE [dbo].[DataRequests]
    ADD CONSTRAINT [DF_DataRequests_RequestedAt] DEFAULT (sysutcdatetime()) FOR [RequestedAt];


GO
PRINT N'Creating Default Constraint [dbo].[DF_DataRequests_Status]...';


GO
ALTER TABLE [dbo].[DataRequests]
    ADD CONSTRAINT [DF_DataRequests_Status] DEFAULT (N'Pending') FOR [Status];


GO
PRINT N'Creating Default Constraint [dbo].[DF_Favorites_FavoritedAt]...';


GO
ALTER TABLE [dbo].[Favorites]
    ADD CONSTRAINT [DF_Favorites_FavoritedAt] DEFAULT (sysutcdatetime()) FOR [FavoritedAt];


GO
PRINT N'Creating Default Constraint [dbo].[DF_LearningGoals_CreatedAt]...';


GO
ALTER TABLE [dbo].[LearningGoals]
    ADD CONSTRAINT [DF_LearningGoals_CreatedAt] DEFAULT (sysutcdatetime()) FOR [CreatedAt];


GO
PRINT N'Creating Default Constraint [dbo].[DF_LearningPathItems_IsRequired]...';


GO
ALTER TABLE [dbo].[LearningPathItems]
    ADD CONSTRAINT [DF_LearningPathItems_IsRequired] DEFAULT ((1)) FOR [IsRequired];


GO
PRINT N'Creating Default Constraint [dbo].[DF_LearningPaths_IsActive]...';


GO
ALTER TABLE [dbo].[LearningPaths]
    ADD CONSTRAINT [DF_LearningPaths_IsActive] DEFAULT ((1)) FOR [IsActive];


GO
PRINT N'Creating Default Constraint [dbo].[DF_LearningSessions_ActiveSeconds]...';


GO
ALTER TABLE [dbo].[LearningSessions]
    ADD CONSTRAINT [DF_LearningSessions_ActiveSeconds] DEFAULT ((0)) FOR [ActiveSeconds];


GO
PRINT N'Creating Default Constraint [dbo].[DF_LearningSessions_LastHeartbeatAt]...';


GO
ALTER TABLE [dbo].[LearningSessions]
    ADD CONSTRAINT [DF_LearningSessions_LastHeartbeatAt] DEFAULT (sysutcdatetime()) FOR [LastHeartbeatAt];


GO
PRINT N'Creating Default Constraint [dbo].[DF_LearningSessions_StartedAt]...';


GO
ALTER TABLE [dbo].[LearningSessions]
    ADD CONSTRAINT [DF_LearningSessions_StartedAt] DEFAULT (sysutcdatetime()) FOR [StartedAt];


GO
PRINT N'Creating Default Constraint [dbo].[DF_LessonActivities_Status]...';


GO
ALTER TABLE [dbo].[LessonActivities]
    ADD CONSTRAINT [DF_LessonActivities_Status] DEFAULT (N'Started') FOR [Status];


GO
PRINT N'Creating Default Constraint [dbo].[DF_LessonActivities_UpdatedAt]...';


GO
ALTER TABLE [dbo].[LessonActivities]
    ADD CONSTRAINT [DF_LessonActivities_UpdatedAt] DEFAULT (sysutcdatetime()) FOR [UpdatedAt];


GO
PRINT N'Creating Default Constraint [dbo].[DF_LessonActivities_LastPositionSec]...';


GO
ALTER TABLE [dbo].[LessonActivities]
    ADD CONSTRAINT [DF_LessonActivities_LastPositionSec] DEFAULT ((0)) FOR [LastPositionSec];


GO
PRINT N'Creating Default Constraint [dbo].[DF_LessonActivities_ProgressPercent]...';


GO
ALTER TABLE [dbo].[LessonActivities]
    ADD CONSTRAINT [DF_LessonActivities_ProgressPercent] DEFAULT ((0)) FOR [ProgressPercent];


GO
PRINT N'Creating Default Constraint [dbo].[DF_LessonActivities_StartedAt]...';


GO
ALTER TABLE [dbo].[LessonActivities]
    ADD CONSTRAINT [DF_LessonActivities_StartedAt] DEFAULT (sysutcdatetime()) FOR [StartedAt];


GO
PRINT N'Creating Default Constraint [dbo].[DF_LessonActivities_WatchSeconds]...';


GO
ALTER TABLE [dbo].[LessonActivities]
    ADD CONSTRAINT [DF_LessonActivities_WatchSeconds] DEFAULT ((0)) FOR [WatchSeconds];


GO
PRINT N'Creating Default Constraint [dbo].[DF_LessonContents_SortOrder]...';


GO
ALTER TABLE [dbo].[LessonContents]
    ADD CONSTRAINT [DF_LessonContents_SortOrder] DEFAULT ((0)) FOR [SortOrder];


GO
PRINT N'Creating Default Constraint [dbo].[DF_LessonContents_ContentKind]...';


GO
ALTER TABLE [dbo].[LessonContents]
    ADD CONSTRAINT [DF_LessonContents_ContentKind] DEFAULT (N'Video') FOR [ContentKind];


GO
PRINT N'Creating Default Constraint [dbo].[DF_LessonRequests_Status]...';


GO
ALTER TABLE [dbo].[LessonRequests]
    ADD CONSTRAINT [DF_LessonRequests_Status] DEFAULT (N'Open') FOR [Status];


GO
PRINT N'Creating Default Constraint [dbo].[DF_LessonRequests_CreatedAt]...';


GO
ALTER TABLE [dbo].[LessonRequests]
    ADD CONSTRAINT [DF_LessonRequests_CreatedAt] DEFAULT (sysutcdatetime()) FOR [CreatedAt];


GO
PRINT N'Creating Default Constraint [dbo].[DF_LessonRequestVotes_CreatedAt]...';


GO
ALTER TABLE [dbo].[LessonRequestVotes]
    ADD CONSTRAINT [DF_LessonRequestVotes_CreatedAt] DEFAULT (sysutcdatetime()) FOR [CreatedAt];


GO
PRINT N'Creating Default Constraint [dbo].[DF_Lessons_UpdatedAt]...';


GO
ALTER TABLE [dbo].[Lessons]
    ADD CONSTRAINT [DF_Lessons_UpdatedAt] DEFAULT (sysutcdatetime()) FOR [UpdatedAt];


GO
PRINT N'Creating Default Constraint [dbo].[DF_Lessons_CreatedAt]...';


GO
ALTER TABLE [dbo].[Lessons]
    ADD CONSTRAINT [DF_Lessons_CreatedAt] DEFAULT (sysutcdatetime()) FOR [CreatedAt];


GO
PRINT N'Creating Default Constraint [dbo].[DF_Lessons_Status]...';


GO
ALTER TABLE [dbo].[Lessons]
    ADD CONSTRAINT [DF_Lessons_Status] DEFAULT (N'Draft') FOR [Status];


GO
PRINT N'Creating Default Constraint [dbo].[DF_Lessons_SortOrder]...';


GO
ALTER TABLE [dbo].[Lessons]
    ADD CONSTRAINT [DF_Lessons_SortOrder] DEFAULT ((0)) FOR [SortOrder];


GO
PRINT N'Creating Default Constraint [dbo].[DF_Lessons_DurationMin]...';


GO
ALTER TABLE [dbo].[Lessons]
    ADD CONSTRAINT [DF_Lessons_DurationMin] DEFAULT ((10)) FOR [DurationMin];


GO
PRINT N'Creating Default Constraint [dbo].[DF_Lessons_LessonType]...';


GO
ALTER TABLE [dbo].[Lessons]
    ADD CONSTRAINT [DF_Lessons_LessonType] DEFAULT (N'Video') FOR [LessonType];


GO
PRINT N'Creating Default Constraint [dbo].[DF_LoginHistories_CreatedAt]...';


GO
ALTER TABLE [dbo].[LoginHistories]
    ADD CONSTRAINT [DF_LoginHistories_CreatedAt] DEFAULT (sysutcdatetime()) FOR [CreatedAt];


GO
PRINT N'Creating Default Constraint [dbo].[DF_Notifications_IsRead]...';


GO
ALTER TABLE [dbo].[Notifications]
    ADD CONSTRAINT [DF_Notifications_IsRead] DEFAULT ((0)) FOR [IsRead];


GO
PRINT N'Creating Default Constraint [dbo].[DF_Notifications_CreatedAt]...';


GO
ALTER TABLE [dbo].[Notifications]
    ADD CONSTRAINT [DF_Notifications_CreatedAt] DEFAULT (sysutcdatetime()) FOR [CreatedAt];


GO
PRINT N'Creating Default Constraint [dbo].[DF_ParentSources_Status]...';


GO
ALTER TABLE [dbo].[ParentSources]
    ADD CONSTRAINT [DF_ParentSources_Status] DEFAULT (N'Allowed') FOR [Status];


GO
PRINT N'Creating Default Constraint [dbo].[DF_ParentSources_AddedAt]...';


GO
ALTER TABLE [dbo].[ParentSources]
    ADD CONSTRAINT [DF_ParentSources_AddedAt] DEFAULT (sysutcdatetime()) FOR [AddedAt];


GO
PRINT N'Creating Default Constraint [dbo].[DF_QuestionOptions_SortOrder]...';


GO
ALTER TABLE [dbo].[QuestionOptions]
    ADD CONSTRAINT [DF_QuestionOptions_SortOrder] DEFAULT ((0)) FOR [SortOrder];


GO
PRINT N'Creating Default Constraint [dbo].[DF_QuestionOptions_IsCorrect]...';


GO
ALTER TABLE [dbo].[QuestionOptions]
    ADD CONSTRAINT [DF_QuestionOptions_IsCorrect] DEFAULT ((0)) FOR [IsCorrect];


GO
PRINT N'Creating Default Constraint [dbo].[DF_Questions_Difficulty]...';


GO
ALTER TABLE [dbo].[Questions]
    ADD CONSTRAINT [DF_Questions_Difficulty] DEFAULT ((1)) FOR [Difficulty];


GO
PRINT N'Creating Default Constraint [dbo].[DF_Questions_CreatedAt]...';


GO
ALTER TABLE [dbo].[Questions]
    ADD CONSTRAINT [DF_Questions_CreatedAt] DEFAULT (sysutcdatetime()) FOR [CreatedAt];


GO
PRINT N'Creating Default Constraint [dbo].[DF_Questions_Status]...';


GO
ALTER TABLE [dbo].[Questions]
    ADD CONSTRAINT [DF_Questions_Status] DEFAULT (N'Draft') FOR [Status];


GO
PRINT N'Creating Default Constraint [dbo].[DF_Questions_QuestionType]...';


GO
ALTER TABLE [dbo].[Questions]
    ADD CONSTRAINT [DF_Questions_QuestionType] DEFAULT (N'SingleChoice') FOR [QuestionType];


GO
PRINT N'Creating Default Constraint [dbo].[DF_Questions_Origin]...';


GO
ALTER TABLE [dbo].[Questions]
    ADD CONSTRAINT [DF_Questions_Origin] DEFAULT (N'Manual') FOR [Origin];


GO
PRINT N'Creating Default Constraint [dbo].[DF_QuizAttemptAnswers_AnsweredAt]...';


GO
ALTER TABLE [dbo].[QuizAttemptAnswers]
    ADD CONSTRAINT [DF_QuizAttemptAnswers_AnsweredAt] DEFAULT (sysutcdatetime()) FOR [AnsweredAt];


GO
PRINT N'Creating Default Constraint [dbo].[DF_QuizAttempts_TotalCount]...';


GO
ALTER TABLE [dbo].[QuizAttempts]
    ADD CONSTRAINT [DF_QuizAttempts_TotalCount] DEFAULT ((0)) FOR [TotalCount];


GO
PRINT N'Creating Default Constraint [dbo].[DF_QuizAttempts_StartedAt]...';


GO
ALTER TABLE [dbo].[QuizAttempts]
    ADD CONSTRAINT [DF_QuizAttempts_StartedAt] DEFAULT (sysutcdatetime()) FOR [StartedAt];


GO
PRINT N'Creating Default Constraint [dbo].[DF_QuizAttempts_CorrectCount]...';


GO
ALTER TABLE [dbo].[QuizAttempts]
    ADD CONSTRAINT [DF_QuizAttempts_CorrectCount] DEFAULT ((0)) FOR [CorrectCount];


GO
PRINT N'Creating Default Constraint [dbo].[DF_QuizQuestions_SortOrder]...';


GO
ALTER TABLE [dbo].[QuizQuestions]
    ADD CONSTRAINT [DF_QuizQuestions_SortOrder] DEFAULT ((0)) FOR [SortOrder];


GO
PRINT N'Creating Default Constraint [dbo].[DF_QuizQuestions_Points]...';


GO
ALTER TABLE [dbo].[QuizQuestions]
    ADD CONSTRAINT [DF_QuizQuestions_Points] DEFAULT ((1)) FOR [Points];


GO
PRINT N'Creating Default Constraint [dbo].[DF_Quizzes_Status]...';


GO
ALTER TABLE [dbo].[Quizzes]
    ADD CONSTRAINT [DF_Quizzes_Status] DEFAULT (N'Draft') FOR [Status];


GO
PRINT N'Creating Default Constraint [dbo].[DF_Quizzes_CreatedAt]...';


GO
ALTER TABLE [dbo].[Quizzes]
    ADD CONSTRAINT [DF_Quizzes_CreatedAt] DEFAULT (sysutcdatetime()) FOR [CreatedAt];


GO
PRINT N'Creating Default Constraint [dbo].[DF_Quizzes_ShuffleQuestions]...';


GO
ALTER TABLE [dbo].[Quizzes]
    ADD CONSTRAINT [DF_Quizzes_ShuffleQuestions] DEFAULT ((1)) FOR [ShuffleQuestions];


GO
PRINT N'Creating Default Constraint [dbo].[DF_Quizzes_PassPercent]...';


GO
ALTER TABLE [dbo].[Quizzes]
    ADD CONSTRAINT [DF_Quizzes_PassPercent] DEFAULT ((70)) FOR [PassPercent];


GO
PRINT N'Creating Default Constraint [dbo].[DF_SearchLogs_WasBlocked]...';


GO
ALTER TABLE [dbo].[SearchLogs]
    ADD CONSTRAINT [DF_SearchLogs_WasBlocked] DEFAULT ((0)) FOR [WasBlocked];


GO
PRINT N'Creating Default Constraint [dbo].[DF_SearchLogs_ResultCount]...';


GO
ALTER TABLE [dbo].[SearchLogs]
    ADD CONSTRAINT [DF_SearchLogs_ResultCount] DEFAULT ((0)) FOR [ResultCount];


GO
PRINT N'Creating Default Constraint [dbo].[DF_SearchLogs_CreatedAt]...';


GO
ALTER TABLE [dbo].[SearchLogs]
    ADD CONSTRAINT [DF_SearchLogs_CreatedAt] DEFAULT (sysutcdatetime()) FOR [CreatedAt];


GO
PRINT N'Creating Default Constraint [dbo].[DF_SourceVideos_Status]...';


GO
ALTER TABLE [dbo].[SourceVideos]
    ADD CONSTRAINT [DF_SourceVideos_Status] DEFAULT (N'Pending') FOR [Status];


GO
PRINT N'Creating Default Constraint [dbo].[DF_SourceVideos_Platform]...';


GO
ALTER TABLE [dbo].[SourceVideos]
    ADD CONSTRAINT [DF_SourceVideos_Platform] DEFAULT (N'YouTube') FOR [Platform];


GO
PRINT N'Creating Default Constraint [dbo].[DF_SourceVideoSources_DiscoveredAt]...';


GO
ALTER TABLE [dbo].[SourceVideoSources]
    ADD CONSTRAINT [DF_SourceVideoSources_DiscoveredAt] DEFAULT (sysutcdatetime()) FOR [DiscoveredAt];


GO
PRINT N'Creating Default Constraint [dbo].[DF_Streaks_LongestDays]...';


GO
ALTER TABLE [dbo].[Streaks]
    ADD CONSTRAINT [DF_Streaks_LongestDays] DEFAULT ((0)) FOR [LongestDays];


GO
PRINT N'Creating Default Constraint [dbo].[DF_Streaks_CurrentDays]...';


GO
ALTER TABLE [dbo].[Streaks]
    ADD CONSTRAINT [DF_Streaks_CurrentDays] DEFAULT ((0)) FOR [CurrentDays];


GO
PRINT N'Creating Default Constraint [dbo].[DF_SubjectProgresses_UpdatedAt]...';


GO
ALTER TABLE [dbo].[SubjectProgresses]
    ADD CONSTRAINT [DF_SubjectProgresses_UpdatedAt] DEFAULT (sysutcdatetime()) FOR [UpdatedAt];


GO
PRINT N'Creating Default Constraint [dbo].[DF_SubjectProgresses_TotalLessons]...';


GO
ALTER TABLE [dbo].[SubjectProgresses]
    ADD CONSTRAINT [DF_SubjectProgresses_TotalLessons] DEFAULT ((0)) FOR [TotalLessons];


GO
PRINT N'Creating Default Constraint [dbo].[DF_SubjectProgresses_CompletedLessons]...';


GO
ALTER TABLE [dbo].[SubjectProgresses]
    ADD CONSTRAINT [DF_SubjectProgresses_CompletedLessons] DEFAULT ((0)) FOR [CompletedLessons];


GO
PRINT N'Creating Default Constraint [dbo].[DF_Subjects_SortOrder]...';


GO
ALTER TABLE [dbo].[Subjects]
    ADD CONSTRAINT [DF_Subjects_SortOrder] DEFAULT ((0)) FOR [SortOrder];


GO
PRINT N'Creating Default Constraint [dbo].[DF_Subjects_IsActive]...';


GO
ALTER TABLE [dbo].[Subjects]
    ADD CONSTRAINT [DF_Subjects_IsActive] DEFAULT ((1)) FOR [IsActive];


GO
PRINT N'Creating Default Constraint [dbo].[DF_SystemMetricsDaily_TotalMinutes]...';


GO
ALTER TABLE [dbo].[SystemMetricsDaily]
    ADD CONSTRAINT [DF_SystemMetricsDaily_TotalMinutes] DEFAULT ((0)) FOR [TotalMinutes];


GO
PRINT N'Creating Default Constraint [dbo].[DF_SystemMetricsDaily_NewSources]...';


GO
ALTER TABLE [dbo].[SystemMetricsDaily]
    ADD CONSTRAINT [DF_SystemMetricsDaily_NewSources] DEFAULT ((0)) FOR [NewSources];


GO
PRINT N'Creating Default Constraint [dbo].[DF_SystemMetricsDaily_ActiveParents]...';


GO
ALTER TABLE [dbo].[SystemMetricsDaily]
    ADD CONSTRAINT [DF_SystemMetricsDaily_ActiveParents] DEFAULT ((0)) FOR [ActiveParents];


GO
PRINT N'Creating Default Constraint [dbo].[DF_SystemMetricsDaily_OpenReports]...';


GO
ALTER TABLE [dbo].[SystemMetricsDaily]
    ADD CONSTRAINT [DF_SystemMetricsDaily_OpenReports] DEFAULT ((0)) FOR [OpenReports];


GO
PRINT N'Creating Default Constraint [dbo].[DF_SystemMetricsDaily_LessonsCompleted]...';


GO
ALTER TABLE [dbo].[SystemMetricsDaily]
    ADD CONSTRAINT [DF_SystemMetricsDaily_LessonsCompleted] DEFAULT ((0)) FOR [LessonsCompleted];


GO
PRINT N'Creating Default Constraint [dbo].[DF_SystemMetricsDaily_ActiveChildren]...';


GO
ALTER TABLE [dbo].[SystemMetricsDaily]
    ADD CONSTRAINT [DF_SystemMetricsDaily_ActiveChildren] DEFAULT ((0)) FOR [ActiveChildren];


GO
PRINT N'Creating Default Constraint [dbo].[DF_TimeExtensionRequests_RequestedAt]...';


GO
ALTER TABLE [dbo].[TimeExtensionRequests]
    ADD CONSTRAINT [DF_TimeExtensionRequests_RequestedAt] DEFAULT (sysutcdatetime()) FOR [RequestedAt];


GO
PRINT N'Creating Default Constraint [dbo].[DF_TimeExtensionRequests_Status]...';


GO
ALTER TABLE [dbo].[TimeExtensionRequests]
    ADD CONSTRAINT [DF_TimeExtensionRequests_Status] DEFAULT (N'Pending') FOR [Status];


GO
PRINT N'Creating Default Constraint [dbo].[DF_TimeLimitPolicies_DailyMinutes]...';


GO
ALTER TABLE [dbo].[TimeLimitPolicies]
    ADD CONSTRAINT [DF_TimeLimitPolicies_DailyMinutes] DEFAULT ((60)) FOR [DailyMinutes];


GO
PRINT N'Creating Default Constraint [dbo].[DF_TimeLimitPolicies_UpdatedAt]...';


GO
ALTER TABLE [dbo].[TimeLimitPolicies]
    ADD CONSTRAINT [DF_TimeLimitPolicies_UpdatedAt] DEFAULT (sysutcdatetime()) FOR [UpdatedAt];


GO
PRINT N'Creating Default Constraint [dbo].[DF_UserConsents_DecidedAt]...';


GO
ALTER TABLE [dbo].[UserConsents]
    ADD CONSTRAINT [DF_UserConsents_DecidedAt] DEFAULT (sysutcdatetime()) FOR [DecidedAt];


GO
PRINT N'Creating Default Constraint [dbo].[DF_Users_CreatedAt]...';


GO
ALTER TABLE [dbo].[Users]
    ADD CONSTRAINT [DF_Users_CreatedAt] DEFAULT (sysutcdatetime()) FOR [CreatedAt];


GO
PRINT N'Creating Default Constraint [dbo].[DF_Users_IsActive]...';


GO
ALTER TABLE [dbo].[Users]
    ADD CONSTRAINT [DF_Users_IsActive] DEFAULT ((1)) FOR [IsActive];


GO
PRINT N'Creating Default Constraint [dbo].[DF_Users_PhoneConfirmed]...';


GO
ALTER TABLE [dbo].[Users]
    ADD CONSTRAINT [DF_Users_PhoneConfirmed] DEFAULT ((0)) FOR [PhoneNumberConfirmed];


GO
PRINT N'Creating Default Constraint [dbo].[DF_Users_2FA]...';


GO
ALTER TABLE [dbo].[Users]
    ADD CONSTRAINT [DF_Users_2FA] DEFAULT ((0)) FOR [TwoFactorEnabled];


GO
PRINT N'Creating Default Constraint [dbo].[DF_Users_AccessFailed]...';


GO
ALTER TABLE [dbo].[Users]
    ADD CONSTRAINT [DF_Users_AccessFailed] DEFAULT ((0)) FOR [AccessFailedCount];


GO
PRINT N'Creating Default Constraint [dbo].[DF_Users_EmailConfirmed]...';


GO
ALTER TABLE [dbo].[Users]
    ADD CONSTRAINT [DF_Users_EmailConfirmed] DEFAULT ((0)) FOR [EmailConfirmed];


GO
PRINT N'Creating Default Constraint [dbo].[DF_Users_LockoutEnabled]...';


GO
ALTER TABLE [dbo].[Users]
    ADD CONSTRAINT [DF_Users_LockoutEnabled] DEFAULT ((1)) FOR [LockoutEnabled];


GO
PRINT N'Creating Default Constraint [dbo].[DF_UserSettings_NotifyContentReport]...';


GO
ALTER TABLE [dbo].[UserSettings]
    ADD CONSTRAINT [DF_UserSettings_NotifyContentReport] DEFAULT ((1)) FOR [NotifyContentReport];


GO
PRINT N'Creating Default Constraint [dbo].[DF_UserSettings_Theme]...';


GO
ALTER TABLE [dbo].[UserSettings]
    ADD CONSTRAINT [DF_UserSettings_Theme] DEFAULT (N'Auto') FOR [Theme];


GO
PRINT N'Creating Default Constraint [dbo].[DF_UserSettings_UpdatedAt]...';


GO
ALTER TABLE [dbo].[UserSettings]
    ADD CONSTRAINT [DF_UserSettings_UpdatedAt] DEFAULT (sysutcdatetime()) FOR [UpdatedAt];


GO
PRINT N'Creating Default Constraint [dbo].[DF_UserSettings_Language]...';


GO
ALTER TABLE [dbo].[UserSettings]
    ADD CONSTRAINT [DF_UserSettings_Language] DEFAULT (N'vi') FOR [Language];


GO
PRINT N'Creating Default Constraint [dbo].[DF_UserSettings_NotifyTimeRequest]...';


GO
ALTER TABLE [dbo].[UserSettings]
    ADD CONSTRAINT [DF_UserSettings_NotifyTimeRequest] DEFAULT ((1)) FOR [NotifyTimeRequest];


GO
PRINT N'Creating Default Constraint [dbo].[DF_UserSettings_WeeklyEmail]...';


GO
ALTER TABLE [dbo].[UserSettings]
    ADD CONSTRAINT [DF_UserSettings_WeeklyEmail] DEFAULT ((1)) FOR [WeeklyEmail];


GO
PRINT N'Creating Default Constraint [dbo].[DF_WeeklyReports_GeneratedAt]...';


GO
ALTER TABLE [dbo].[WeeklyReports]
    ADD CONSTRAINT [DF_WeeklyReports_GeneratedAt] DEFAULT (sysutcdatetime()) FOR [GeneratedAt];


GO
PRINT N'Creating Default Constraint [dbo].[DF_WeeklyReports_QuizAttempts]...';


GO
ALTER TABLE [dbo].[WeeklyReports]
    ADD CONSTRAINT [DF_WeeklyReports_QuizAttempts] DEFAULT ((0)) FOR [QuizAttempts];


GO
PRINT N'Creating Default Constraint [dbo].[DF_WeeklyReports_TotalMinutes]...';


GO
ALTER TABLE [dbo].[WeeklyReports]
    ADD CONSTRAINT [DF_WeeklyReports_TotalMinutes] DEFAULT ((0)) FOR [TotalMinutes];


GO
PRINT N'Creating Default Constraint [dbo].[DF_WeeklyReports_LessonsCompleted]...';


GO
ALTER TABLE [dbo].[WeeklyReports]
    ADD CONSTRAINT [DF_WeeklyReports_LessonsCompleted] DEFAULT ((0)) FOR [LessonsCompleted];


GO
PRINT N'Creating Foreign Key [dbo].[FK_Assignments_Lessons_LessonId]...';


GO
ALTER TABLE [dbo].[Assignments] WITH NOCHECK
    ADD CONSTRAINT [FK_Assignments_Lessons_LessonId] FOREIGN KEY ([LessonId]) REFERENCES [dbo].[Lessons] ([Id]);


GO
PRINT N'Creating Foreign Key [dbo].[FK_Assignments_Users_AssignedById]...';


GO
ALTER TABLE [dbo].[Assignments] WITH NOCHECK
    ADD CONSTRAINT [FK_Assignments_Users_AssignedById] FOREIGN KEY ([AssignedById]) REFERENCES [dbo].[Users] ([Id]);


GO
PRINT N'Creating Foreign Key [dbo].[FK_Assignments_ChildProfiles_ChildProfileId]...';


GO
ALTER TABLE [dbo].[Assignments] WITH NOCHECK
    ADD CONSTRAINT [FK_Assignments_ChildProfiles_ChildProfileId] FOREIGN KEY ([ChildProfileId]) REFERENCES [dbo].[ChildProfiles] ([Id]) ON DELETE CASCADE;


GO
PRINT N'Creating Foreign Key [dbo].[FK_Assignments_Quizzes_QuizId]...';


GO
ALTER TABLE [dbo].[Assignments] WITH NOCHECK
    ADD CONSTRAINT [FK_Assignments_Quizzes_QuizId] FOREIGN KEY ([QuizId]) REFERENCES [dbo].[Quizzes] ([Id]);


GO
PRINT N'Creating Foreign Key [dbo].[FK_AuditLogs_Users_ActorId]...';


GO
ALTER TABLE [dbo].[AuditLogs] WITH NOCHECK
    ADD CONSTRAINT [FK_AuditLogs_Users_ActorId] FOREIGN KEY ([ActorId]) REFERENCES [dbo].[Users] ([Id]) ON DELETE SET NULL;


GO
PRINT N'Creating Foreign Key [dbo].[FK_BlockedKeywords_Users_CreatedById]...';


GO
ALTER TABLE [dbo].[BlockedKeywords] WITH NOCHECK
    ADD CONSTRAINT [FK_BlockedKeywords_Users_CreatedById] FOREIGN KEY ([CreatedById]) REFERENCES [dbo].[Users] ([Id]);


GO
PRINT N'Creating Foreign Key [dbo].[FK_BlockedKeywords_Users_ParentId]...';


GO
ALTER TABLE [dbo].[BlockedKeywords] WITH NOCHECK
    ADD CONSTRAINT [FK_BlockedKeywords_Users_ParentId] FOREIGN KEY ([ParentId]) REFERENCES [dbo].[Users] ([Id]) ON DELETE CASCADE;


GO
PRINT N'Creating Foreign Key [dbo].[FK_ChildBadges_Badges_BadgeId]...';


GO
ALTER TABLE [dbo].[ChildBadges] WITH NOCHECK
    ADD CONSTRAINT [FK_ChildBadges_Badges_BadgeId] FOREIGN KEY ([BadgeId]) REFERENCES [dbo].[Badges] ([Id]);


GO
PRINT N'Creating Foreign Key [dbo].[FK_ChildBadges_ChildProfiles_ChildProfileId]...';


GO
ALTER TABLE [dbo].[ChildBadges] WITH NOCHECK
    ADD CONSTRAINT [FK_ChildBadges_ChildProfiles_ChildProfileId] FOREIGN KEY ([ChildProfileId]) REFERENCES [dbo].[ChildProfiles] ([Id]) ON DELETE CASCADE;


GO
PRINT N'Creating Foreign Key [dbo].[FK_ChildPaths_LearningPaths_PathId]...';


GO
ALTER TABLE [dbo].[ChildPaths] WITH NOCHECK
    ADD CONSTRAINT [FK_ChildPaths_LearningPaths_PathId] FOREIGN KEY ([PathId]) REFERENCES [dbo].[LearningPaths] ([Id]);


GO
PRINT N'Creating Foreign Key [dbo].[FK_ChildPaths_ChildProfiles_ChildProfileId]...';


GO
ALTER TABLE [dbo].[ChildPaths] WITH NOCHECK
    ADD CONSTRAINT [FK_ChildPaths_ChildProfiles_ChildProfileId] FOREIGN KEY ([ChildProfileId]) REFERENCES [dbo].[ChildProfiles] ([Id]) ON DELETE CASCADE;


GO
PRINT N'Creating Foreign Key [dbo].[FK_ChildPaths_LearningPathItems_CurrentItemId]...';


GO
ALTER TABLE [dbo].[ChildPaths] WITH NOCHECK
    ADD CONSTRAINT [FK_ChildPaths_LearningPathItems_CurrentItemId] FOREIGN KEY ([CurrentItemId]) REFERENCES [dbo].[LearningPathItems] ([Id]);


GO
PRINT N'Creating Foreign Key [dbo].[FK_ChildPaths_LearningPathItems_CurrentItemId_PathId]...';


GO
ALTER TABLE [dbo].[ChildPaths] WITH NOCHECK
    ADD CONSTRAINT [FK_ChildPaths_LearningPathItems_CurrentItemId_PathId] FOREIGN KEY ([CurrentItemId], [PathId]) REFERENCES [dbo].[LearningPathItems] ([Id], [PathId]);


GO
PRINT N'Creating Foreign Key [dbo].[FK_ChildProfiles_GradeLevels_GradeLevelId]...';


GO
ALTER TABLE [dbo].[ChildProfiles] WITH NOCHECK
    ADD CONSTRAINT [FK_ChildProfiles_GradeLevels_GradeLevelId] FOREIGN KEY ([GradeLevelId]) REFERENCES [dbo].[GradeLevels] ([Id]);


GO
PRINT N'Creating Foreign Key [dbo].[FK_ChildProfiles_Users_ParentId]...';


GO
ALTER TABLE [dbo].[ChildProfiles] WITH NOCHECK
    ADD CONSTRAINT [FK_ChildProfiles_Users_ParentId] FOREIGN KEY ([ParentId]) REFERENCES [dbo].[Users] ([Id]) ON DELETE CASCADE;


GO
PRINT N'Creating Foreign Key [dbo].[FK_ChildProfiles_Users_UserId]...';


GO
ALTER TABLE [dbo].[ChildProfiles] WITH NOCHECK
    ADD CONSTRAINT [FK_ChildProfiles_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [dbo].[Users] ([Id]);


GO
PRINT N'Creating Foreign Key [dbo].[FK_ChildSubjects_ChildProfiles_ChildProfileId]...';


GO
ALTER TABLE [dbo].[ChildSubjects] WITH NOCHECK
    ADD CONSTRAINT [FK_ChildSubjects_ChildProfiles_ChildProfileId] FOREIGN KEY ([ChildProfileId]) REFERENCES [dbo].[ChildProfiles] ([Id]) ON DELETE CASCADE;


GO
PRINT N'Creating Foreign Key [dbo].[FK_ChildSubjects_Subjects_SubjectId]...';


GO
ALTER TABLE [dbo].[ChildSubjects] WITH NOCHECK
    ADD CONSTRAINT [FK_ChildSubjects_Subjects_SubjectId] FOREIGN KEY ([SubjectId]) REFERENCES [dbo].[Subjects] ([Id]);


GO
PRINT N'Creating Foreign Key [dbo].[FK_ContentReports_ChildProfiles_ChildProfileId]...';


GO
ALTER TABLE [dbo].[ContentReports] WITH NOCHECK
    ADD CONSTRAINT [FK_ContentReports_ChildProfiles_ChildProfileId] FOREIGN KEY ([ChildProfileId]) REFERENCES [dbo].[ChildProfiles] ([Id]) ON DELETE CASCADE;


GO
PRINT N'Creating Foreign Key [dbo].[FK_ContentReports_Users_ReportedById]...';


GO
ALTER TABLE [dbo].[ContentReports] WITH NOCHECK
    ADD CONSTRAINT [FK_ContentReports_Users_ReportedById] FOREIGN KEY ([ReportedById]) REFERENCES [dbo].[Users] ([Id]);


GO
PRINT N'Creating Foreign Key [dbo].[FK_ContentReports_SourceVideos_SourceVideoId]...';


GO
ALTER TABLE [dbo].[ContentReports] WITH NOCHECK
    ADD CONSTRAINT [FK_ContentReports_SourceVideos_SourceVideoId] FOREIGN KEY ([SourceVideoId]) REFERENCES [dbo].[SourceVideos] ([Id]);


GO
PRINT N'Creating Foreign Key [dbo].[FK_ContentReports_Users_HandledById]...';


GO
ALTER TABLE [dbo].[ContentReports] WITH NOCHECK
    ADD CONSTRAINT [FK_ContentReports_Users_HandledById] FOREIGN KEY ([HandledById]) REFERENCES [dbo].[Users] ([Id]);


GO
PRINT N'Creating Foreign Key [dbo].[FK_ContentReports_Lessons_LessonId]...';


GO
ALTER TABLE [dbo].[ContentReports] WITH NOCHECK
    ADD CONSTRAINT [FK_ContentReports_Lessons_LessonId] FOREIGN KEY ([LessonId]) REFERENCES [dbo].[Lessons] ([Id]);


GO
PRINT N'Creating Foreign Key [dbo].[FK_ContentSources_Users_ReviewedById]...';


GO
ALTER TABLE [dbo].[ContentSources] WITH NOCHECK
    ADD CONSTRAINT [FK_ContentSources_Users_ReviewedById] FOREIGN KEY ([ReviewedById]) REFERENCES [dbo].[Users] ([Id]);


GO
PRINT N'Creating Foreign Key [dbo].[FK_ContentSources_Users_SuggestedById]...';


GO
ALTER TABLE [dbo].[ContentSources] WITH NOCHECK
    ADD CONSTRAINT [FK_ContentSources_Users_SuggestedById] FOREIGN KEY ([SuggestedById]) REFERENCES [dbo].[Users] ([Id]);


GO
PRINT N'Creating Foreign Key [dbo].[FK_CurationSuggestions_Users_DecidedById]...';


GO
ALTER TABLE [dbo].[CurationSuggestions] WITH NOCHECK
    ADD CONSTRAINT [FK_CurationSuggestions_Users_DecidedById] FOREIGN KEY ([DecidedById]) REFERENCES [dbo].[Users] ([Id]);


GO
PRINT N'Creating Foreign Key [dbo].[FK_CurationSuggestions_ContentSources_SourceId]...';


GO
ALTER TABLE [dbo].[CurationSuggestions] WITH NOCHECK
    ADD CONSTRAINT [FK_CurationSuggestions_ContentSources_SourceId] FOREIGN KEY ([SourceId]) REFERENCES [dbo].[ContentSources] ([Id]) ON DELETE CASCADE;


GO
PRINT N'Creating Foreign Key [dbo].[FK_DailyUsages_ChildProfiles_ChildProfileId]...';


GO
ALTER TABLE [dbo].[DailyUsages] WITH NOCHECK
    ADD CONSTRAINT [FK_DailyUsages_ChildProfiles_ChildProfileId] FOREIGN KEY ([ChildProfileId]) REFERENCES [dbo].[ChildProfiles] ([Id]) ON DELETE CASCADE;


GO
PRINT N'Creating Foreign Key [dbo].[FK_DataRequests_Users_ParentId]...';


GO
ALTER TABLE [dbo].[DataRequests] WITH NOCHECK
    ADD CONSTRAINT [FK_DataRequests_Users_ParentId] FOREIGN KEY ([ParentId]) REFERENCES [dbo].[Users] ([Id]) ON DELETE CASCADE;


GO
PRINT N'Creating Foreign Key [dbo].[FK_DataRequests_Users_ProcessedById]...';


GO
ALTER TABLE [dbo].[DataRequests] WITH NOCHECK
    ADD CONSTRAINT [FK_DataRequests_Users_ProcessedById] FOREIGN KEY ([ProcessedById]) REFERENCES [dbo].[Users] ([Id]);


GO
PRINT N'Creating Foreign Key [dbo].[FK_Favorites_Lessons_LessonId]...';


GO
ALTER TABLE [dbo].[Favorites] WITH NOCHECK
    ADD CONSTRAINT [FK_Favorites_Lessons_LessonId] FOREIGN KEY ([LessonId]) REFERENCES [dbo].[Lessons] ([Id]);


GO
PRINT N'Creating Foreign Key [dbo].[FK_Favorites_ChildProfiles_ChildProfileId]...';


GO
ALTER TABLE [dbo].[Favorites] WITH NOCHECK
    ADD CONSTRAINT [FK_Favorites_ChildProfiles_ChildProfileId] FOREIGN KEY ([ChildProfileId]) REFERENCES [dbo].[ChildProfiles] ([Id]) ON DELETE CASCADE;


GO
PRINT N'Creating Foreign Key [dbo].[FK_LearningGoals_Users_CreatedById]...';


GO
ALTER TABLE [dbo].[LearningGoals] WITH NOCHECK
    ADD CONSTRAINT [FK_LearningGoals_Users_CreatedById] FOREIGN KEY ([CreatedById]) REFERENCES [dbo].[Users] ([Id]);


GO
PRINT N'Creating Foreign Key [dbo].[FK_LearningGoals_ChildProfiles_ChildProfileId]...';


GO
ALTER TABLE [dbo].[LearningGoals] WITH NOCHECK
    ADD CONSTRAINT [FK_LearningGoals_ChildProfiles_ChildProfileId] FOREIGN KEY ([ChildProfileId]) REFERENCES [dbo].[ChildProfiles] ([Id]) ON DELETE CASCADE;


GO
PRINT N'Creating Foreign Key [dbo].[FK_LearningPathItems_Lessons_LessonId]...';


GO
ALTER TABLE [dbo].[LearningPathItems] WITH NOCHECK
    ADD CONSTRAINT [FK_LearningPathItems_Lessons_LessonId] FOREIGN KEY ([LessonId]) REFERENCES [dbo].[Lessons] ([Id]);


GO
PRINT N'Creating Foreign Key [dbo].[FK_LearningPathItems_LearningPaths_PathId]...';


GO
ALTER TABLE [dbo].[LearningPathItems] WITH NOCHECK
    ADD CONSTRAINT [FK_LearningPathItems_LearningPaths_PathId] FOREIGN KEY ([PathId]) REFERENCES [dbo].[LearningPaths] ([Id]) ON DELETE CASCADE;


GO
PRINT N'Creating Foreign Key [dbo].[FK_LearningPaths_GradeLevels_GradeLevelId]...';


GO
ALTER TABLE [dbo].[LearningPaths] WITH NOCHECK
    ADD CONSTRAINT [FK_LearningPaths_GradeLevels_GradeLevelId] FOREIGN KEY ([GradeLevelId]) REFERENCES [dbo].[GradeLevels] ([Id]);


GO
PRINT N'Creating Foreign Key [dbo].[FK_LearningPaths_Subjects_SubjectId]...';


GO
ALTER TABLE [dbo].[LearningPaths] WITH NOCHECK
    ADD CONSTRAINT [FK_LearningPaths_Subjects_SubjectId] FOREIGN KEY ([SubjectId]) REFERENCES [dbo].[Subjects] ([Id]);


GO
PRINT N'Creating Foreign Key [dbo].[FK_LearningSessions_ChildProfiles_ChildProfileId]...';


GO
ALTER TABLE [dbo].[LearningSessions] WITH NOCHECK
    ADD CONSTRAINT [FK_LearningSessions_ChildProfiles_ChildProfileId] FOREIGN KEY ([ChildProfileId]) REFERENCES [dbo].[ChildProfiles] ([Id]) ON DELETE CASCADE;


GO
PRINT N'Creating Foreign Key [dbo].[FK_LessonActivities_LearningSessions_SessionId]...';


GO
ALTER TABLE [dbo].[LessonActivities] WITH NOCHECK
    ADD CONSTRAINT [FK_LessonActivities_LearningSessions_SessionId] FOREIGN KEY ([SessionId]) REFERENCES [dbo].[LearningSessions] ([Id]);


GO
PRINT N'Creating Foreign Key [dbo].[FK_LessonActivities_LearningSessions_SessionId_ChildProfileId]...';


GO
ALTER TABLE [dbo].[LessonActivities] WITH NOCHECK
    ADD CONSTRAINT [FK_LessonActivities_LearningSessions_SessionId_ChildProfileId] FOREIGN KEY ([SessionId], [ChildProfileId]) REFERENCES [dbo].[LearningSessions] ([Id], [ChildProfileId]);


GO
PRINT N'Creating Foreign Key [dbo].[FK_LessonActivities_Lessons_LessonId]...';


GO
ALTER TABLE [dbo].[LessonActivities] WITH NOCHECK
    ADD CONSTRAINT [FK_LessonActivities_Lessons_LessonId] FOREIGN KEY ([LessonId]) REFERENCES [dbo].[Lessons] ([Id]);


GO
PRINT N'Creating Foreign Key [dbo].[FK_LessonActivities_ChildProfiles_ChildProfileId]...';


GO
ALTER TABLE [dbo].[LessonActivities] WITH NOCHECK
    ADD CONSTRAINT [FK_LessonActivities_ChildProfiles_ChildProfileId] FOREIGN KEY ([ChildProfileId]) REFERENCES [dbo].[ChildProfiles] ([Id]) ON DELETE CASCADE;


GO
PRINT N'Creating Foreign Key [dbo].[FK_LessonContents_Lessons_LessonId]...';


GO
ALTER TABLE [dbo].[LessonContents] WITH NOCHECK
    ADD CONSTRAINT [FK_LessonContents_Lessons_LessonId] FOREIGN KEY ([LessonId]) REFERENCES [dbo].[Lessons] ([Id]) ON DELETE CASCADE;


GO
PRINT N'Creating Foreign Key [dbo].[FK_LessonContents_SourceVideos_SourceVideoId]...';


GO
ALTER TABLE [dbo].[LessonContents] WITH NOCHECK
    ADD CONSTRAINT [FK_LessonContents_SourceVideos_SourceVideoId] FOREIGN KEY ([SourceVideoId]) REFERENCES [dbo].[SourceVideos] ([Id]);


GO
PRINT N'Creating Foreign Key [dbo].[FK_LessonRequests_Users_HandledById]...';


GO
ALTER TABLE [dbo].[LessonRequests] WITH NOCHECK
    ADD CONSTRAINT [FK_LessonRequests_Users_HandledById] FOREIGN KEY ([HandledById]) REFERENCES [dbo].[Users] ([Id]);


GO
PRINT N'Creating Foreign Key [dbo].[FK_LessonRequests_GradeLevels]...';


GO
ALTER TABLE [dbo].[LessonRequests] WITH NOCHECK
    ADD CONSTRAINT [FK_LessonRequests_GradeLevels] FOREIGN KEY ([GradeLevelId]) REFERENCES [dbo].[GradeLevels] ([Id]);


GO
PRINT N'Creating Foreign Key [dbo].[FK_LessonRequests_Users_ParentId]...';


GO
ALTER TABLE [dbo].[LessonRequests] WITH NOCHECK
    ADD CONSTRAINT [FK_LessonRequests_Users_ParentId] FOREIGN KEY ([ParentId]) REFERENCES [dbo].[Users] ([Id]) ON DELETE CASCADE;


GO
PRINT N'Creating Foreign Key [dbo].[FK_LessonRequests_Lessons]...';


GO
ALTER TABLE [dbo].[LessonRequests] WITH NOCHECK
    ADD CONSTRAINT [FK_LessonRequests_Lessons] FOREIGN KEY ([LessonId]) REFERENCES [dbo].[Lessons] ([Id]) ON DELETE SET NULL;


GO
PRINT N'Creating Foreign Key [dbo].[FK_LessonRequests_Subjects]...';


GO
ALTER TABLE [dbo].[LessonRequests] WITH NOCHECK
    ADD CONSTRAINT [FK_LessonRequests_Subjects] FOREIGN KEY ([SubjectId]) REFERENCES [dbo].[Subjects] ([Id]);


GO
PRINT N'Creating Foreign Key [dbo].[FK_LessonRequestVotes_LessonRequests]...';


GO
ALTER TABLE [dbo].[LessonRequestVotes] WITH NOCHECK
    ADD CONSTRAINT [FK_LessonRequestVotes_LessonRequests] FOREIGN KEY ([RequestId]) REFERENCES [dbo].[LessonRequests] ([Id]) ON DELETE CASCADE;


GO
PRINT N'Creating Foreign Key [dbo].[FK_LessonRequestVotes_Users]...';


GO
ALTER TABLE [dbo].[LessonRequestVotes] WITH NOCHECK
    ADD CONSTRAINT [FK_LessonRequestVotes_Users] FOREIGN KEY ([ParentId]) REFERENCES [dbo].[Users] ([Id]);


GO
PRINT N'Creating Foreign Key [dbo].[FK_Lessons_Users_OwnerParentId]...';


GO
ALTER TABLE [dbo].[Lessons] WITH NOCHECK
    ADD CONSTRAINT [FK_Lessons_Users_OwnerParentId] FOREIGN KEY ([OwnerParentId]) REFERENCES [dbo].[Users] ([Id]);


GO
PRINT N'Creating Foreign Key [dbo].[FK_Lessons_Users_CreatedById]...';


GO
ALTER TABLE [dbo].[Lessons] WITH NOCHECK
    ADD CONSTRAINT [FK_Lessons_Users_CreatedById] FOREIGN KEY ([CreatedById]) REFERENCES [dbo].[Users] ([Id]);


GO
PRINT N'Creating Foreign Key [dbo].[FK_Lessons_GradeLevels_GradeLevelId]...';


GO
ALTER TABLE [dbo].[Lessons] WITH NOCHECK
    ADD CONSTRAINT [FK_Lessons_GradeLevels_GradeLevelId] FOREIGN KEY ([GradeLevelId]) REFERENCES [dbo].[GradeLevels] ([Id]);


GO
PRINT N'Creating Foreign Key [dbo].[FK_Lessons_Users_ApprovedById]...';


GO
ALTER TABLE [dbo].[Lessons] WITH NOCHECK
    ADD CONSTRAINT [FK_Lessons_Users_ApprovedById] FOREIGN KEY ([ApprovedById]) REFERENCES [dbo].[Users] ([Id]);


GO
PRINT N'Creating Foreign Key [dbo].[FK_Lessons_SubjectGrades_SubjectId_GradeLevelId]...';


GO
ALTER TABLE [dbo].[Lessons] WITH NOCHECK
    ADD CONSTRAINT [FK_Lessons_SubjectGrades_SubjectId_GradeLevelId] FOREIGN KEY ([SubjectId], [GradeLevelId]) REFERENCES [dbo].[SubjectGrades] ([SubjectId], [GradeLevelId]);


GO
PRINT N'Creating Foreign Key [dbo].[FK_Lessons_Subjects_SubjectId]...';


GO
ALTER TABLE [dbo].[Lessons] WITH NOCHECK
    ADD CONSTRAINT [FK_Lessons_Subjects_SubjectId] FOREIGN KEY ([SubjectId]) REFERENCES [dbo].[Subjects] ([Id]);


GO
PRINT N'Creating Foreign Key [dbo].[FK_LessonTags_Lessons_LessonId]...';


GO
ALTER TABLE [dbo].[LessonTags] WITH NOCHECK
    ADD CONSTRAINT [FK_LessonTags_Lessons_LessonId] FOREIGN KEY ([LessonId]) REFERENCES [dbo].[Lessons] ([Id]) ON DELETE CASCADE;


GO
PRINT N'Creating Foreign Key [dbo].[FK_LessonTags_Tags_TagId]...';


GO
ALTER TABLE [dbo].[LessonTags] WITH NOCHECK
    ADD CONSTRAINT [FK_LessonTags_Tags_TagId] FOREIGN KEY ([TagId]) REFERENCES [dbo].[Tags] ([Id]) ON DELETE CASCADE;


GO
PRINT N'Creating Foreign Key [dbo].[FK_LoginHistories_Users_UserId]...';


GO
ALTER TABLE [dbo].[LoginHistories] WITH NOCHECK
    ADD CONSTRAINT [FK_LoginHistories_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [dbo].[Users] ([Id]) ON DELETE SET NULL;


GO
PRINT N'Creating Foreign Key [dbo].[FK_Notifications_Users_UserId]...';


GO
ALTER TABLE [dbo].[Notifications] WITH NOCHECK
    ADD CONSTRAINT [FK_Notifications_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [dbo].[Users] ([Id]) ON DELETE CASCADE;


GO
PRINT N'Creating Foreign Key [dbo].[FK_ParentSources_Users_ParentId]...';


GO
ALTER TABLE [dbo].[ParentSources] WITH NOCHECK
    ADD CONSTRAINT [FK_ParentSources_Users_ParentId] FOREIGN KEY ([ParentId]) REFERENCES [dbo].[Users] ([Id]) ON DELETE CASCADE;


GO
PRINT N'Creating Foreign Key [dbo].[FK_ParentSources_ContentSources_SourceId]...';


GO
ALTER TABLE [dbo].[ParentSources] WITH NOCHECK
    ADD CONSTRAINT [FK_ParentSources_ContentSources_SourceId] FOREIGN KEY ([SourceId]) REFERENCES [dbo].[ContentSources] ([Id]);


GO
PRINT N'Creating Foreign Key [dbo].[FK_QuestionOptions_Questions_QuestionId]...';


GO
ALTER TABLE [dbo].[QuestionOptions] WITH NOCHECK
    ADD CONSTRAINT [FK_QuestionOptions_Questions_QuestionId] FOREIGN KEY ([QuestionId]) REFERENCES [dbo].[Questions] ([Id]) ON DELETE CASCADE;


GO
PRINT N'Creating Foreign Key [dbo].[FK_Questions_SourceVideos_SourceVideoId]...';


GO
ALTER TABLE [dbo].[Questions] WITH NOCHECK
    ADD CONSTRAINT [FK_Questions_SourceVideos_SourceVideoId] FOREIGN KEY ([SourceVideoId]) REFERENCES [dbo].[SourceVideos] ([Id]);


GO
PRINT N'Creating Foreign Key [dbo].[FK_Questions_Lessons_LessonId]...';


GO
ALTER TABLE [dbo].[Questions] WITH NOCHECK
    ADD CONSTRAINT [FK_Questions_Lessons_LessonId] FOREIGN KEY ([LessonId]) REFERENCES [dbo].[Lessons] ([Id]);


GO
PRINT N'Creating Foreign Key [dbo].[FK_Questions_Users_CreatedById]...';


GO
ALTER TABLE [dbo].[Questions] WITH NOCHECK
    ADD CONSTRAINT [FK_Questions_Users_CreatedById] FOREIGN KEY ([CreatedById]) REFERENCES [dbo].[Users] ([Id]);


GO
PRINT N'Creating Foreign Key [dbo].[FK_Questions_Users_ApprovedById]...';


GO
ALTER TABLE [dbo].[Questions] WITH NOCHECK
    ADD CONSTRAINT [FK_Questions_Users_ApprovedById] FOREIGN KEY ([ApprovedById]) REFERENCES [dbo].[Users] ([Id]);


GO
PRINT N'Creating Foreign Key [dbo].[FK_QuizAttemptAnswers_QuestionOptions_SelectedOptionId]...';


GO
ALTER TABLE [dbo].[QuizAttemptAnswers] WITH NOCHECK
    ADD CONSTRAINT [FK_QuizAttemptAnswers_QuestionOptions_SelectedOptionId] FOREIGN KEY ([SelectedOptionId]) REFERENCES [dbo].[QuestionOptions] ([Id]);


GO
PRINT N'Creating Foreign Key [dbo].[FK_QuizAttemptAnswers_Quizzes_QuizId]...';


GO
ALTER TABLE [dbo].[QuizAttemptAnswers] WITH NOCHECK
    ADD CONSTRAINT [FK_QuizAttemptAnswers_Quizzes_QuizId] FOREIGN KEY ([QuizId]) REFERENCES [dbo].[Quizzes] ([Id]);


GO
PRINT N'Creating Foreign Key [dbo].[FK_QuizAttemptAnswers_QuestionOptions_SelectedOptionId_QuestionId]...';


GO
ALTER TABLE [dbo].[QuizAttemptAnswers] WITH NOCHECK
    ADD CONSTRAINT [FK_QuizAttemptAnswers_QuestionOptions_SelectedOptionId_QuestionId] FOREIGN KEY ([SelectedOptionId], [QuestionId]) REFERENCES [dbo].[QuestionOptions] ([Id], [QuestionId]);


GO
PRINT N'Creating Foreign Key [dbo].[FK_QuizAttemptAnswers_QuizAttempts_AttemptId]...';


GO
ALTER TABLE [dbo].[QuizAttemptAnswers] WITH NOCHECK
    ADD CONSTRAINT [FK_QuizAttemptAnswers_QuizAttempts_AttemptId] FOREIGN KEY ([AttemptId]) REFERENCES [dbo].[QuizAttempts] ([Id]) ON DELETE CASCADE;


GO
PRINT N'Creating Foreign Key [dbo].[FK_QuizAttemptAnswers_QuizQuestions_QuizId_QuestionId]...';


GO
ALTER TABLE [dbo].[QuizAttemptAnswers] WITH NOCHECK
    ADD CONSTRAINT [FK_QuizAttemptAnswers_QuizQuestions_QuizId_QuestionId] FOREIGN KEY ([QuizId], [QuestionId]) REFERENCES [dbo].[QuizQuestions] ([QuizId], [QuestionId]);


GO
PRINT N'Creating Foreign Key [dbo].[FK_QuizAttemptAnswers_Questions_QuestionId]...';


GO
ALTER TABLE [dbo].[QuizAttemptAnswers] WITH NOCHECK
    ADD CONSTRAINT [FK_QuizAttemptAnswers_Questions_QuestionId] FOREIGN KEY ([QuestionId]) REFERENCES [dbo].[Questions] ([Id]);


GO
PRINT N'Creating Foreign Key [dbo].[FK_QuizAttemptAnswers_QuizAttempts_AttemptId_QuizId]...';


GO
ALTER TABLE [dbo].[QuizAttemptAnswers] WITH NOCHECK
    ADD CONSTRAINT [FK_QuizAttemptAnswers_QuizAttempts_AttemptId_QuizId] FOREIGN KEY ([AttemptId], [QuizId]) REFERENCES [dbo].[QuizAttempts] ([Id], [QuizId]);


GO
PRINT N'Creating Foreign Key [dbo].[FK_QuizAttempts_Assignments_AssignmentId]...';


GO
ALTER TABLE [dbo].[QuizAttempts] WITH NOCHECK
    ADD CONSTRAINT [FK_QuizAttempts_Assignments_AssignmentId] FOREIGN KEY ([AssignmentId]) REFERENCES [dbo].[Assignments] ([Id]);


GO
PRINT N'Creating Foreign Key [dbo].[FK_QuizAttempts_ChildProfiles_ChildProfileId]...';


GO
ALTER TABLE [dbo].[QuizAttempts] WITH NOCHECK
    ADD CONSTRAINT [FK_QuizAttempts_ChildProfiles_ChildProfileId] FOREIGN KEY ([ChildProfileId]) REFERENCES [dbo].[ChildProfiles] ([Id]) ON DELETE CASCADE;


GO
PRINT N'Creating Foreign Key [dbo].[FK_QuizAttempts_Assignments_AssignmentId_ChildProfileId_QuizId]...';


GO
ALTER TABLE [dbo].[QuizAttempts] WITH NOCHECK
    ADD CONSTRAINT [FK_QuizAttempts_Assignments_AssignmentId_ChildProfileId_QuizId] FOREIGN KEY ([AssignmentId], [ChildProfileId], [QuizId]) REFERENCES [dbo].[Assignments] ([Id], [ChildProfileId], [QuizId]);


GO
PRINT N'Creating Foreign Key [dbo].[FK_QuizAttempts_Quizzes_QuizId]...';


GO
ALTER TABLE [dbo].[QuizAttempts] WITH NOCHECK
    ADD CONSTRAINT [FK_QuizAttempts_Quizzes_QuizId] FOREIGN KEY ([QuizId]) REFERENCES [dbo].[Quizzes] ([Id]);


GO
PRINT N'Creating Foreign Key [dbo].[FK_QuizQuestions_Questions_QuestionId]...';


GO
ALTER TABLE [dbo].[QuizQuestions] WITH NOCHECK
    ADD CONSTRAINT [FK_QuizQuestions_Questions_QuestionId] FOREIGN KEY ([QuestionId]) REFERENCES [dbo].[Questions] ([Id]);


GO
PRINT N'Creating Foreign Key [dbo].[FK_QuizQuestions_Quizzes_QuizId]...';


GO
ALTER TABLE [dbo].[QuizQuestions] WITH NOCHECK
    ADD CONSTRAINT [FK_QuizQuestions_Quizzes_QuizId] FOREIGN KEY ([QuizId]) REFERENCES [dbo].[Quizzes] ([Id]) ON DELETE CASCADE;


GO
PRINT N'Creating Foreign Key [dbo].[FK_Quizzes_Lessons_LessonId]...';


GO
ALTER TABLE [dbo].[Quizzes] WITH NOCHECK
    ADD CONSTRAINT [FK_Quizzes_Lessons_LessonId] FOREIGN KEY ([LessonId]) REFERENCES [dbo].[Lessons] ([Id]);


GO
PRINT N'Creating Foreign Key [dbo].[FK_RoleClaims_Roles]...';


GO
ALTER TABLE [dbo].[RoleClaims] WITH NOCHECK
    ADD CONSTRAINT [FK_RoleClaims_Roles] FOREIGN KEY ([RoleId]) REFERENCES [dbo].[Roles] ([Id]) ON DELETE CASCADE;


GO
PRINT N'Creating Foreign Key [dbo].[FK_SearchLogs_ChildProfiles_ChildProfileId]...';


GO
ALTER TABLE [dbo].[SearchLogs] WITH NOCHECK
    ADD CONSTRAINT [FK_SearchLogs_ChildProfiles_ChildProfileId] FOREIGN KEY ([ChildProfileId]) REFERENCES [dbo].[ChildProfiles] ([Id]) ON DELETE CASCADE;


GO
PRINT N'Creating Foreign Key [dbo].[FK_SourceVideos_Users_ReviewedById]...';


GO
ALTER TABLE [dbo].[SourceVideos] WITH NOCHECK
    ADD CONSTRAINT [FK_SourceVideos_Users_ReviewedById] FOREIGN KEY ([ReviewedById]) REFERENCES [dbo].[Users] ([Id]);


GO
PRINT N'Creating Foreign Key [dbo].[FK_SourceVideoSources_ContentSources_SourceId]...';


GO
ALTER TABLE [dbo].[SourceVideoSources] WITH NOCHECK
    ADD CONSTRAINT [FK_SourceVideoSources_ContentSources_SourceId] FOREIGN KEY ([SourceId]) REFERENCES [dbo].[ContentSources] ([Id]) ON DELETE CASCADE;


GO
PRINT N'Creating Foreign Key [dbo].[FK_SourceVideoSources_SourceVideos_SourceVideoId]...';


GO
ALTER TABLE [dbo].[SourceVideoSources] WITH NOCHECK
    ADD CONSTRAINT [FK_SourceVideoSources_SourceVideos_SourceVideoId] FOREIGN KEY ([SourceVideoId]) REFERENCES [dbo].[SourceVideos] ([Id]) ON DELETE CASCADE;


GO
PRINT N'Creating Foreign Key [dbo].[FK_Streaks_ChildProfiles_ChildProfileId]...';


GO
ALTER TABLE [dbo].[Streaks] WITH NOCHECK
    ADD CONSTRAINT [FK_Streaks_ChildProfiles_ChildProfileId] FOREIGN KEY ([ChildProfileId]) REFERENCES [dbo].[ChildProfiles] ([Id]) ON DELETE CASCADE;


GO
PRINT N'Creating Foreign Key [dbo].[FK_SubjectGrades_Subjects_SubjectId]...';


GO
ALTER TABLE [dbo].[SubjectGrades] WITH NOCHECK
    ADD CONSTRAINT [FK_SubjectGrades_Subjects_SubjectId] FOREIGN KEY ([SubjectId]) REFERENCES [dbo].[Subjects] ([Id]) ON DELETE CASCADE;


GO
PRINT N'Creating Foreign Key [dbo].[FK_SubjectGrades_GradeLevels_GradeLevelId]...';


GO
ALTER TABLE [dbo].[SubjectGrades] WITH NOCHECK
    ADD CONSTRAINT [FK_SubjectGrades_GradeLevels_GradeLevelId] FOREIGN KEY ([GradeLevelId]) REFERENCES [dbo].[GradeLevels] ([Id]);


GO
PRINT N'Creating Foreign Key [dbo].[FK_SubjectProgresses_ChildProfiles_ChildProfileId]...';


GO
ALTER TABLE [dbo].[SubjectProgresses] WITH NOCHECK
    ADD CONSTRAINT [FK_SubjectProgresses_ChildProfiles_ChildProfileId] FOREIGN KEY ([ChildProfileId]) REFERENCES [dbo].[ChildProfiles] ([Id]) ON DELETE CASCADE;


GO
PRINT N'Creating Foreign Key [dbo].[FK_SubjectProgresses_Subjects_SubjectId]...';


GO
ALTER TABLE [dbo].[SubjectProgresses] WITH NOCHECK
    ADD CONSTRAINT [FK_SubjectProgresses_Subjects_SubjectId] FOREIGN KEY ([SubjectId]) REFERENCES [dbo].[Subjects] ([Id]);


GO
PRINT N'Creating Foreign Key [dbo].[FK_TimeExtensionRequests_ChildProfiles_ChildProfileId]...';


GO
ALTER TABLE [dbo].[TimeExtensionRequests] WITH NOCHECK
    ADD CONSTRAINT [FK_TimeExtensionRequests_ChildProfiles_ChildProfileId] FOREIGN KEY ([ChildProfileId]) REFERENCES [dbo].[ChildProfiles] ([Id]) ON DELETE CASCADE;


GO
PRINT N'Creating Foreign Key [dbo].[FK_TimeExtensionRequests_Users_DecidedById]...';


GO
ALTER TABLE [dbo].[TimeExtensionRequests] WITH NOCHECK
    ADD CONSTRAINT [FK_TimeExtensionRequests_Users_DecidedById] FOREIGN KEY ([DecidedById]) REFERENCES [dbo].[Users] ([Id]);


GO
PRINT N'Creating Foreign Key [dbo].[FK_TimeLimitOverrides_TimeLimitPolicies_PolicyId]...';


GO
ALTER TABLE [dbo].[TimeLimitOverrides] WITH NOCHECK
    ADD CONSTRAINT [FK_TimeLimitOverrides_TimeLimitPolicies_PolicyId] FOREIGN KEY ([PolicyId]) REFERENCES [dbo].[TimeLimitPolicies] ([Id]) ON DELETE CASCADE;


GO
PRINT N'Creating Foreign Key [dbo].[FK_TimeLimitPolicies_Users_UpdatedById]...';


GO
ALTER TABLE [dbo].[TimeLimitPolicies] WITH NOCHECK
    ADD CONSTRAINT [FK_TimeLimitPolicies_Users_UpdatedById] FOREIGN KEY ([UpdatedById]) REFERENCES [dbo].[Users] ([Id]);


GO
PRINT N'Creating Foreign Key [dbo].[FK_TimeLimitPolicies_ChildProfiles_ChildProfileId]...';


GO
ALTER TABLE [dbo].[TimeLimitPolicies] WITH NOCHECK
    ADD CONSTRAINT [FK_TimeLimitPolicies_ChildProfiles_ChildProfileId] FOREIGN KEY ([ChildProfileId]) REFERENCES [dbo].[ChildProfiles] ([Id]) ON DELETE CASCADE;


GO
PRINT N'Creating Foreign Key [dbo].[FK_UserClaims_Users]...';


GO
ALTER TABLE [dbo].[UserClaims] WITH NOCHECK
    ADD CONSTRAINT [FK_UserClaims_Users] FOREIGN KEY ([UserId]) REFERENCES [dbo].[Users] ([Id]) ON DELETE CASCADE;


GO
PRINT N'Creating Foreign Key [dbo].[FK_UserConsents_Users_UserId]...';


GO
ALTER TABLE [dbo].[UserConsents] WITH NOCHECK
    ADD CONSTRAINT [FK_UserConsents_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [dbo].[Users] ([Id]) ON DELETE CASCADE;


GO
PRINT N'Creating Foreign Key [dbo].[FK_UserLogins_Users]...';


GO
ALTER TABLE [dbo].[UserLogins] WITH NOCHECK
    ADD CONSTRAINT [FK_UserLogins_Users] FOREIGN KEY ([UserId]) REFERENCES [dbo].[Users] ([Id]) ON DELETE CASCADE;


GO
PRINT N'Creating Foreign Key [dbo].[FK_UserRoles_Users]...';


GO
ALTER TABLE [dbo].[UserRoles] WITH NOCHECK
    ADD CONSTRAINT [FK_UserRoles_Users] FOREIGN KEY ([UserId]) REFERENCES [dbo].[Users] ([Id]) ON DELETE CASCADE;


GO
PRINT N'Creating Foreign Key [dbo].[FK_UserRoles_Roles]...';


GO
ALTER TABLE [dbo].[UserRoles] WITH NOCHECK
    ADD CONSTRAINT [FK_UserRoles_Roles] FOREIGN KEY ([RoleId]) REFERENCES [dbo].[Roles] ([Id]) ON DELETE CASCADE;


GO
PRINT N'Creating Foreign Key [dbo].[FK_UserSettings_Users_UserId]...';


GO
ALTER TABLE [dbo].[UserSettings] WITH NOCHECK
    ADD CONSTRAINT [FK_UserSettings_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [dbo].[Users] ([Id]) ON DELETE CASCADE;


GO
PRINT N'Creating Foreign Key [dbo].[FK_UserTokens_Users]...';


GO
ALTER TABLE [dbo].[UserTokens] WITH NOCHECK
    ADD CONSTRAINT [FK_UserTokens_Users] FOREIGN KEY ([UserId]) REFERENCES [dbo].[Users] ([Id]) ON DELETE CASCADE;


GO
PRINT N'Creating Foreign Key [dbo].[FK_WeeklyReports_ChildProfiles_ChildProfileId]...';


GO
ALTER TABLE [dbo].[WeeklyReports] WITH NOCHECK
    ADD CONSTRAINT [FK_WeeklyReports_ChildProfiles_ChildProfileId] FOREIGN KEY ([ChildProfileId]) REFERENCES [dbo].[ChildProfiles] ([Id]) ON DELETE CASCADE;


GO
PRINT N'Creating Check Constraint [dbo].[CK_Assignments_3]...';


GO
ALTER TABLE [dbo].[Assignments] WITH NOCHECK
    ADD CONSTRAINT [CK_Assignments_3] CHECK ([Status]='Cancelled' OR [Status]='Completed' OR [Status]='InProgress' OR [Status]='Assigned');


GO
PRINT N'Creating Check Constraint [dbo].[CK_Assignments_1]...';


GO
ALTER TABLE [dbo].[Assignments] WITH NOCHECK
    ADD CONSTRAINT [CK_Assignments_1] CHECK ([LessonId] IS NOT NULL AND [QuizId] IS NULL OR [LessonId] IS NULL AND [QuizId] IS NOT NULL);


GO
PRINT N'Creating Check Constraint [dbo].[CK_Assignments_2]...';


GO
ALTER TABLE [dbo].[Assignments] WITH NOCHECK
    ADD CONSTRAINT [CK_Assignments_2] CHECK ([CompletedAt] IS NULL OR [CompletedAt]>=[AssignedAt]);


GO
PRINT N'Creating Check Constraint [dbo].[CK_Badges_1]...';


GO
ALTER TABLE [dbo].[Badges] WITH NOCHECK
    ADD CONSTRAINT [CK_Badges_1] CHECK ([Threshold]>=(0));


GO
PRINT N'Creating Check Constraint [dbo].[CK_Badges_2]...';


GO
ALTER TABLE [dbo].[Badges] WITH NOCHECK
    ADD CONSTRAINT [CK_Badges_2] CHECK ([CriteriaType]='MinutesTotal' OR [CriteriaType]='StreakDays' OR [CriteriaType]='QuizPerfect' OR [CriteriaType]='LessonsCompleted');


GO
PRINT N'Creating Check Constraint [dbo].[CK_ChildPaths_1]...';


GO
ALTER TABLE [dbo].[ChildPaths] WITH NOCHECK
    ADD CONSTRAINT [CK_ChildPaths_1] CHECK ([Status]='Completed' OR [Status]='InProgress' OR [Status]='NotStarted');


GO
PRINT N'Creating Check Constraint [dbo].[CK_ChildProfiles_2]...';


GO
ALTER TABLE [dbo].[ChildProfiles] WITH NOCHECK
    ADD CONSTRAINT [CK_ChildProfiles_2] CHECK ([PinFailedAttempts]>=(0));


GO
PRINT N'Creating Check Constraint [dbo].[CK_ChildProfiles_1]...';


GO
ALTER TABLE [dbo].[ChildProfiles] WITH NOCHECK
    ADD CONSTRAINT [CK_ChildProfiles_1] CHECK ([BirthYear] IS NULL OR [BirthYear]>=(1900) AND [BirthYear]<=(2100));


GO
PRINT N'Creating Check Constraint [dbo].[CK_ContentReports_2]...';


GO
ALTER TABLE [dbo].[ContentReports] WITH NOCHECK
    ADD CONSTRAINT [CK_ContentReports_2] CHECK ([LessonId] IS NOT NULL OR [SourceVideoId] IS NOT NULL);


GO
PRINT N'Creating Check Constraint [dbo].[CK_ContentReports_4]...';


GO
ALTER TABLE [dbo].[ContentReports] WITH NOCHECK
    ADD CONSTRAINT [CK_ContentReports_4] CHECK ([Status]='Dismissed' OR [Status]='Resolved' OR [Status]='InReview' OR [Status]='Open');


GO
PRINT N'Creating Check Constraint [dbo].[CK_ContentReports_1]...';


GO
ALTER TABLE [dbo].[ContentReports] WITH NOCHECK
    ADD CONSTRAINT [CK_ContentReports_1] CHECK ([ChildProfileId] IS NOT NULL OR [ReportedById] IS NOT NULL);


GO
PRINT N'Creating Check Constraint [dbo].[CK_ContentReports_3]...';


GO
ALTER TABLE [dbo].[ContentReports] WITH NOCHECK
    ADD CONSTRAINT [CK_ContentReports_3] CHECK ([Reason]='Other' OR [Reason]='Ads' OR [Reason]='Wrong' OR [Reason]='Scary' OR [Reason]='Inappropriate');


GO
PRINT N'Creating Check Constraint [dbo].[CK_ContentSources_1]...';


GO
ALTER TABLE [dbo].[ContentSources] WITH NOCHECK
    ADD CONSTRAINT [CK_ContentSources_1] CHECK ([Platform]='Website' OR [Platform]='YouTubePlaylist' OR [Platform]='YouTubeChannel');


GO
PRINT N'Creating Check Constraint [dbo].[CK_ContentSources_2]...';


GO
ALTER TABLE [dbo].[ContentSources] WITH NOCHECK
    ADD CONSTRAINT [CK_ContentSources_2] CHECK ([Status]='Suspended' OR [Status]='Rejected' OR [Status]='Approved' OR [Status]='Suggested');


GO
PRINT N'Creating Check Constraint [dbo].[CK_CurationSuggestions_2]...';


GO
ALTER TABLE [dbo].[CurationSuggestions] WITH NOCHECK
    ADD CONSTRAINT [CK_CurationSuggestions_2] CHECK ([Status]='Dismissed' OR [Status]='Accepted' OR [Status]='Pending');


GO
PRINT N'Creating Check Constraint [dbo].[CK_CurationSuggestions_1]...';


GO
ALTER TABLE [dbo].[CurationSuggestions] WITH NOCHECK
    ADD CONSTRAINT [CK_CurationSuggestions_1] CHECK ([Score]>=(0) AND [Score]<=(100));


GO
PRINT N'Creating Check Constraint [dbo].[CK_DailyUsages_1]...';


GO
ALTER TABLE [dbo].[DailyUsages] WITH NOCHECK
    ADD CONSTRAINT [CK_DailyUsages_1] CHECK ([Seconds]>=(0));


GO
PRINT N'Creating Check Constraint [dbo].[CK_DataRequests_2]...';


GO
ALTER TABLE [dbo].[DataRequests] WITH NOCHECK
    ADD CONSTRAINT [CK_DataRequests_2] CHECK ([Status]='Rejected' OR [Status]='Done' OR [Status]='Processing' OR [Status]='Pending');


GO
PRINT N'Creating Check Constraint [dbo].[CK_DataRequests_1]...';


GO
ALTER TABLE [dbo].[DataRequests] WITH NOCHECK
    ADD CONSTRAINT [CK_DataRequests_1] CHECK ([RequestType]='Delete' OR [RequestType]='Export');


GO
PRINT N'Creating Check Constraint [dbo].[CK_LearningGoals_2]...';


GO
ALTER TABLE [dbo].[LearningGoals] WITH NOCHECK
    ADD CONSTRAINT [CK_LearningGoals_2] CHECK ([TargetLessons] IS NOT NULL OR [TargetMinutes] IS NOT NULL);


GO
PRINT N'Creating Check Constraint [dbo].[CK_LearningGoals_1]...';


GO
ALTER TABLE [dbo].[LearningGoals] WITH NOCHECK
    ADD CONSTRAINT [CK_LearningGoals_1] CHECK (([TargetLessons] IS NULL OR [TargetLessons]>=(0)) AND ([TargetMinutes] IS NULL OR [TargetMinutes]>=(0)));


GO
PRINT N'Creating Check Constraint [dbo].[CK_LearningSessions_2]...';


GO
ALTER TABLE [dbo].[LearningSessions] WITH NOCHECK
    ADD CONSTRAINT [CK_LearningSessions_2] CHECK ([LastHeartbeatAt]>=[StartedAt] AND ([EndedAt] IS NULL OR [EndedAt]>=[StartedAt]));


GO
PRINT N'Creating Check Constraint [dbo].[CK_LearningSessions_3]...';


GO
ALTER TABLE [dbo].[LearningSessions] WITH NOCHECK
    ADD CONSTRAINT [CK_LearningSessions_3] CHECK ([EndReason]='ParentPin' OR [EndReason]='Timeout' OR [EndReason]='TimeUp' OR [EndReason]='Exit');


GO
PRINT N'Creating Check Constraint [dbo].[CK_LearningSessions_1]...';


GO
ALTER TABLE [dbo].[LearningSessions] WITH NOCHECK
    ADD CONSTRAINT [CK_LearningSessions_1] CHECK ([ActiveSeconds]>=(0));


GO
PRINT N'Creating Check Constraint [dbo].[CK_LessonActivities_1]...';


GO
ALTER TABLE [dbo].[LessonActivities] WITH NOCHECK
    ADD CONSTRAINT [CK_LessonActivities_1] CHECK ([ProgressPercent]>=(0) AND [ProgressPercent]<=(100));


GO
PRINT N'Creating Check Constraint [dbo].[CK_LessonActivities_3]...';


GO
ALTER TABLE [dbo].[LessonActivities] WITH NOCHECK
    ADD CONSTRAINT [CK_LessonActivities_3] CHECK ([Status]='Completed' OR [Status]='Started');


GO
PRINT N'Creating Check Constraint [dbo].[CK_LessonActivities_2]...';


GO
ALTER TABLE [dbo].[LessonActivities] WITH NOCHECK
    ADD CONSTRAINT [CK_LessonActivities_2] CHECK ([LastPositionSec]>=(0) AND [WatchSeconds]>=(0));


GO
PRINT N'Creating Check Constraint [dbo].[CK_LessonContents_2]...';


GO
ALTER TABLE [dbo].[LessonContents] WITH NOCHECK
    ADD CONSTRAINT [CK_LessonContents_2] CHECK ([ContentKind]='Document' OR [ContentKind]='Image' OR [ContentKind]='Reading' OR [ContentKind]='Video');


GO
PRINT N'Creating Check Constraint [dbo].[CK_LessonContents_1]...';


GO
ALTER TABLE [dbo].[LessonContents] WITH NOCHECK
    ADD CONSTRAINT [CK_LessonContents_1] CHECK ([ContentKind]='Video' AND [SourceVideoId] IS NOT NULL OR [ContentKind]='Reading' AND [Body] IS NOT NULL OR ([ContentKind]='Document' OR [ContentKind]='Image') AND [FileUrl] IS NOT NULL);


GO
PRINT N'Creating Check Constraint [dbo].[CK_LessonRequests_Status]...';


GO
ALTER TABLE [dbo].[LessonRequests] WITH NOCHECK
    ADD CONSTRAINT [CK_LessonRequests_Status] CHECK ([Status]=N'Declined' OR [Status]=N'Done' OR [Status]=N'Planned' OR [Status]=N'Open');


GO
PRINT N'Creating Check Constraint [dbo].[CK_Lessons_3]...';


GO
ALTER TABLE [dbo].[Lessons] WITH NOCHECK
    ADD CONSTRAINT [CK_Lessons_3] CHECK ([Status]='Archived' OR [Status]='Published' OR [Status]='InReview' OR [Status]='Draft');


GO
PRINT N'Creating Check Constraint [dbo].[CK_Lessons_ShareStatus]...';


GO
ALTER TABLE [dbo].[Lessons] WITH NOCHECK
    ADD CONSTRAINT [CK_Lessons_ShareStatus] CHECK ([ShareStatus] IS NULL OR [OwnerParentId] IS NOT NULL AND ([ShareStatus]=N'Rejected' OR [ShareStatus]=N'Approved' OR [ShareStatus]=N'Pending'));


GO
PRINT N'Creating Check Constraint [dbo].[CK_Lessons_1]...';


GO
ALTER TABLE [dbo].[Lessons] WITH NOCHECK
    ADD CONSTRAINT [CK_Lessons_1] CHECK ([DurationMin]>=(0));


GO
PRINT N'Creating Check Constraint [dbo].[CK_Lessons_2]...';


GO
ALTER TABLE [dbo].[Lessons] WITH NOCHECK
    ADD CONSTRAINT [CK_Lessons_2] CHECK ([LessonType]='Mixed' OR [LessonType]='Reading' OR [LessonType]='Video');


GO
PRINT N'Creating Check Constraint [dbo].[CK_Notifications_1]...';


GO
ALTER TABLE [dbo].[Notifications] WITH NOCHECK
    ADD CONSTRAINT [CK_Notifications_1] CHECK ([NotificationType]='System' OR [NotificationType]='Security' OR [NotificationType]='Badge' OR [NotificationType]='Assignment' OR [NotificationType]='ContentReport' OR [NotificationType]='WeeklyReport' OR [NotificationType]='TimeRequest');


GO
PRINT N'Creating Check Constraint [dbo].[CK_ParentSources_1]...';


GO
ALTER TABLE [dbo].[ParentSources] WITH NOCHECK
    ADD CONSTRAINT [CK_ParentSources_1] CHECK ([Status]='Blocked' OR [Status]='Allowed');


GO
PRINT N'Creating Check Constraint [dbo].[CK_Questions_2]...';


GO
ALTER TABLE [dbo].[Questions] WITH NOCHECK
    ADD CONSTRAINT [CK_Questions_2] CHECK ([QuestionType]='MultiChoice' OR [QuestionType]='TrueFalse' OR [QuestionType]='SingleChoice');


GO
PRINT N'Creating Check Constraint [dbo].[CK_Questions_3]...';


GO
ALTER TABLE [dbo].[Questions] WITH NOCHECK
    ADD CONSTRAINT [CK_Questions_3] CHECK ([Origin]='AiSuggested' OR [Origin]='Manual');


GO
PRINT N'Creating Check Constraint [dbo].[CK_Questions_4]...';


GO
ALTER TABLE [dbo].[Questions] WITH NOCHECK
    ADD CONSTRAINT [CK_Questions_4] CHECK ([Status]='Archived' OR [Status]='Published' OR [Status]='InReview' OR [Status]='Draft');


GO
PRINT N'Creating Check Constraint [dbo].[CK_Questions_1]...';


GO
ALTER TABLE [dbo].[Questions] WITH NOCHECK
    ADD CONSTRAINT [CK_Questions_1] CHECK ([Difficulty]>=(1) AND [Difficulty]<=(3));


GO
PRINT N'Creating Check Constraint [dbo].[CK_QuizAttempts_1]...';


GO
ALTER TABLE [dbo].[QuizAttempts] WITH NOCHECK
    ADD CONSTRAINT [CK_QuizAttempts_1] CHECK ([ScorePercent] IS NULL OR [ScorePercent]>=(0) AND [ScorePercent]<=(100));


GO
PRINT N'Creating Check Constraint [dbo].[CK_QuizAttempts_2]...';


GO
ALTER TABLE [dbo].[QuizAttempts] WITH NOCHECK
    ADD CONSTRAINT [CK_QuizAttempts_2] CHECK ([CorrectCount]>=(0) AND [TotalCount]>=(0) AND [CorrectCount]<=[TotalCount]);


GO
PRINT N'Creating Check Constraint [dbo].[CK_QuizQuestions_1]...';


GO
ALTER TABLE [dbo].[QuizQuestions] WITH NOCHECK
    ADD CONSTRAINT [CK_QuizQuestions_1] CHECK ([Points]>(0));


GO
PRINT N'Creating Check Constraint [dbo].[CK_Quizzes_1]...';


GO
ALTER TABLE [dbo].[Quizzes] WITH NOCHECK
    ADD CONSTRAINT [CK_Quizzes_1] CHECK ([PassPercent]>=(0) AND [PassPercent]<=(100));


GO
PRINT N'Creating Check Constraint [dbo].[CK_Quizzes_2]...';


GO
ALTER TABLE [dbo].[Quizzes] WITH NOCHECK
    ADD CONSTRAINT [CK_Quizzes_2] CHECK ([TimeLimitSec] IS NULL OR [TimeLimitSec]>(0));


GO
PRINT N'Creating Check Constraint [dbo].[CK_Quizzes_3]...';


GO
ALTER TABLE [dbo].[Quizzes] WITH NOCHECK
    ADD CONSTRAINT [CK_Quizzes_3] CHECK ([Status]='Archived' OR [Status]='Published' OR [Status]='InReview' OR [Status]='Draft');


GO
PRINT N'Creating Check Constraint [dbo].[CK_SearchLogs_1]...';


GO
ALTER TABLE [dbo].[SearchLogs] WITH NOCHECK
    ADD CONSTRAINT [CK_SearchLogs_1] CHECK ([ResultCount]>=(0));


GO
PRINT N'Creating Check Constraint [dbo].[CK_SourceVideos_2]...';


GO
ALTER TABLE [dbo].[SourceVideos] WITH NOCHECK
    ADD CONSTRAINT [CK_SourceVideos_2] CHECK ([Status]='Blocked' OR [Status]='Approved' OR [Status]='Pending');


GO
PRINT N'Creating Check Constraint [dbo].[CK_SourceVideos_1]...';


GO
ALTER TABLE [dbo].[SourceVideos] WITH NOCHECK
    ADD CONSTRAINT [CK_SourceVideos_1] CHECK ([DurationSec] IS NULL OR [DurationSec]>=(0));


GO
PRINT N'Creating Check Constraint [dbo].[CK_Streaks_1]...';


GO
ALTER TABLE [dbo].[Streaks] WITH NOCHECK
    ADD CONSTRAINT [CK_Streaks_1] CHECK ([CurrentDays]>=(0) AND [LongestDays]>=[CurrentDays]);


GO
PRINT N'Creating Check Constraint [dbo].[CK_SubjectProgresses_2]...';


GO
ALTER TABLE [dbo].[SubjectProgresses] WITH NOCHECK
    ADD CONSTRAINT [CK_SubjectProgresses_2] CHECK ([AvgQuizPercent] IS NULL OR [AvgQuizPercent]>=(0) AND [AvgQuizPercent]<=(100));


GO
PRINT N'Creating Check Constraint [dbo].[CK_SubjectProgresses_1]...';


GO
ALTER TABLE [dbo].[SubjectProgresses] WITH NOCHECK
    ADD CONSTRAINT [CK_SubjectProgresses_1] CHECK ([CompletedLessons]>=(0) AND [TotalLessons]>=(0) AND [CompletedLessons]<=[TotalLessons]);


GO
PRINT N'Creating Check Constraint [dbo].[CK_SystemMetricsDaily_1]...';


GO
ALTER TABLE [dbo].[SystemMetricsDaily] WITH NOCHECK
    ADD CONSTRAINT [CK_SystemMetricsDaily_1] CHECK ([ActiveParents]>=(0) AND [ActiveChildren]>=(0) AND [TotalMinutes]>=(0) AND [LessonsCompleted]>=(0) AND [OpenReports]>=(0) AND [NewSources]>=(0));


GO
PRINT N'Creating Check Constraint [dbo].[CK_TimeExtensionRequests_1]...';


GO
ALTER TABLE [dbo].[TimeExtensionRequests] WITH NOCHECK
    ADD CONSTRAINT [CK_TimeExtensionRequests_1] CHECK ([RequestedMinutes]>=(1) AND [RequestedMinutes]<=(1440));


GO
PRINT N'Creating Check Constraint [dbo].[CK_TimeExtensionRequests_3]...';


GO
ALTER TABLE [dbo].[TimeExtensionRequests] WITH NOCHECK
    ADD CONSTRAINT [CK_TimeExtensionRequests_3] CHECK ([Status]='Expired' OR [Status]='Denied' OR [Status]='Approved' OR [Status]='Pending');


GO
PRINT N'Creating Check Constraint [dbo].[CK_TimeExtensionRequests_2]...';


GO
ALTER TABLE [dbo].[TimeExtensionRequests] WITH NOCHECK
    ADD CONSTRAINT [CK_TimeExtensionRequests_2] CHECK ([GrantedMinutes] IS NULL OR [GrantedMinutes]>=(0) AND [GrantedMinutes]<=[RequestedMinutes]);


GO
PRINT N'Creating Check Constraint [dbo].[CK_TimeLimitOverrides_1]...';


GO
ALTER TABLE [dbo].[TimeLimitOverrides] WITH NOCHECK
    ADD CONSTRAINT [CK_TimeLimitOverrides_1] CHECK ([Weekday]>=(1) AND [Weekday]<=(7));


GO
PRINT N'Creating Check Constraint [dbo].[CK_TimeLimitOverrides_2]...';


GO
ALTER TABLE [dbo].[TimeLimitOverrides] WITH NOCHECK
    ADD CONSTRAINT [CK_TimeLimitOverrides_2] CHECK ([Minutes]>=(0) AND [Minutes]<=(1440));


GO
PRINT N'Creating Check Constraint [dbo].[CK_TimeLimitPolicies_1]...';


GO
ALTER TABLE [dbo].[TimeLimitPolicies] WITH NOCHECK
    ADD CONSTRAINT [CK_TimeLimitPolicies_1] CHECK ([DailyMinutes]>=(0) AND [DailyMinutes]<=(1440));


GO
PRINT N'Creating Check Constraint [dbo].[CK_TimeLimitPolicies_3]...';


GO
ALTER TABLE [dbo].[TimeLimitPolicies] WITH NOCHECK
    ADD CONSTRAINT [CK_TimeLimitPolicies_3] CHECK ([StudyStart] IS NULL AND [StudyEnd] IS NULL OR [StudyStart] IS NOT NULL AND [StudyEnd] IS NOT NULL);


GO
PRINT N'Creating Check Constraint [dbo].[CK_TimeLimitPolicies_2]...';


GO
ALTER TABLE [dbo].[TimeLimitPolicies] WITH NOCHECK
    ADD CONSTRAINT [CK_TimeLimitPolicies_2] CHECK ([BreakEveryMinutes] IS NULL OR [BreakEveryMinutes]>=(1) AND [BreakEveryMinutes]<=(1440));


GO
PRINT N'Creating Check Constraint [dbo].[CK_UserConsents_2]...';


GO
ALTER TABLE [dbo].[UserConsents] WITH NOCHECK
    ADD CONSTRAINT [CK_UserConsents_2] CHECK ([ConsentType]='ChildDataPolicy' OR [ConsentType]='Terms');


GO
PRINT N'Creating Check Constraint [dbo].[CK_UserConsents_1]...';


GO
ALTER TABLE [dbo].[UserConsents] WITH NOCHECK
    ADD CONSTRAINT [CK_UserConsents_1] CHECK ([Decision]=N'Revoked' OR [Decision]=N'Declined' OR [Decision]=N'Accepted');


GO
PRINT N'Creating Check Constraint [dbo].[CK_UserConsents_Dates]...';


GO
ALTER TABLE [dbo].[UserConsents] WITH NOCHECK
    ADD CONSTRAINT [CK_UserConsents_Dates] CHECK ([Decision]=N'Accepted' AND [AcceptedAt] IS NOT NULL AND [RevokedAt] IS NULL OR [Decision]=N'Declined' AND [AcceptedAt] IS NULL AND [RevokedAt] IS NULL OR [Decision]=N'Revoked' AND [AcceptedAt] IS NOT NULL AND [RevokedAt] IS NOT NULL);


GO
PRINT N'Creating Check Constraint [dbo].[CK_Users_FailedCount]...';


GO
ALTER TABLE [dbo].[Users] WITH NOCHECK
    ADD CONSTRAINT [CK_Users_FailedCount] CHECK ([AccessFailedCount]>=(0));


GO
PRINT N'Creating Check Constraint [dbo].[CK_UserSettings_1]...';


GO
ALTER TABLE [dbo].[UserSettings] WITH NOCHECK
    ADD CONSTRAINT [CK_UserSettings_1] CHECK ([Theme]='Auto' OR [Theme]='Dark' OR [Theme]='Light');


GO
PRINT N'Creating Check Constraint [dbo].[CK_WeeklyReports_1]...';


GO
ALTER TABLE [dbo].[WeeklyReports] WITH NOCHECK
    ADD CONSTRAINT [CK_WeeklyReports_1] CHECK ([TotalMinutes]>=(0) AND [LessonsCompleted]>=(0) AND [QuizAttempts]>=(0));


GO
PRINT N'Creating Check Constraint [dbo].[CK_WeeklyReports_2]...';


GO
ALTER TABLE [dbo].[WeeklyReports] WITH NOCHECK
    ADD CONSTRAINT [CK_WeeklyReports_2] CHECK ([AvgQuizPercent] IS NULL OR [AvgQuizPercent]>=(0) AND [AvgQuizPercent]<=(100));


GO
PRINT N'Checking existing data against newly created constraints';


GO



GO
ALTER TABLE [dbo].[Assignments] WITH CHECK CHECK CONSTRAINT [FK_Assignments_Lessons_LessonId];

ALTER TABLE [dbo].[Assignments] WITH CHECK CHECK CONSTRAINT [FK_Assignments_Users_AssignedById];

ALTER TABLE [dbo].[Assignments] WITH CHECK CHECK CONSTRAINT [FK_Assignments_ChildProfiles_ChildProfileId];

ALTER TABLE [dbo].[Assignments] WITH CHECK CHECK CONSTRAINT [FK_Assignments_Quizzes_QuizId];

ALTER TABLE [dbo].[AuditLogs] WITH CHECK CHECK CONSTRAINT [FK_AuditLogs_Users_ActorId];

ALTER TABLE [dbo].[BlockedKeywords] WITH CHECK CHECK CONSTRAINT [FK_BlockedKeywords_Users_CreatedById];

ALTER TABLE [dbo].[BlockedKeywords] WITH CHECK CHECK CONSTRAINT [FK_BlockedKeywords_Users_ParentId];

ALTER TABLE [dbo].[ChildBadges] WITH CHECK CHECK CONSTRAINT [FK_ChildBadges_Badges_BadgeId];

ALTER TABLE [dbo].[ChildBadges] WITH CHECK CHECK CONSTRAINT [FK_ChildBadges_ChildProfiles_ChildProfileId];

ALTER TABLE [dbo].[ChildPaths] WITH CHECK CHECK CONSTRAINT [FK_ChildPaths_LearningPaths_PathId];

ALTER TABLE [dbo].[ChildPaths] WITH CHECK CHECK CONSTRAINT [FK_ChildPaths_ChildProfiles_ChildProfileId];

ALTER TABLE [dbo].[ChildPaths] WITH CHECK CHECK CONSTRAINT [FK_ChildPaths_LearningPathItems_CurrentItemId];

ALTER TABLE [dbo].[ChildPaths] WITH CHECK CHECK CONSTRAINT [FK_ChildPaths_LearningPathItems_CurrentItemId_PathId];

ALTER TABLE [dbo].[ChildProfiles] WITH CHECK CHECK CONSTRAINT [FK_ChildProfiles_GradeLevels_GradeLevelId];

ALTER TABLE [dbo].[ChildProfiles] WITH CHECK CHECK CONSTRAINT [FK_ChildProfiles_Users_ParentId];

ALTER TABLE [dbo].[ChildProfiles] WITH CHECK CHECK CONSTRAINT [FK_ChildProfiles_Users_UserId];

ALTER TABLE [dbo].[ChildSubjects] WITH CHECK CHECK CONSTRAINT [FK_ChildSubjects_ChildProfiles_ChildProfileId];

ALTER TABLE [dbo].[ChildSubjects] WITH CHECK CHECK CONSTRAINT [FK_ChildSubjects_Subjects_SubjectId];

ALTER TABLE [dbo].[ContentReports] WITH CHECK CHECK CONSTRAINT [FK_ContentReports_ChildProfiles_ChildProfileId];

ALTER TABLE [dbo].[ContentReports] WITH CHECK CHECK CONSTRAINT [FK_ContentReports_Users_ReportedById];

ALTER TABLE [dbo].[ContentReports] WITH CHECK CHECK CONSTRAINT [FK_ContentReports_SourceVideos_SourceVideoId];

ALTER TABLE [dbo].[ContentReports] WITH CHECK CHECK CONSTRAINT [FK_ContentReports_Users_HandledById];

ALTER TABLE [dbo].[ContentReports] WITH CHECK CHECK CONSTRAINT [FK_ContentReports_Lessons_LessonId];

ALTER TABLE [dbo].[ContentSources] WITH CHECK CHECK CONSTRAINT [FK_ContentSources_Users_ReviewedById];

ALTER TABLE [dbo].[ContentSources] WITH CHECK CHECK CONSTRAINT [FK_ContentSources_Users_SuggestedById];

ALTER TABLE [dbo].[CurationSuggestions] WITH CHECK CHECK CONSTRAINT [FK_CurationSuggestions_Users_DecidedById];

ALTER TABLE [dbo].[CurationSuggestions] WITH CHECK CHECK CONSTRAINT [FK_CurationSuggestions_ContentSources_SourceId];

ALTER TABLE [dbo].[DailyUsages] WITH CHECK CHECK CONSTRAINT [FK_DailyUsages_ChildProfiles_ChildProfileId];

ALTER TABLE [dbo].[DataRequests] WITH CHECK CHECK CONSTRAINT [FK_DataRequests_Users_ParentId];

ALTER TABLE [dbo].[DataRequests] WITH CHECK CHECK CONSTRAINT [FK_DataRequests_Users_ProcessedById];

ALTER TABLE [dbo].[Favorites] WITH CHECK CHECK CONSTRAINT [FK_Favorites_Lessons_LessonId];

ALTER TABLE [dbo].[Favorites] WITH CHECK CHECK CONSTRAINT [FK_Favorites_ChildProfiles_ChildProfileId];

ALTER TABLE [dbo].[LearningGoals] WITH CHECK CHECK CONSTRAINT [FK_LearningGoals_Users_CreatedById];

ALTER TABLE [dbo].[LearningGoals] WITH CHECK CHECK CONSTRAINT [FK_LearningGoals_ChildProfiles_ChildProfileId];

ALTER TABLE [dbo].[LearningPathItems] WITH CHECK CHECK CONSTRAINT [FK_LearningPathItems_Lessons_LessonId];

ALTER TABLE [dbo].[LearningPathItems] WITH CHECK CHECK CONSTRAINT [FK_LearningPathItems_LearningPaths_PathId];

ALTER TABLE [dbo].[LearningPaths] WITH CHECK CHECK CONSTRAINT [FK_LearningPaths_GradeLevels_GradeLevelId];

ALTER TABLE [dbo].[LearningPaths] WITH CHECK CHECK CONSTRAINT [FK_LearningPaths_Subjects_SubjectId];

ALTER TABLE [dbo].[LearningSessions] WITH CHECK CHECK CONSTRAINT [FK_LearningSessions_ChildProfiles_ChildProfileId];

ALTER TABLE [dbo].[LessonActivities] WITH CHECK CHECK CONSTRAINT [FK_LessonActivities_LearningSessions_SessionId];

ALTER TABLE [dbo].[LessonActivities] WITH CHECK CHECK CONSTRAINT [FK_LessonActivities_LearningSessions_SessionId_ChildProfileId];

ALTER TABLE [dbo].[LessonActivities] WITH CHECK CHECK CONSTRAINT [FK_LessonActivities_Lessons_LessonId];

ALTER TABLE [dbo].[LessonActivities] WITH CHECK CHECK CONSTRAINT [FK_LessonActivities_ChildProfiles_ChildProfileId];

ALTER TABLE [dbo].[LessonContents] WITH CHECK CHECK CONSTRAINT [FK_LessonContents_Lessons_LessonId];

ALTER TABLE [dbo].[LessonContents] WITH CHECK CHECK CONSTRAINT [FK_LessonContents_SourceVideos_SourceVideoId];

ALTER TABLE [dbo].[LessonRequests] WITH CHECK CHECK CONSTRAINT [FK_LessonRequests_Users_HandledById];

ALTER TABLE [dbo].[LessonRequests] WITH CHECK CHECK CONSTRAINT [FK_LessonRequests_GradeLevels];

ALTER TABLE [dbo].[LessonRequests] WITH CHECK CHECK CONSTRAINT [FK_LessonRequests_Users_ParentId];

ALTER TABLE [dbo].[LessonRequests] WITH CHECK CHECK CONSTRAINT [FK_LessonRequests_Lessons];

ALTER TABLE [dbo].[LessonRequests] WITH CHECK CHECK CONSTRAINT [FK_LessonRequests_Subjects];

ALTER TABLE [dbo].[LessonRequestVotes] WITH CHECK CHECK CONSTRAINT [FK_LessonRequestVotes_LessonRequests];

ALTER TABLE [dbo].[LessonRequestVotes] WITH CHECK CHECK CONSTRAINT [FK_LessonRequestVotes_Users];

ALTER TABLE [dbo].[Lessons] WITH CHECK CHECK CONSTRAINT [FK_Lessons_Users_OwnerParentId];

ALTER TABLE [dbo].[Lessons] WITH CHECK CHECK CONSTRAINT [FK_Lessons_Users_CreatedById];

ALTER TABLE [dbo].[Lessons] WITH CHECK CHECK CONSTRAINT [FK_Lessons_GradeLevels_GradeLevelId];

ALTER TABLE [dbo].[Lessons] WITH CHECK CHECK CONSTRAINT [FK_Lessons_Users_ApprovedById];

ALTER TABLE [dbo].[Lessons] WITH CHECK CHECK CONSTRAINT [FK_Lessons_SubjectGrades_SubjectId_GradeLevelId];

ALTER TABLE [dbo].[Lessons] WITH CHECK CHECK CONSTRAINT [FK_Lessons_Subjects_SubjectId];

ALTER TABLE [dbo].[LessonTags] WITH CHECK CHECK CONSTRAINT [FK_LessonTags_Lessons_LessonId];

ALTER TABLE [dbo].[LessonTags] WITH CHECK CHECK CONSTRAINT [FK_LessonTags_Tags_TagId];

ALTER TABLE [dbo].[LoginHistories] WITH CHECK CHECK CONSTRAINT [FK_LoginHistories_Users_UserId];

ALTER TABLE [dbo].[Notifications] WITH CHECK CHECK CONSTRAINT [FK_Notifications_Users_UserId];

ALTER TABLE [dbo].[ParentSources] WITH CHECK CHECK CONSTRAINT [FK_ParentSources_Users_ParentId];

ALTER TABLE [dbo].[ParentSources] WITH CHECK CHECK CONSTRAINT [FK_ParentSources_ContentSources_SourceId];

ALTER TABLE [dbo].[QuestionOptions] WITH CHECK CHECK CONSTRAINT [FK_QuestionOptions_Questions_QuestionId];

ALTER TABLE [dbo].[Questions] WITH CHECK CHECK CONSTRAINT [FK_Questions_SourceVideos_SourceVideoId];

ALTER TABLE [dbo].[Questions] WITH CHECK CHECK CONSTRAINT [FK_Questions_Lessons_LessonId];

ALTER TABLE [dbo].[Questions] WITH CHECK CHECK CONSTRAINT [FK_Questions_Users_CreatedById];

ALTER TABLE [dbo].[Questions] WITH CHECK CHECK CONSTRAINT [FK_Questions_Users_ApprovedById];

ALTER TABLE [dbo].[QuizAttemptAnswers] WITH CHECK CHECK CONSTRAINT [FK_QuizAttemptAnswers_QuestionOptions_SelectedOptionId];

ALTER TABLE [dbo].[QuizAttemptAnswers] WITH CHECK CHECK CONSTRAINT [FK_QuizAttemptAnswers_Quizzes_QuizId];

ALTER TABLE [dbo].[QuizAttemptAnswers] WITH CHECK CHECK CONSTRAINT [FK_QuizAttemptAnswers_QuestionOptions_SelectedOptionId_QuestionId];

ALTER TABLE [dbo].[QuizAttemptAnswers] WITH CHECK CHECK CONSTRAINT [FK_QuizAttemptAnswers_QuizAttempts_AttemptId];

ALTER TABLE [dbo].[QuizAttemptAnswers] WITH CHECK CHECK CONSTRAINT [FK_QuizAttemptAnswers_QuizQuestions_QuizId_QuestionId];

ALTER TABLE [dbo].[QuizAttemptAnswers] WITH CHECK CHECK CONSTRAINT [FK_QuizAttemptAnswers_Questions_QuestionId];

ALTER TABLE [dbo].[QuizAttemptAnswers] WITH CHECK CHECK CONSTRAINT [FK_QuizAttemptAnswers_QuizAttempts_AttemptId_QuizId];

ALTER TABLE [dbo].[QuizAttempts] WITH CHECK CHECK CONSTRAINT [FK_QuizAttempts_Assignments_AssignmentId];

ALTER TABLE [dbo].[QuizAttempts] WITH CHECK CHECK CONSTRAINT [FK_QuizAttempts_ChildProfiles_ChildProfileId];

ALTER TABLE [dbo].[QuizAttempts] WITH CHECK CHECK CONSTRAINT [FK_QuizAttempts_Assignments_AssignmentId_ChildProfileId_QuizId];

ALTER TABLE [dbo].[QuizAttempts] WITH CHECK CHECK CONSTRAINT [FK_QuizAttempts_Quizzes_QuizId];

ALTER TABLE [dbo].[QuizQuestions] WITH CHECK CHECK CONSTRAINT [FK_QuizQuestions_Questions_QuestionId];

ALTER TABLE [dbo].[QuizQuestions] WITH CHECK CHECK CONSTRAINT [FK_QuizQuestions_Quizzes_QuizId];

ALTER TABLE [dbo].[Quizzes] WITH CHECK CHECK CONSTRAINT [FK_Quizzes_Lessons_LessonId];

ALTER TABLE [dbo].[RoleClaims] WITH CHECK CHECK CONSTRAINT [FK_RoleClaims_Roles];

ALTER TABLE [dbo].[SearchLogs] WITH CHECK CHECK CONSTRAINT [FK_SearchLogs_ChildProfiles_ChildProfileId];

ALTER TABLE [dbo].[SourceVideos] WITH CHECK CHECK CONSTRAINT [FK_SourceVideos_Users_ReviewedById];

ALTER TABLE [dbo].[SourceVideoSources] WITH CHECK CHECK CONSTRAINT [FK_SourceVideoSources_ContentSources_SourceId];

ALTER TABLE [dbo].[SourceVideoSources] WITH CHECK CHECK CONSTRAINT [FK_SourceVideoSources_SourceVideos_SourceVideoId];

ALTER TABLE [dbo].[Streaks] WITH CHECK CHECK CONSTRAINT [FK_Streaks_ChildProfiles_ChildProfileId];

ALTER TABLE [dbo].[SubjectGrades] WITH CHECK CHECK CONSTRAINT [FK_SubjectGrades_Subjects_SubjectId];

ALTER TABLE [dbo].[SubjectGrades] WITH CHECK CHECK CONSTRAINT [FK_SubjectGrades_GradeLevels_GradeLevelId];

ALTER TABLE [dbo].[SubjectProgresses] WITH CHECK CHECK CONSTRAINT [FK_SubjectProgresses_ChildProfiles_ChildProfileId];

ALTER TABLE [dbo].[SubjectProgresses] WITH CHECK CHECK CONSTRAINT [FK_SubjectProgresses_Subjects_SubjectId];

ALTER TABLE [dbo].[TimeExtensionRequests] WITH CHECK CHECK CONSTRAINT [FK_TimeExtensionRequests_ChildProfiles_ChildProfileId];

ALTER TABLE [dbo].[TimeExtensionRequests] WITH CHECK CHECK CONSTRAINT [FK_TimeExtensionRequests_Users_DecidedById];

ALTER TABLE [dbo].[TimeLimitOverrides] WITH CHECK CHECK CONSTRAINT [FK_TimeLimitOverrides_TimeLimitPolicies_PolicyId];

ALTER TABLE [dbo].[TimeLimitPolicies] WITH CHECK CHECK CONSTRAINT [FK_TimeLimitPolicies_Users_UpdatedById];

ALTER TABLE [dbo].[TimeLimitPolicies] WITH CHECK CHECK CONSTRAINT [FK_TimeLimitPolicies_ChildProfiles_ChildProfileId];

ALTER TABLE [dbo].[UserClaims] WITH CHECK CHECK CONSTRAINT [FK_UserClaims_Users];

ALTER TABLE [dbo].[UserConsents] WITH CHECK CHECK CONSTRAINT [FK_UserConsents_Users_UserId];

ALTER TABLE [dbo].[UserLogins] WITH CHECK CHECK CONSTRAINT [FK_UserLogins_Users];

ALTER TABLE [dbo].[UserRoles] WITH CHECK CHECK CONSTRAINT [FK_UserRoles_Users];

ALTER TABLE [dbo].[UserRoles] WITH CHECK CHECK CONSTRAINT [FK_UserRoles_Roles];

ALTER TABLE [dbo].[UserSettings] WITH CHECK CHECK CONSTRAINT [FK_UserSettings_Users_UserId];

ALTER TABLE [dbo].[UserTokens] WITH CHECK CHECK CONSTRAINT [FK_UserTokens_Users];

ALTER TABLE [dbo].[WeeklyReports] WITH CHECK CHECK CONSTRAINT [FK_WeeklyReports_ChildProfiles_ChildProfileId];

ALTER TABLE [dbo].[Assignments] WITH CHECK CHECK CONSTRAINT [CK_Assignments_3];

ALTER TABLE [dbo].[Assignments] WITH CHECK CHECK CONSTRAINT [CK_Assignments_1];

ALTER TABLE [dbo].[Assignments] WITH CHECK CHECK CONSTRAINT [CK_Assignments_2];

ALTER TABLE [dbo].[Badges] WITH CHECK CHECK CONSTRAINT [CK_Badges_1];

ALTER TABLE [dbo].[Badges] WITH CHECK CHECK CONSTRAINT [CK_Badges_2];

ALTER TABLE [dbo].[ChildPaths] WITH CHECK CHECK CONSTRAINT [CK_ChildPaths_1];

ALTER TABLE [dbo].[ChildProfiles] WITH CHECK CHECK CONSTRAINT [CK_ChildProfiles_2];

ALTER TABLE [dbo].[ChildProfiles] WITH CHECK CHECK CONSTRAINT [CK_ChildProfiles_1];

ALTER TABLE [dbo].[ContentReports] WITH CHECK CHECK CONSTRAINT [CK_ContentReports_2];

ALTER TABLE [dbo].[ContentReports] WITH CHECK CHECK CONSTRAINT [CK_ContentReports_4];

ALTER TABLE [dbo].[ContentReports] WITH CHECK CHECK CONSTRAINT [CK_ContentReports_1];

ALTER TABLE [dbo].[ContentReports] WITH CHECK CHECK CONSTRAINT [CK_ContentReports_3];

ALTER TABLE [dbo].[ContentSources] WITH CHECK CHECK CONSTRAINT [CK_ContentSources_1];

ALTER TABLE [dbo].[ContentSources] WITH CHECK CHECK CONSTRAINT [CK_ContentSources_2];

ALTER TABLE [dbo].[CurationSuggestions] WITH CHECK CHECK CONSTRAINT [CK_CurationSuggestions_2];

ALTER TABLE [dbo].[CurationSuggestions] WITH CHECK CHECK CONSTRAINT [CK_CurationSuggestions_1];

ALTER TABLE [dbo].[DailyUsages] WITH CHECK CHECK CONSTRAINT [CK_DailyUsages_1];

ALTER TABLE [dbo].[DataRequests] WITH CHECK CHECK CONSTRAINT [CK_DataRequests_2];

ALTER TABLE [dbo].[DataRequests] WITH CHECK CHECK CONSTRAINT [CK_DataRequests_1];

ALTER TABLE [dbo].[LearningGoals] WITH CHECK CHECK CONSTRAINT [CK_LearningGoals_2];

ALTER TABLE [dbo].[LearningGoals] WITH CHECK CHECK CONSTRAINT [CK_LearningGoals_1];

ALTER TABLE [dbo].[LearningSessions] WITH CHECK CHECK CONSTRAINT [CK_LearningSessions_2];

ALTER TABLE [dbo].[LearningSessions] WITH CHECK CHECK CONSTRAINT [CK_LearningSessions_3];

ALTER TABLE [dbo].[LearningSessions] WITH CHECK CHECK CONSTRAINT [CK_LearningSessions_1];

ALTER TABLE [dbo].[LessonActivities] WITH CHECK CHECK CONSTRAINT [CK_LessonActivities_1];

ALTER TABLE [dbo].[LessonActivities] WITH CHECK CHECK CONSTRAINT [CK_LessonActivities_3];

ALTER TABLE [dbo].[LessonActivities] WITH CHECK CHECK CONSTRAINT [CK_LessonActivities_2];

ALTER TABLE [dbo].[LessonContents] WITH CHECK CHECK CONSTRAINT [CK_LessonContents_2];

ALTER TABLE [dbo].[LessonContents] WITH CHECK CHECK CONSTRAINT [CK_LessonContents_1];

ALTER TABLE [dbo].[LessonRequests] WITH CHECK CHECK CONSTRAINT [CK_LessonRequests_Status];

ALTER TABLE [dbo].[Lessons] WITH CHECK CHECK CONSTRAINT [CK_Lessons_3];

ALTER TABLE [dbo].[Lessons] WITH CHECK CHECK CONSTRAINT [CK_Lessons_ShareStatus];

ALTER TABLE [dbo].[Lessons] WITH CHECK CHECK CONSTRAINT [CK_Lessons_1];

ALTER TABLE [dbo].[Lessons] WITH CHECK CHECK CONSTRAINT [CK_Lessons_2];

ALTER TABLE [dbo].[Notifications] WITH CHECK CHECK CONSTRAINT [CK_Notifications_1];

ALTER TABLE [dbo].[ParentSources] WITH CHECK CHECK CONSTRAINT [CK_ParentSources_1];

ALTER TABLE [dbo].[Questions] WITH CHECK CHECK CONSTRAINT [CK_Questions_2];

ALTER TABLE [dbo].[Questions] WITH CHECK CHECK CONSTRAINT [CK_Questions_3];

ALTER TABLE [dbo].[Questions] WITH CHECK CHECK CONSTRAINT [CK_Questions_4];

ALTER TABLE [dbo].[Questions] WITH CHECK CHECK CONSTRAINT [CK_Questions_1];

ALTER TABLE [dbo].[QuizAttempts] WITH CHECK CHECK CONSTRAINT [CK_QuizAttempts_1];

ALTER TABLE [dbo].[QuizAttempts] WITH CHECK CHECK CONSTRAINT [CK_QuizAttempts_2];

ALTER TABLE [dbo].[QuizQuestions] WITH CHECK CHECK CONSTRAINT [CK_QuizQuestions_1];

ALTER TABLE [dbo].[Quizzes] WITH CHECK CHECK CONSTRAINT [CK_Quizzes_1];

ALTER TABLE [dbo].[Quizzes] WITH CHECK CHECK CONSTRAINT [CK_Quizzes_2];

ALTER TABLE [dbo].[Quizzes] WITH CHECK CHECK CONSTRAINT [CK_Quizzes_3];

ALTER TABLE [dbo].[SearchLogs] WITH CHECK CHECK CONSTRAINT [CK_SearchLogs_1];

ALTER TABLE [dbo].[SourceVideos] WITH CHECK CHECK CONSTRAINT [CK_SourceVideos_2];

ALTER TABLE [dbo].[SourceVideos] WITH CHECK CHECK CONSTRAINT [CK_SourceVideos_1];

ALTER TABLE [dbo].[Streaks] WITH CHECK CHECK CONSTRAINT [CK_Streaks_1];

ALTER TABLE [dbo].[SubjectProgresses] WITH CHECK CHECK CONSTRAINT [CK_SubjectProgresses_2];

ALTER TABLE [dbo].[SubjectProgresses] WITH CHECK CHECK CONSTRAINT [CK_SubjectProgresses_1];

ALTER TABLE [dbo].[SystemMetricsDaily] WITH CHECK CHECK CONSTRAINT [CK_SystemMetricsDaily_1];

ALTER TABLE [dbo].[TimeExtensionRequests] WITH CHECK CHECK CONSTRAINT [CK_TimeExtensionRequests_1];

ALTER TABLE [dbo].[TimeExtensionRequests] WITH CHECK CHECK CONSTRAINT [CK_TimeExtensionRequests_3];

ALTER TABLE [dbo].[TimeExtensionRequests] WITH CHECK CHECK CONSTRAINT [CK_TimeExtensionRequests_2];

ALTER TABLE [dbo].[TimeLimitOverrides] WITH CHECK CHECK CONSTRAINT [CK_TimeLimitOverrides_1];

ALTER TABLE [dbo].[TimeLimitOverrides] WITH CHECK CHECK CONSTRAINT [CK_TimeLimitOverrides_2];

ALTER TABLE [dbo].[TimeLimitPolicies] WITH CHECK CHECK CONSTRAINT [CK_TimeLimitPolicies_1];

ALTER TABLE [dbo].[TimeLimitPolicies] WITH CHECK CHECK CONSTRAINT [CK_TimeLimitPolicies_3];

ALTER TABLE [dbo].[TimeLimitPolicies] WITH CHECK CHECK CONSTRAINT [CK_TimeLimitPolicies_2];

ALTER TABLE [dbo].[UserConsents] WITH CHECK CHECK CONSTRAINT [CK_UserConsents_2];

ALTER TABLE [dbo].[UserConsents] WITH CHECK CHECK CONSTRAINT [CK_UserConsents_1];

ALTER TABLE [dbo].[UserConsents] WITH CHECK CHECK CONSTRAINT [CK_UserConsents_Dates];

ALTER TABLE [dbo].[Users] WITH CHECK CHECK CONSTRAINT [CK_Users_FailedCount];

ALTER TABLE [dbo].[UserSettings] WITH CHECK CHECK CONSTRAINT [CK_UserSettings_1];

ALTER TABLE [dbo].[WeeklyReports] WITH CHECK CHECK CONSTRAINT [CK_WeeklyReports_1];

ALTER TABLE [dbo].[WeeklyReports] WITH CHECK CHECK CONSTRAINT [CK_WeeklyReports_2];


GO
PRINT N'Update complete.';


GO
