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

PRINT ''
PRINT '============================================'
PRINT 'All tables dropped successfully.'
PRINT '============================================'
GO
