-- ============================================
-- Script: 006_CreateCatalogTables.sql
-- Description: The medicine reference catalog - a shared, platform-owned list of every
--              medicine sold in Bangladesh, loaded from the Kaggle dataset
--              "Assorted Medicine Dataset of Bangladesh" (Ahmed Shahriar Sakib).
--
-- WHY THESE TABLES HAVE NO TenantId
--
--   Every other business table in this system carries a TenantId and is invisible across
--   pharmacies. These five deliberately do not. They are one shared catalogue that exists so
--   a pharmacy signing up can search for the medicines it stocks and import them, instead of
--   typing several hundred entries by hand before the software is usable.
--
--   They sit outside tenant isolation the same way the Tenants table does. Nothing here can
--   leak between pharmacies because nothing here belongs to one. The other half of that
--   bargain: tenant users never write to these tables. Only platform processes do - the
--   importer in tools/PMS.DataImport, and whatever refreshes the catalogue later.
--
--   A pharmacy's OWN catalogue, with its own prices and pack configuration, is the
--   tenant-scoped Medicine table. It copies from here; it is not this.
--
-- Requires: 001 (database). Independent of the tenant tables.
-- See: docs/data/medicine-reference-catalog.md
-- ============================================

USE [PMSDb]
GO

-- No filtered indexes below, but stating these keeps every script in this folder behaving
-- identically under sqlcmd, which defaults QUOTED_IDENTIFIER to OFF.
SET ANSI_NULLS ON
SET QUOTED_IDENTIFIER ON
GO

-- == Lookups =============================================================================

IF OBJECT_ID('[dbo].[CatalogManufacturers]', 'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[CatalogManufacturers]
    (
        [Id]    INT            NOT NULL IDENTITY(1,1),
        -- Display casing as first seen in the source. The unique index below is
        -- case-insensitive under the default collation, so "Beximco" and "BEXIMCO" cannot
        -- both exist; the importer also normalises before lookup rather than relying on it.
        [Name]  NVARCHAR(300)  NOT NULL,

        CONSTRAINT [PK_CatalogManufacturers] PRIMARY KEY CLUSTERED ([Id])
    )

    CREATE UNIQUE NONCLUSTERED INDEX [UX_CatalogManufacturers_Name]
        ON [dbo].[CatalogManufacturers] ([Name])

    PRINT 'Table [CatalogManufacturers] created successfully.'
END
ELSE
    PRINT 'Table [CatalogManufacturers] already exists.'
GO

IF OBJECT_ID('[dbo].[CatalogDrugClasses]', 'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[CatalogDrugClasses]
    (
        [Id]                INT            NOT NULL IDENTITY(1,1),
        [Name]              NVARCHAR(300)  NOT NULL,

        -- Whether this class was classified as antibacterial at import time.
        --
        -- Keyword-derived and NOT authoritative. The source classes are imprecise - both
        -- Linezolid and Clindamycin are filed under "Macrolides" - and some classes name an
        -- indication rather than a drug family, so "Anti-diarrhoeal Antimicrobial drugs"
        -- holds Ciprofloxacin. Every import writes a review report; see the docs.
        [IsAntibioticClass] BIT            NOT NULL CONSTRAINT [DF_CatalogDrugClasses_IsAntibioticClass] DEFAULT 0,

        CONSTRAINT [PK_CatalogDrugClasses] PRIMARY KEY CLUSTERED ([Id])
    )

    CREATE UNIQUE NONCLUSTERED INDEX [UX_CatalogDrugClasses_Name]
        ON [dbo].[CatalogDrugClasses] ([Name])

    PRINT 'Table [CatalogDrugClasses] created successfully.'
END
ELSE
    PRINT 'Table [CatalogDrugClasses] already exists.'
GO

IF OBJECT_ID('[dbo].[CatalogDosageForms]', 'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[CatalogDosageForms]
    (
        [Id]    INT            NOT NULL IDENTITY(1,1),
        [Name]  NVARCHAR(200)  NOT NULL,

        CONSTRAINT [PK_CatalogDosageForms] PRIMARY KEY CLUSTERED ([Id])
    )

    CREATE UNIQUE NONCLUSTERED INDEX [UX_CatalogDosageForms_Name]
        ON [dbo].[CatalogDosageForms] ([Name])

    PRINT 'Table [CatalogDosageForms] created successfully.'
END
ELSE
    PRINT 'Table [CatalogDosageForms] already exists.'
GO

-- == Generics ============================================================================

IF OBJECT_ID('[dbo].[CatalogGenerics]', 'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[CatalogGenerics]
    (
        [Id]                INT             NOT NULL IDENTITY(1,1),
        [Name]              NVARCHAR(500)   NOT NULL,

        -- Nullable because 61 of the 1,711 source generics carry no drug class at all - and
        -- one of them is Azithromycin, which is precisely why the antibiotic flag below is
        -- not derived from the class alone.
        [DrugClassId]       INT             NULL,

        [MonographUrl]      NVARCHAR(1000)  NULL,

        -- The short indication text from the source, e.g. "Ulcerative colitis". For browsing
        -- only; it is not clinical guidance and must never be presented as advice.
        [IndicationSummary] NVARCHAR(MAX)   NULL,

        -- Drives the regulatory module: employees cannot sell antibiotics, each such sale
        -- captures a prescription, and the antibiotic register is built from these rows.
        --
        -- REQUIRES HUMAN VERIFICATION. Derived from two weak signals (drug class name and
        -- generic name) because the source has no antibiotic field. AntibioticSignal records
        -- which evidence fired; 4 = ManuallyReviewed, which the importer leaves alone.
        [IsAntibiotic]      BIT             NOT NULL CONSTRAINT [DF_CatalogGenerics_IsAntibiotic] DEFAULT 0,
        [AntibioticSignal]  INT             NOT NULL CONSTRAINT [DF_CatalogGenerics_AntibioticSignal] DEFAULT 0,

        CONSTRAINT [PK_CatalogGenerics] PRIMARY KEY CLUSTERED ([Id]),

        CONSTRAINT [FK_CatalogGenerics_CatalogDrugClasses_DrugClassId]
            FOREIGN KEY ([DrugClassId]) REFERENCES [dbo].[CatalogDrugClasses] ([Id]),

        CONSTRAINT [CK_CatalogGenerics_AntibioticSignal] CHECK ([AntibioticSignal] IN (0,1,2,3,4))
    )

    CREATE UNIQUE NONCLUSTERED INDEX [UX_CatalogGenerics_Name]
        ON [dbo].[CatalogGenerics] ([Name])

    -- Tenants browse by ingredient, and the register filters on the flag.
    CREATE NONCLUSTERED INDEX [IX_CatalogGenerics_IsAntibiotic]
        ON [dbo].[CatalogGenerics] ([IsAntibiotic])

    CREATE NONCLUSTERED INDEX [IX_CatalogGenerics_DrugClassId]
        ON [dbo].[CatalogGenerics] ([DrugClassId])

    PRINT 'Table [CatalogGenerics] created successfully.'
END
ELSE
    PRINT 'Table [CatalogGenerics] already exists.'
GO

-- == Medicines ===========================================================================

IF OBJECT_ID('[dbo].[CatalogMedicines]', 'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[CatalogMedicines]
    (
        [Id]              INT             NOT NULL IDENTITY(1,1),

        -- The product id from the source dataset, and the key the importer upserts on.
        --
        -- Not in the original specification; added because the proposed natural key does not
        -- hold. Brand + strength + manufacturer collapses 532 of the 21,714 source rows, and
        -- adding dosage form still collapses 66: Glarine 100 IU/ml by ACI genuinely exists
        -- as a cartridge, a vial and a biopen at three different prices. Those are three
        -- products, not one row imported three times.
        [SourceBrandId]   INT             NOT NULL,

        [BrandName]       NVARCHAR(300)   NOT NULL,
        [GenericId]       INT             NULL,
        [ManufacturerId]  INT             NULL,
        [DosageFormId]    INT             NULL,

        -- e.g. "500 mg", "(10 mg+30 mg+1.25 mg)/5 ml". Blank on 849 source rows.
        [Strength]        NVARCHAR(300)   NULL,

        -- "allopathic" or "herbal" in this dataset. Kept as found.
        [MedicineType]    NVARCHAR(50)    NULL,

        -- Raw pack text, verbatim. Deliberately not parsed into pieces-per-strip or
        -- strips-per-box: the source has no reliable pack structure, and a guessed one would
        -- look authoritative to every tenant. Pack configuration is per-tenant.
        [PackageInfo]     NVARCHAR(1000)  NULL,

        -- Scraped unit price in BDT. REFERENCE ONLY - never billing, margin or profit. It is
        -- a point-in-time scrape, already stale, and a selling price belongs to the pharmacy.
        -- It exists so a pharmacy browsing the catalogue recognises a product by roughly the
        -- price it expects.
        [SourceUnitPrice] DECIMAL(18,4)   NULL,

        -- Lets a later refresh retire a discontinued product without deleting a row that a
        -- tenant may already have imported from.
        [IsActive]        BIT             NOT NULL CONSTRAINT [DF_CatalogMedicines_IsActive] DEFAULT 1,

        [ImportedAt]      DATETIME2       NOT NULL,
        [LastUpdatedAt]   DATETIME2       NOT NULL,

        CONSTRAINT [PK_CatalogMedicines] PRIMARY KEY CLUSTERED ([Id]),

        CONSTRAINT [FK_CatalogMedicines_CatalogGenerics_GenericId]
            FOREIGN KEY ([GenericId]) REFERENCES [dbo].[CatalogGenerics] ([Id]),

        CONSTRAINT [FK_CatalogMedicines_CatalogManufacturers_ManufacturerId]
            FOREIGN KEY ([ManufacturerId]) REFERENCES [dbo].[CatalogManufacturers] ([Id]),

        CONSTRAINT [FK_CatalogMedicines_CatalogDosageForms_DosageFormId]
            FOREIGN KEY ([DosageFormId]) REFERENCES [dbo].[CatalogDosageForms] ([Id])
    )

    -- The upsert key. Unique, so a second import can only ever update.
    CREATE UNIQUE NONCLUSTERED INDEX [UX_CatalogMedicines_SourceBrandId]
        ON [dbo].[CatalogMedicines] ([SourceBrandId])

    -- The search a tenant runs constantly while importing: type "nap", get Napa. A B-tree
    -- index serves LIKE 'nap%' as a seek. It cannot serve '%nap%' - if infix search is ever
    -- needed, that is what a full-text index would be for. The INCLUDE list makes the search
    -- result grid covered, so the seek needs no key lookups.
    CREATE NONCLUSTERED INDEX [IX_CatalogMedicines_BrandName]
        ON [dbo].[CatalogMedicines] ([BrandName])
        INCLUDE ([GenericId], [ManufacturerId], [DosageFormId], [Strength], [SourceUnitPrice])

    CREATE NONCLUSTERED INDEX [IX_CatalogMedicines_GenericId]
        ON [dbo].[CatalogMedicines] ([GenericId])

    PRINT 'Table [CatalogMedicines] created successfully.'
END
ELSE
    PRINT 'Table [CatalogMedicines] already exists.'
GO

PRINT ''
PRINT 'Catalog tables ready. Load them with:'
PRINT '  dotnet run --project tools/PMS.DataImport -- --source <folder> --dry-run'
PRINT '  dotnet run --project tools/PMS.DataImport -- --source <folder>'
GO
