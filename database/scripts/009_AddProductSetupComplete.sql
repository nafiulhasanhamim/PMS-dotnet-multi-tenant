-- ============================================
-- Script: 009_AddProductSetupComplete.sql
-- Description: Makes a product's sale price optional, and records whether its setup is finished.
--
-- WHY THE PRICE BECOMES NULLABLE
--
--   Bulk import. A pharmacy onboarding onto the platform has to enter two hundred or more
--   products before the system is usable, and pricing each one as it is entered turns an
--   afternoon into a fortnight. So the bulk path lets a pharmacy save the selection and price
--   it afterwards.
--
--   The alternative was storing zero for "not priced yet". Rejected: zero is a real price - a
--   sample, a giveaway - and a column that cannot tell "free" from "nobody has decided" will
--   eventually be asked to. Sooner or later something sells for nothing.
--
-- WHY IsSetupComplete IS STORED AND NOT DERIVED
--
--   It is a pure function of the price and unit columns, so a computed value would always
--   agree. But the medicines list filters on it, a banner counts it, and Module 5's sale path
--   will check it - and none of those can put a C# expression in a WHERE clause. Stored, it is
--   one indexed predicate; derived, it means loading every product to ask.
--
--   Nothing outside the Product entity writes it: Product.RecomputeSetupComplete runs on
--   construction and after every change to a price or a unit level. Adding a bulk pack to a
--   product with no bulk price makes it incomplete again, which is why the unit setter
--   recomputes too.
--
-- FORWARD DEPENDENCY
--
--   A product with IsSetupComplete = 0 must not be sellable. Module 5 (Billing) is where that
--   is enforced - there is nothing to enforce it in yet. Until then the flag is advisory, and
--   the UI is what keeps such products visible and flagged rather than quietly on sale.
--
-- Requires: 007 (Products).
-- See: docs/02-product-master.md
-- ============================================

USE [PMSDb]
GO

SET ANSI_NULLS ON
SET QUOTED_IDENTIFIER ON
GO

-- ── The price becomes optional ──────────────────────────────────────────────────────────
--
-- The CHECK constraint on prices needs no change: a NULL comparison evaluates to UNKNOWN,
-- which a CHECK treats as satisfied, so "PricePerBase >= 0" continues to reject negatives
-- and to permit an absent price.
IF EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('[dbo].[Products]')
      AND name = 'PricePerBase'
      AND is_nullable = 0)
BEGIN
    ALTER TABLE [dbo].[Products] ALTER COLUMN [PricePerBase] DECIMAL(18,4) NULL
    PRINT 'Column [Products].[PricePerBase] is now nullable.'
END
ELSE
    PRINT 'Column [Products].[PricePerBase] is already nullable.'
GO

-- ── The completeness flag ───────────────────────────────────────────────────────────────
IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('[dbo].[Products]') AND name = 'IsSetupComplete')
BEGIN
    -- Added with a default of 1 so the ALTER does not have to rewrite every row twice, then
    -- backfilled from the real rule below. Every product that existed before this script was
    -- created through a form that required its prices, so nearly all of them are complete -
    -- but "nearly" is not "all", and the backfill decides rather than assumes.
    ALTER TABLE [dbo].[Products]
        ADD [IsSetupComplete] BIT NOT NULL
            CONSTRAINT [DF_Products_IsSetupComplete] DEFAULT 1

    PRINT 'Column [Products].[IsSetupComplete] added.'
END
ELSE
    PRINT 'Column [Products].[IsSetupComplete] already exists.'
GO

-- ── Backfill, using the same rule the entity applies ────────────────────────────────────
--
-- A price is required for each level the product actually defines, and for no others: a
-- product sold only in bags needs one price, not three. Note it asks whether the price is
-- PRESENT, not whether it is positive - zero is a legitimate price.
UPDATE [dbo].[Products]
SET [IsSetupComplete] = CASE
        WHEN [PricePerBase] IS NOT NULL
         AND ([MidUnitName] IS NULL   OR [PricePerMid] IS NOT NULL)
         AND ([LargeUnitName] IS NULL OR [PricePerLarge] IS NOT NULL)
        THEN 1
        ELSE 0
    END
GO

PRINT 'Backfilled [Products].[IsSetupComplete].'
GO

-- ── The index behind the filter and the banner ──────────────────────────────────────────
--
-- Filtered to the incomplete rows only. Those are a small and shrinking set - the products
-- somebody is working through - while the complete ones are the entire catalogue. An
-- unfiltered index would be almost entirely pages nothing ever reads.
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = 'IX_Products_Tenant_SetupIncomplete'
      AND object_id = OBJECT_ID('[dbo].[Products]'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_Products_Tenant_SetupIncomplete]
        ON [dbo].[Products] ([TenantId], [IsSetupComplete])
        INCLUDE ([BrandName], [Strength], [IsActive])
        WHERE [IsSetupComplete] = 0

    PRINT 'Index [IX_Products_Tenant_SetupIncomplete] created.'
END
ELSE
    PRINT 'Index [IX_Products_Tenant_SetupIncomplete] already exists.'
GO

PRINT '009 complete.'
GO
