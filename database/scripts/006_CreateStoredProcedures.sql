-- ============================================
-- Script: 006_CreateStoredProcedures.sql
-- Description: Creates stored procedures for reporting and complex queries
-- ============================================

USE [PMSDb]
GO

PRINT 'Creating stored procedures...'
GO

-- ============================================
-- sp_GetCustomerOrderSummary
-- Returns order summary for a specific customer
-- ============================================
IF OBJECT_ID('[dbo].[sp_GetCustomerOrderSummary]', 'P') IS NOT NULL
    DROP PROCEDURE [dbo].[sp_GetCustomerOrderSummary]
GO

CREATE PROCEDURE [dbo].[sp_GetCustomerOrderSummary]
    @CustomerId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        c.Id AS CustomerId,
        c.FirstName,
        c.LastName,
        c.Email,
        COUNT(DISTINCT o.Id) AS TotalOrders,
        ISNULL(SUM(oi.Quantity * oi.UnitPrice), 0) AS TotalSpent,
        ISNULL(AVG(sub.OrderTotal), 0) AS AverageOrderValue,
        MAX(o.OrderDateUtc) AS LastOrderDate,
        MIN(o.OrderDateUtc) AS FirstOrderDate
    FROM [dbo].[Customers] c
    LEFT JOIN [dbo].[Orders] o ON c.Id = o.CustomerId AND o.IsDeleted = 0
    LEFT JOIN [dbo].[OrderItems] oi ON o.Id = oi.OrderId
    LEFT JOIN (
        SELECT o2.Id, SUM(oi2.Quantity * oi2.UnitPrice) AS OrderTotal
        FROM [dbo].[Orders] o2
        INNER JOIN [dbo].[OrderItems] oi2 ON o2.Id = oi2.OrderId
        WHERE o2.IsDeleted = 0
        GROUP BY o2.Id
    ) sub ON o.Id = sub.Id
    WHERE c.Id = @CustomerId
      AND c.IsDeleted = 0
    GROUP BY c.Id, c.FirstName, c.LastName, c.Email
END
GO

PRINT 'Created [sp_GetCustomerOrderSummary]'
GO

-- ============================================
-- sp_GetTopSellingProducts
-- Returns top selling products within a date range
-- ============================================
IF OBJECT_ID('[dbo].[sp_GetTopSellingProducts]', 'P') IS NOT NULL
    DROP PROCEDURE [dbo].[sp_GetTopSellingProducts]
GO

CREATE PROCEDURE [dbo].[sp_GetTopSellingProducts]
    @StartDate DATETIME2 = NULL,
    @EndDate DATETIME2 = NULL,
    @TopCount INT = 10
AS
BEGIN
    SET NOCOUNT ON;

    -- Default to last 30 days if no date range specified
    SET @StartDate = ISNULL(@StartDate, DATEADD(DAY, -30, GETUTCDATE()))
    SET @EndDate = ISNULL(@EndDate, GETUTCDATE())

    SELECT TOP (@TopCount)
        p.Id AS ProductId,
        p.Name AS ProductName,
        p.Sku,
        p.Price,
        SUM(oi.Quantity) AS TotalQuantitySold,
        SUM(oi.Quantity * oi.UnitPrice) AS TotalRevenue,
        COUNT(DISTINCT o.Id) AS NumberOfOrders
    FROM [dbo].[Products] p
    INNER JOIN [dbo].[OrderItems] oi ON p.Id = oi.ProductId
    INNER JOIN [dbo].[Orders] o ON oi.OrderId = o.Id
    WHERE o.OrderDateUtc BETWEEN @StartDate AND @EndDate
      AND o.IsDeleted = 0
      AND p.IsDeleted = 0
    GROUP BY p.Id, p.Name, p.Sku, p.Price
    ORDER BY TotalQuantitySold DESC
END
GO

PRINT 'Created [sp_GetTopSellingProducts]'
GO

-- ============================================
-- sp_GetSalesSummaryByDateRange
-- Returns sales summary for a date range
-- ============================================
IF OBJECT_ID('[dbo].[sp_GetSalesSummaryByDateRange]', 'P') IS NOT NULL
    DROP PROCEDURE [dbo].[sp_GetSalesSummaryByDateRange]
GO

CREATE PROCEDURE [dbo].[sp_GetSalesSummaryByDateRange]
    @StartDate DATETIME2,
    @EndDate DATETIME2
AS
BEGIN
    SET NOCOUNT ON;

    ;WITH OrderTotals AS (
        SELECT
            o.Id AS OrderId,
            o.CustomerId,
            SUM(oi.Quantity * oi.UnitPrice) AS OrderTotal,
            SUM(oi.Quantity) AS ItemCount
        FROM [dbo].[Orders] o
        INNER JOIN [dbo].[OrderItems] oi ON o.Id = oi.OrderId
        WHERE o.OrderDateUtc BETWEEN @StartDate AND @EndDate
          AND o.IsDeleted = 0
        GROUP BY o.Id, o.CustomerId
    )
    SELECT
        COUNT(OrderId) AS TotalOrders,
        COUNT(DISTINCT CustomerId) AS UniqueCustomers,
        SUM(ItemCount) AS TotalItemsSold,
        SUM(OrderTotal) AS TotalRevenue,
        AVG(OrderTotal) AS AverageOrderValue,
        MIN(OrderTotal) AS MinOrderValue,
        MAX(OrderTotal) AS MaxOrderValue
    FROM OrderTotals
END
GO

PRINT 'Created [sp_GetSalesSummaryByDateRange]'
GO

-- ============================================
-- sp_GetDailySalesReport
-- Returns daily sales breakdown for a date range
-- ============================================
IF OBJECT_ID('[dbo].[sp_GetDailySalesReport]', 'P') IS NOT NULL
    DROP PROCEDURE [dbo].[sp_GetDailySalesReport]
GO

CREATE PROCEDURE [dbo].[sp_GetDailySalesReport]
    @StartDate DATETIME2,
    @EndDate DATETIME2
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        CAST(o.OrderDateUtc AS DATE) AS SaleDate,
        COUNT(DISTINCT o.Id) AS OrderCount,
        COUNT(DISTINCT o.CustomerId) AS CustomerCount,
        SUM(oi.Quantity) AS ItemsSold,
        SUM(oi.Quantity * oi.UnitPrice) AS DailyRevenue
    FROM [dbo].[Orders] o
    INNER JOIN [dbo].[OrderItems] oi ON o.Id = oi.OrderId
    WHERE o.OrderDateUtc BETWEEN @StartDate AND @EndDate
      AND o.IsDeleted = 0
    GROUP BY CAST(o.OrderDateUtc AS DATE)
    ORDER BY SaleDate
END
GO

PRINT 'Created [sp_GetDailySalesReport]'
GO

-- ============================================
-- sp_SearchCustomers
-- Search customers by name or email with pagination
-- ============================================
IF OBJECT_ID('[dbo].[sp_SearchCustomers]', 'P') IS NOT NULL
    DROP PROCEDURE [dbo].[sp_SearchCustomers]
GO

CREATE PROCEDURE [dbo].[sp_SearchCustomers]
    @SearchTerm NVARCHAR(100) = NULL,
    @PageNumber INT = 1,
    @PageSize INT = 20
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @Offset INT = (@PageNumber - 1) * @PageSize

    -- Get total count
    SELECT COUNT(*) AS TotalCount
    FROM [dbo].[Customers]
    WHERE IsDeleted = 0
      AND (@SearchTerm IS NULL
           OR FirstName LIKE '%' + @SearchTerm + '%'
           OR LastName LIKE '%' + @SearchTerm + '%'
           OR Email LIKE '%' + @SearchTerm + '%')

    -- Get paginated results
    SELECT
        c.Id,
        c.FirstName,
        c.LastName,
        c.Email,
        c.PhoneNumber,
        c.Status,
        c.CreatedOnUtc,
        (SELECT COUNT(*) FROM [dbo].[Orders] WHERE CustomerId = c.Id AND IsDeleted = 0) AS OrderCount
    FROM [dbo].[Customers] c
    WHERE c.IsDeleted = 0
      AND (@SearchTerm IS NULL
           OR c.FirstName LIKE '%' + @SearchTerm + '%'
           OR c.LastName LIKE '%' + @SearchTerm + '%'
           OR c.Email LIKE '%' + @SearchTerm + '%')
    ORDER BY c.LastName, c.FirstName
    OFFSET @Offset ROWS
    FETCH NEXT @PageSize ROWS ONLY
END
GO

PRINT 'Created [sp_SearchCustomers]'
GO

-- ============================================
-- sp_GetProductInventoryStatus
-- Returns inventory status for all products
-- ============================================
IF OBJECT_ID('[dbo].[sp_GetProductInventoryStatus]', 'P') IS NOT NULL
    DROP PROCEDURE [dbo].[sp_GetProductInventoryStatus]
GO

CREATE PROCEDURE [dbo].[sp_GetProductInventoryStatus]
    @LowStockThreshold INT = 10
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        p.Id,
        p.Name,
        p.Sku,
        p.StockQuantity,
        p.Price,
        p.IsActive,
        CASE
            WHEN p.StockQuantity = 0 THEN 'Out of Stock'
            WHEN p.StockQuantity <= @LowStockThreshold THEN 'Low Stock'
            ELSE 'In Stock'
        END AS StockStatus,
        (SELECT ISNULL(SUM(oi.Quantity), 0)
         FROM [dbo].[OrderItems] oi
         INNER JOIN [dbo].[Orders] o ON oi.OrderId = o.Id
         WHERE oi.ProductId = p.Id
           AND o.IsDeleted = 0
           AND o.OrderDateUtc >= DATEADD(DAY, -30, GETUTCDATE())) AS Last30DaysSold
    FROM [dbo].[Products] p
    WHERE p.IsDeleted = 0
    ORDER BY p.StockQuantity ASC, p.Name
END
GO

PRINT 'Created [sp_GetProductInventoryStatus]'
GO

PRINT ''
PRINT '============================================'
PRINT 'All stored procedures created successfully.'
PRINT '============================================'
GO
