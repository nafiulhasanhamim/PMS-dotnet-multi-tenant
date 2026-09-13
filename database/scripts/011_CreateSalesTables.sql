/* =============================================================================================
   011_CreateSalesTables.sql — Module 5: Billing & Invoice

   Four tables:

     Sales             one completed transaction at the counter
     SaleLines         one product out of one batch; a FEFO split makes several per cart item
     SalesReturns      goods coming back against a line, with the refund that went with them
     InvoiceSequences  a per-pharmacy counter, so invoice numbers are sequential and unique

   Three things in here are worth reading rather than skimming:

     1. Money precision is not uniform, on purpose. Totals and refunds are DECIMAL(18,2) —
        they are cash, and a third of a paisa of change does not exist. Snapshot unit prices
        are DECIMAL(18,4), matching Products, because a price per base unit derived from a pack
        genuinely is fractional (a strip of three at 10 taka) and rounding the snapshot would
        make a historical invoice disagree with what was charged.

     2. SaleLines.UnitSalePrice, DiscountShare and NetLineTotal are stored, not computed. They
        are the record of what happened, and the arithmetic that produced them involves a
        rounding reconciliation across the whole sale — recomputing one line in isolation
        cannot reproduce it. See docs/05-billing-and-invoice.md.

     3. Every unique index is on (TenantId, ...). Two pharmacies both have an INV-000001.

   Idempotent, like every script here: safe to run against a database that already has some of
   this.
   ============================================================================================= */

USE [PMSDb];
GO

/* ── Sales ─────────────────────────────────────────────────────────────────────────────── */

IF OBJECT_ID(N'[dbo].[Sales]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[Sales]
    (
        [Id]                  UNIQUEIDENTIFIER NOT NULL CONSTRAINT [PK_Sales] PRIMARY KEY,
        [TenantId]            UNIQUEIDENTIFIER NOT NULL,

        [InvoiceNumber]       NVARCHAR(40)     NOT NULL,
        [SaleDate]            DATETIME2(7)     NOT NULL,
        [CashierUserId]       UNIQUEIDENTIFIER NOT NULL,

        [Subtotal]            DECIMAL(18, 2)   NOT NULL,
        [DiscountType]        INT              NULL,
        [DiscountValue]       DECIMAL(18, 2)   NULL,
        [DiscountAmount]      DECIMAL(18, 2)   NOT NULL CONSTRAINT [DF_Sales_DiscountAmount] DEFAULT (0),
        [NetTotal]            DECIMAL(18, 2)   NOT NULL,
        [CashReceived]        DECIMAL(18, 2)   NOT NULL,
        [ChangeGiven]         DECIMAL(18, 2)   NOT NULL,

        [Status]              INT              NOT NULL CONSTRAINT [DF_Sales_Status] DEFAULT (0),
        [CancelledReason]     NVARCHAR(500)    NULL,
        [CancelledByUserId]   UNIQUEIDENTIFIER NULL,
        [CancelledAt]         DATETIME2(7)     NULL,

        /* Plain text, no customer table. See the entity for why. */
        [CustomerName]        NVARCHAR(200)    NULL,
        [CustomerPhone]       NVARCHAR(40)     NULL,

        /* Populated only when the sale contained an antibiotic. Module 7's register reads
           these columns to find the sales it has to list. */
        [PatientName]         NVARCHAR(200)    NULL,
        [PatientPhone]        NVARCHAR(40)     NULL,
        [DoctorName]          NVARCHAR(200)    NULL,
        [PrescriptionNumber]  NVARCHAR(100)    NULL,
        [PrescriptionDate]    DATE             NULL,
        [PrescriptionVerified] BIT             NOT NULL CONSTRAINT [DF_Sales_PrescriptionVerified] DEFAULT (0),

        [CreatedOnUtc]        DATETIME2(7)     NOT NULL,
        [CreatedBy]           NVARCHAR(256)    NULL,
        [ModifiedOnUtc]       DATETIME2(7)     NULL,
        [ModifiedBy]          NVARCHAR(256)    NULL,

        -- Declared on AggregateRoot but not configured as a concurrency token, so EF selects it
        -- on every read and the column has to exist. Same as Products, Batches and the rest;
        -- see the note in script 003.
        [RowVersion]          VARBINARY(MAX)   NULL,

        CONSTRAINT [FK_Sales_Tenants]
            FOREIGN KEY ([TenantId]) REFERENCES [dbo].[Tenants] ([Id]),

        /* Restrict on the cashier: deleting the user would erase who rang up every sale they
           made, which is the first column an owner reads when a till does not balance. */
        CONSTRAINT [FK_Sales_Cashier]
            FOREIGN KEY ([CashierUserId]) REFERENCES [dbo].[Users] ([Id]),

        CONSTRAINT [FK_Sales_CancelledBy]
            FOREIGN KEY ([CancelledByUserId]) REFERENCES [dbo].[Users] ([Id]),

        /* The arithmetic, enforced at rest. Every one of these is also checked in the domain;
           these exist because a hand-written UPDATE does not go through the domain, and a sale
           whose columns disagree with each other is one nobody can reconcile afterwards. */
        CONSTRAINT [CK_Sales_SubtotalNotNegative]     CHECK ([Subtotal] >= 0),
        CONSTRAINT [CK_Sales_DiscountWithinSubtotal]  CHECK ([DiscountAmount] >= 0 AND [DiscountAmount] <= [Subtotal]),
        CONSTRAINT [CK_Sales_NetIsSubtotalLessDiscount] CHECK ([NetTotal] = [Subtotal] - [DiscountAmount]),
        CONSTRAINT [CK_Sales_CashCoversNet]           CHECK ([CashReceived] >= [NetTotal]),
        CONSTRAINT [CK_Sales_ChangeIsCashLessNet]     CHECK ([ChangeGiven] = [CashReceived] - [NetTotal]),

        /* A discount is both fields or neither. Half a discount is a client bug that would
           leave the invoice unable to say what the customer was promised. */
        CONSTRAINT [CK_Sales_DiscountFieldsTogether]
            CHECK (([DiscountType] IS NULL AND [DiscountValue] IS NULL)
                OR ([DiscountType] IS NOT NULL AND [DiscountValue] IS NOT NULL)),

        /* A cancellation is a reason, a person and a time, together or not at all. */
        CONSTRAINT [CK_Sales_CancellationComplete]
            CHECK (([Status] = 0 AND [CancelledReason] IS NULL AND [CancelledByUserId] IS NULL AND [CancelledAt] IS NULL)
                OR ([Status] = 1 AND [CancelledReason] IS NOT NULL AND [CancelledByUserId] IS NOT NULL AND [CancelledAt] IS NOT NULL))
    );

    PRINT 'Created table [Sales].';
END
ELSE
    PRINT 'Table [Sales] already exists.';
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_Sales_Tenant_InvoiceNumber')
BEGIN
    /* Per pharmacy. Also the backstop under InvoiceNumberGenerator: if its atomic allocation
       were ever replaced by a read-then-write, this turns a silent duplicate into a failure. */
    CREATE UNIQUE NONCLUSTERED INDEX [UX_Sales_Tenant_InvoiceNumber]
        ON [dbo].[Sales] ([TenantId], [InvoiceNumber]);

    PRINT 'Created index [UX_Sales_Tenant_InvoiceNumber].';
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Sales_Tenant_SaleDate')
BEGIN
    /* Module 8's reports are all date ranges, and so is "today's takings". */
    CREATE NONCLUSTERED INDEX [IX_Sales_Tenant_SaleDate]
        ON [dbo].[Sales] ([TenantId], [SaleDate] DESC)
        INCLUDE ([InvoiceNumber], [CashierUserId], [NetTotal], [Status]);

    PRINT 'Created index [IX_Sales_Tenant_SaleDate].';
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Sales_Tenant_Cashier')
BEGIN
    /* "Who sold what", and the index that makes an Employee's own-sales-only list a seek
       rather than a scan of the whole pharmacy's sales. */
    CREATE NONCLUSTERED INDEX [IX_Sales_Tenant_Cashier]
        ON [dbo].[Sales] ([TenantId], [CashierUserId], [SaleDate] DESC)
        INCLUDE ([InvoiceNumber], [NetTotal], [Status]);

    PRINT 'Created index [IX_Sales_Tenant_Cashier].';
END
GO

/* ── SaleLines ─────────────────────────────────────────────────────────────────────────── */

IF OBJECT_ID(N'[dbo].[SaleLines]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[SaleLines]
    (
        [Id]                  UNIQUEIDENTIFIER NOT NULL CONSTRAINT [PK_SaleLines] PRIMARY KEY,
        [TenantId]            UNIQUEIDENTIFIER NOT NULL,

        [SaleId]              UNIQUEIDENTIFIER NOT NULL,
        [ProductId]           UNIQUEIDENTIFIER NOT NULL,

        /* The batch this line deducted from. A return reverses THIS deduction rather than
           guessing, which is what keeps expiry and cost accounting attached to the right
           stock. */
        [BatchId]             UNIQUEIDENTIFIER NOT NULL,

        [QuantityInBaseUnits] INT              NOT NULL,

        /* What the customer bought in, for the invoice to read "2 strips". Presentational
           only; every calculation runs on base units. */
        [UnitSold]            INT              NOT NULL,

        /* THE PRICE SNAPSHOT. 18,4 like Products.PricePerBase — see the header note. Never
           re-read from the product to display or report on a past sale. */
        [UnitSalePrice]       DECIMAL(18, 4)   NOT NULL,

        [LineTotal]           DECIMAL(18, 2)   NOT NULL,

        /* This line's proportional share of the bill discount. Summed across a sale's lines
           this equals Sales.DiscountAmount exactly — the split absorbs its rounding residual
           into the last line. Without this column a partial return would refund the
           pre-discount price and overpay the customer. */
        [DiscountShare]       DECIMAL(18, 2)   NOT NULL CONSTRAINT [DF_SaleLines_DiscountShare] DEFAULT (0),

        [NetLineTotal]        DECIMAL(18, 2)   NOT NULL,

        [CreatedOnUtc]        DATETIME2(7)     NOT NULL,
        [CreatedBy]           NVARCHAR(256)    NULL,
        [ModifiedOnUtc]       DATETIME2(7)     NULL,
        [ModifiedBy]          NVARCHAR(256)    NULL,

        -- Declared on AggregateRoot but not configured as a concurrency token, so EF selects it
        -- on every read and the column has to exist. Same as Products, Batches and the rest;
        -- see the note in script 003.
        [RowVersion]          VARBINARY(MAX)   NULL,

        CONSTRAINT [FK_SaleLines_Tenants]
            FOREIGN KEY ([TenantId]) REFERENCES [dbo].[Tenants] ([Id]),

        CONSTRAINT [FK_SaleLines_Sales]
            FOREIGN KEY ([SaleId]) REFERENCES [dbo].[Sales] ([Id]) ON DELETE CASCADE,

        /* No cascade on either of these. Deleting a product or a batch out from under a sale
           line would erase what was sold and where it came from — and losing the batch would
           leave a return with nowhere to put the stock back. */
        CONSTRAINT [FK_SaleLines_Products]
            FOREIGN KEY ([ProductId]) REFERENCES [dbo].[Products] ([Id]),

        CONSTRAINT [FK_SaleLines_Batches]
            FOREIGN KEY ([BatchId]) REFERENCES [dbo].[Batches] ([Id]),

        CONSTRAINT [CK_SaleLines_QuantityPositive] CHECK ([QuantityInBaseUnits] > 0),
        CONSTRAINT [CK_SaleLines_PriceNotNegative] CHECK ([UnitSalePrice] >= 0),
        CONSTRAINT [CK_SaleLines_ShareWithinLine]  CHECK ([DiscountShare] >= 0 AND [DiscountShare] <= [LineTotal]),
        CONSTRAINT [CK_SaleLines_NetIsLineLessShare] CHECK ([NetLineTotal] = [LineTotal] - [DiscountShare])
    );

    PRINT 'Created table [SaleLines].';
END
ELSE
    PRINT 'Table [SaleLines] already exists.';
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_SaleLines_Tenant_Sale')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_SaleLines_Tenant_Sale]
        ON [dbo].[SaleLines] ([TenantId], [SaleId]);

    PRINT 'Created index [IX_SaleLines_Tenant_Sale].';
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_SaleLines_Tenant_Product')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_SaleLines_Tenant_Product]
        ON [dbo].[SaleLines] ([TenantId], [ProductId])
        INCLUDE ([QuantityInBaseUnits], [NetLineTotal]);

    PRINT 'Created index [IX_SaleLines_Tenant_Product].';
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_SaleLines_Tenant_Batch')
BEGIN
    /* Module 8 joins lines to batches for the purchase cost behind each sale. */
    CREATE NONCLUSTERED INDEX [IX_SaleLines_Tenant_Batch]
        ON [dbo].[SaleLines] ([TenantId], [BatchId]);

    PRINT 'Created index [IX_SaleLines_Tenant_Batch].';
END
GO

/* ── SalesReturns ──────────────────────────────────────────────────────────────────────── */

IF OBJECT_ID(N'[dbo].[SalesReturns]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[SalesReturns]
    (
        [Id]                          UNIQUEIDENTIFIER NOT NULL CONSTRAINT [PK_SalesReturns] PRIMARY KEY,
        [TenantId]                    UNIQUEIDENTIFIER NOT NULL,

        [SaleLineId]                  UNIQUEIDENTIFIER NOT NULL,
        [QuantityReturnedInBaseUnits] INT              NOT NULL,
        [Reason]                      NVARCHAR(500)    NOT NULL,

        /* Computed from the line's NetLineTotal, so the discount is already absorbed. Stored
           rather than derived: the return that empties a line pays the remainder rather than
           its own proportion, which a later recomputation from quantity alone could not
           reproduce. */
        [RefundAmount]                DECIMAL(18, 2)   NOT NULL,
        [ReturnedByUserId]            UNIQUEIDENTIFIER NOT NULL,

        [CreatedOnUtc]                DATETIME2(7)     NOT NULL,
        [CreatedBy]                   NVARCHAR(256)    NULL,
        [ModifiedOnUtc]               DATETIME2(7)     NULL,
        [ModifiedBy]                  NVARCHAR(256)    NULL,

        -- Declared on AggregateRoot but not configured as a concurrency token, so EF selects it
        -- on every read and the column has to exist. Same as Products, Batches and the rest;
        -- see the note in script 003.
        [RowVersion]                  VARBINARY(MAX)   NULL,

        CONSTRAINT [FK_SalesReturns_Tenants]
            FOREIGN KEY ([TenantId]) REFERENCES [dbo].[Tenants] ([Id]),

        CONSTRAINT [FK_SalesReturns_SaleLines]
            FOREIGN KEY ([SaleLineId]) REFERENCES [dbo].[SaleLines] ([Id]) ON DELETE CASCADE,

        CONSTRAINT [FK_SalesReturns_Users]
            FOREIGN KEY ([ReturnedByUserId]) REFERENCES [dbo].[Users] ([Id]),

        CONSTRAINT [CK_SalesReturns_QuantityPositive]  CHECK ([QuantityReturnedInBaseUnits] > 0),
        CONSTRAINT [CK_SalesReturns_RefundNotNegative] CHECK ([RefundAmount] >= 0)

        /* What is NOT here: "the sum of returns against a line cannot exceed what the line
           sold". That spans rows, which a CHECK cannot see. A trigger could, and would put a
           business rule somewhere nobody reading the handler would think to look. It is
           enforced in SalesReturn.Record, the only way one of these can be constructed. */
    );

    PRINT 'Created table [SalesReturns].';
END
ELSE
    PRINT 'Table [SalesReturns] already exists.';
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_SalesReturns_Tenant_SaleLine')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_SalesReturns_Tenant_SaleLine]
        ON [dbo].[SalesReturns] ([TenantId], [SaleLineId])
        INCLUDE ([QuantityReturnedInBaseUnits], [RefundAmount]);

    PRINT 'Created index [IX_SalesReturns_Tenant_SaleLine].';
END
GO

/* ── InvoiceSequences ──────────────────────────────────────────────────────────────────────

   One row per pharmacy holding the next number to hand out. Deliberately NOT mapped as an EF
   entity: it carries no business data, only InvoiceNumberGenerator reads or writes it, and
   mapping it would give it a global tenant query filter that the generator's SQL would then
   have to bypass — establishing exactly the pattern worth avoiding. The tenant id is an
   explicit parameter on every statement there instead.

   Allocation is a single atomic statement:

       UPDATE dbo.InvoiceSequences SET NextNumber = NextNumber + 1
       OUTPUT deleted.NextNumber AS [Value]
       WHERE TenantId = @tenantId

   which reads and writes under one update lock. Two cashiers completing a sale in the same
   instant get 452 and 453, never both 452.
   ─────────────────────────────────────────────────────────────────────────────────────────── */

IF OBJECT_ID(N'[dbo].[InvoiceSequences]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[InvoiceSequences]
    (
        [TenantId]   UNIQUEIDENTIFIER NOT NULL CONSTRAINT [PK_InvoiceSequences] PRIMARY KEY,

        /* The number the NEXT sale will use. Starts at 1; the generator seeds the row at 2
           because the sale that created it is taking 1. */
        [NextNumber] BIGINT           NOT NULL CONSTRAINT [DF_InvoiceSequences_NextNumber] DEFAULT (1),

        CONSTRAINT [FK_InvoiceSequences_Tenants]
            FOREIGN KEY ([TenantId]) REFERENCES [dbo].[Tenants] ([Id]) ON DELETE CASCADE,

        CONSTRAINT [CK_InvoiceSequences_Positive] CHECK ([NextNumber] > 0)
    );

    PRINT 'Created table [InvoiceSequences].';
END
ELSE
    PRINT 'Table [InvoiceSequences] already exists.';
GO

PRINT '011_CreateSalesTables.sql complete.';
GO
