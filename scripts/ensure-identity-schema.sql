-- Run against the existing hoctapchotre database, after reviewing the target.
-- Adds Identity support tables only; does not recreate or alter Users/Roles/UserRoles.
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;

IF DB_NAME() <> N'hoctapchotre'
    THROW 50000, 'Select the existing hoctapchotre database before running this script.', 1;

IF OBJECT_ID(N'dbo.Users', N'U') IS NULL
    OR OBJECT_ID(N'dbo.Roles', N'U') IS NULL
    OR OBJECT_ID(N'dbo.UserRoles', N'U') IS NULL
    THROW 50001, 'The existing Users, Roles and UserRoles tables are required.', 1;

BEGIN TRANSACTION;

IF OBJECT_ID(N'dbo.UserClaims', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.UserClaims (
        Id int IDENTITY(1,1) NOT NULL CONSTRAINT PK_UserClaims PRIMARY KEY,
        UserId nvarchar(450) NOT NULL,
        ClaimType nvarchar(max) NULL,
        ClaimValue nvarchar(max) NULL,
        CONSTRAINT FK_UserClaims_Users FOREIGN KEY (UserId) REFERENCES dbo.Users(Id) ON DELETE CASCADE
    );
    CREATE INDEX IX_UserClaims_UserId ON dbo.UserClaims(UserId);
END;

IF OBJECT_ID(N'dbo.RoleClaims', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.RoleClaims (
        Id int IDENTITY(1,1) NOT NULL CONSTRAINT PK_RoleClaims PRIMARY KEY,
        RoleId nvarchar(450) NOT NULL,
        ClaimType nvarchar(max) NULL,
        ClaimValue nvarchar(max) NULL,
        CONSTRAINT FK_RoleClaims_Roles FOREIGN KEY (RoleId) REFERENCES dbo.Roles(Id) ON DELETE CASCADE
    );
    CREATE INDEX IX_RoleClaims_RoleId ON dbo.RoleClaims(RoleId);
END;

IF OBJECT_ID(N'dbo.UserLogins', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.UserLogins (
        LoginProvider nvarchar(128) NOT NULL,
        ProviderKey nvarchar(128) NOT NULL,
        ProviderDisplayName nvarchar(max) NULL,
        UserId nvarchar(450) NOT NULL,
        CONSTRAINT PK_UserLogins PRIMARY KEY (LoginProvider, ProviderKey),
        CONSTRAINT FK_UserLogins_Users FOREIGN KEY (UserId) REFERENCES dbo.Users(Id) ON DELETE CASCADE
    );
    CREATE INDEX IX_UserLogins_UserId ON dbo.UserLogins(UserId);
END;

IF OBJECT_ID(N'dbo.UserTokens', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.UserTokens (
        UserId nvarchar(450) NOT NULL,
        LoginProvider nvarchar(128) NOT NULL,
        Name nvarchar(128) NOT NULL,
        Value nvarchar(max) NULL,
        -- The composite nvarchar key can exceed the 900-byte clustered index limit.
        CONSTRAINT PK_UserTokens PRIMARY KEY NONCLUSTERED (UserId, LoginProvider, Name),
        CONSTRAINT FK_UserTokens_Users FOREIGN KEY (UserId) REFERENCES dbo.Users(Id) ON DELETE CASCADE
    );
END;

COMMIT TRANSACTION;
