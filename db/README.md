# Database Migrations

Use this project for EF Core migrations.

1. Set `DatabaseConnectionString` in your shell.
   PowerShell:
   ```powershell
   $env:DatabaseConnectionString="Host=<DB_HOST>;Port=<DB_PORT>;Database=<DB_NAME>;Username=<DB_USER>;Password=<DB_PASSWORD>"
   ```
   Bash:
   ```bash
   export DatabaseConnectionString="Host=<DB_HOST>;Port=<DB_PORT>;Database=<DB_NAME>;Username=<DB_USER>;Password=<DB_PASSWORD>"
   ```
   Or run the commands below inside the API container where `DatabaseConnectionString` is already set.
   Example:
   ```bash
   docker compose exec <API_SERVICE> sh
   ```
2. Create a migration from repo root:
   ```bash
   dotnet ef migrations add <Name> --project db --startup-project api/Unified.Api --context UnifiedDbContext
   ```
3. Apply migrations:
   ```bash
   dotnet ef database update --project db --startup-project api/Unified.Api --context UnifiedDbContext
   ```
4. Commit migration files under `db/Migrations`.

Notes:
- `UnifiedDbContextFactory` is used only by EF CLI commands at design-time so tooling can create `UnifiedDbContext` without starting the API.
- At runtime, `UnifiedDbContext` is registered by `AddDbModule(...)` and reads `DatabaseConnectionString` from application configuration/environment.
- Keep secrets out of source control; use environment variables, secret stores, or deployment configuration for real credentials.
