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

-- Module 3 stock, dependants first: an adjustment references its batch, and a batch
-- Module 5 first of all: sale lines reference products AND batches, and returns reference
-- sale lines, so the whole billing stack has to come out before stock or products.
IF OBJECT_ID('[dbo].[SalesReturns]', 'U') IS NOT NULL
BEGIN
    DROP TABLE [dbo].[SalesReturns]
    PRINT 'Table [SalesReturns] dropped.'
END
GO

IF OBJECT_ID('[dbo].[SaleLines]', 'U') IS NOT NULL
BEGIN
    DROP TABLE [dbo].[SaleLines]
    PRINT 'Table [SaleLines] dropped.'
END
GO

IF OBJECT_ID('[dbo].[Sales]', 'U') IS NOT NULL
BEGIN
    DROP TABLE [dbo].[Sales]
    PRINT 'Table [Sales] dropped.'
END
GO

IF OBJECT_ID('[dbo].[InvoiceSequences]', 'U') IS NOT NULL
BEGIN
    DROP TABLE [dbo].[InvoiceSequences]
    PRINT 'Table [InvoiceSequences] dropped.'
END
GO

-- references its product, so these have to go before Products below.
IF OBJECT_ID('[dbo].[StockAdjustments]', 'U') IS NOT NULL
BEGIN
    DROP TABLE [dbo].[StockAdjustments]
    PRINT 'Table [StockAdjustments] dropped.'
END
GO

IF OBJECT_ID('[dbo].[Batches]', 'U') IS NOT NULL
BEGIN
    DROP TABLE [dbo].[Batches]
    PRINT 'Table [Batches] dropped.'
END
GO

-- Drop audit tables (no foreign key dependencies)
IF OBJECT_ID('[dbo].[AccessLogs]', 'U') IS NOT NULL
BEGIN
    DROP TABLE [dbo].[AccessLogs]
    PRINT 'Table [AccessLogs] dropped.'
END
GO

-- Products reference both Tenants and the medicine catalog, so they go before either.
IF OBJECT_ID('[dbo].[Products]', 'U') IS NOT NULL
BEGIN
    DROP TABLE [dbo].[Products]
    PRINT 'Table [Products] dropped.'
END
GO

-- The medicine reference catalog. Independent of the tenant tables, but internally ordered:
-- medicines reference generics and the lookups, generics reference drug classes.
IF OBJECT_ID('[dbo].[CatalogMedicines]', 'U') IS NOT NULL
BEGIN
    DROP TABLE [dbo].[CatalogMedicines]
    PRINT 'Table [CatalogMedicines] dropped.'
END
GO

IF OBJECT_ID('[dbo].[CatalogGenerics]', 'U') IS NOT NULL
BEGIN
    DROP TABLE [dbo].[CatalogGenerics]
    PRINT 'Table [CatalogGenerics] dropped.'
END
GO

IF OBJECT_ID('[dbo].[CatalogDrugClasses]', 'U') IS NOT NULL
BEGIN
    DROP TABLE [dbo].[CatalogDrugClasses]
    PRINT 'Table [CatalogDrugClasses] dropped.'
END
GO

IF OBJECT_ID('[dbo].[CatalogDosageForms]', 'U') IS NOT NULL
BEGIN
    DROP TABLE [dbo].[CatalogDosageForms]
    PRINT 'Table [CatalogDosageForms] dropped.'
END
GO

IF OBJECT_ID('[dbo].[CatalogManufacturers]', 'U') IS NOT NULL
BEGIN
    DROP TABLE [dbo].[CatalogManufacturers]
    PRINT 'Table [CatalogManufacturers] dropped.'
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
