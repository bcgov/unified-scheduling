# Auth Database Migrations

## Overview

The Auth module uses Entity Framework Core for database management. This document explains how migrations work in development, CI/CD, and production environments.

## Design-Time vs Runtime

### Design-Time (EF Core Tools)
- **File**: `AuthDbContextFactory.cs` (implements `IDesignTimeDbContextFactory`)
- **Used by**: `dotnet ef migrations add`, `dotnet ef database update`, etc.
- **Purpose**: Allows EF Core tools to create a DbContext instance without running the application
- **Connection String Priority**:
  1. `DatabaseConnectionString` environment variable (if set)
  2. Fallback to local dev default: `Host=localhost;Database=unifieddb;Username=uniuser;Password=n05dmkFjio1GCUVY`

### Runtime (Application Execution)
- **File**: `AuthModule.cs` (service registration)
- **Used by**: Application when running (dev, staging, production)
- **Purpose**: Creates DbContext through dependency injection
- **Connection String**: From configuration (appsettings.json, environment variables, Azure Key Vault, etc.)

## ⚠️ Important

**The design-time factory is NEVER used when your application runs!**

At runtime:
- Connection string comes from `appsettings.json` or environment variables
- Registered via `AddAuthModule()` in `AuthModule.cs`
- Injected through dependency injection

## Creating Migrations

### Local Development

```bash
cd api/Unified.Auth
dotnet ef migrations add MigrationName --context AuthDbContext
```

This uses the factory's fallback connection string (localhost).

### CI/CD Pipeline

Set the `DatabaseConnectionString` environment variable:

```bash
export DatabaseConnectionString="Host=build-db;Database=unifieddb;Username=user;Password=pass"
dotnet ef migrations add MigrationName --context AuthDbContext
```

## Applying Migrations

### Automatic (Recommended)
Migrations run automatically on application startup via `MigrateAuthDatabaseAsync()` in `Program.cs`.

This is safe for all environments:
- ✅ Checks for pending migrations
- ✅ Only applies new migrations
- ✅ Uses runtime connection string from configuration
- ✅ Logs all operations

### Manual (Alternative)

If you need to run migrations manually:

```bash
cd api/Unified.Auth
dotnet ef database update --context AuthDbContext
```

For production, set the connection string via environment variable:

```bash
DatabaseConnectionString="Host=prod-db;..." dotnet ef database update --context AuthDbContext
```

## Production Deployment

### Option 1: Automatic on Startup (Current)
Migrations run when the application starts. This is suitable for:
- Single-instance deployments
- Blue-green deployments
- Environments with database migration locks

### Option 2: Pre-deployment Migration
Run migrations before deploying the application:

```bash
# In deployment pipeline
dotnet ef database update --project api/Unified.Auth --context AuthDbContext
```

Then deploy the application.

## Environment Variables

### Development
```bash
DatabaseConnectionString="Host=localhost;Database=unifieddb;Username=uniuser;Password=n05dmkFjio1GCUVY"
```

### Production (Example)
```bash
DatabaseConnectionString="Host=prod-db.postgres.database.azure.com;Database=unifieddb;Username=admin@prod-db;Password=${DB_PASSWORD};SSL Mode=Require"
```

### Docker Compose
Defined in `docker/.env`:
```env
DatabaseConnectionString=Host=db;Port=5432;Database=unifieddb;Username=uniuser;Password=n05dmkFjio1GCUVY;Enlist=true;MinPoolSize=10;
```

## Files to Commit

### ✅ DO Commit
- `AuthDbContextFactory.cs` - Needed for EF Core tools
- `Migrations/*.cs` - All migration files
- `AuthDbContext.cs` - DbContext definition
- `AuthModule.cs` - Service registration

### ❌ DO NOT Commit
- Production connection strings in code
- Secrets or passwords in appsettings.json (use user secrets or Key Vault)

## Security Best Practices

1. **Never hardcode production credentials** in the factory or code
2. **Use User Secrets** for local development sensitive data
3. **Use Azure Key Vault** or similar for production secrets
4. **Environment variables** are acceptable for containerized deployments
5. The factory's default connection string is for **local development only**

## Troubleshooting

### "Unable to create DbContext" error
- Ensure `Microsoft.EntityFrameworkCore.Design` package is installed
- Check that `AuthDbContextFactory.cs` exists and is correct

### Migrations not applying
- Check application logs for migration errors
- Verify `DatabaseConnectionString` is set correctly
- Ensure database is accessible from the application

### Different schema in production
- The factory's default connection is **not used** in production
- Production uses the runtime connection string from configuration
- Check your production environment variables/configuration

## Further Reading

- [EF Core Design-Time Services](https://docs.microsoft.com/en-us/ef/core/cli/dbcontext-creation)
- [EF Core Migrations](https://docs.microsoft.com/en-us/ef/core/managing-schemas/migrations/)
