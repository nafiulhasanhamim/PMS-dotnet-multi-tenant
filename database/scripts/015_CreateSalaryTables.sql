/* =============================================================================================
   015_CreateSalaryTables.sql — Module 9: Salary Management

   Three tables:

     EmployeeSalaryProfiles   what one member of staff is paid, and on what terms
     SalaryEntries            one employee's salary for one month, once generated
     SalaryAdvances           cash handed over mid-month, to come out of a later salary

   Created in that order because the dependency runs in a small circle: an advance points at the
   salary entry that settled it, and an entry points at the profile it was generated for. Making
   the profile first, then entries, then advances lets every foreign key be declared inline
   instead of bolted on afterwards.

   Four things worth reading rather than skimming:

     1. THE PROFILE IS NOT THE USER. Not every user draws a salary, and a user row is read on
        every request while a salary is read by one Admin a few times a month. Keeping the two
        apart means a bug in authentication cannot expose what people earn. See the entity.

     2. SalaryEntries.BaseSalary DUPLICATES the profile's MonthlyBaseSalary on purpose, exactly
        as PurchaseLines duplicates a batch's cost in 013. The profile holds what somebody earns
        now; the entry holds what they were paid in a month that has already happened. A raise in
        October must not restate July.

     3. NetPayable is stored, and its CHECK is ">= 0" rather than an equality against the other
        four columns. The arithmetic is BaseSalary + Bonus - AdvanceDeduction - OtherDeduction
        FLOORED AT ZERO, and a floor is not an equation: when somebody's advances exceed what the
        month can pay, net payable is 0 while the components sum below it. The flooring rule
        lives in SalaryEntry.Generate, which is the only thing that writes this table.

     4. Both money-moving dates are DATE, not DATETIME2. A salary is paid on a day, and an
        advance is handed over on a day; storing a time would invite a report to slice on it and
        quietly drop everything recorded at 00:00 from a range that starts at 09:00.

   Idempotent, like every script here: safe to run against a database that already has some of
   this.
   ============================================================================================= */

USE [PMSDb];
GO

/* -- EmployeeSalaryProfiles ------------------------------------------------------------- */

IF OBJECT_ID(N'[dbo].[EmployeeSalaryProfiles]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[EmployeeSalaryProfiles]
    (
        [Id]                 UNIQUEIDENTIFIER NOT NULL
            CONSTRAINT [PK_EmployeeSalaryProfiles] PRIMARY KEY,
        [TenantId]           UNIQUEIDENTIFIER NOT NULL,

        [UserId]             UNIQUEIDENTIFIER NOT NULL,
        [Designation]        NVARCHAR(200)    NULL,
        [MonthlyBaseSalary]  DECIMAL(18, 2)   NOT NULL,
        [JoiningDate]        DATE             NOT NULL,

        /* Soft delete, and it must stay one. Every salary entry and every advance ever recorded
           points here; removing the row would either orphan a year of payslips or cascade them
           away, and those are the record of money that left the till. */
        [IsActive]           BIT              NOT NULL
            CONSTRAINT [DF_EmployeeSalaryProfiles_IsActive] DEFAULT (1),

        [CreatedBy]          NVARCHAR(256)    NULL,
        [CreatedOnUtc]       DATETIME2(7)     NOT NULL,
        [ModifiedBy]         NVARCHAR(256)    NULL,
        [ModifiedOnUtc]      DATETIME2(7)     NULL,
        [RowVersion]         VARBINARY(MAX)   NULL,

        CONSTRAINT [FK_EmployeeSalaryProfiles_Tenants] FOREIGN KEY ([TenantId])
            REFERENCES [dbo].[Tenants] ([Id]),

        /* NO ACTION. A user with a salary history cannot be deleted out from under it. */
        CONSTRAINT [FK_EmployeeSalaryProfiles_Users] FOREIGN KEY ([UserId])
            REFERENCES [dbo].[Users] ([Id]),

        CONSTRAINT [CK_EmployeeSalaryProfiles_SalaryNotNegative]
            CHECK ([MonthlyBaseSalary] >= 0)
    );

    PRINT 'Created table EmployeeSalaryProfiles';
END
ELSE
    PRINT 'Table EmployeeSalaryProfiles already exists';
GO

/* One ACTIVE profile per user per pharmacy — filtered, so somebody who leaves and rejoins gets a
   second profile while the first stays put with its history attached. An unfiltered unique index
   would make rejoining impossible without editing the old row, which is the one thing this
   table must never do. */
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_EmployeeSalaryProfiles_Tenant_User_Active')
    CREATE UNIQUE INDEX [UX_EmployeeSalaryProfiles_Tenant_User_Active]
        ON [dbo].[EmployeeSalaryProfiles] ([TenantId], [UserId])
        WHERE [IsActive] = 1;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_EmployeeSalaryProfiles_Tenant_Active')
    CREATE INDEX [IX_EmployeeSalaryProfiles_Tenant_Active]
        ON [dbo].[EmployeeSalaryProfiles] ([TenantId], [IsActive]);
GO

/* -- SalaryEntries ---------------------------------------------------------------------- */

IF OBJECT_ID(N'[dbo].[SalaryEntries]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[SalaryEntries]
    (
        [Id]                       UNIQUEIDENTIFIER NOT NULL
            CONSTRAINT [PK_SalaryEntries] PRIMARY KEY,
        [TenantId]                 UNIQUEIDENTIFIER NOT NULL,

        [EmployeeSalaryProfileId]  UNIQUEIDENTIFIER NOT NULL,

        [Month]                    INT              NOT NULL,
        [Year]                     INT              NOT NULL,

        /* Frozen at generation. See note 2 in the header. */
        [BaseSalary]               DECIMAL(18, 2)   NOT NULL,

        [Bonus]                    DECIMAL(18, 2)   NOT NULL
            CONSTRAINT [DF_SalaryEntries_Bonus] DEFAULT (0),
        [AdvanceDeduction]         DECIMAL(18, 2)   NOT NULL
            CONSTRAINT [DF_SalaryEntries_AdvanceDeduction] DEFAULT (0),
        [OtherDeduction]           DECIMAL(18, 2)   NOT NULL
            CONSTRAINT [DF_SalaryEntries_OtherDeduction] DEFAULT (0),

        [AdjustmentNotes]          NVARCHAR(1000)   NULL,

        /* Stored, floored at zero. See note 3 in the header. */
        [NetPayable]               DECIMAL(18, 2)   NOT NULL,

        [PaymentStatus]            INT              NOT NULL
            CONSTRAINT [DF_SalaryEntries_PaymentStatus] DEFAULT (0),

        /* The date the money left, which is the date the expense report keys on — NOT the month
           the salary covers. An August salary paid on 2 September is a September expense. */
        [PaymentDate]              DATE             NULL,

        [GeneratedByUserId]        UNIQUEIDENTIFIER NOT NULL,

        [CreatedBy]                NVARCHAR(256)    NULL,
        [CreatedOnUtc]             DATETIME2(7)     NOT NULL,
        [ModifiedBy]               NVARCHAR(256)    NULL,
        [ModifiedOnUtc]            DATETIME2(7)     NULL,
        [RowVersion]               VARBINARY(MAX)   NULL,

        CONSTRAINT [FK_SalaryEntries_Tenants] FOREIGN KEY ([TenantId])
            REFERENCES [dbo].[Tenants] ([Id]),

        /* NO ACTION, matching the soft-delete rule on the profile. */
        CONSTRAINT [FK_SalaryEntries_Profiles] FOREIGN KEY ([EmployeeSalaryProfileId])
            REFERENCES [dbo].[EmployeeSalaryProfiles] ([Id]),

        CONSTRAINT [FK_SalaryEntries_Users] FOREIGN KEY ([GeneratedByUserId])
            REFERENCES [dbo].[Users] ([Id]),

        CONSTRAINT [CK_SalaryEntries_Month] CHECK ([Month] BETWEEN 1 AND 12),
        CONSTRAINT [CK_SalaryEntries_Year]  CHECK ([Year] BETWEEN 2000 AND 2200),

        CONSTRAINT [CK_SalaryEntries_AmountsNotNegative] CHECK (
            [BaseSalary] >= 0 AND [Bonus] >= 0
            AND [AdvanceDeduction] >= 0 AND [OtherDeduction] >= 0),

        /* ">= 0", not an equality against the components. See note 3. */
        CONSTRAINT [CK_SalaryEntries_NetPayableNotNegative] CHECK ([NetPayable] >= 0),

        CONSTRAINT [CK_SalaryEntries_PaymentStatus] CHECK ([PaymentStatus] IN (0, 1)),

        /* Paid means a date; unpaid means none. Either half alone is a row no screen can render
           honestly — a paid salary nobody can say the date of, or a payment date on money that
           has not moved. */
        CONSTRAINT [CK_SalaryEntries_PaidHasDate] CHECK (
            ([PaymentStatus] = 0 AND [PaymentDate] IS NULL)
            OR ([PaymentStatus] = 1 AND [PaymentDate] IS NOT NULL))
    );

    PRINT 'Created table SalaryEntries';
END
ELSE
    PRINT 'Table SalaryEntries already exists';
GO

/* An employee's month can only be generated once. This is the constraint that makes "generate
   for August" safe to press twice — the second press is refused by the database, not merely by
   a handler that happened to check first. */
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_SalaryEntries_Tenant_Profile_Period')
    CREATE UNIQUE INDEX [UX_SalaryEntries_Tenant_Profile_Period]
        ON [dbo].[SalaryEntries] ([TenantId], [EmployeeSalaryProfileId], [Year], [Month]);
GO

/* The month listing: everything for one period. */
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_SalaryEntries_Tenant_Period')
    CREATE INDEX [IX_SalaryEntries_Tenant_Period]
        ON [dbo].[SalaryEntries] ([TenantId], [Year], [Month]);
GO

/* The operating-expense query: paid entries whose PaymentDate falls in a range. Includes
   NetPayable so that sum is answered from the index alone. */
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_SalaryEntries_Tenant_PaymentDate')
    CREATE INDEX [IX_SalaryEntries_Tenant_PaymentDate]
        ON [dbo].[SalaryEntries] ([TenantId], [PaymentStatus], [PaymentDate])
        INCLUDE ([NetPayable]);
GO

/* -- SalaryAdvances --------------------------------------------------------------------- */

IF OBJECT_ID(N'[dbo].[SalaryAdvances]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[SalaryAdvances]
    (
        [Id]                       UNIQUEIDENTIFIER NOT NULL
            CONSTRAINT [PK_SalaryAdvances] PRIMARY KEY,
        [TenantId]                 UNIQUEIDENTIFIER NOT NULL,

        [EmployeeSalaryProfileId]  UNIQUEIDENTIFIER NOT NULL,

        [Amount]                   DECIMAL(18, 2)   NOT NULL,

        /* The day the cash was handed over, and THE DAY THE EXPENSE FALLS ON — not the day it is
           later deducted from a salary. See IOperatingExpenses. */
        [AdvanceDate]              DATE             NOT NULL,

        [Reason]                   NVARCHAR(500)    NULL,
        [GivenByUserId]            UNIQUEIDENTIFIER NOT NULL,

        [IsSettled]                BIT              NOT NULL
            CONSTRAINT [DF_SalaryAdvances_IsSettled] DEFAULT (0),
        [SettledInSalaryEntryId]   UNIQUEIDENTIFIER NULL,

        [CreatedBy]                NVARCHAR(256)    NULL,
        [CreatedOnUtc]             DATETIME2(7)     NOT NULL,
        [ModifiedBy]               NVARCHAR(256)    NULL,
        [ModifiedOnUtc]            DATETIME2(7)     NULL,
        [RowVersion]               VARBINARY(MAX)   NULL,

        CONSTRAINT [FK_SalaryAdvances_Tenants] FOREIGN KEY ([TenantId])
            REFERENCES [dbo].[Tenants] ([Id]),

        CONSTRAINT [FK_SalaryAdvances_Profiles] FOREIGN KEY ([EmployeeSalaryProfileId])
            REFERENCES [dbo].[EmployeeSalaryProfiles] ([Id]),

        /* NO ACTION rather than SET NULL. Deleting an unpaid salary entry must first release the
           advances it settled — silently blanking the link would leave IsSettled = 1 pointing at
           nothing, and the money would never be recovered. Refusing the delete makes the handler
           do it properly. */
        CONSTRAINT [FK_SalaryAdvances_SalaryEntries] FOREIGN KEY ([SettledInSalaryEntryId])
            REFERENCES [dbo].[SalaryEntries] ([Id]),

        CONSTRAINT [FK_SalaryAdvances_Users] FOREIGN KEY ([GivenByUserId])
            REFERENCES [dbo].[Users] ([Id]),

        /* Money going the other way is a salary, not an advance. */
        CONSTRAINT [CK_SalaryAdvances_AmountPositive] CHECK ([Amount] > 0),

        /* Settled means a month that settled it; unsettled means none. Without this the pair can
           drift, and "how much of Karim's advance is still outstanding" stops being answerable. */
        CONSTRAINT [CK_SalaryAdvances_SettledHasEntry] CHECK (
            ([IsSettled] = 0 AND [SettledInSalaryEntryId] IS NULL)
            OR ([IsSettled] = 1 AND [SettledInSalaryEntryId] IS NOT NULL))
    );

    PRINT 'Created table SalaryAdvances';
END
ELSE
    PRINT 'Table SalaryAdvances already exists';
GO

/* Generation's question: what is still unsettled for this employee, oldest first. */
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_SalaryAdvances_Tenant_Profile_Settled')
    CREATE INDEX [IX_SalaryAdvances_Tenant_Profile_Settled]
        ON [dbo].[SalaryAdvances] ([TenantId], [EmployeeSalaryProfileId], [IsSettled], [AdvanceDate])
        INCLUDE ([Amount]);
GO

/* The operating-expense query: advances handed over within a date range, settled or not. */
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_SalaryAdvances_Tenant_AdvanceDate')
    CREATE INDEX [IX_SalaryAdvances_Tenant_AdvanceDate]
        ON [dbo].[SalaryAdvances] ([TenantId], [AdvanceDate])
        INCLUDE ([Amount]);
GO

/* Releasing advances when an unpaid entry is revised or deleted reads by the entry. */
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_SalaryAdvances_Tenant_SettledEntry')
    CREATE INDEX [IX_SalaryAdvances_Tenant_SettledEntry]
        ON [dbo].[SalaryAdvances] ([TenantId], [SettledInSalaryEntryId]);
GO

PRINT '015_CreateSalaryTables.sql complete';
GO
