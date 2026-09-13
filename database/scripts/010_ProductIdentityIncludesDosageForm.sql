-- ============================================
-- Script: 010_ProductIdentityIncludesDosageForm.sql
-- Description: Makes dosage form part of a product's identity, so a pharmacy can stock a
--              cream and a lotion of the same brand and strength as separate products.
--
-- THE PROBLEM
--
--   Until now a product was identified by (TenantId, BrandName, Strength). The reference
--   catalogue has 548 brand+strength groups holding more than one entry, differing only by
--   dosage form. The worst is Nyclobate 0.05%, which exists six times:
--
--       Lotion | Topical Spray | Shampoo | Scalp Solution | Ointment | Cream
--
--   A pharmacy genuinely stocks several of those, at different prices. Under the old identity
--   it could hold exactly one, and the bulk import refused the rest - which is how this was
--   found. See docs/02-product-master.md.
--
-- WHY A COMPUTED COLUMN AND NOT MORE FILTERED INDEXES
--
--   SQL Server treats NULLs as EQUAL in a unique index. That is why script 007 needed two
--   filtered indexes rather than one: an index over (TenantId, BrandName, Strength) would
--   have allowed only ONE product with no strength per pharmacy, and every non-medicine has
--   no strength.
--
--   Adding a second nullable column to the key doubles that: Strength and DosageForm are each
--   nullable, so covering every combination without a hole takes FOUR filtered indexes
--   (both present, strength only, form only, neither). Correct, and unpleasant - and a third
--   nullable component later would need eight.
--
--   Instead the identity is materialised once as a persisted computed column, with ISNULL
--   collapsing the absent parts, and ONE unique index over it. No combinations to enumerate,
--   no hole possible, and the expression is the same shape as ProductKeys.Identity in the
--   application - so the in-memory duplicate check and the constraint agree by construction
--   rather than by coincidence.
--
--   CHAR(31) is the separator: a unit separator cannot appear in a brand name, a strength or a
--   dosage form, so "Napa" + "500 mg" cannot collide with "Napa 500" + "mg". A space or a
--   pipe could appear in any of the three.
--
--   Case sensitivity comes from the database collation, SQL_Latin1_General_CP1_CI_AS, which is
--   case-insensitive - matching the old behaviour and the ToLowerInvariant on the app side.
--
-- Requires: 007 (Products), 009 (nullable price).
-- See: docs/02-product-master.md
-- ============================================

USE [PMSDb]
GO

SET ANSI_NULLS ON
SET QUOTED_IDENTIFIER ON
GO

-- ── Report anything that would violate the new rule ─────────────────────────────────────
--
-- Nothing should: the new identity is strictly WEAKER than the old one (it permits everything
-- the old one did, plus rows that differ by dosage form). Checked anyway, because a migration
-- that silently could not create its own index would leave the table unprotected.
IF EXISTS (
    SELECT 1
    FROM [dbo].[Products]
    GROUP BY [TenantId], [BrandName], ISNULL([Strength], N''), ISNULL([DosageForm], N'')
    HAVING COUNT(*) > 1)
BEGIN
    RAISERROR('Products already contains duplicate brand+strength+dosage form rows. The unique index cannot be created until they are resolved.', 16, 1)
END
GO

-- ── The computed identity ───────────────────────────────────────────────────────────────
IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('[dbo].[Products]') AND name = 'IdentityKey')
BEGIN
    ALTER TABLE [dbo].[Products] ADD [IdentityKey]
        AS (CONCAT(
                [BrandName], CHAR(31),
                ISNULL([Strength], N''), CHAR(31),
                ISNULL([DosageForm], N'')))
        PERSISTED NOT NULL

    PRINT 'Computed column [Products].[IdentityKey] added.'
END
ELSE
    PRINT 'Computed column [Products].[IdentityKey] already exists.'
GO

-- ── One unique index replaces the two from script 007 ───────────────────────────────────
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = 'UX_Products_Tenant_Identity'
      AND object_id = OBJECT_ID('[dbo].[Products]'))
BEGIN
    CREATE UNIQUE NONCLUSTERED INDEX [UX_Products_Tenant_Identity]
        ON [dbo].[Products] ([TenantId], [IdentityKey])

    PRINT 'Index [UX_Products_Tenant_Identity] created.'
END
ELSE
    PRINT 'Index [UX_Products_Tenant_Identity] already exists.'
GO

-- Dropped only after the replacement exists, so the table is never unprotected.
IF EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = 'UX_Products_Tenant_Brand_Strength'
      AND object_id = OBJECT_ID('[dbo].[Products]'))
BEGIN
    DROP INDEX [UX_Products_Tenant_Brand_Strength] ON [dbo].[Products]
    PRINT 'Index [UX_Products_Tenant_Brand_Strength] dropped - superseded.'
END
GO

IF EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = 'UX_Products_Tenant_Brand_NoStrength'
      AND object_id = OBJECT_ID('[dbo].[Products]'))
BEGIN
    DROP INDEX [UX_Products_Tenant_Brand_NoStrength] ON [dbo].[Products]
    PRINT 'Index [UX_Products_Tenant_Brand_NoStrength] dropped - superseded.'
END
GO

-- The brand-name lookup the lists use was previously served by the unique index above, which
-- has gone. Kept as a plain index so the search does not fall back to a scan.
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = 'IX_Products_Tenant_Brand_Strength'
      AND object_id = OBJECT_ID('[dbo].[Products]'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_Products_Tenant_Brand_Strength]
        ON [dbo].[Products] ([TenantId], [BrandName], [Strength])
        INCLUDE ([DosageForm])

    PRINT 'Index [IX_Products_Tenant_Brand_Strength] created.'
END
GO

PRINT '010 complete.'
GO
