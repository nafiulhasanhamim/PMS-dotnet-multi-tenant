/* =============================================================================================
   012_AddAntibioticPrescriptionMode.sql — Module 7: Antibiotic Register

   One column. How strictly a pharmacy captures prescriptions when it dispenses an antibiotic:

       0  Off       sells like any other product; no prescription recorded    (the default)
       1  Optional  panel shown, every field optional, sale never blocked
       2  Required  complete verified prescription, and no Employee may dispense

   WHY IT IS ON [Tenants] AND NOT IN A SETTINGS TABLE
   --------------------------------------------------
   Because there is no settings table yet. This is the first per-pharmacy *preference* the
   system has had - every other column here identifies or governs a tenant rather than
   configuring it - and when a Settings module arrives, this is the column that should move into
   it. The TODO is on the entity too.

   What it could not be is a constant. Two pharmacies on one deployment genuinely operate
   differently: a Model Pharmacy applies the rule strictly and the shop down the road does not,
   and a global switch would force one of them to either loosen or to fabricate patient data.

   WHY THE DEFAULT IS 0
   --------------------
   It is what an unconfigured pharmacy is actually doing. A default that claims otherwise is a
   default somebody turns off on their first day, having first entered a fake patient name to
   get past it. See the enum for the full argument.

   Idempotent, like every script here.
   ============================================================================================= */

USE [PMSDb];
GO

IF COL_LENGTH(N'[dbo].[Tenants]', N'AntibioticPrescriptionMode') IS NULL
BEGIN
    ALTER TABLE [dbo].[Tenants]
        ADD [AntibioticPrescriptionMode] INT NOT NULL
            CONSTRAINT [DF_Tenants_AntibioticPrescriptionMode] DEFAULT (0);

    PRINT 'Added [Tenants].[AntibioticPrescriptionMode], defaulting to Off.';
END
ELSE
    PRINT 'Column [Tenants].[AntibioticPrescriptionMode] already exists.';
GO

/* Existing pharmacies keep the default. Deliberate: switching a live pharmacy into Required
   without telling its staff would block every Employee from selling an antibiotic with no
   warning, mid-shift. The setting is theirs to turn on. */

IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_Tenants_AntibioticMode')
    PRINT 'Constraint [CK_Tenants_AntibioticMode] already exists.';
ELSE
BEGIN
    ALTER TABLE [dbo].[Tenants]
        ADD CONSTRAINT [CK_Tenants_AntibioticMode]
            CHECK ([AntibioticPrescriptionMode] IN (0, 1, 2));

    PRINT 'Added constraint [CK_Tenants_AntibioticMode].';
END
GO

PRINT '012_AddAntibioticPrescriptionMode.sql complete.';
GO
