-- ============================================
-- Script: 004_CreateReportingTables.sql
-- Description: Creates reporting-specific tables for analytics
-- Run after: 002_CreateTables.sql
-- Note: These tables are ONLY used by ReportingDbContext
--       Uses [reporting] schema to separate from transactional tables
-- ============================================

USE [PMSDb]
GO

-- ============================================
-- Create reporting schema if it doesn't exist
-- ============================================
IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = 'reporting')
BEGIN
    EXEC('CREATE SCHEMA [reporting]')
    PRINT 'Schema [reporting] created successfully.'
END
GO

-- ============================================
-- Table: reporting.DailySalesSummaries
-- Purpose: Pre-aggregated daily sales metrics for dashboards
-- ============================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'DailySalesSummaries' AND schema_id = SCHEMA_ID('reporting'))
BEGIN
    CREATE TABLE [reporting].[DailySalesSummaries]
    (
        [Id] UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        [SalesDate] DATE NOT NULL,
        [TotalOrders] INT NOT NULL DEFAULT 0,
        [CompletedOrders] INT NOT NULL DEFAULT 0,
        [CancelledOrders] INT NOT NULL DEFAULT 0,
        [TotalRevenue] DECIMAL(18,2) NOT NULL DEFAULT 0,
        [Currency] NVARCHAR(3) NOT NULL DEFAULT 'USD',
        [TotalItemsSold] INT NOT NULL DEFAULT 0,
        [UniqueCustomers] INT NOT NULL DEFAULT 0,
        [NewCustomers] INT NOT NULL DEFAULT 0,
        [AverageOrderValue] DECIMAL(18,2) NOT NULL DEFAULT 0,
        [LastCalculatedUtc] DATETIME2 NOT NULL DEFAULT GETUTCDATE()
    );

    -- Indexes
    CREATE UNIQUE INDEX [IX_DailySalesSummaries_SalesDate] ON [reporting].[DailySalesSummaries] ([SalesDate]);
    CREATE INDEX [IX_DailySalesSummaries_SalesDate_Revenue] ON [reporting].[DailySalesSummaries] ([SalesDate], [TotalRevenue]);

    PRINT 'Table [reporting].[DailySalesSummaries] created successfully.'
END
ELSE
BEGIN
    PRINT 'Table [reporting].[DailySalesSummaries] already exists.'
END
GO

-- ============================================
-- Table: reporting.MonthlySalesSummaries
-- Purpose: Pre-aggregated monthly sales metrics for trend analysis
-- ============================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'MonthlySalesSummaries' AND schema_id = SCHEMA_ID('reporting'))
BEGIN
    CREATE TABLE [reporting].[MonthlySalesSummaries]
    (
        [Id] UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        [Year] INT NOT NULL,
        [Month] INT NOT NULL,
        [TotalOrders] INT NOT NULL DEFAULT 0,
        [CompletedOrders] INT NOT NULL DEFAULT 0,
        [CancelledOrders] INT NOT NULL DEFAULT 0,
        [TotalRevenue] DECIMAL(18,2) NOT NULL DEFAULT 0,
        [Currency] NVARCHAR(3) NOT NULL DEFAULT 'USD',
        [TotalItemsSold] INT NOT NULL DEFAULT 0,
        [UniqueCustomers] INT NOT NULL DEFAULT 0,
        [NewCustomers] INT NOT NULL DEFAULT 0,
        [ReturningCustomers] INT NOT NULL DEFAULT 0,
        [AverageOrderValue] DECIMAL(18,2) NOT NULL DEFAULT 0,
        [RevenueGrowthPercent] DECIMAL(10,2) NULL,
        [YearOverYearGrowthPercent] DECIMAL(10,2) NULL,
        [LastCalculatedUtc] DATETIME2 NOT NULL DEFAULT GETUTCDATE()
    );

    -- Indexes
    CREATE UNIQUE INDEX [IX_MonthlySalesSummaries_YearMonth] ON [reporting].[MonthlySalesSummaries] ([Year], [Month]);
    CREATE INDEX [IX_MonthlySalesSummaries_Year] ON [reporting].[MonthlySalesSummaries] ([Year]);

    PRINT 'Table [reporting].[MonthlySalesSummaries] created successfully.'
END
ELSE
BEGIN
    PRINT 'Table [reporting].[MonthlySalesSummaries] already exists.'
END
GO

-- ============================================
-- Table: reporting.ProductSalesAggregates
-- Purpose: Pre-aggregated product sales metrics
-- ============================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'ProductSalesAggregates' AND schema_id = SCHEMA_ID('reporting'))
BEGIN
    CREATE TABLE [reporting].[ProductSalesAggregates]
    (
        [Id] UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        [ProductId] UNIQUEIDENTIFIER NOT NULL,
        [Sku] NVARCHAR(50) NOT NULL,
        [ProductName] NVARCHAR(200) NOT NULL,
        [TotalQuantitySold] INT NOT NULL DEFAULT 0,
        [TotalRevenue] DECIMAL(18,2) NOT NULL DEFAULT 0,
        [Currency] NVARCHAR(3) NOT NULL DEFAULT 'USD',
        [OrderCount] INT NOT NULL DEFAULT 0,
        [UniqueCustomers] INT NOT NULL DEFAULT 0,
        [FirstOrderDateUtc] DATETIME2 NULL,
        [LastOrderDateUtc] DATETIME2 NULL,
        [AverageQuantityPerOrder] DECIMAL(10,2) NOT NULL DEFAULT 0,
        [AverageSellingPrice] DECIMAL(18,2) NOT NULL DEFAULT 0,
        [LastCalculatedUtc] DATETIME2 NOT NULL DEFAULT GETUTCDATE()
    );

    -- Indexes
    CREATE UNIQUE INDEX [IX_ProductSalesAggregates_ProductId] ON [reporting].[ProductSalesAggregates] ([ProductId]);
    CREATE INDEX [IX_ProductSalesAggregates_Sku] ON [reporting].[ProductSalesAggregates] ([Sku]);
    CREATE INDEX [IX_ProductSalesAggregates_TotalRevenue] ON [reporting].[ProductSalesAggregates] ([TotalRevenue] DESC);
    CREATE INDEX [IX_ProductSalesAggregates_TotalQuantitySold] ON [reporting].[ProductSalesAggregates] ([TotalQuantitySold] DESC);

    PRINT 'Table [reporting].[ProductSalesAggregates] created successfully.'
END
ELSE
BEGIN
    PRINT 'Table [reporting].[ProductSalesAggregates] already exists.'
END
GO

-- ============================================
-- Table: reporting.CustomerLifetimeValues
-- Purpose: Customer value metrics and RFM segmentation
-- ============================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'CustomerLifetimeValues' AND schema_id = SCHEMA_ID('reporting'))
BEGIN
    CREATE TABLE [reporting].[CustomerLifetimeValues]
    (
        [Id] UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        [CustomerId] UNIQUEIDENTIFIER NOT NULL,
        [Email] NVARCHAR(320) NOT NULL,
        [CustomerName] NVARCHAR(200) NOT NULL,
        [TotalSpent] DECIMAL(18,2) NOT NULL DEFAULT 0,
        [Currency] NVARCHAR(3) NOT NULL DEFAULT 'USD',
        [TotalOrders] INT NOT NULL DEFAULT 0,
        [TotalItemsPurchased] INT NOT NULL DEFAULT 0,
        [AverageOrderValue] DECIMAL(18,2) NOT NULL DEFAULT 0,
        [AverageDaysBetweenOrders] DECIMAL(10,2) NULL,
        [FirstOrderDateUtc] DATETIME2 NULL,
        [LastOrderDateUtc] DATETIME2 NULL,
        [DaysSinceLastOrder] INT NULL,
        [Segment] NVARCHAR(20) NOT NULL DEFAULT 'New',
        [RecencyScore] INT NOT NULL DEFAULT 0,
        [FrequencyScore] INT NOT NULL DEFAULT 0,
        [MonetaryScore] INT NOT NULL DEFAULT 0,
        [LastCalculatedUtc] DATETIME2 NOT NULL DEFAULT GETUTCDATE()
    );

    -- Indexes
    CREATE UNIQUE INDEX [IX_CustomerLifetimeValues_CustomerId] ON [reporting].[CustomerLifetimeValues] ([CustomerId]);
    CREATE INDEX [IX_CustomerLifetimeValues_Email] ON [reporting].[CustomerLifetimeValues] ([Email]);
    CREATE INDEX [IX_CustomerLifetimeValues_Segment] ON [reporting].[CustomerLifetimeValues] ([Segment]);
    CREATE INDEX [IX_CustomerLifetimeValues_TotalSpent] ON [reporting].[CustomerLifetimeValues] ([TotalSpent] DESC);
    CREATE INDEX [IX_CustomerLifetimeValues_RFM] ON [reporting].[CustomerLifetimeValues] ([RecencyScore], [FrequencyScore], [MonetaryScore]);

    PRINT 'Table [reporting].[CustomerLifetimeValues] created successfully.'
END
ELSE
BEGIN
    PRINT 'Table [reporting].[CustomerLifetimeValues] already exists.'
END
GO

PRINT ''
PRINT '============================================'
PRINT 'All reporting tables created successfully!'
PRINT 'Schema: [reporting]'
PRINT '============================================'
GO
