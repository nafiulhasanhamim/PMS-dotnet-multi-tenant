/* =============================================================================================
   014_AddSupplierPaymentDirection.sql — money can move both ways

   One column on SupplierPayments.

   Module 4 shipped with payments flowing one way only: out, to the supplier. A CHECK constraint
   enforced Amount > 0 and the comment beside it said "money going the other way is a purchase
   return, not a negative payment."

   That was half right. A purchase return records GOODS going back, and it is what CREATES a
   credit. It says nothing about the supplier handing the cash back afterwards — and with no way
   to record that, a settled credit sat on the dues report forever.

   So: a direction, not a negative amount.

       0  Payment    money out, to the supplier          (everything recorded before this)
       1  Refund     money back, settling a credit
       2  WriteOff   a credit given up; no money moved

   Amount stays strictly positive for all three. Allowing it below zero would leave a table
   called "payments" holding receipts, and every sum over it would depend on a sign convention
   invisible from the column name.

   The balance becomes:

       purchased − returned − paid + refunded + written off

   Refunds and write-offs move it the same way — back toward what the pharmacy owes. They are
   kept apart because only one of them involves cash, and a pharmacy reconciling its till has to
   be able to tell them apart.

   BACKFILL: the default is 0, so every existing row is a Payment and every existing balance is
   unchanged by this migration. That is the point of defaulting rather than making it nullable.

   Idempotent, like every script here.
   ============================================================================================= */

USE [PMSDb];
GO

IF OBJECT_ID(N'[dbo].[SupplierPayments]', N'U') IS NOT NULL
   AND COL_LENGTH(N'[dbo].[SupplierPayments]', N'Direction') IS NULL
BEGIN
    ALTER TABLE [dbo].[SupplierPayments]
        ADD [Direction] INT NOT NULL
            CONSTRAINT [DF_SupplierPayments_Direction] DEFAULT (0);

    PRINT 'Added SupplierPayments.Direction (every existing row becomes a Payment)';
END
ELSE
    PRINT 'SupplierPayments.Direction already exists (or the table is missing)';
GO

/* Only the three values above. A fourth would change what the balance means, and it should be a
   migration somebody has to write rather than a value that can be inserted by accident. */
IF OBJECT_ID(N'[dbo].[SupplierPayments]', N'U') IS NOT NULL
   AND NOT EXISTS (
       SELECT 1 FROM sys.check_constraints WHERE name = N'CK_SupplierPayments_Direction')
BEGIN
    ALTER TABLE [dbo].[SupplierPayments]
        ADD CONSTRAINT [CK_SupplierPayments_Direction] CHECK ([Direction] IN (0, 1, 2));

    PRINT 'Added CK_SupplierPayments_Direction';
END
GO

/* A refund or a write-off settles a credit on the ACCOUNT, never on one bill. Attaching one to a
   purchase would mean reducing that purchase's AmountPaid, which is a record of money handed over
   and must not be rewritten. The screens allocate credits across bills for display instead — the
   same treatment general payments get.

   Enforced here as well as in the validator, because this is the constraint that still holds when
   somebody writes to the database directly. */
IF OBJECT_ID(N'[dbo].[SupplierPayments]', N'U') IS NOT NULL
   AND NOT EXISTS (
       SELECT 1 FROM sys.check_constraints
       WHERE name = N'CK_SupplierPayments_IncomingIsAccountLevel')
BEGIN
    ALTER TABLE [dbo].[SupplierPayments]
        ADD CONSTRAINT [CK_SupplierPayments_IncomingIsAccountLevel]
            CHECK ([Direction] = 0 OR [PurchaseId] IS NULL);

    PRINT 'Added CK_SupplierPayments_IncomingIsAccountLevel';
END
GO

/* The balance service sums each direction separately, so it groups on this column every time a
   supplier page or the dues report is opened. */
IF NOT EXISTS (SELECT 1 FROM sys.indexes
               WHERE name = N'IX_SupplierPayments_Tenant_Supplier_Direction')
    CREATE INDEX [IX_SupplierPayments_Tenant_Supplier_Direction]
        ON [dbo].[SupplierPayments] ([TenantId], [SupplierId], [Direction]);
GO

PRINT '014_AddSupplierPaymentDirection.sql complete';
GO
