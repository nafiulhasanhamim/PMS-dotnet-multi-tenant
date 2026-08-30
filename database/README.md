# Database Scripts

This folder contains SQL scripts for setting up and managing the PMS database.

## Scripts

The template's sample tables (Customers, Products, Orders) and their seed data
have been removed. PMS tables are added here as each module is built.

| Script | Description |
|--------|-------------|
| `000_RunAll.sql` | Instructions for running all scripts |
| `001_CreateDatabase.sql` | Creates the database |
| `099_DropAllTables.sql` | Drops all tables (for development reset) |

## Quick Start

### Option 1: SQL Server Management Studio (SSMS)

1. Open SSMS and connect to your SQL Server instance
2. Execute scripts in order:
   ```
   001_CreateDatabase.sql
   ```

### Option 2: Command Line (sqlcmd)

```bash
# Create database
sqlcmd -S YOUR_SERVER -E -i "001_CreateDatabase.sql"

# Create tables

# Seed data (optional)

# Create reporting tables (optional)
```

Replace `YOUR_SERVER` with your SQL Server instance (e.g., `localhost`, `.\SQLEXPRESS`, `(localdb)\mssqllocaldb`).

### Option 3: Visual Studio

1. Open Server Explorer
2. Connect to your SQL Server
3. Right-click the connection > New Query
4. Paste and execute each script in order

## Connection String

Update your `appsettings.json` or `appsettings.Development.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=YOUR_SERVER;Database=PMSDb;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true",
    "ReportingConnection": "Server=YOUR_SERVER;Database=PMSDb;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true"
  }
}
```

> **Note:** For production, `ReportingConnection` can point to a read replica for better performance.

### Common Server Names

| Server Type | Connection String Server Value |
|-------------|-------------------------------|
| LocalDB | `(localdb)\mssqllocaldb` |
| SQL Express | `.\SQLEXPRESS` or `localhost\SQLEXPRESS` |
| SQL Server | `localhost` or `YOUR_SERVER_NAME` |

## Tables Overview

### Transactional Tables (ApplicationDbContext)

These tables are used for normal CRUD operations:

#### Customers
- Basic customer information
- Email (unique)
- Phone number (optional)
- Shipping address
- Status (Active, Inactive, Suspended)
- Soft delete support

#### Products
- Product catalog
- SKU (unique)
- Price with currency
- Stock quantity
- Active/Inactive status
- Soft delete support

#### Orders
- Order header with shipping address
- Links to Customer
- Order status tracking
- Soft delete support

#### OrderItems
- Order line items
- Links to Order and Product
- Quantity and pricing
- Product name snapshot (for historical accuracy)

### Reporting Tables (ReportingDbContext Only)

These tables are used for analytics and dashboards. They are populated by scheduled jobs or background processes:

#### DailySalesSummaries
- Pre-aggregated daily sales metrics
- Total orders, revenue, items sold
- Unique and new customers per day
- Average order value

#### MonthlySalesSummaries
- Monthly trend analysis
- Year-over-year comparisons
- Revenue growth percentages
- New vs returning customer counts

#### ProductSalesAggregates
- Cumulative product sales metrics
- Total quantity sold, revenue
- Unique customer counts
- Average selling price

#### CustomerLifetimeValues
- Customer value metrics (CLV)
- RFM (Recency, Frequency, Monetary) scores
- Customer segmentation (Champion, Loyal, At Risk, etc.)
- Days since last order

## Resetting the Database

To reset the database during development:

```sql
-- Run this to drop all tables (both transactional and reporting)
EXEC sp_executesql N'
    DROP TABLE IF EXISTS DailySalesSummaries;
    DROP TABLE IF EXISTS MonthlySalesSummaries;
    DROP TABLE IF EXISTS ProductSalesAggregates;
    DROP TABLE IF EXISTS CustomerLifetimeValues;
    DROP TABLE IF EXISTS OrderItems;
    DROP TABLE IF EXISTS Orders;
    DROP TABLE IF EXISTS Products;
    DROP TABLE IF EXISTS Customers;
'
```

