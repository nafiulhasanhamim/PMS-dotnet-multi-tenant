-- ============================================
-- Script: 008_CreateBatchesAndStockAdjustmentsTables.sql
-- Description: Physical stock. Batches (one delivery each) and the audit trail of every
--              non-sale change to their quantities.
--
-- WHY COST AND EXPIRY LIVE ON THE BATCH AND NOT ON THE PRODUCT
--
--   A pharmacy holds Napa 500 bought in March at 0.80 expiring next January, and more of it
--   bought in July at 0.85 expiring the following June. Those are two different physical
--   things on the same shelf. A single cost column on Products would force one answer to
--   "what did this cost", and margin would be computed against whichever price was entered
--   last. A single expiry column would either cry wolf about stock that is fine or stay
--   silent about stock that is not.
--
-- QUANTITIES ARE INTEGER BASE UNITS
--
--   Pieces for tablets, bottles for handwash, bags for saline. Nothing in these tables knows
--   which - the unit vocabulary belongs to Products, and the conversion from what a person
--   typed ("2 cartons") happens in the application layer before the insert. Prices are
--   DECIMAL(18,4), never float: money and floating point do not belong in the same column.
--
-- QUANTITY CANNOT CHANGE WITHOUT AN AUDIT ROW
--
--   Batch.Adjust in the domain mutates the quantity and returns the StockAdjustment that
--   explains it, so there is no code path that produces one without the other, and the
--   handler writes both in one transaction. The database cannot enforce "these two happen
--   together" - a trigger could, at the cost of hiding the rule from everyone reading the
--   C#. What it does enforce is the part that matters most: quantity never goes negative.
--
-- TENANT SCOPED
--
--   Both tables carry TenantId. Batch and StockAdjustment implement ITenantEntity, so EF
--   filters every read to the current pharmacy - including the aggregates behind the stock
--   list - and stamps TenantId on insert.
--
-- Requires: 002 (Tenants), 003 (Users), 007 (Products).
-- See: docs/03-batches-and-stock.md
-- ============================================

USE [PMSDb]
GO

-- Filtered and unique indexes below; sqlcmd defaults QUOTED_IDENTIFIER to OFF and would
-- create the tables while silently skipping them.
SET ANSI_NULLS ON
SET QUOTED_IDENTIFIER ON
GO

IF OBJECT_ID('[dbo].[Batches]', 'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[Batches]
    (
        [Id]                        UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID(),

        -- The owning pharmacy. Stamped by TenantEntityInterceptor, never by a handler.
        [TenantId]                  UNIQUEIDENTIFIER NOT NULL,

        [ProductId]                 UNIQUEIDENTIFIER NOT NULL,

        -- The manufacturer's number off the pack, not generated here: it is how a recall
        -- notice identifies stock, so it has to be what the pack says.
        [BatchNumber]               NVARCHAR(100)    NOT NULL,

        -- DATE, not DATETIME2. A pack prints a month and a year; nothing about an expiry
        -- happens at a particular time of day, and a time component would be one more thing
        -- a time-zone conversion could shift across a day boundary.
        --
        -- NULL is allowed because diapers, syringes and dressings genuinely do not expire.
        -- The rule that a MEDICINE must have one is enforced in validation and not here: it
        -- depends on Products.ProductType, which a CHECK constraint cannot see.
        [ExpiryDate]                DATE             NULL,
        [ManufactureDate]           DATE             NULL,

        [PurchasePricePerBaseUnit]  DECIMAL(18,4)    NOT NULL,

        -- What the batch holds now.
        [QuantityInBaseUnits]       INT              NOT NULL
            CONSTRAINT [DF_Batches_QuantityInBaseUnits] DEFAULT 0,

        -- What it held when it arrived. Set once at insert and never updated by anything.
        --
        -- It is the denominator: turnover, wastage as a share of a delivery, and "did this
        -- batch sell before it expired" all need to know how much arrived, by which time the
        -- live quantity has been reduced by every sale and write-off. Stock arriving later is
        -- a NEW BATCH, with its own expiry and cost - never an increase to this figure.
        [InitialQuantityInBaseUnits] INT             NOT NULL,

        -- No foreign key yet: the Supplier entity arrives in Module 4. The column is here now
        -- so that migration adds a constraint to existing rows rather than adding a column to
        -- a table already full of rows that cannot populate it.
        [SupplierId]                UNIQUEIDENTIFIER NULL,

        -- What was written on the delivery note. Kept even after SupplierId becomes real.
        [SupplierNameText]          NVARCHAR(200)    NULL,

        [Notes]                     NVARCHAR(1000)   NULL,

        -- For hiding a row entered in error. NOT depletion: a sold-out batch stays active and
        -- stays visible, because it is the cost and expiry behind sales that already happened.
        [IsActive]                  BIT              NOT NULL
            CONSTRAINT [DF_Batches_IsActive] DEFAULT 1,

        -- Audit (IAuditable)
        [CreatedOnUtc]              DATETIME2        NOT NULL,
        [CreatedBy]                 NVARCHAR(256)    NULL,
        [ModifiedOnUtc]             DATETIME2        NULL,
        [ModifiedBy]                NVARCHAR(256)    NULL,

        -- Declared on AggregateRoot but not configured as a concurrency token, so EF selects
        -- it on every read and the column has to exist. See the note in script 003.
        [RowVersion]                VARBINARY(MAX)   NULL,

        CONSTRAINT [PK_Batches] PRIMARY KEY CLUSTERED ([Id]),

        CONSTRAINT [FK_Batches_Tenants_TenantId]
            FOREIGN KEY ([TenantId]) REFERENCES [dbo].[Tenants] ([Id]),

        -- Restrict, not cascade. Deleting a product out from under its stock would erase the
        -- record of what was bought and sold; the product's own delete is a soft one anyway.
        CONSTRAINT [FK_Batches_Products_ProductId]
            FOREIGN KEY ([ProductId]) REFERENCES [dbo].[Products] ([Id]),

        -- THE constraint of this module. Stock cannot go negative, and this is the last line
        -- behind the application check and the domain guard: a bug in a future deduction path,
        -- or somebody with a SQL prompt, is refused here.
        CONSTRAINT [CK_Batches_QuantityNotNegative] CHECK ([QuantityInBaseUnits] >= 0),

        -- A batch that arrived empty is not a delivery. Zero would also break every turnover
        -- calculation that divides by it.
        CONSTRAINT [CK_Batches_InitialQuantityPositive] CHECK ([InitialQuantityInBaseUnits] > 0),

        CONSTRAINT [CK_Batches_PurchasePriceNotNegative]
            CHECK ([PurchasePricePerBaseUnit] >= 0),

        -- Only checked when both dates are present. A pack that shows only an expiry is
        -- normal and must stay enterable.
        CONSTRAINT [CK_Batches_ManufactureBeforeExpiry] CHECK (
            [ManufactureDate] IS NULL
         OR [ExpiryDate] IS NULL
         OR [ManufactureDate] < [ExpiryDate]
        )
    )

    -- Batch number unique PER PRODUCT PER PHARMACY.
    --
    -- Not filtered and not conditional on quantity, deliberately. A depleted batch keeps its
    -- number reserved, because a recall notice names a batch number and "recall B-100 of
    -- Napa" has to resolve to exactly one row - including a row that sold out, which is
    -- precisely the stock that has already left the shop and may need chasing. The duplicate
    -- policy and the alternative that was rejected are in docs/03-batches-and-stock.md.
    CREATE UNIQUE NONCLUSTERED INDEX [UX_Batches_Tenant_Product_BatchNumber]
        ON [dbo].[Batches] ([TenantId], [ProductId], [BatchNumber])

    -- The FEFO index. TenantId leads, so the seek lands inside one pharmacy before it does
    -- anything else, then ProductId, then expiry.
    --
    -- It does not remove the sort: the ordering puts NULL expiries LAST with an explicit key
    -- and an index cannot be read in that order. It does not need to - a product holds a
    -- handful of live batches. What the index is for is finding those rows without touching
    -- another product's, and answering the stock list's expiry filters without scanning every
    -- batch in the pharmacy.
    CREATE NONCLUSTERED INDEX [IX_Batches_Tenant_Product_ExpiryDate]
        ON [dbo].[Batches] ([TenantId], [ProductId], [ExpiryDate])
        INCLUDE ([QuantityInBaseUnits], [IsActive], [BatchNumber],
                 [PurchasePricePerBaseUnit], [InitialQuantityInBaseUnits])

    PRINT 'Table [Batches] created successfully.'
END
ELSE
    PRINT 'Table [Batches] already exists.'
GO

IF OBJECT_ID('[dbo].[StockAdjustments]', 'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[StockAdjustments]
    (
        [Id]                        UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID(),

        [TenantId]                  UNIQUEIDENTIFIER NOT NULL,

        [BatchId]                   UNIQUEIDENTIFIER NOT NULL,

        -- 0 Add, 1 Remove, 2 Correction. A SALE IS NOT ONE OF THESE: sales deduct stock in
        -- the billing module and are audited by their own sale lines, which carry the price
        -- charged and the customer. Folding them in would produce two competing records of
        -- one event and drown the handful of entries that actually need explaining.
        [AdjustmentType]            INT              NOT NULL,

        -- The change, signed: positive to add, negative to remove. Stored as the delta rather
        -- than the new total because the delta is what sums - "how much did we write off this
        -- quarter" is one SUM over this column.
        [QuantityChangeInBaseUnits] INT              NOT NULL,

        -- What the batch held immediately afterwards. Redundant with the deltas and kept
        -- anyway: replaying deltas to answer "what did this batch hold in March" only works
        -- if no row is ever missing, and this column is what makes a gap visible instead of
        -- silently shifting every later figure.
        [QuantityAfterInBaseUnits]  INT              NOT NULL,

        -- Required. Free text, because a fixed list gets filled in as "Other" for exactly the
        -- cases that matter; the form offers quick-pick buttons for the common ones.
        [Reason]                    NVARCHAR(500)    NOT NULL,

        -- Who did it. The column an owner follows when a pattern of write-offs points one way.
        [AdjustedByUserId]          UNIQUEIDENTIFIER NOT NULL,

        -- Audit (IAuditable)
        [CreatedOnUtc]              DATETIME2        NOT NULL,
        [CreatedBy]                 NVARCHAR(256)    NULL,
        [ModifiedOnUtc]             DATETIME2        NULL,
        [ModifiedBy]                NVARCHAR(256)    NULL,

        [RowVersion]                VARBINARY(MAX)   NULL,

        CONSTRAINT [PK_StockAdjustments] PRIMARY KEY CLUSTERED ([Id]),

        CONSTRAINT [FK_StockAdjustments_Tenants_TenantId]
            FOREIGN KEY ([TenantId]) REFERENCES [dbo].[Tenants] ([Id]),

        -- Cascade here, unlike everywhere else in this schema. An adjustment has no meaning
        -- without the batch it adjusted, so an orphan would be an audit row nobody could
        -- interpret. Nothing deletes a batch today; this settles what happens if anything
        -- ever does.
        CONSTRAINT [FK_StockAdjustments_Batches_BatchId]
            FOREIGN KEY ([BatchId]) REFERENCES [dbo].[Batches] ([Id]) ON DELETE CASCADE,

        -- No foreign key to Users, matching the entity: Users is a global table with no
        -- TenantId, and this is a tenant-scoped row. The reference is by id and the name is
        -- resolved by an explicit join in the query, which keeps that crossing visible.

        CONSTRAINT [CK_StockAdjustments_AdjustmentType]
            CHECK ([AdjustmentType] BETWEEN 0 AND 2),

        -- An adjustment of zero records that nothing happened. It is noise in the one history
        -- that has to stay readable, and the application refuses it too.
        CONSTRAINT [CK_StockAdjustments_ChangeNotZero]
            CHECK ([QuantityChangeInBaseUnits] <> 0),

        -- The resulting quantity is subject to the same floor as the batch itself.
        CONSTRAINT [CK_StockAdjustments_QuantityAfterNotNegative]
            CHECK ([QuantityAfterInBaseUnits] >= 0),

        CONSTRAINT [CK_StockAdjustments_ReasonNotBlank]
            CHECK (LEN(LTRIM(RTRIM([Reason]))) > 0)
    )

    -- The history query: one batch, newest first. CreatedOnUtc is in the key so the ordering
    -- is served by the index rather than sorted afterwards.
    CREATE NONCLUSTERED INDEX [IX_StockAdjustments_Tenant_Batch_CreatedOnUtc]
        ON [dbo].[StockAdjustments] ([TenantId], [BatchId], [CreatedOnUtc] DESC)
        INCLUDE ([AdjustmentType], [QuantityChangeInBaseUnits],
                 [QuantityAfterInBaseUnits], [Reason], [AdjustedByUserId])

    PRINT 'Table [StockAdjustments] created successfully.'
END
ELSE
    PRINT 'Table [StockAdjustments] already exists.'
GO
