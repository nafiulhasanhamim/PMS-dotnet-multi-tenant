-- ============================================
-- Script: 007_CreateProductsTable.sql
-- Description: Products - each pharmacy's own catalogue of what it sells.
--
-- WHY "Product" AND NOT "Medicine"
--
--   Bangladeshi pharmacies also sell saline, syringes, bandages, diapers, baby formula,
--   handwash, sanitiser, soap and supplements. A table called Medicine gets diapers entered
--   as medicines with invented generic names and strengths, which corrupts the catalogue and
--   makes every downstream report meaningless. So a medicine is one ProductType among six,
--   and the medicine-only columns are nullable.
--
--   One table rather than two because everything downstream - batches, FEFO, billing,
--   reports - works on integer quantities of a product's BASE UNIT and never needs to know
--   whether that unit is a tablet, a bottle or a tin.
--
-- TENANT SCOPED
--
--   Unlike the Catalog* tables from script 006, this one DOES carry a TenantId: it is one
--   pharmacy's own list, with its own prices. Product implements ITenantEntity, so EF filters
--   every read to the current pharmacy and stamps TenantId on insert.
--
-- Requires: 002 (Tenants), 006 (CatalogMedicines, for the provenance FK).
-- See: docs/02-product-master.md
-- ============================================

USE [PMSDb]
GO

-- Filtered unique index below; sqlcmd defaults QUOTED_IDENTIFIER to OFF and would create the
-- table while silently skipping the index.
SET ANSI_NULLS ON
SET QUOTED_IDENTIFIER ON
GO

IF OBJECT_ID('[dbo].[Products]', 'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[Products]
    (
        [Id]                UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID(),

        -- The owning pharmacy. Stamped by TenantEntityInterceptor, never by a handler.
        [TenantId]          UNIQUEIDENTIFIER NOT NULL,

        -- 0 Medicine, 1 MedicalSupply, 2 BabyCare, 3 PersonalCare, 4 Supplement, 5 Other.
        [ProductType]       INT              NOT NULL,

        [BrandName]         NVARCHAR(200)    NOT NULL,
        [Company]           NVARCHAR(200)    NULL,

        -- Free text secondary descriptor: "Painkiller", "Feeding". A managed category table
        -- is deliberately out of scope for now.
        [Category]          NVARCHAR(100)    NULL,

        -- Soft delete. A hard delete would either orphan or cascade away the pharmacy's
        -- purchase and sales history, which still refers to this row.
        [IsActive]          BIT              NOT NULL CONSTRAINT [DF_Products_IsActive] DEFAULT 1,

        -- Provenance: the platform catalogue row this was imported from, or NULL when it was
        -- entered by hand.
        --
        -- A foreign key from tenant-scoped data to a PLATFORM-LEVEL table, which is correct
        -- and expected. The catalogue has no TenantId; two pharmacies importing Napa 500 get
        -- two separate rows here pointing at the same catalogue row. Nothing about this
        -- reference is a reason to put a query filter on the catalogue.
        [CatalogMedicineId] INT              NULL,

        -- == Medicine-only. NULL for every other product type. =========================
        [GenericName]       NVARCHAR(300)    NULL,
        [Strength]          NVARCHAR(100)    NULL,
        [DosageForm]        NVARCHAR(100)    NULL,

        -- Drives real behaviour downstream: employees cannot sell antibiotics, each sale
        -- captures a prescription, and the regulatory register is built from these. Set from
        -- the catalogue's provisional flag on import, then confirmed by a pharmacist - which
        -- is where a machine-derived guess becomes a human decision.
        [IsAntibiotic]      BIT              NOT NULL CONSTRAINT [DF_Products_IsAntibiotic] DEFAULT 0,

        -- == Unit configuration ========================================================
        --
        -- Per product, because "piece / strip / box" only fits tablets. A sanitiser is sold
        -- in bottles and cartons, formula in tins and cartons, saline in bags and nothing
        -- else. Valid shapes: base; base+mid; base+large; base+mid+large.

        [BaseUnitName]      NVARCHAR(50)     NOT NULL,
        [MidUnitName]       NVARCHAR(50)     NULL,
        [LargeUnitName]     NVARCHAR(50)     NULL,

        -- Base units in one mid unit. Set if and only if MidUnitName is.
        [BasePerMid]        INT              NULL,

        -- Units in one large unit - AND WHICH UNIT DEPENDS ON WHETHER A MID LEVEL EXISTS.
        --
        --   with a mid level:    mid units per large   (10 strips per box)
        --   WITHOUT a mid level: BASE units per large  (24 bottles per carton)
        --
        -- This is the module's one genuine trap. Application code goes through
        -- Product.BaseUnitsPerLarge, which resolves both shapes in one place; nothing should
        -- multiply these two columns itself.
        [MidPerLarge]       INT              NULL,

        -- == Pricing. CURRENT DEFAULTS, not history. ===================================
        --
        -- The price actually charged is snapshotted onto the sale line at sale time (Billing
        -- module), so changing a price here never rewrites what a customer was charged.
        -- Decimal, never float: money.
        [PricePerBase]      DECIMAL(18,4)    NOT NULL,
        [PricePerMid]       DECIMAL(18,4)    NULL,
        [PricePerLarge]     DECIMAL(18,4)    NULL,

        -- == Inventory settings ========================================================
        [ReorderLevel]      INT              NOT NULL CONSTRAINT [DF_Products_ReorderLevel] DEFAULT 100,
        [ShelfLocation]     NVARCHAR(100)    NULL,

        -- Audit (IAuditable)
        [CreatedOnUtc]      DATETIME2        NOT NULL,
        [CreatedBy]         NVARCHAR(256)    NULL,
        [ModifiedOnUtc]     DATETIME2        NULL,
        [ModifiedBy]        NVARCHAR(256)    NULL,

        -- Declared on AggregateRoot but not configured as a concurrency token, so EF selects
        -- it on every read and the column has to exist. See the note in script 003.
        [RowVersion]        VARBINARY(MAX)   NULL,

        CONSTRAINT [PK_Products] PRIMARY KEY CLUSTERED ([Id]),

        CONSTRAINT [FK_Products_Tenants_TenantId]
            FOREIGN KEY ([TenantId]) REFERENCES [dbo].[Tenants] ([Id]),

        -- Restrict, not cascade: a catalogue refresh must never delete a pharmacy's products.
        CONSTRAINT [FK_Products_CatalogMedicines_CatalogMedicineId]
            FOREIGN KEY ([CatalogMedicineId]) REFERENCES [dbo].[CatalogMedicines] ([Id]),

        CONSTRAINT [CK_Products_ProductType] CHECK ([ProductType] BETWEEN 0 AND 5),

        -- The unit shape rules, enforced by the database and not only by FluentValidation.
        -- A pack count with no pack name is a number nothing can interpret, and a pack name
        -- with no count makes every quantity for that product ambiguous.
        CONSTRAINT [CK_Products_MidUnitPairing] CHECK (
            ([MidUnitName] IS NULL     AND [BasePerMid] IS NULL)
         OR ([MidUnitName] IS NOT NULL AND [BasePerMid] > 1)
        ),

        CONSTRAINT [CK_Products_LargeUnitPairing] CHECK (
            ([LargeUnitName] IS NULL     AND [MidPerLarge] IS NULL)
         OR ([LargeUnitName] IS NOT NULL AND [MidPerLarge] > 1)
        ),

        -- Only a medicine may carry the medicine-only fields. This is the corruption the
        -- module exists to prevent, so the database refuses it too - not just the validator.
        CONSTRAINT [CK_Products_MedicineOnlyFields] CHECK (
            [ProductType] = 0
         OR ([GenericName] IS NULL AND [Strength] IS NULL AND [DosageForm] IS NULL
             AND [IsAntibiotic] = 0)
        ),

        CONSTRAINT [CK_Products_PricesNotNegative] CHECK (
            [PricePerBase] >= 0
        AND ([PricePerMid] IS NULL OR [PricePerMid] >= 0)
        AND ([PricePerLarge] IS NULL OR [PricePerLarge] >= 0)
        )
    )

    -- BrandName + Strength unique PER PHARMACY. Two pharmacies may both stock Napa 500.
    --
    -- Two indexes rather than one, because SQL Server treats NULLs as EQUAL in a unique
    -- index: a single index over (TenantId, BrandName, Strength) would allow only ONE
    -- product with no strength per pharmacy, and every non-medicine has no strength.
    CREATE UNIQUE NONCLUSTERED INDEX [UX_Products_Tenant_Brand_Strength]
        ON [dbo].[Products] ([TenantId], [BrandName], [Strength])
        WHERE [Strength] IS NOT NULL

    CREATE UNIQUE NONCLUSTERED INDEX [UX_Products_Tenant_Brand_NoStrength]
        ON [dbo].[Products] ([TenantId], [BrandName])
        WHERE [Strength] IS NULL

    -- The list searches. TenantId leads both, so the seek lands inside one pharmacy's rows
    -- before it does anything else.
    CREATE NONCLUSTERED INDEX [IX_Products_Tenant_BrandName]
        ON [dbo].[Products] ([TenantId], [BrandName])
        INCLUDE ([ProductType], [GenericName], [Company], [Strength], [DosageForm],
                 [IsAntibiotic], [IsActive], [BaseUnitName], [PricePerBase])

    CREATE NONCLUSTERED INDEX [IX_Products_Tenant_GenericName]
        ON [dbo].[Products] ([TenantId], [GenericName])

    -- "Have I already imported this?" on the catalogue import screen.
    CREATE NONCLUSTERED INDEX [IX_Products_Tenant_CatalogMedicineId]
        ON [dbo].[Products] ([TenantId], [CatalogMedicineId])
        WHERE [CatalogMedicineId] IS NOT NULL

    PRINT 'Table [Products] created successfully.'
END
ELSE
    PRINT 'Table [Products] already exists.'
GO
