# Malume Infrastructure (`infra/`)

Local development infrastructure for the Malume platform.

## Contents

* `docker/docker-compose.yml` — Local **SQL Server 2022** container (`mcr.microsoft.com/mssql/server:2022-latest`) with spatial (`geography` SRID 4326) support and persistent storage (`malume_mssql_data`).
* `docker/.env.example` — Template environment variables for Docker Compose.
* `docker/api.Dockerfile.reference` — Reference `.NET 8` multi-stage Dockerfile to copy into `src/Malume.Api/Dockerfile` after creating the Web API project.
* `sql/init-database.sql` — Idempotent script to initialize `MalumeDb` and verify SQL Server `geography` / `STDistance` queries.

## Quick Start (Local SQL Server)

1. Copy the environment template:
   ```powershell
   Copy-Item infra/docker/.env.example infra/docker/.env
   ```
2. Update `SA_PASSWORD` in `infra/docker/.env` if desired (must meet SQL Server complexity requirements).
3. Start SQL Server in the background:
   ```powershell
   docker compose -f infra/docker/docker-compose.yml --env-file infra/docker/.env up -d
   ```
4. Check container health:
   ```powershell
   docker compose -f infra/docker/docker-compose.yml ps
   ```
5. Initialize `MalumeDb` (optional — EF Core migrations will also create it):
   ```powershell
   docker exec -i malume-sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "Malume_Dev_Sql2026!" -C -i /opt/malume/sql/init-database.sql
   ```

## Connection String (Local Dev)

```text
Server=localhost,1433;Database=MalumeDb;User Id=sa;Password=<SA_PASSWORD>;TrustServerCertificate=True;MultipleActiveResultSets=True
```

Store local connection strings in **.NET User Secrets** (`dotnet user-secrets`) or git-ignored `appsettings.Local.json`—never commit real credentials.

> **Note on Integration Tests:** `tests/Malume.Api.IntegrationTests` uses **Testcontainers (`Testcontainers.MsSql`)** to spin up ephemeral SQL Server containers automatically, independent of `malume-sqlserver`. Docker Desktop simply needs to be running.
