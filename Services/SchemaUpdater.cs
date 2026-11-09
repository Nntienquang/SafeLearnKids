using Microsoft.EntityFrameworkCore;
using SafeLearnKids.Web.Data;

namespace SafeLearnKids.Web.Services;

// Additive, re-runnable schema changes made after the database was first created. Runs at startup in SQL mode,
// so a teammate who pulls the code gets the new columns and tables without running scripts by hand.
// Each step only adds what is missing; nothing is dropped or rewritten.
public static class SchemaUpdater
{
    private static readonly string[] Steps =
    [
        // Family lessons and the community library.
        "IF COL_LENGTH(N'dbo.Lessons', N'OwnerParentId') IS NULL ALTER TABLE dbo.Lessons ADD OwnerParentId nvarchar(450) NULL;",
        "IF COL_LENGTH(N'dbo.Lessons', N'ShareStatus') IS NULL ALTER TABLE dbo.Lessons ADD ShareStatus nvarchar(30) NULL;",
        "IF COL_LENGTH(N'dbo.Lessons', N'ShareNote') IS NULL ALTER TABLE dbo.Lessons ADD ShareNote nvarchar(500) NULL;",
        "IF COL_LENGTH(N'dbo.Lessons', N'SharedAt') IS NULL ALTER TABLE dbo.Lessons ADD SharedAt datetime2 NULL;",
        "IF COL_LENGTH(N'dbo.Lessons', N'ShareReviewedAt') IS NULL ALTER TABLE dbo.Lessons ADD ShareReviewedAt datetime2 NULL;",
        """
        IF OBJECT_ID(N'dbo.FK_Lessons_Users_OwnerParentId', N'F') IS NULL
            ALTER TABLE dbo.Lessons ADD CONSTRAINT FK_Lessons_Users_OwnerParentId FOREIGN KEY (OwnerParentId) REFERENCES dbo.Users(Id);
        """,
        """
        IF OBJECT_ID(N'dbo.CK_Lessons_ShareStatus', N'C') IS NULL
            ALTER TABLE dbo.Lessons ADD CONSTRAINT CK_Lessons_ShareStatus CHECK (ShareStatus IS NULL OR (OwnerParentId IS NOT NULL AND ShareStatus IN (N'Pending', N'Approved', N'Rejected')));
        """,
        "IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Lessons_OwnerParentId' AND object_id = OBJECT_ID(N'dbo.Lessons')) CREATE INDEX IX_Lessons_OwnerParentId ON dbo.Lessons(OwnerParentId);",
        // Lesson requests from parents, and "we need this too" votes.
        """
        IF OBJECT_ID(N'dbo.LessonRequests', N'U') IS NULL
        BEGIN
            CREATE TABLE dbo.LessonRequests (
                Id int IDENTITY(1,1) NOT NULL CONSTRAINT PK_LessonRequests PRIMARY KEY,
                ParentId nvarchar(450) NOT NULL CONSTRAINT FK_LessonRequests_Users_ParentId REFERENCES dbo.Users(Id) ON DELETE CASCADE,
                SubjectId int NOT NULL CONSTRAINT FK_LessonRequests_Subjects REFERENCES dbo.Subjects(Id),
                GradeLevelId int NOT NULL CONSTRAINT FK_LessonRequests_GradeLevels REFERENCES dbo.GradeLevels(Id),
                Topic nvarchar(200) NOT NULL,
                Note nvarchar(500) NULL,
                Status nvarchar(30) NOT NULL CONSTRAINT DF_LessonRequests_Status DEFAULT (N'Open')
                    CONSTRAINT CK_LessonRequests_Status CHECK (Status IN (N'Open', N'Planned', N'Done', N'Declined')),
                Reply nvarchar(500) NULL,
                LessonId int NULL CONSTRAINT FK_LessonRequests_Lessons REFERENCES dbo.Lessons(Id) ON DELETE SET NULL,
                HandledById nvarchar(450) NULL CONSTRAINT FK_LessonRequests_Users_HandledById REFERENCES dbo.Users(Id),
                CreatedAt datetime2 NOT NULL CONSTRAINT DF_LessonRequests_CreatedAt DEFAULT (sysutcdatetime()),
                HandledAt datetime2 NULL
            );
            CREATE INDEX IX_LessonRequests_Status_SubjectId_GradeLevelId ON dbo.LessonRequests(Status, SubjectId, GradeLevelId);
        END
        """,
        """
        IF OBJECT_ID(N'dbo.LessonRequestVotes', N'U') IS NULL
            CREATE TABLE dbo.LessonRequestVotes (
                RequestId int NOT NULL CONSTRAINT FK_LessonRequestVotes_LessonRequests REFERENCES dbo.LessonRequests(Id) ON DELETE CASCADE,
                ParentId nvarchar(450) NOT NULL CONSTRAINT FK_LessonRequestVotes_Users REFERENCES dbo.Users(Id),
                CreatedAt datetime2 NOT NULL CONSTRAINT DF_LessonRequestVotes_CreatedAt DEFAULT (sysutcdatetime()),
                CONSTRAINT PK_LessonRequestVotes PRIMARY KEY (RequestId, ParentId)
            );
        """
    ];

    public static async Task RunAsync(SafeLearnDbContext db, ILogger logger, CancellationToken cancellationToken = default)
    {
        // Steps are compile-time constants in this file, never user input, so concatenating them is safe.
#pragma warning disable EF1003
        foreach (var step in Steps)
            await db.Database.ExecuteSqlRawAsync("SET QUOTED_IDENTIFIER ON; SET ANSI_NULLS ON; " + step, cancellationToken);
#pragma warning restore EF1003
        logger.LogInformation("Database schema is up to date (family lessons, community library, lesson requests).");
    }
}
