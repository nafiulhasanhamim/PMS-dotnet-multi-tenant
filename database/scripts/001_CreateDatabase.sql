-- ============================================
-- Script: 001_CreateDatabase.sql
-- Description: Creates the PMS database
-- ============================================

USE [master]
GO

-- Create database if it doesn't exist
IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = N'PMSDb')
BEGIN
    CREATE DATABASE [PMSDb]
END
GO

USE [PMSDb]
GO

PRINT 'Database [PMSDb] created successfully.'
GO
