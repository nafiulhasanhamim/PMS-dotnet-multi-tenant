-- ============================================
-- Script: 000_RunAll.sql
-- Description: Master script that runs all database scripts in order
-- Usage: Execute this script to set up the complete database
-- ============================================

PRINT '============================================'
PRINT 'PMS Database Setup'
PRINT 'Starting at: ' + CONVERT(VARCHAR, GETDATE(), 120)
PRINT '============================================'
PRINT ''

-- Note: In SQL Server Management Studio, you'll need to run each script separately
-- or use SQLCMD mode to use :r command for including files.

-- If using SQLCMD mode (Query > SQLCMD Mode), uncomment below:
-- :r "001_CreateDatabase.sql"
-- :r "002_CreateTenantsTable.sql"
-- :r "003_CreateUsersTable.sql"
-- :r "004_CreateUserTenantMembershipsTable.sql"
-- :r "005_CreateAccessLogsTable.sql"
-- :r "006_CreateCatalogTables.sql"

-- Otherwise, execute each script in order:
-- 1. 001_CreateDatabase.sql
-- 2. 002_CreateTenantsTable.sql      (before any tenant-owned table)
-- 3. 003_CreateUsersTable.sql
-- 4. 004_CreateUserTenantMembershipsTable.sql   (needs Tenants and Users)
-- 5. 005_CreateAccessLogsTable.sql
-- 6. 006_CreateCatalogTables.sql   (platform-level medicine catalog; no tenant)

PRINT ''
PRINT '============================================'
PRINT 'Instructions:'
PRINT '============================================'
PRINT '1. Run 001_CreateDatabase.sql first'
PRINT '2. Run 002_CreateTenantsTable.sql   (before any tenant-owned table)'
PRINT '3. Run 003_CreateUsersTable.sql'
PRINT '4. Run 004_CreateUserTenantMembershipsTable.sql   (needs Tenants and Users)'
PRINT '5. Run 005_CreateAccessLogsTable.sql'
PRINT '6. Run 006_CreateCatalogTables.sql   (platform-level medicine catalog)'
PRINT ''
PRINT 'Or enable SQLCMD mode in SSMS (Query > SQLCMD Mode)'
PRINT 'and uncomment the :r commands above to run all at once.'
PRINT '============================================'
GO
