-- ============================================
-- Script: 003_SeedData.sql
-- Description: Seeds initial test data
-- Run after: 002_CreateTables.sql
-- ============================================

USE [PMSDb]
GO

-- ============================================
-- Seed Customers
-- ============================================
IF NOT EXISTS (SELECT 1 FROM [dbo].[Customers])
BEGIN
    INSERT INTO [dbo].[Customers]
    ([Id], [FirstName], [LastName], [Email], [PhoneNumber],
     [ShippingStreet], [ShippingCity], [ShippingState], [ShippingPostalCode], [ShippingCountry],
     [Status], [CreatedOnUtc], [CreatedBy])
    VALUES
    (NEWID(), 'John', 'Doe', 'john.doe@example.com', '+15551234567',
     '123 Main Street', 'New York', 'NY', '10001', 'USA',
     'Active', GETUTCDATE(), 'System'),
    (NEWID(), 'Jane', 'Smith', 'jane.smith@example.com', '+15559876543',
     '456 Oak Avenue', 'Los Angeles', 'CA', '90001', 'USA',
     'Active', GETUTCDATE(), 'System'),
    (NEWID(), 'Bob', 'Johnson', 'bob.johnson@example.com', '+15555551234',
     '789 Pine Road', 'Chicago', 'IL', '60601', 'USA',
     'Active', GETUTCDATE(), 'System'),
    (NEWID(), 'Alice', 'Williams', 'alice.williams@example.com', NULL,
     NULL, NULL, NULL, NULL, NULL,
     'Active', GETUTCDATE(), 'System'),
    (NEWID(), 'Charlie', 'Brown', 'charlie.brown@example.com', '+447911123456',
     '10 Downing Street', 'London', 'England', 'SW1A 2AA', 'UK',
     'Inactive', GETUTCDATE(), 'System')

    PRINT 'Customers seeded successfully.'
END
GO

-- ============================================
-- Seed Products
-- ============================================
IF NOT EXISTS (SELECT 1 FROM [dbo].[Products])
BEGIN
    INSERT INTO [dbo].[Products]
    ([Id], [Name], [Description], [Sku], [Price], [PriceCurrency], [StockQuantity], [IsActive], [CreatedOnUtc], [CreatedBy])
    VALUES
    (NEWID(), 'Laptop Pro 15', 'High-performance laptop with 15-inch display', 'LAPTOP-PRO-15', 1299.99, 'USD', 50, 1, GETUTCDATE(), 'System'),
    (NEWID(), 'Wireless Mouse', 'Ergonomic wireless mouse with long battery life', 'MOUSE-WL-001', 29.99, 'USD', 200, 1, GETUTCDATE(), 'System'),
    (NEWID(), 'Mechanical Keyboard', 'RGB mechanical keyboard with Cherry MX switches', 'KB-MECH-RGB', 149.99, 'USD', 75, 1, GETUTCDATE(), 'System'),
    (NEWID(), 'USB-C Hub', '7-in-1 USB-C hub with HDMI and ethernet', 'HUB-USBC-7', 59.99, 'USD', 150, 1, GETUTCDATE(), 'System'),
    (NEWID(), '27" Monitor', '4K IPS monitor with HDR support', 'MON-27-4K', 449.99, 'USD', 30, 1, GETUTCDATE(), 'System'),
    (NEWID(), 'Webcam HD', '1080p webcam with built-in microphone', 'CAM-HD-1080', 79.99, 'USD', 100, 1, GETUTCDATE(), 'System'),
    (NEWID(), 'Noise Cancelling Headphones', 'Premium wireless headphones with ANC', 'HP-ANC-PRO', 299.99, 'USD', 40, 1, GETUTCDATE(), 'System'),
    (NEWID(), 'Laptop Stand', 'Adjustable aluminum laptop stand', 'STAND-AL-01', 39.99, 'USD', 120, 1, GETUTCDATE(), 'System'),
    (NEWID(), 'Desk Pad XL', 'Extra large desk pad with stitched edges', 'PAD-XL-BLK', 24.99, 'USD', 200, 1, GETUTCDATE(), 'System'),
    (NEWID(), 'Cable Management Kit', 'Complete cable management solution', 'CABLE-KIT-01', 19.99, 'USD', 300, 0, GETUTCDATE(), 'System') -- Inactive product

    PRINT 'Products seeded successfully.'
END
GO

-- ============================================
-- Seed Sample Orders (using existing customer and product IDs)
-- ============================================
DECLARE @CustomerId UNIQUEIDENTIFIER
DECLARE @ProductId1 UNIQUEIDENTIFIER
DECLARE @ProductId2 UNIQUEIDENTIFIER
DECLARE @OrderId UNIQUEIDENTIFIER

-- Get first customer
SELECT TOP 1 @CustomerId = [Id] FROM [dbo].[Customers] WHERE [Email] = 'john.doe@example.com'

-- Get some products
SELECT TOP 1 @ProductId1 = [Id] FROM [dbo].[Products] WHERE [Sku] = 'LAPTOP-PRO-15'
SELECT TOP 1 @ProductId2 = [Id] FROM [dbo].[Products] WHERE [Sku] = 'MOUSE-WL-001'

IF @CustomerId IS NOT NULL AND @ProductId1 IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [dbo].[Orders])
BEGIN
    SET @OrderId = NEWID()

    -- Insert Order
    INSERT INTO [dbo].[Orders]
    ([Id], [OrderNumber], [CustomerId], [OrderDateUtc], [Status],
     [ShippingStreet], [ShippingCity], [ShippingState], [ShippingPostalCode], [ShippingCountry],
     [Notes], [CreatedOnUtc], [CreatedBy])
    VALUES
    (@OrderId, 'ORD-2024-0001', @CustomerId, GETUTCDATE(), 'Confirmed',
     '123 Main Street', 'New York', 'NY', '10001', 'USA',
     'Please handle with care', GETUTCDATE(), 'System')

    -- Insert Order Items
    INSERT INTO [dbo].[OrderItems]
    ([Id], [OrderId], [ProductId], [ProductName], [Quantity], [UnitPrice], [UnitPriceCurrency])
    VALUES
    (NEWID(), @OrderId, @ProductId1, 'Laptop Pro 15', 1, 1299.99, 'USD')

    IF @ProductId2 IS NOT NULL
    BEGIN
        INSERT INTO [dbo].[OrderItems]
        ([Id], [OrderId], [ProductId], [ProductName], [Quantity], [UnitPrice], [UnitPriceCurrency])
        VALUES
        (NEWID(), @OrderId, @ProductId2, 'Wireless Mouse', 1, 29.99, 'USD')
    END

    PRINT 'Sample order seeded successfully.'
END
GO

PRINT 'Seed data completed successfully.'
GO
