-- ============================================
-- Script: 002_CreateTenantsTable.sql
-- Description: Creates the Tenants table - one row per pharmacy using the system.
--
-- Run this before any tenant-owned table: every one of those carries a TenantId
-- that references this table, so it has to exist first.
-- ============================================

USE [PMSDb]
GO

-- Filtered indexes (the WHERE clauses below) will not be created unless both of these are ON.
-- sqlcmd defaults QUOTED_IDENTIFIER to OFF, so stating them here is not redundant: without
-- them the table is created and the unique indexes silently are not.
SET ANSI_NULLS ON
SET QUOTED_IDENTIFIER ON
GO

IF OBJECT_ID('[dbo].[Tenants]', 'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[Tenants]
    (
        [Id]                UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID(),

        -- The pharmacy name, as printed on its invoices.
        [Name]              NVARCHAR(200)    NOT NULL,

        -- The pharmacy own host, e.g. 'citycare.com'. Required: staff type this at sign-in,
        -- so it is what decides which pharmacy a login targets. Stored as a bare lowercase
        -- host - no scheme, port or path. 253 is the longest a fully-qualified domain name
        -- can be (RFC 1035).
        [DomainName]        NVARCHAR(253)    NOT NULL,

        -- 0 = Trial, 1 = Active, 2 = Suspended. Suspended stops every sign-in for the
        -- pharmacy without touching a row of its data.
        [Status]            INT              NOT NULL DEFAULT 0,

        -- Free text for now, e.g. 'Standard'. Billing is not modelled yet.
        [SubscriptionPlan]  NVARCHAR(100)    NULL,

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

        -- Soft delete (ISoftDelete). A tenant is never hard-deleted: its sales, purchases
        -- and audit history have to remain readable.
        [IsDeleted]         BIT              NOT NULL DEFAULT 0,
        [DeletedOnUtc]      DATETIME2        NULL,
        [DeletedBy]         NVARCHAR(256)    NULL,

        CONSTRAINT [PK_Tenants] PRIMARY KEY CLUSTERED ([Id]),

        CONSTRAINT [CK_Tenants_Status] CHECK ([Status] IN (0, 1, 2))
    )

    -- A domain decides which pharmacy a login targets, so it must resolve to exactly one.
    -- Filtered on IsDeleted so a domain can be reused after a pharmacy is removed.
    CREATE UNIQUE NONCLUSTERED INDEX [IX_Tenants_DomainName]
        ON [dbo].[Tenants] ([DomainName])
        WHERE [IsDeleted] = 0

    -- Sign-in checks status on every login, and the platform list groups by it.
    CREATE NONCLUSTERED INDEX [IX_Tenants_Status]
        ON [dbo].[Tenants] ([Status])
        WHERE [IsDeleted] = 0

    PRINT 'Table [Tenants] created successfully.'
END
ELSE
BEGIN
    PRINT 'Table [Tenants] already exists.'
END
GO
