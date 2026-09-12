/* =============================================================================================
   013_CreateSupplierAndPurchaseTables.sql — Module 4: Suppliers & Purchase Management

   Six objects:

     Suppliers          who the pharmacy buys from
     Purchases          one delivery against one bill
     PurchaseLines      one product out of one batch on that bill
     SupplierPayments   money going out, against a bill or against the account
     PurchaseReturns    goods going back, against one line
     PurchaseSequences  a per-pharmacy counter, so purchase numbers are sequential and unique

   This script also completes a promise Module 3 left in place: Batches.SupplierId has existed
   as a nullable column with no foreign key since 008, explicitly so that this migration could
   add a constraint rather than a column. It does that at the end.

   Four things worth reading rather than skimming:

     1. NO BALANCE COLUMN EXISTS, on Suppliers or anywhere else. What is owed is the sum of
        purchases, less returns, less payments — three tables that move independently. A stored
        total would be wrong the moment any one of them changed without it. See
        ISupplierBalanceQueries, which is the only place that arithmetic lives.

     2. Money precision is not uniform, on purpose, and follows 011's rule. Amounts that are
        cash — TotalAmount, AmountPaid, LineTotal, payment Amount, ReturnAmount — are
        DECIMAL(18,2). PurchasePricePerBaseUnit is DECIMAL(18,4), matching Batches and Products,
        because a per-unit price derived from a pack genuinely is fractional.

     3. PurchaseLines duplicates the batch's quantity and cost deliberately. The batch records
        what is on the shelf now and falls as stock sells; the line records what the supplier
        invoiced, which must never move. See the entity.

     4. Every unique index is on (TenantId, ...). Two pharmacies both have a PUR-000001.

   Idempotent, like every script here: safe to run against a database that already has some of
   this.
   ============================================================================================= */

USE [PMSDb];
GO

/* ── Suppliers ─────────────────────────────────────────────────────────────────────────── */

IF OBJECT_ID(N'[dbo].[Suppliers]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[Suppliers]
    (
        [Id]              UNIQUEIDENTIFIER NOT NULL CONSTRAINT [PK_Suppliers] PRIMARY KEY,
        [TenantId]        UNIQUEIDENTIFIER NOT NULL,

        [Name]            NVARCHAR(200)    NOT NULL,
        [Phone]           NVARCHAR(40)     NOT NULL,
        [ContactPerson]   NVARCHAR(200)    NULL,
        [Email]           NVARCHAR(256)    NULL,
        [Address]         NVARCHAR(500)    NULL,
        [Company]         NVARCHAR(200)    NULL,

        /* Soft delete. A supplier is referenced by every purchase ever recorded against them,
           so the row is hidden, never removed. */
        [IsActive]        BIT              NOT NULL CONSTRAINT [DF_Suppliers_IsActive] DEFAULT (1),

        [CreatedBy]       NVARCHAR(256)    NULL,
        [CreatedOnUtc]    DATETIME2(7)     NOT NULL,
        [ModifiedBy]      NVARCHAR(256)    NULL,
        [ModifiedOnUtc]   DATETIME2(7)     NULL,
        [RowVersion]      VARBINARY(MAX)   NULL,

        CONSTRAINT [FK_Suppliers_Tenants] FOREIGN KEY ([TenantId])
            REFERENCES [dbo].[Tenants] ([Id])
    );

    PRINT 'Created table Suppliers';
END
ELSE
    PRINT 'Table Suppliers already exists';
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Suppliers_Tenant_Name')
    CREATE INDEX [IX_Suppliers_Tenant_Name]
        ON [dbo].[Suppliers] ([TenantId], [Name]);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Suppliers_Tenant_Phone')
    CREATE INDEX [IX_Suppliers_Tenant_Phone]
        ON [dbo].[Suppliers] ([TenantId], [Phone]);
GO

/* ── Purchases ─────────────────────────────────────────────────────────────────────────── */

IF OBJECT_ID(N'[dbo].[Purchases]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[Purchases]
    (
        [Id]                UNIQUEIDENTIFIER NOT NULL CONSTRAINT [PK_Purchases] PRIMARY KEY,
        [TenantId]          UNIQUEIDENTIFIER NOT NULL,

        [SupplierId]        UNIQUEIDENTIFIER NOT NULL,
        [PurchaseNumber]    NVARCHAR(40)     NOT NULL,

        /* DATE, not DATETIME2. A delivery is booked in against a day — often the day after it
           physically arrived — and a time of day would be invented precision. */
        [PurchaseDate]      DATE             NOT NULL,

        /* The sum of the line totals, frozen at save. Safe to store precisely because it is a
           fact about a bill that was issued once and cannot change; the supplier's BALANCE is
           not stored, because that moves every time a payment or return happens. */
        [TotalAmount]       DECIMAL(18, 2)   NOT NULL,

        /* What has been paid against THIS bill. General payments do not touch it — see the
           entity and the module doc. */
        [AmountPaid]        DECIMAL(18, 2)   NOT NULL CONSTRAINT [DF_Purchases_AmountPaid] DEFAULT (0),

        [Notes]             NVARCHAR(1000)   NULL,
        [CreatedByUserId]   UNIQUEIDENTIFIER NOT NULL,

        [CreatedBy]         NVARCHAR(256)    NULL,
        [CreatedOnUtc]      DATETIME2(7)     NOT NULL,
        [ModifiedBy]        NVARCHAR(256)    NULL,
        [ModifiedOnUtc]     DATETIME2(7)     NULL,
        [RowVersion]        VARBINARY(MAX)   NULL,

        CONSTRAINT [FK_Purchases_Tenants] FOREIGN KEY ([TenantId])
            REFERENCES [dbo].[Tenants] ([Id]),

        /* NO ACTION, not CASCADE: a supplier is soft-deleted and never removed, so this should
           never fire — and if anything ever tried, taking the purchase history with it is the
           worst available outcome. */
        CONSTRAINT [FK_Purchases_Suppliers] FOREIGN KEY ([SupplierId])
            REFERENCES [dbo].[Suppliers] ([Id]),

        CONSTRAINT [FK_Purchases_Users] FOREIGN KEY ([CreatedByUserId])
            REFERENCES [dbo].[Users] ([Id]),

        /* A bill for nothing is not a bill. Negative would mean a credit note, which is what a
           purchase return is for. */
        CONSTRAINT [CK_Purchases_TotalAmount] CHECK ([TotalAmount] >= 0)
    );

    PRINT 'Created table Purchases';
END
ELSE
    PRINT 'Table Purchases already exists';
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_Purchases_Tenant_PurchaseNumber')
    CREATE UNIQUE INDEX [UX_Purchases_Tenant_PurchaseNumber]
        ON [dbo].[Purchases] ([TenantId], [PurchaseNumber]);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Purchases_Tenant_PurchaseDate')
    CREATE INDEX [IX_Purchases_Tenant_PurchaseDate]
        ON [dbo].[Purchases] ([TenantId], [PurchaseDate]);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Purchases_Tenant_Supplier')
    CREATE INDEX [IX_Purchases_Tenant_Supplier]
        ON [dbo].[Purchases] ([TenantId], [SupplierId]);
GO

/* ── PurchaseLines ─────────────────────────────────────────────────────────────────────── */

IF OBJECT_ID(N'[dbo].[PurchaseLines]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[PurchaseLines]
    (
        [Id]                          UNIQUEIDENTIFIER NOT NULL CONSTRAINT [PK_PurchaseLines] PRIMARY KEY,
        [TenantId]                    UNIQUEIDENTIFIER NOT NULL,

        [PurchaseId]                  UNIQUEIDENTIFIER NOT NULL,
        [ProductId]                   UNIQUEIDENTIFIER NOT NULL,

        /* The batch this delivery became. Set in the same transaction that created it. */
        [BatchId]                     UNIQUEIDENTIFIER NOT NULL,

        /* Duplicates the batch's figures ON PURPOSE — see the header note and the entity. */
        [QuantityInBaseUnits]         INT              NOT NULL,
        [PurchasePricePerBaseUnit]    DECIMAL(18, 4)   NOT NULL,
        [LineTotal]                   DECIMAL(18, 2)   NOT NULL,

        [CreatedBy]                   NVARCHAR(256)    NULL,
        [CreatedOnUtc]                DATETIME2(7)     NOT NULL,
        [ModifiedBy]                  NVARCHAR(256)    NULL,
        [ModifiedOnUtc]               DATETIME2(7)     NULL,
        [RowVersion]                  VARBINARY(MAX)   NULL,

        CONSTRAINT [FK_PurchaseLines_Tenants] FOREIGN KEY ([TenantId])
            REFERENCES [dbo].[Tenants] ([Id]),

        CONSTRAINT [FK_PurchaseLines_Purchases] FOREIGN KEY ([PurchaseId])
            REFERENCES [dbo].[Purchases] ([Id]) ON DELETE CASCADE,

        CONSTRAINT [FK_PurchaseLines_Products] FOREIGN KEY ([ProductId])
            REFERENCES [dbo].[Products] ([Id]),

        /* No cascade: deleting a batch out from under a purchase line would erase the record of
           what was delivered and invoiced. */
        CONSTRAINT [FK_PurchaseLines_Batches] FOREIGN KEY ([BatchId])
            REFERENCES [dbo].[Batches] ([Id]),

        CONSTRAINT [CK_PurchaseLines_Quantity] CHECK ([QuantityInBaseUnits] > 0),
        CONSTRAINT [CK_PurchaseLines_Price] CHECK ([PurchasePricePerBaseUnit] >= 0)
    );

    PRINT 'Created table PurchaseLines';
END
ELSE
    PRINT 'Table PurchaseLines already exists';
GO

/* Module 6's retrofit asks "did this batch come from a purchase?" once per row on the expired
   stock page. Without this index that is a scan of every purchase line the pharmacy holds. */
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_PurchaseLines_Tenant_Batch')
    CREATE INDEX [IX_PurchaseLines_Tenant_Batch]
        ON [dbo].[PurchaseLines] ([TenantId], [BatchId]);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_PurchaseLines_Tenant_Product')
    CREATE INDEX [IX_PurchaseLines_Tenant_Product]
        ON [dbo].[PurchaseLines] ([TenantId], [ProductId]);
GO

/* ── SupplierPayments ──────────────────────────────────────────────────────────────────── */

IF OBJECT_ID(N'[dbo].[SupplierPayments]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[SupplierPayments]
    (
        [Id]                 UNIQUEIDENTIFIER NOT NULL CONSTRAINT [PK_SupplierPayments] PRIMARY KEY,
        [TenantId]           UNIQUEIDENTIFIER NOT NULL,

        [SupplierId]         UNIQUEIDENTIFIER NOT NULL,

        /* NULL means a general payment against the account rather than against one bill. It
           reduces the supplier's balance and modifies no individual purchase. */
        [PurchaseId]         UNIQUEIDENTIFIER NULL,

        [Amount]             DECIMAL(18, 2)   NOT NULL,
        [PaymentDate]        DATE             NOT NULL,

        /* Free text rather than an enum: the column costs nothing today and adding bKash or a
           bank transfer later should not need a migration. The UI offers Cash. */
        [PaymentMethod]      NVARCHAR(40)     NOT NULL,
        [Notes]              NVARCHAR(1000)   NULL,
        [RecordedByUserId]   UNIQUEIDENTIFIER NOT NULL,

        [CreatedBy]          NVARCHAR(256)    NULL,
        [CreatedOnUtc]       DATETIME2(7)     NOT NULL,
        [ModifiedBy]         NVARCHAR(256)    NULL,
        [ModifiedOnUtc]      DATETIME2(7)     NULL,
        [RowVersion]         VARBINARY(MAX)   NULL,

        CONSTRAINT [FK_SupplierPayments_Tenants] FOREIGN KEY ([TenantId])
            REFERENCES [dbo].[Tenants] ([Id]),

        CONSTRAINT [FK_SupplierPayments_Suppliers] FOREIGN KEY ([SupplierId])
            REFERENCES [dbo].[Suppliers] ([Id]),

        CONSTRAINT [FK_SupplierPayments_Purchases] FOREIGN KEY ([PurchaseId])
            REFERENCES [dbo].[Purchases] ([Id]),

        CONSTRAINT [FK_SupplierPayments_Users] FOREIGN KEY ([RecordedByUserId])
            REFERENCES [dbo].[Users] ([Id]),

        /* Money going the other way is a purchase return, not a negative payment. */
        CONSTRAINT [CK_SupplierPayments_Amount] CHECK ([Amount] > 0)
    );

    PRINT 'Created table SupplierPayments';
END
ELSE
    PRINT 'Table SupplierPayments already exists';
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_SupplierPayments_Tenant_Supplier')
    CREATE INDEX [IX_SupplierPayments_Tenant_Supplier]
        ON [dbo].[SupplierPayments] ([TenantId], [SupplierId]);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_SupplierPayments_Tenant_Purchase')
    CREATE INDEX [IX_SupplierPayments_Tenant_Purchase]
        ON [dbo].[SupplierPayments] ([TenantId], [PurchaseId]);
GO

/* ── PurchaseReturns ───────────────────────────────────────────────────────────────────── */

IF OBJECT_ID(N'[dbo].[PurchaseReturns]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[PurchaseReturns]
    (
        [Id]                    UNIQUEIDENTIFIER NOT NULL CONSTRAINT [PK_PurchaseReturns] PRIMARY KEY,
        [TenantId]              UNIQUEIDENTIFIER NOT NULL,

        /* A return targets a LINE, not a purchase: a delivery of six products is returned one
           product at a time, and the credit depends on what that specific line cost. */
        [PurchaseLineId]        UNIQUEIDENTIFIER NOT NULL,
        [BatchId]               UNIQUEIDENTIFIER NOT NULL,

        [QuantityInBaseUnits]   INT              NOT NULL,
        [Reason]                NVARCHAR(500)    NOT NULL,
        [ReturnAmount]          DECIMAL(18, 2)   NOT NULL,
        [ReturnedByUserId]      UNIQUEIDENTIFIER NOT NULL,

        [CreatedBy]             NVARCHAR(256)    NULL,
        [CreatedOnUtc]          DATETIME2(7)     NOT NULL,
        [ModifiedBy]            NVARCHAR(256)    NULL,
        [ModifiedOnUtc]         DATETIME2(7)     NULL,
        [RowVersion]            VARBINARY(MAX)   NULL,

        CONSTRAINT [FK_PurchaseReturns_Tenants] FOREIGN KEY ([TenantId])
            REFERENCES [dbo].[Tenants] ([Id]),

        CONSTRAINT [FK_PurchaseReturns_PurchaseLines] FOREIGN KEY ([PurchaseLineId])
            REFERENCES [dbo].[PurchaseLines] ([Id]),

        CONSTRAINT [FK_PurchaseReturns_Batches] FOREIGN KEY ([BatchId])
            REFERENCES [dbo].[Batches] ([Id]),

        CONSTRAINT [FK_PurchaseReturns_Users] FOREIGN KEY ([ReturnedByUserId])
            REFERENCES [dbo].[Users] ([Id]),

        CONSTRAINT [CK_PurchaseReturns_Quantity] CHECK ([QuantityInBaseUnits] > 0)
    );

    PRINT 'Created table PurchaseReturns';
END
ELSE
    PRINT 'Table PurchaseReturns already exists';
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_PurchaseReturns_Tenant_PurchaseLine')
    CREATE INDEX [IX_PurchaseReturns_Tenant_PurchaseLine]
        ON [dbo].[PurchaseReturns] ([TenantId], [PurchaseLineId]);
GO

/* ── PurchaseSequences ─────────────────────────────────────────────────────────────────────

   One row per pharmacy holding the next purchase number to hand out. Deliberately NOT mapped as
   an EF entity, for the same reasons as InvoiceSequences in 011: it carries no business data,
   only PurchaseNumberGenerator touches it, and mapping it would give it a global tenant query
   filter that the generator's SQL would then have to bypass — establishing exactly the pattern
   worth avoiding. The tenant id is an explicit parameter on every statement there instead.

   Allocation is a single atomic statement:

       UPDATE dbo.PurchaseSequences SET NextNumber = NextNumber + 1
       OUTPUT deleted.NextNumber AS [Value]
       WHERE TenantId = @tenantId

   which reads and writes under one update lock. Naive count-of-purchases + 1 would hand two
   simultaneous deliveries the same number, and the unique index above would then reject one of
   them — losing a real purchase to a race.
   ─────────────────────────────────────────────────────────────────────────────────────────── */

IF OBJECT_ID(N'[dbo].[PurchaseSequences]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[PurchaseSequences]
    (
        [TenantId]     UNIQUEIDENTIFIER NOT NULL CONSTRAINT [PK_PurchaseSequences] PRIMARY KEY,
        [NextNumber]   BIGINT           NOT NULL CONSTRAINT [DF_PurchaseSequences_NextNumber] DEFAULT (1),

        CONSTRAINT [FK_PurchaseSequences_Tenants] FOREIGN KEY ([TenantId])
            REFERENCES [dbo].[Tenants] ([Id])
    );

    PRINT 'Created table PurchaseSequences';
END
ELSE
    PRINT 'Table PurchaseSequences already exists';
GO

/* ── Retrofit: the foreign key Module 3 left room for ──────────────────────────────────────

   Batches.SupplierId has been a nullable UNIQUEIDENTIFIER with no constraint since 008, and the
   entity says why: "No foreign key yet — the Supplier entity arrives in Module 4 and the column
   is here so that migration adds a constraint rather than a column."

   This is that migration.

   WITH NOCHECK, and that is the whole point of the exercise. Existing rows may carry a
   SupplierId that points at nothing, or more commonly carry NULL with a free-text name in
   SupplierNameText. Historical free text is NOT matched to supplier records — see the module
   doc's "known historical-data gap". NOCHECK validates nothing already there and enforces the
   constraint from here on, which is exactly the intent: new batches reference a real supplier,
   old ones keep whatever they had.
   ─────────────────────────────────────────────────────────────────────────────────────────── */

IF OBJECT_ID(N'[dbo].[Batches]', N'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Batches_Suppliers')
BEGIN
    /* Any SupplierId already present that does not name a real supplier is cleared first.
       There should be none — nothing has ever written the column — and leaving one behind would
       make the constraint unverifiable forever. */
    UPDATE b
    SET [SupplierId] = NULL
    FROM [dbo].[Batches] AS b
    WHERE b.[SupplierId] IS NOT NULL
      AND NOT EXISTS (
          SELECT 1 FROM [dbo].[Suppliers] AS s WHERE s.[Id] = b.[SupplierId]);

    ALTER TABLE [dbo].[Batches] WITH NOCHECK
        ADD CONSTRAINT [FK_Batches_Suppliers] FOREIGN KEY ([SupplierId])
            REFERENCES [dbo].[Suppliers] ([Id]);

    PRINT 'Added FK_Batches_Suppliers';
END
ELSE
    PRINT 'FK_Batches_Suppliers already exists (or Batches is missing)';
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Batches_Tenant_Supplier')
    CREATE INDEX [IX_Batches_Tenant_Supplier]
        ON [dbo].[Batches] ([TenantId], [SupplierId]);
GO

PRINT '013_CreateSupplierAndPurchaseTables.sql complete';
GO
