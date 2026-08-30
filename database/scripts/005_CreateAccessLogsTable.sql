-- ============================================
-- Script: 005_CreateAccessLogsTable.sql
-- Description: Creates the AccessLogs table for audit trail
-- ============================================

USE [PMSDb]
GO

IF OBJECT_ID('[dbo].[AccessLogs]', 'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[AccessLogs]
    (
        [Id]              UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID(),
        [UserId]          NVARCHAR(450)    NULL,
        [UserEmail]       NVARCHAR(256)    NULL,
        [UserRole]        NVARCHAR(100)    NULL,
        [AccessDateUtc]   DATETIME2        NOT NULL,
        [EntityName]      NVARCHAR(100)    NOT NULL,
        [EntityId]        NVARCHAR(100)    NULL,
        [Action]          NVARCHAR(50)     NOT NULL,
        [RequestUrl]      NVARCHAR(2000)   NULL,
        [HttpMethod]      NVARCHAR(10)     NULL,
        [IpAddress]       NVARCHAR(45)     NULL,
        [UserAgent]       NVARCHAR(500)    NULL,
        [AdditionalInfo]  NVARCHAR(4000)   NULL,
        [IsSuccess]       BIT              NOT NULL DEFAULT 1,
        [ErrorMessage]    NVARCHAR(4000)   NULL,

        CONSTRAINT [PK_AccessLogs] PRIMARY KEY CLUSTERED ([Id])
    )

    -- Indexes for common queries
    CREATE NONCLUSTERED INDEX [IX_AccessLogs_AccessDateUtc]
        ON [dbo].[AccessLogs] ([AccessDateUtc] DESC)

    CREATE NONCLUSTERED INDEX [IX_AccessLogs_UserId]
        ON [dbo].[AccessLogs] ([UserId])

    CREATE NONCLUSTERED INDEX [IX_AccessLogs_EntityName]
        ON [dbo].[AccessLogs] ([EntityName])

    CREATE NONCLUSTERED INDEX [IX_AccessLogs_EntityName_EntityId]
        ON [dbo].[AccessLogs] ([EntityName], [EntityId])

    CREATE NONCLUSTERED INDEX [IX_AccessLogs_Action]
        ON [dbo].[AccessLogs] ([Action])

    PRINT 'Table [AccessLogs] created successfully.'
END
ELSE
BEGIN
    PRINT 'Table [AccessLogs] already exists.'
END
GO
