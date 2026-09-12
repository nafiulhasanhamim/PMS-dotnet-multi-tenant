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
-- :r "007_CreateProductsTable.sql"
-- :r "008_CreateBatchesAndStockAdjustmentsTables.sql"
-- :r "009_AddProductSetupComplete.sql"
-- :r "010_ProductIdentityIncludesDosageForm.sql"

-- Otherwise, execute each script in order:
-- 1. 001_CreateDatabase.sql
-- 2. 002_CreateTenantsTable.sql      (before any tenant-owned table)
-- 3. 003_CreateUsersTable.sql
-- 4. 004_CreateUserTenantMembershipsTable.sql   (needs Tenants and Users)
-- 5. 005_CreateAccessLogsTable.sql
-- 6. 006_CreateCatalogTables.sql   (platform-level medicine catalog; no tenant)
-- 7. 007_CreateProductsTable.sql    (tenant-scoped product master; needs 002 and 006)
-- 8. 008_CreateBatchesAndStockAdjustmentsTables.sql   (stock; needs 002, 003 and 007)
-- 9. 009_AddProductSetupComplete.sql   (nullable price + IsSetupComplete; needs 007)
-- 10. 010_ProductIdentityIncludesDosageForm.sql   (identity gains dosage form; needs 007)
-- 11. 011_CreateSalesTables.sql   (sales, lines, returns, invoice counter; needs 002, 003, 007, 008)
-- 12. 012_AddAntibioticPrescriptionMode.sql   (per-tenant antibiotic mode; needs 002)
-- 13. 013_CreateSupplierAndPurchaseTables.sql   (suppliers, purchases, payments, returns,
--     purchase counter, and the Batches.SupplierId FK Module 3 left room for; needs 002, 003, 007, 008)
-- 14. 014_AddSupplierPaymentDirection.sql   (supplier refunds and write-offs; needs 013)

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
PRINT '7. Run 007_CreateProductsTable.sql    (tenant-scoped product master)'
PRINT '8. Run 008_CreateBatchesAndStockAdjustmentsTables.sql   (batches and stock)'
PRINT '9. Run 009_AddProductSetupComplete.sql   (product setup completeness)'
PRINT '10. Run 010_ProductIdentityIncludesDosageForm.sql   (identity + dosage form)'
PRINT '11. Run 011_CreateSalesTables.sql   (billing: sales, lines, returns)'
PRINT '12. Run 012_AddAntibioticPrescriptionMode.sql   (antibiotic mode setting)'
PRINT '13. Run 013_CreateSupplierAndPurchaseTables.sql   (suppliers, purchases, payments, returns)'
PRINT '14. Run 014_AddSupplierPaymentDirection.sql   (supplier refunds and write-offs)'
PRINT ''
PRINT 'Or enable SQLCMD mode in SSMS (Query > SQLCMD Mode)'
PRINT 'and uncomment the :r commands above to run all at once.'
PRINT '============================================'
GO
