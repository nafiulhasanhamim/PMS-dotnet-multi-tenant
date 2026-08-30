-- ============================================
-- Script: 002_CreateTenantsTable.sql
-- Description: Creates the Tenants table - one row per pharmacy using the system.
--
-- Run this before any tenant-owned table: every one of those carries a TenantId
-- that references this table, so it has to exist first.
-- ============================================

USE [PMSDb]
GO

IF OBJECT_ID('[dbo].[Tenants]', 'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[Tenants]
    (
        [Id]              UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID(),

        -- The pharmacy's name, as printed on its invoices.
        [Name]            NVARCHAR(200)    NOT NULL,

        -- Short stable key used in URLs and support conversations, e.g. 'citycare'.
        -- Kept separate from Name so the name can be corrected without invalidating
        -- anything that refers to the tenant.
        [Slug]            NVARCHAR(64)     NOT NULL,

        -- The pharmacy's own host, e.g. 'citycare.com'. Optional. Stored as a bare
        -- lowercase host: no scheme, port or path. 253 is the longest a fully-qualified
        -- domain name can be (RFC 1035).
        [DomainName]      NVARCHAR(253)    NULL,

        -- Suspends a pharmacy without deleting it: its users cannot sign in, its data
        -- is untouched.
        [IsActive]        BIT              NOT NULL DEFAULT 1,

        -- Audit (IAuditable)
        [CreatedOnUtc]    DATETIME2        NOT NULL,
        [CreatedBy]       NVARCHAR(256)    NULL,
        [ModifiedOnUtc]   DATETIME2        NULL,
        [ModifiedBy]      NVARCHAR(256)    NULL,

        -- Soft delete (ISoftDelete). A tenant is never hard-deleted: its pharmacy's
        -- sales, purchases and audit history have to remain readable.
        [IsDeleted]       BIT              NOT NULL DEFAULT 0,
        [DeletedOnUtc]    DATETIME2        NULL,
        [DeletedBy]       NVARCHAR(256)    NULL,

        CONSTRAINT [PK_Tenants] PRIMARY KEY CLUSTERED ([Id])
    )

    -- Slug identifies a pharmacy system-wide, so it is unique across all tenants.
    -- Filtered on IsDeleted so a slug can be reused after a tenant is removed.
    CREATE UNIQUE NONCLUSTERED INDEX [IX_Tenants_Slug]
        ON [dbo].[Tenants] ([Slug])
        WHERE [IsDeleted] = 0

    -- A domain must resolve to exactly one tenant.
    --
    -- Filtered on IS NOT NULL as well as on IsDeleted, and that part is load-bearing:
    -- SQL Server treats NULLs as equal in a unique index, so without it only ONE
    -- pharmacy could exist without a domain - and most will not have one.
    CREATE UNIQUE NONCLUSTERED INDEX [IX_Tenants_DomainName]
        ON [dbo].[Tenants] ([DomainName])
        WHERE [DomainName] IS NOT NULL AND [IsDeleted] = 0

    -- Sign-in resolves the active tenant for a host or slug on every login.
    CREATE NONCLUSTERED INDEX [IX_Tenants_IsActive]
        ON [dbo].[Tenants] ([IsActive])
        WHERE [IsDeleted] = 0

    PRINT 'Table [Tenants] created successfully.'
END
ELSE
BEGIN
    PRINT 'Table [Tenants] already exists.'
END
GO
