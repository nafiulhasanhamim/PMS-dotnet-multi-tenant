-- ============================================
-- Script: 002_CreateTables.sql
-- Description: Creates all tables for PMS
-- Run after: 001_CreateDatabase.sql
-- ============================================

USE [PMSDb]
GO

-- ============================================
-- Table: Customers
-- ============================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Customers')
BEGIN
    CREATE TABLE [dbo].[Customers]
    (
        [Id] UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        [FirstName] NVARCHAR(100) NOT NULL,
        [LastName] NVARCHAR(100) NOT NULL,
        [Email] NVARCHAR(320) NOT NULL,
        [PhoneNumber] NVARCHAR(20) NULL,
        [ShippingStreet] NVARCHAR(200) NULL,
        [ShippingCity] NVARCHAR(100) NULL,
        [ShippingState] NVARCHAR(100) NULL,
        [ShippingPostalCode] NVARCHAR(20) NULL,
        [ShippingCountry] NVARCHAR(100) NULL,
        [Status] NVARCHAR(20) NOT NULL DEFAULT 'Active',

        -- Audit fields
        [CreatedOnUtc] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        [CreatedBy] NVARCHAR(256) NULL,
        [ModifiedOnUtc] DATETIME2 NULL,
        [ModifiedBy] NVARCHAR(256) NULL,

        -- Soft delete fields
        [IsDeleted] BIT NOT NULL DEFAULT 0,
        [DeletedOnUtc] DATETIME2 NULL,
        [DeletedBy] NVARCHAR(256) NULL,

        -- Concurrency token
        [RowVersion] ROWVERSION NOT NULL
    );

    -- Indexes
    CREATE UNIQUE INDEX [IX_Customers_Email] ON [dbo].[Customers] ([Email]) WHERE [IsDeleted] = 0;
    CREATE INDEX [IX_Customers_LastName] ON [dbo].[Customers] ([LastName]);
    CREATE INDEX [IX_Customers_Status] ON [dbo].[Customers] ([Status]);
    CREATE INDEX [IX_Customers_IsDeleted] ON [dbo].[Customers] ([IsDeleted]);

    PRINT 'Table [Customers] created successfully.'
END
ELSE
BEGIN
    PRINT 'Table [Customers] already exists.'
END
GO

-- ============================================
-- Table: Products
-- ============================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Products')
BEGIN
    CREATE TABLE [dbo].[Products]
    (
        [Id] UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        [Name] NVARCHAR(200) NOT NULL,
        [Description] NVARCHAR(2000) NULL,
        [Sku] NVARCHAR(50) NOT NULL,
        [Price] DECIMAL(18,2) NOT NULL,
        [PriceCurrency] NVARCHAR(3) NOT NULL DEFAULT 'USD',
        [StockQuantity] INT NOT NULL DEFAULT 0,
        [IsActive] BIT NOT NULL DEFAULT 1,

        -- Audit fields
        [CreatedOnUtc] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        [CreatedBy] NVARCHAR(256) NULL,
        [ModifiedOnUtc] DATETIME2 NULL,
        [ModifiedBy] NVARCHAR(256) NULL,

        -- Soft delete fields
        [IsDeleted] BIT NOT NULL DEFAULT 0,
        [DeletedOnUtc] DATETIME2 NULL,
        [DeletedBy] NVARCHAR(256) NULL,

        -- Concurrency token
        [RowVersion] ROWVERSION NOT NULL
    );

    -- Indexes
    CREATE UNIQUE INDEX [IX_Products_Sku] ON [dbo].[Products] ([Sku]) WHERE [IsDeleted] = 0;
    CREATE INDEX [IX_Products_Name] ON [dbo].[Products] ([Name]);
    CREATE INDEX [IX_Products_IsActive] ON [dbo].[Products] ([IsActive]);
    CREATE INDEX [IX_Products_IsDeleted] ON [dbo].[Products] ([IsDeleted]);

    PRINT 'Table [Products] created successfully.'
END
ELSE
BEGIN
    PRINT 'Table [Products] already exists.'
END
GO

-- ============================================
-- Table: Orders
-- ============================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Orders')
BEGIN
    CREATE TABLE [dbo].[Orders]
    (
        [Id] UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        [OrderNumber] NVARCHAR(50) NOT NULL,
        [CustomerId] UNIQUEIDENTIFIER NOT NULL,
        [OrderDateUtc] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        [Status] NVARCHAR(20) NOT NULL DEFAULT 'Pending',
        [Notes] NVARCHAR(2000) NULL,
        [ShippingStreet] NVARCHAR(200) NOT NULL,
        [ShippingCity] NVARCHAR(100) NOT NULL,
        [ShippingState] NVARCHAR(100) NOT NULL,
        [ShippingPostalCode] NVARCHAR(20) NOT NULL,
        [ShippingCountry] NVARCHAR(100) NOT NULL,

        -- Audit fields
        [CreatedOnUtc] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        [CreatedBy] NVARCHAR(256) NULL,
        [ModifiedOnUtc] DATETIME2 NULL,
        [ModifiedBy] NVARCHAR(256) NULL,

        -- Soft delete fields
        [IsDeleted] BIT NOT NULL DEFAULT 0,
        [DeletedOnUtc] DATETIME2 NULL,
        [DeletedBy] NVARCHAR(256) NULL,

        -- Concurrency token
        [RowVersion] ROWVERSION NOT NULL,

        -- Foreign Key
        CONSTRAINT [FK_Orders_Customers] FOREIGN KEY ([CustomerId])
            REFERENCES [dbo].[Customers]([Id]) ON DELETE NO ACTION
    );

    -- Indexes
    CREATE UNIQUE INDEX [IX_Orders_OrderNumber] ON [dbo].[Orders] ([OrderNumber]);
    CREATE INDEX [IX_Orders_CustomerId] ON [dbo].[Orders] ([CustomerId]);
    CREATE INDEX [IX_Orders_OrderDateUtc] ON [dbo].[Orders] ([OrderDateUtc]);
    CREATE INDEX [IX_Orders_Status] ON [dbo].[Orders] ([Status]);
    CREATE INDEX [IX_Orders_IsDeleted] ON [dbo].[Orders] ([IsDeleted]);

    PRINT 'Table [Orders] created successfully.'
END
ELSE
BEGIN
    PRINT 'Table [Orders] already exists.'
END
GO

-- ============================================
-- Table: OrderItems
-- ============================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'OrderItems')
BEGIN
    CREATE TABLE [dbo].[OrderItems]
    (
        [Id] UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        [OrderId] UNIQUEIDENTIFIER NOT NULL,
        [ProductId] UNIQUEIDENTIFIER NOT NULL,
        [ProductName] NVARCHAR(200) NOT NULL,
        [Quantity] INT NOT NULL,
        [UnitPrice] DECIMAL(18,2) NOT NULL,
        [UnitPriceCurrency] NVARCHAR(3) NOT NULL DEFAULT 'USD',

        -- Foreign Keys
        CONSTRAINT [FK_OrderItems_Orders] FOREIGN KEY ([OrderId])
            REFERENCES [dbo].[Orders]([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_OrderItems_Products] FOREIGN KEY ([ProductId])
            REFERENCES [dbo].[Products]([Id]) ON DELETE NO ACTION
    );

    -- Indexes
    CREATE INDEX [IX_OrderItems_OrderId] ON [dbo].[OrderItems] ([OrderId]);
    CREATE INDEX [IX_OrderItems_ProductId] ON [dbo].[OrderItems] ([ProductId]);

    PRINT 'Table [OrderItems] created successfully.'
END
ELSE
BEGIN
    PRINT 'Table [OrderItems] already exists.'
END
GO

PRINT ''
PRINT '============================================'
PRINT 'All tables created successfully!'
PRINT '============================================'
GO
