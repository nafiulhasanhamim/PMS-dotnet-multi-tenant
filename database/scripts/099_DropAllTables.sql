-- ============================================
-- Script: 099_DropAllTables.sql
-- Description: Drops all tables (USE WITH CAUTION!)
-- This is useful for resetting the database during development
-- ============================================

USE [PMSDb]
GO

PRINT 'WARNING: This script will drop all tables!'
PRINT 'Press Ctrl+C to cancel or execute to continue...'
GO

-- Drop PMS tables here as they are added, dependants before their parents.

-- Drop audit tables (no foreign key dependencies)
IF OBJECT_ID('[dbo].[AccessLogs]', 'U') IS NOT NULL
BEGIN
    DROP TABLE [dbo].[AccessLogs]
    PRINT 'Table [AccessLogs] dropped.'
END
GO

-- Memberships reference both Users and Tenants, so they go first.
IF OBJECT_ID('[dbo].[UserTenantMemberships]', 'U') IS NOT NULL
BEGIN
    DROP TABLE [dbo].[UserTenantMemberships]
    PRINT 'Table [UserTenantMemberships] dropped.'
END
GO

IF OBJECT_ID('[dbo].[Users]', 'U') IS NOT NULL
BEGIN
    DROP TABLE [dbo].[Users]
    PRINT 'Table [Users] dropped.'
END
GO

-- Tenants last: every tenant-owned table references it, so it cannot go until
-- they are all gone.
IF OBJECT_ID('[dbo].[Tenants]', 'U') IS NOT NULL
BEGIN
    DROP TABLE [dbo].[Tenants]
    PRINT 'Table [Tenants] dropped.'
END
GO

PRINT ''
PRINT '============================================'
PRINT 'All tables dropped successfully.'
PRINT '============================================'
GO
