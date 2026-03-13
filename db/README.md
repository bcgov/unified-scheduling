# Database Migrations

Use this project for EF Core migrations.

1. From the `docker` folder (where the `docker compose` file lives):
   ```bash
   cd docker
   ```
   Ensure Docker Compose services are running before using `exec`:
   ```bash
   ./maange start
   ```

2. Create a migration:

   Container command (run from `docker` folder) - placeholder:
   ```bash
   docker compose exec <API_SERVICE> bash -lc "dotnet ef migrations add <MigrationName> --project db --startup-project api/Unified.Api --context UnifiedDbContext"
   ```
   Container command (run from `docker` folder) - example:
   ```bash
   docker compose exec api bash -lc "dotnet ef migrations add AddUsersTable --project db --startup-project api/Unified.Api --context UnifiedDbContext"
   ```

3. Apply migrations:
   ```bash
   dotnet ef database update --project db --startup-project api/Unified.Api --context UnifiedDbContext
   ```
   Container command (run from `docker` folder) - placeholder:
   ```bash
   docker compose exec <API_SERVICE> bash -lc "dotnet ef database update --project db --startup-project api/Unified.Api --context UnifiedDbContext"
   ```
   Container command (run from `docker` folder) - example:
   ```bash
   docker compose exec api bash -lc "dotnet ef database update --project db --startup-project api/Unified.Api --context UnifiedDbContext"
   ```

4. Commit migration files under `db/Migrations`.

Notes:
- `UnifiedDbContextFactory` is used only by EF CLI commands at design-time so tooling can create `UnifiedDbContext`.
- At runtime, `UnifiedDbContext` is registered by `AddDbModule(...)` and reads `DatabaseConnectionString` from application configuration/environment.
- In Docker workflows, `DatabaseConnectionString` is already provided by compose/container configuration.
- Keep secrets out of source control; use environment variables, secret stores, or deployment configuration for real credentials.
