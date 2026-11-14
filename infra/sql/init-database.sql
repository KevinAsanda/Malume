-- ============================================================================
-- Malume Platform — Local SQL Server Database Initialization & Spatial Check
-- Run against the local SQL Server container (e.g. via SSMS 22 or sqlcmd).
-- ============================================================================

IF DB_ID(N'MalumeDb') IS NULL
BEGIN
    CREATE DATABASE [MalumeDb];
END;
GO

USE [MalumeDb];
GO

-- Smoke-test SQL Server Spatial (geography SRID 4326) used by NetTopologySuite
-- Example: Johannesburg CBD -> Sandton corridor distance in meters
DECLARE @HomePoint geography = geography::Point(-26.2041, 28.0473, 4326);
DECLARE @CorridorLine geography = geography::STGeomFromText('LINESTRING(28.0473 -26.2041, 28.0570 -26.1076)', 4326);

SELECT
    @CorridorLine.STDistance(@HomePoint) AS DistanceMeters,
    CASE WHEN @CorridorLine.STDistance(@HomePoint) <= 1500.0 THEN 1 ELSE 0 END AS Within1500mCorridor;
GO
