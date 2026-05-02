# Copilot Instructions

Unified Scheduling is a full-stack scheduling platform with an ASP.NET Core (.NET 10) modular monolith API and a Vue 3 + TypeScript frontend.

## Build, Test, and Lint

### Frontend (`web/`)

```bash
cd web
npm install          # install dependencies
npm run dev          # dev server (hot reload)
npm run build        # type-check + production build
npm run lint         # oxlint then eslint (both auto-fix)
npm run format       # prettier (auto-fix)
npm run pre:push     # lint + format + build + test (run before pushing)
```

**Run a single test file:**

```bash
cd web
npx vitest run src/path/to/file.spec.ts
```

**Regenerate API client** (requires API running at `http://localhost:5000`):

```bash
npm run gen:api
```

### Backend (`api/`)

```bash
dotnet restore       # restore NuGet packages
dotnet build         # build solution
dotnet test          # run all tests (xUnit v3 MTP runner)
```

**Run a single test project:**

```bash
dotnet test api/Unified.Tests/Unified.Tests.csproj
```

**Format check (run before pushing):**

```bash
dotnet csharpier check .
dotnet format style --verify-no-changes
dotnet format analyzers --verify-no-changes
```

**Auto-fix formatting:**

```bash
dotnet csharpier .
dotnet format style
```

### Docker (full stack)

```bash
make build   # build all containers
make debug   # start with hot reload
make start   # start in production mode
make stop    # stop (non-destructive)
make down    # stop + remove volumes
```

Services once running:

- `http://localhost:8080/` — Frontend
- `http://localhost:8080/api/` — API (Scalar/OpenAPI docs)
- `http://localhost:5000/api/` — API direct

## Architecture

### Backend — Modular Monolith

Each domain is a separate `.csproj` with a `*Module.cs` file exposing a static `Add*Module()` extension on `IServiceCollection`. `Program.cs` wires all modules together. Adding a new domain follows this pattern:

- Create `Unified.MyDomain/MyDomainModule.cs` with `AddMyDomainModule()`
- Register in `Program.cs`: `builder.Services.AddMyDomainModule()`
- If the module should be feature-flag-gated, add a partial class in `Unified.Infrastructure/FeatureFlags/`

Projects:
| Project | Role |
|---|---|
| `Unified.Api` | Entry point — wires modules, middleware, routing |
| `Unified.Core` | Shared core services |
| `Unified.Common` | Shared validation helpers and error codes |
| `Unified.Infrastructure` | Error handling, OpenAPI (Scalar), options, feature flags, auth |
| `Unified.UserManagement` | User CRUD domain |
| `Unified.Stats` | Optional stats module (feature-flag gated) |
| `Unified.Auth` | Auth controllers |
| `db/` | EF Core DbContext, migrations, model configurations |
| `Unified.Tests` | xUnit tests |

Database is **PostgreSQL** via EF Core (Npgsql). Model-to-table configuration lives in `db/Configuration/` using `IEntityTypeConfiguration<T>` applied via `modelBuilder.ApplyAllConfigurations()`. Migrations run automatically on startup via `MigrationAndSeedService`.

Each domain module registers its own seeders (e.g., `UserSeeder`, `RegionSeeder`) via `SeederFactory<T>` and they are executed at startup alongside migrations.

### Frontend — Feature Module System

Each feature lives in `src/modules/<name>/` with a `*Module.ts` that exports `registerModule(routes)`. The router (`src/router/index.ts`) calls `registerModule` per module and optionally gates registration behind feature flags from the API config:

```ts
if (accessControl.isFeatureFlagEnabled("statsModule")) {
  statsModule.registerModule(routes);
}
```

Route `meta.requiresAuth: true` triggers an auth guard that calls `/api/auth/user`. On 401, the user is redirected to `/api/auth/login` (Keycloak SSO) with a `redirectUri` param for return navigation.

### API Client Generation (Orval)

The TypeScript API client in `src/api-access/generated/` is **generated** — do not edit it manually. It is produced from the OpenAPI spec at `http://localhost:5000/openapi/v1.json` using Orval:

- Files are split by OpenAPI tags (`mode: 'tags-split'`)
- A custom fetch mutator (`src/api-access/useFetchAPI.ts`) wraps `@vueuse/core`'s `createFetch`
- Zod schemas are also generated (`.zod.ts` files) with strict validation
- `MaybeRef<T>` is injected into query param types via a transformer so params are reactive

Run `npm run gen:api` (with the API running) to regenerate. Run `npm run gen:api:clean` to wipe and regenerate from scratch.

## Key Conventions

### Shared UI Components (`Ua*`)

Always use `Ua{Component}` wrappers from `src/shared/components/` instead of raw Vuetify components.

| Ua component        | Wraps                                      |
| ------------------- | ------------------------------------------ |
| `UaAlert`           | `v-alert`                                  |
| `UaBtn`             | `v-btn`                                    |
| `UaCard`            | custom card with header/body/actions slots |
| `UaFormGrid`        | label+field grid layout                    |
| `UaModal`           | `v-dialog`                                 |
| `UaPageHeader`      | page title + actions slot                  |
| `UaPlaceholderPage` | empty state page                           |
| `UaSelect`          | `v-select`                                 |
| `UaTextarea`        | `v-textarea`                               |
| `UaTextField`       | `v-text-field`                             |

**Rule**: Never use `v-btn`, `v-card`, `v-select`, `v-text-field`, `v-textarea` directly in feature modules. If a raw Vuetify component is needed in more than one place and no `Ua*` wrapper exists, create one in `src/shared/components/` first.

### Feature Flags (dual-sided)

Feature flags are declared in both the backend and frontend and must stay in sync:

- **Backend**: Partial class in `Unified.Infrastructure/FeatureFlags/<Name>.FeatureFlags.cs` adds a `[Required] bool` property. The `FeatureFlags` section in `appsettings.json` must include the flag.
- **Frontend**: Flags come from `/api/config` and are consumed via `accessControl.isFeatureFlagEnabled('flagName')` (camelCase matching the generated `FeatureFlags` model).

### Validation (Backend)

FluentValidation is used for request validation. Validators live in the domain module's `Validators/` folder and are injected directly into controllers (not as filters). Shared error codes are in `Unified.Common/Validation/ApiValidationErrorCodes.cs`.

### Testing Patterns

**Backend**: Tests use hand-rolled `Fake*Service` inner classes (not mocking frameworks). Controllers are tested directly by instantiating them with fake services. Use `TestContext.Current.CancellationToken` in async tests.

**Frontend**: Tests use `createTestApp()` from `src/__tests__/helpers/createTestApp.ts` which sets up Pinia, Vue Router, Vuetify, and MSW. MSW handlers from `src/__tests__/mocks/` intercept API calls. Feature flags and auth state can be overridden per test:

```ts
const { mountPlugins } = await createTestApp({
  featureFlags: { statsModule: false },
});
```

### GitHub Actions

Workflow actions are pinned to full commit SHAs (not version tags), with a comment indicating the release version.

### URL Structure

All API routes use lowercase URLs (`RouteOptions.LowercaseUrls = true`). The app supports a configurable base path via `WEB_BASE_HREF` environment variable (used for sub-path deployments behind nginx).
