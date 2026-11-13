# Database Migrations

Use this project for EF Core migrations.

1. From the `docker` folder (where the `docker compose` file lives):
   ```bash
   cd docker
   ```
   Ensure Docker Compose services are running before using `exec`:
   ```bash
   ./manage start
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

## Exception-reason migration deployment gate

`20261112220220_RefactorCalendarEventExceptionReasonCodes` drops `Events.IsException`
and `Events.ExceptionReason`. Old API binaries require those columns; new binaries
require `ExceptionReasonCodes`. A rolling deployment across this change is unsafe.

**Deployment blocker:** the existing process does not guarantee a coordinated cutover.
[Release](../.github/workflows/release.yml) promotes both image references, while
[Publish API](../.github/workflows/publish-api.yml) and
[Publish Web](../.github/workflows/publish-web.yml) can promote dev images independently.
The [GitOps API deployment](https://github.com/bcgov-c/tenant-gitops-cb6495/blob/main/services/unified-scheduling/base/resources/api-deployment.yaml)
uses `RollingUpdate` with `maxSurge: 1` in the inspected local `main` and `develop` refs.
The new API [applies migrations at startup](../api/Unified.Api/Services/MigrationAndSeedService.cs),
before serving traffic. Readiness probes and publishing both images together do not
prevent old API instances from using the changed schema. Live cluster settings have
not been verified.

Do not merge/promote this change into an automatically deploying environment until
the GitOps owner approves a maintenance cutover that guarantees the following sequence:

1. Confirm this migration is absent from the target database's `__EFMigrationsHistory`.
   If it has already been applied to a shared environment, stop: do not rewrite its
   definition; agree a corrective migration strategy first.
2. Hold automatic image promotion and GitOps reconciliation that could restart old
   workloads. Block user traffic, drain requests, and stop every old API instance
   and any background process accessing the affected schema. Confirm they have
   terminated, then take and verify a restorable database backup.
3. Start one new API instance to apply the migration and seeds. Confirm the migration
   history, schema, and successful startup before starting other new instances. If
   preflight rejects records, keep the release stopped and review the reported
   reasons/event IDs with the data owner; do not silently normalize their meaning.
4. Deploy the matching web image and only new API instances. Verify them before
   reopening traffic and resuming promotion/reconciliation. The client cutover must
   also address cached bundles and already-open browser tabs using the old API
   contract; changing the web image alone does not replace loaded JavaScript.

This is a required operator-approved cutover, not a guarantee provided by the current
workflows. No deployment configuration is changed here. If this sequence cannot be
guaranteed, keep deployment blocked and agree an expand-and-contract compatibility
approach before changing the implementation.

After a preflight rejection, restore the old images only after confirming the retained
legacy schema and absent migration-history entry. Check the database even when API
startup fails: migrations may have committed before a later seed/startup failure.
After migration commits, an image-only rollback is unsafe. Stop all new workloads and
restore the verified pre-cutover database and matching old images, or use an explicitly
reviewed downgrade. `Down` recreates the legacy fields but cannot give the old application
independent reason handling. After new writes are accepted, restoring a backup loses
those writes unless they are reconciled; prefer a forward fix where appropriate.
