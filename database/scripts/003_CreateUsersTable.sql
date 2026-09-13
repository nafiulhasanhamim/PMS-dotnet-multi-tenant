-- ============================================
-- Script: 003_CreateUsersTable.sql
-- Description: Creates the Users table - one row per person, platform-wide.
--
-- Users are deliberately NOT tenant-scoped. A person is one account with one password,
-- and their access to each pharmacy is a row in UserTenantMemberships. That is what lets
-- a pharmacist work at two pharmacies without maintaining two passwords.
--
-- Consequence worth knowing before you run this: Email is unique across the ENTIRE
-- platform, not per pharmacy. Two pharmacies cannot each have their own 'admin@x.com'.
-- ============================================

USE [PMSDb]
GO

-- Filtered indexes (the WHERE clauses below) will not be created unless both of these are ON.
-- sqlcmd defaults QUOTED_IDENTIFIER to OFF, so stating them here is not redundant: without
-- them the table is created and the unique indexes silently are not.
SET ANSI_NULLS ON
SET QUOTED_IDENTIFIER ON
GO

IF OBJECT_ID('[dbo].[Users]', 'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[Users]
    (
        [Id]                UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID(),

        -- The identity anchor. Stored lowercase and trimmed; the application normalises it
        -- on the way in so that lookups never need a case-insensitive scan.
        [Email]             NVARCHAR(256)    NOT NULL,

        -- BCrypt hash, work factor 12. 256 is generous - a BCrypt hash is 60 characters -
        -- but leaves room to move to a longer algorithm without a schema change.
        [PasswordHash]      NVARCHAR(256)    NOT NULL,

        [FullName]          NVARCHAR(200)    NOT NULL,

        -- A platform-wide off switch, separate from per-pharmacy membership status.
        -- Clearing this locks the person out everywhere at once; deactivating a membership
        -- removes access to one pharmacy only.
        [IsGloballyActive]  BIT              NOT NULL DEFAULT 1,

        -- Declared on AggregateRoot as a concurrency token, but not currently configured as
        -- one (no IsRowVersion() anywhere), so EF maps it as a plain nullable blob and never
        -- populates it. The column has to exist regardless: EF selects it on every read.
        -- To make it do its job, map it with .IsRowVersion() and change this to ROWVERSION.
        [RowVersion]        VARBINARY(MAX)   NULL,

        -- Audit (IAuditable)
        [CreatedOnUtc]      DATETIME2        NOT NULL,
        [CreatedBy]         NVARCHAR(256)    NULL,
        [ModifiedOnUtc]     DATETIME2        NULL,
        [ModifiedBy]        NVARCHAR(256)    NULL,

        CONSTRAINT [PK_Users] PRIMARY KEY CLUSTERED ([Id])
    )

    -- Not filtered: unlike Tenants, a User has no soft-delete column, so an email is
    -- claimed for good once used. Revisit this alongside any decision to make users
    -- soft-deletable.
    CREATE UNIQUE NONCLUSTERED INDEX [IX_Users_Email]
        ON [dbo].[Users] ([Email])

    PRINT 'Table [Users] created successfully.'
END
ELSE
BEGIN
    PRINT 'Table [Users] already exists.'
END
GO
