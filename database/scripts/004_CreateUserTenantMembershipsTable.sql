-- ============================================
-- Script: 004_CreateUserTenantMembershipsTable.sql
-- Description: Creates UserTenantMemberships - who may act at which pharmacy, and as what.
--
-- Requires: 002 (Tenants) and 003 (Users).
--
-- The one design decision to understand before reading the constraints:
-- TenantId is NULLABLE, and a NULL TenantId means "platform level, belongs to no pharmacy".
-- That is how a platform administrator is represented - not as a flag on Users, and not as
-- a magic tenant row. Everything below exists to keep that representation from being abused.
-- ============================================

USE [PMSDb]
GO

-- Filtered indexes (the WHERE clauses below) will not be created unless both of these are ON.
-- sqlcmd defaults QUOTED_IDENTIFIER to OFF, so stating them here is not redundant: without
-- them the table is created and the unique indexes silently are not.
SET ANSI_NULLS ON
SET QUOTED_IDENTIFIER ON
GO

IF OBJECT_ID('[dbo].[UserTenantMemberships]', 'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[UserTenantMemberships]
    (
        [Id]              UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID(),

        -- NULL = a platform-level membership. See the header.
        [TenantId]        UNIQUEIDENTIFIER NULL,

        [UserId]          UNIQUEIDENTIFIER NOT NULL,

        -- 0 = PlatformAdmin, 1 = Admin, 2 = Pharmacist, 3 = Employee.
        [Role]            INT              NOT NULL,

        -- Per-pharmacy access switch. Turning this off ends access to THIS pharmacy while
        -- leaving the person able to sign in at any other pharmacy they belong to.
        [IsActive]        BIT              NOT NULL DEFAULT 1,

        [JoinedAt]        DATETIME2        NOT NULL,

        -- Declared on AggregateRoot as a concurrency token, but not currently configured as
        -- one (no IsRowVersion() anywhere), so EF maps it as a plain nullable blob and never
        -- populates it. The column has to exist regardless: EF selects it on every read.
        -- To make it do its job, map it with .IsRowVersion() and change this to ROWVERSION.
        [RowVersion]      VARBINARY(MAX)   NULL,

        -- Audit (IAuditable)
        [CreatedOnUtc]    DATETIME2        NOT NULL,
        [CreatedBy]       NVARCHAR(256)    NULL,
        [ModifiedOnUtc]   DATETIME2        NULL,
        [ModifiedBy]      NVARCHAR(256)    NULL,

        CONSTRAINT [PK_UserTenantMemberships] PRIMARY KEY CLUSTERED ([Id]),

        CONSTRAINT [FK_UserTenantMemberships_Users_UserId]
            FOREIGN KEY ([UserId]) REFERENCES [dbo].[Users] ([Id]) ON DELETE CASCADE,

        -- Restrict, not cascade: deleting a pharmacy must not silently strip its staff of
        -- access rows that audit history refers to. Tenants are soft-deleted anyway.
        CONSTRAINT [FK_UserTenantMemberships_Tenants_TenantId]
            FOREIGN KEY ([TenantId]) REFERENCES [dbo].[Tenants] ([Id]),

        CONSTRAINT [CK_UserTenantMemberships_Role] CHECK ([Role] IN (0, 1, 2, 3)),

        -- The invariant the whole identity model rests on, enforced here rather than only in
        -- the domain factories: a PlatformAdmin row pointing at a real pharmacy would be a
        -- pharmacy user holding platform powers, and a pharmacy role with no pharmacy would
        -- be unreachable by every tenant query filter.
        CONSTRAINT [CK_UserTenantMemberships_PlatformAdminHasNoTenant] CHECK (
            ([Role] = 0 AND [TenantId] IS NULL) OR
            ([Role] <> 0 AND [TenantId] IS NOT NULL)
        )
    )

    -- One membership per person per pharmacy.
    --
    -- The IS NOT NULL filter is load-bearing: SQL Server treats NULLs as EQUAL in a unique
    -- index, so without it the whole platform could hold only one platform-level membership.
    CREATE UNIQUE NONCLUSTERED INDEX [UX_UserTenantMemberships_Tenant_User]
        ON [dbo].[UserTenantMemberships] ([TenantId], [UserId])
        WHERE [TenantId] IS NOT NULL

    -- At most one platform-level membership per person. Same NULL-equality reasoning,
    -- used the other way round: here we WANT one row per user among the NULL-tenant rows.
    CREATE UNIQUE NONCLUSTERED INDEX [UX_UserTenantMemberships_PlatformPerUser]
        ON [dbo].[UserTenantMemberships] ([UserId])
        WHERE [TenantId] IS NULL

    -- Sign-in looks up a person memberships by user, then narrows to the tenant.
    CREATE NONCLUSTERED INDEX [IX_UserTenantMemberships_UserId]
        ON [dbo].[UserTenantMemberships] ([UserId])

    PRINT 'Table [UserTenantMemberships] created successfully.'
END
ELSE
BEGIN
    PRINT 'Table [UserTenantMemberships] already exists.'
END
GO
