/* =============================================================================================
   016_CreateAppSettingsTable.sql — Module 10: Dashboard & Settings

   One table, a data migration that seeds it for every existing pharmacy, and the removal of the
   column that 012 said should move here.

   WHY KEY-VALUE AND NOT TYPED COLUMNS
   -----------------------------------
   Adding a setting later becomes a seed row rather than a migration plus an entity change plus a
   configuration change plus a DTO change. The cost is real and worth stating plainly: SQL Server
   cannot type-check any of this. [Value] is NVARCHAR, so "90" and "ninety" are equally valid
   here, and the CHECK constraint that used to guard the antibiotic mode is gone.

   That cost is paid in exactly one place. ISettingsService owns every cast and every fallback,
   and UpdateSettingsCommand validates before writing. No call site parses a string. See
   docs/10-dashboard-and-settings.md.

   WHY THE ANTIBIOTIC MODE MOVES
   -----------------------------
   012 put it on [Tenants] and said why: there was no settings table yet, and it was the first
   per-pharmacy preference the system had. It also said, in as many words, that this is the
   migration that should move it. Two places to store one value is exactly the drift worth
   avoiding - a settings screen writing one column while billing reads another is a bug nobody
   finds until an inspection.

   Existing values are COPIED, not defaulted. A pharmacy running under Required keeps running
   under Required across this migration; silently relaxing a regulatory setting during an upgrade
   would be the worst thing this script could do.

   Idempotent, like every script here: safe to run against a database that already has some of
   this. The seed inserts only what is missing, so re-running it never overwrites a value an
   Admin has since changed.
   ============================================================================================= */

USE [PMSDb];
GO

-- No filtered index in this script, but stated anyway so the whole folder behaves
-- identically however it is run - and so a filtered index added here later cannot fail
-- for a reason nobody would look for.
SET ANSI_NULLS ON
SET QUOTED_IDENTIFIER ON
GO

/* -- The table ---------------------------------------------------------------------------- */

IF OBJECT_ID(N'[dbo].[AppSettings]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[AppSettings]
    (
        [Id]               UNIQUEIDENTIFIER NOT NULL
            CONSTRAINT [PK_AppSettings] PRIMARY KEY,
        [TenantId]         UNIQUEIDENTIFIER NOT NULL,

        /* Snake case, because these strings appear in the database, in this script and in the
           API's JSON alike. A convention that survives all three unchanged beats matching C#. */
        [Key]              NVARCHAR(100)    NOT NULL,

        /* NOT NULL, and empty rather than NULL for a blank setting. A drug licence number the
           pharmacy does not have is "no value", not "unknown", and removing the distinction
           removes the question of which one an empty string meant. */
        [Value]            NVARCHAR(1000)   NOT NULL
            CONSTRAINT [DF_AppSettings_Value] DEFAULT (N''),

        /* No foreign key to Users, deliberately. A setting outlives the person who set it:
           Restrict would block removing a user who once saved this page, and Cascade would take
           the setting with them. The id answers "who changed the discount cap", and an id that
           no longer resolves to a name still answers it better than a blank. */
        [UpdatedByUserId]  UNIQUEIDENTIFIER NULL,

        [CreatedBy]        NVARCHAR(256)    NULL,
        [CreatedOnUtc]     DATETIME2(7)     NOT NULL,
        [ModifiedBy]       NVARCHAR(256)    NULL,
        [ModifiedOnUtc]    DATETIME2(7)     NULL,
        [RowVersion]       VARBINARY(MAX)   NULL,

        CONSTRAINT [FK_AppSettings_Tenants] FOREIGN KEY ([TenantId])
            REFERENCES [dbo].[Tenants] ([Id]),

        CONSTRAINT [CK_AppSettings_KeyNotBlank]
            CHECK (LEN(LTRIM(RTRIM([Key]))) > 0)
    );

    PRINT 'Created table AppSettings';
END
ELSE
    PRINT 'Table AppSettings already exists';
GO

/* One row per key per pharmacy. This is the constraint that makes the seed below safe to re-run
   and makes "which value is in force" a question with one answer. */
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_AppSettings_Tenant_Key')
    CREATE UNIQUE INDEX [UX_AppSettings_Tenant_Key]
        ON [dbo].[AppSettings] ([TenantId], [Key])
        INCLUDE ([Value]);
GO

/* -- Seeding every existing pharmacy -------------------------------------------------------
   Values are the literals each module had hardcoded before this migration. The numbers do not
   change here; only where they live does. Two are drawn from the tenant itself rather than from
   a constant:

     pharmacy_name                 the tenant's own Name. "My Pharmacy" on an existing shop that
                                   already has a name would be a worse default than no default.
     antibiotic_prescription_mode  whatever that pharmacy is running under right now.
   ------------------------------------------------------------------------------------------ */

DECLARE @Defaults TABLE ([Key] NVARCHAR(100) PRIMARY KEY, [Value] NVARCHAR(1000) NOT NULL);

INSERT INTO @Defaults ([Key], [Value])
VALUES
    (N'pharmacy_address',                 N'Address line one, Dhaka'),
    (N'pharmacy_phone',                   N'01700-000000'),
    (N'pharmacy_license_number',          N''),
    (N'expiry_alert_window_days',         N'90'),      /* Module 6 */
    (N'dead_stock_threshold_days',        N'90'),      /* Module 8 */
    (N'default_reorder_level',            N'100'),     /* Module 2 */
    (N'discount_cap_employee_percent',    N'5'),       /* Module 5 */
    (N'discount_cap_pharmacist_percent',  N'10');      /* Module 5 */

/* The eight constant-valued keys. NOT EXISTS rather than MERGE: re-running must never overwrite
   a value an Admin has since changed. */
INSERT INTO [dbo].[AppSettings] ([Id], [TenantId], [Key], [Value], [CreatedOnUtc])
SELECT NEWID(), t.[Id], d.[Key], d.[Value], SYSUTCDATETIME()
FROM [dbo].[Tenants] AS t
CROSS JOIN @Defaults AS d
WHERE NOT EXISTS (
    SELECT 1 FROM [dbo].[AppSettings] AS s
    WHERE s.[TenantId] = t.[Id] AND s.[Key] = d.[Key]);

PRINT 'Seeded constant-valued settings for ' + CAST(@@ROWCOUNT AS NVARCHAR(20)) + ' rows';
GO

/* pharmacy_name, from the tenant's own name. */
INSERT INTO [dbo].[AppSettings] ([Id], [TenantId], [Key], [Value], [CreatedOnUtc])
SELECT NEWID(), t.[Id], N'pharmacy_name',
       CASE WHEN LEN(LTRIM(RTRIM(ISNULL(t.[Name], N'')))) > 0
            THEN t.[Name] ELSE N'My Pharmacy' END,
       SYSUTCDATETIME()
FROM [dbo].[Tenants] AS t
WHERE NOT EXISTS (
    SELECT 1 FROM [dbo].[AppSettings] AS s
    WHERE s.[TenantId] = t.[Id] AND s.[Key] = N'pharmacy_name');

PRINT 'Seeded pharmacy_name for ' + CAST(@@ROWCOUNT AS NVARCHAR(20)) + ' pharmacies';
GO

/* antibiotic_prescription_mode, COPIED from whatever each pharmacy is running under. Stored as
   the enum's NAME rather than its number: a settings table read by a person during an incident
   should say "Required", not "2". */
IF COL_LENGTH(N'[dbo].[Tenants]', N'AntibioticPrescriptionMode') IS NOT NULL
BEGIN
    EXEC sp_executesql N'
        INSERT INTO [dbo].[AppSettings] ([Id], [TenantId], [Key], [Value], [CreatedOnUtc])
        SELECT NEWID(), t.[Id], N''antibiotic_prescription_mode'',
               CASE t.[AntibioticPrescriptionMode]
                   WHEN 1 THEN N''Optional''
                   WHEN 2 THEN N''Required''
                   ELSE N''Off''
               END,
               SYSUTCDATETIME()
        FROM [dbo].[Tenants] AS t
        WHERE NOT EXISTS (
            SELECT 1 FROM [dbo].[AppSettings] AS s
            WHERE s.[TenantId] = t.[Id]
              AND s.[Key] = N''antibiotic_prescription_mode'');';

    PRINT 'Migrated antibiotic_prescription_mode from [Tenants]';
END
ELSE
BEGIN
    /* The column is already gone, so this script has run before. Any pharmacy still missing the
       key gets the safe default - see 012 for why Off rather than Required. */
    INSERT INTO [dbo].[AppSettings] ([Id], [TenantId], [Key], [Value], [CreatedOnUtc])
    SELECT NEWID(), t.[Id], N'antibiotic_prescription_mode', N'Off', SYSUTCDATETIME()
    FROM [dbo].[Tenants] AS t
    WHERE NOT EXISTS (
        SELECT 1 FROM [dbo].[AppSettings] AS s
        WHERE s.[TenantId] = t.[Id] AND s.[Key] = N'antibiotic_prescription_mode');

    PRINT 'antibiotic_prescription_mode already migrated; filled any gaps with Off';
END
GO

/* -- Retiring the old column ---------------------------------------------------------------
   Only after every pharmacy has the setting. The guard is not ceremonial: dropping the column
   first and failing the insert afterwards would lose the regulatory setting of every pharmacy
   running under Required.
   ------------------------------------------------------------------------------------------ */

IF COL_LENGTH(N'[dbo].[Tenants]', N'AntibioticPrescriptionMode') IS NOT NULL
   AND NOT EXISTS (
       SELECT 1 FROM [dbo].[Tenants] AS t
       WHERE NOT EXISTS (
           SELECT 1 FROM [dbo].[AppSettings] AS s
           WHERE s.[TenantId] = t.[Id]
             AND s.[Key] = N'antibiotic_prescription_mode'))
BEGIN
    IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_Tenants_AntibioticMode')
        ALTER TABLE [dbo].[Tenants] DROP CONSTRAINT [CK_Tenants_AntibioticMode];

    IF EXISTS (SELECT 1 FROM sys.default_constraints
               WHERE name = N'DF_Tenants_AntibioticPrescriptionMode')
        ALTER TABLE [dbo].[Tenants] DROP CONSTRAINT [DF_Tenants_AntibioticPrescriptionMode];

    ALTER TABLE [dbo].[Tenants] DROP COLUMN [AntibioticPrescriptionMode];

    PRINT 'Dropped [Tenants].[AntibioticPrescriptionMode] - AppSettings is now the only source';
END
ELSE
    PRINT 'Nothing to drop on [Tenants] (already migrated, or a pharmacy is still unseeded)';
GO

/* Settings are read on nearly every request - billing wants the discount caps and the antibiotic
   mode, the invoice wants the pharmacy details. The unique index above already covers
   (TenantId, Key) with Value included, which answers both the single-key read and the whole-set
   read from the index alone. No second index is needed and one would only cost writes. */

PRINT '016_CreateAppSettingsTable.sql complete';
GO
