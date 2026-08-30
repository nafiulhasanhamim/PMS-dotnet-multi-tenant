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

-- Drop reporting tables first (no foreign key dependencies)
-- These are in the [reporting] schema
IF OBJECT_ID('[reporting].[DailySalesSummaries]', 'U') IS NOT NULL
BEGIN
    DROP TABLE [reporting].[DailySalesSummaries]
    PRINT 'Table [reporting].[DailySalesSummaries] dropped.'
END
GO

IF OBJECT_ID('[reporting].[MonthlySalesSummaries]', 'U') IS NOT NULL
BEGIN
    DROP TABLE [reporting].[MonthlySalesSummaries]
    PRINT 'Table [reporting].[MonthlySalesSummaries] dropped.'
END
GO

IF OBJECT_ID('[reporting].[ProductSalesAggregates]', 'U') IS NOT NULL
BEGIN
    DROP TABLE [reporting].[ProductSalesAggregates]
    PRINT 'Table [reporting].[ProductSalesAggregates] dropped.'
END
GO

IF OBJECT_ID('[reporting].[CustomerLifetimeValues]', 'U') IS NOT NULL
BEGIN
    DROP TABLE [reporting].[CustomerLifetimeValues]
    PRINT 'Table [reporting].[CustomerLifetimeValues] dropped.'
END
GO

-- Drop audit tables (no foreign key dependencies)
IF OBJECT_ID('[dbo].[AccessLogs]', 'U') IS NOT NULL
BEGIN
    DROP TABLE [dbo].[AccessLogs]
    PRINT 'Table [AccessLogs] dropped.'
END
GO

-- Drop transactional tables in correct order (respecting foreign keys)
IF OBJECT_ID('[dbo].[OrderItems]', 'U') IS NOT NULL
BEGIN
    DROP TABLE [dbo].[OrderItems]
    PRINT 'Table [OrderItems] dropped.'
END
GO

IF OBJECT_ID('[dbo].[Orders]', 'U') IS NOT NULL
BEGIN
    DROP TABLE [dbo].[Orders]
    PRINT 'Table [Orders] dropped.'
END
GO

IF OBJECT_ID('[dbo].[Products]', 'U') IS NOT NULL
BEGIN
    DROP TABLE [dbo].[Products]
    PRINT 'Table [Products] dropped.'
END
GO

IF OBJECT_ID('[dbo].[Customers]', 'U') IS NOT NULL
BEGIN
    DROP TABLE [dbo].[Customers]
    PRINT 'Table [Customers] dropped.'
END
GO

PRINT ''
PRINT '============================================'
PRINT 'All tables dropped successfully.'
PRINT '============================================'
GO
