## Using the Application

- By default, the main developer UI is exposed at; https://localhost:8080/
- The Swagger API and documentation is available at; https://localhost:8080/api/
- Which is also exposed directly at; http://localhost:5000/api/

# Running the Application on Docker

## Management Script

The `manage` script wraps the Docker process in easy to use commands.

To get full usage information on the script, run:

```
./manage -h
```

### Build all containers

```bash
./manage build
```

### Build specific container

```bash
./manage build api
```

### Start services

```bash
./manage start
```

### Upgrade a local PostgreSQL 16 database

After updating to the PostgreSQL 18 Docker configuration, leave the existing PostgreSQL 16 `db` container
running and run:

```bash
./manage upgrade-db-16-18
```

The script stops the application services, backs up and validates the configured application database, removes
the PostgreSQL 16 container without removing its data volume, starts PostgreSQL 18 with a new volume, restores
the backup, and restarts the application services. Backups are retained under `docker/postgres-backups/` and are
excluded from Git.

Do not use `./manage down` before upgrading because that command removes project volumes.

The `./manage upgrade-db-16-18` command and its `upgrade-postgres-16-to-18.sh` script can be removed once all
developers have upgraded their local databases to PostgreSQL 18.

### Start in debug mode (with hot reload)

```bash
./manage debug
```

## Data migration UI

The local `.env.template` enables the Data Migration feature and selects its permission dataset. The
development user receives seeded permissions at startup. After applying `.env` changes, recreate the API
container and visit `http://localhost:8080/data-migration` as an authenticated, registered user with
`DataMigrationOperate`. Disable the feature and remove the dataset setting together when not needed.
`SeedData__DataSets__10` assumes the ten datasets in `appsettings.Development.json` remain at indexes 0-9.

The SS and CASS sources remain disabled by default; enabling a source requires its own configuration.
Migration actions default to dry runs. Checking **Run apply job** and confirming the warning sends an
apply request that commits changes to the configured target database.

## CHES configuration

The API container maps CHES settings from Docker Compose variables into the .NET `Ches` configuration section. CHES remains disabled when `ChesEnabled` by default.

Set the following values through your local `.env` file other secrets.

```text
ChesEnabled=true
ChesBaseUrl=https://ches.api.gov.bc.ca/api/v1/
ChesAuthUrl=https://example.invalid/token
ChesClientId=replace-with-secret-managed-client-id
ChesClientSecret=replace-with-secret-managed-client-secret
ChesSenderName=Unified Scheduling
ChesSenderEmail=replace-with-approved-sender@gov.bc.ca
ChesTokenRefreshSkewSeconds=60
ChesTimeoutSeconds=30
ChesAllowedAttachment0Extension=.pdf
ChesAllowedAttachment0ContentType=application/pdf
ChesMaxAttachmentSizeBytes=20971520
ChesMaxRecipientsPerMessage=500
```

Additional approved attachment types use matching zero-based .NET configuration indexes. For example, add both `Ches__AllowedAttachmentTypes__1__Extension` and `Ches__AllowedAttachmentTypes__1__ContentType` mappings to `docker-compose.yaml`, backed by corresponding Docker Compose interpolation variables. Both values must be supplied as one approved pair.

## Schedule PDF rendering

Schedule distribution uses server-side Playwright against the application's frontend route. Set
`ScheduleDistributionFrontendBaseUrl` to an absolute HTTP or HTTPS frontend URL reachable from the API container,
including the application's base path. The URL must not contain credentials, a query, or a fragment.

```text
ScheduleDistributionFrontendBaseUrl=https://example.gov.bc.ca/unified-scheduling/
ScheduleDistributionDefaultViewportWidth=1440
ScheduleDistributionDefaultViewportHeight=1000
ScheduleDistributionRenderTimeoutSeconds=45
```

The frontend sends its current browser viewport in CSS pixels as optional `context.viewportWidth` and
`context.viewportHeight` values when distributing a schedule. Both dimensions must be supplied and fall within
1280–3840 pixels wide and 720–2160 pixels high (720p through 4K). Otherwise, rendering uses both configured
default dimensions. Configured defaults must also fall within these bounds.

For internal OpenShift service routing, set the API's `ScheduleDistribution__FrontendBaseUrl` configuration to
the frontend service address, for example `http://web:8080/unified-scheduling/` (adjust the service name, port,
and base path to match the deployment). HTTP is supported in every environment. Playwright forwards the
authenticated session cookie to this configured origin, so HTTP targets must be trusted internal services.

Local Docker startup automatically configures schedule PDF rendering for the selected frontend mode:

| Command | PDF frontend base URL |
| --- | --- |
| `./manage start` | `http://web:8080/` |
| `./manage debug` | `http://web:8080/` |
| `./manage start-proxy` | `http://web:8080/sheriff-scheduling/` |
| `./manage debug-proxy` | `http://web:8080/sheriff-scheduling/` |

Playwright appends `calendar?...` to the configured base URL and connects directly to the internal
`web:8080` service in both modes, bypassing the external Nginx proxy. Switching modes selects the
appropriate default without editing `.env`.

An explicit, non-empty `ScheduleDistributionFrontendBaseUrl` in `.env` or the environment overrides
the mode-specific default. It must be reachable from the API container and include the appropriate
frontend base path. An explicit override remains in effect when switching modes; remove it to use
the automatic defaults.

Running the API directly in the Development environment continues to use
`http://localhost:8080/` from `appsettings.Development.json`.

The API development and release images contain the Chromium build matching the pinned Microsoft.Playwright package. After changing the Playwright package or image version, rebuild the API image with `./manage build api` before starting it. Each render uses a new, non-persistent browser context; only the Chromium process is reused.

For OpenShift, build the API with Docker strategy using `docker/api/Dockerfile.release` (with the repository root as build context), rather than the development/S2I image. The release image provides Chromium at `/ms-playwright` and sets `PLAYWRIGHT_BROWSERS_PATH=/ms-playwright`; do not mount a volume over that directory. It also supports OpenShift's arbitrary non-root runtime UID. Keep the `mcr.microsoft.com/playwright/dotnet` image tag equal to the `Microsoft.Playwright` package version.

### Stop services

```bash
./manage stop
```

### Remove containers and volumes

```bash
./manage down
# or
./manage rm
```

## Optional production-prefix reproduction

Proxy mode runs browser -> Nginx -> the existing Vite frontend -> existing API
and database, with the same live source mounts and hot reload. No release frontend
image is needed. Run these commands from `docker/`:

| Command | Frontend URL | Backend mode |
| --- | --- | --- |
| `./manage start` | `http://localhost:8080/` | Normal |
| `./manage start-proxy` | `http://localhost:8080/sheriff-scheduling/` | Normal |
| `./manage debug` | `http://localhost:8080/` | Debug |
| `./manage debug-proxy` | `http://localhost:8080/sheriff-scheduling/` | Debug |

Proxy mode requires Docker Compose **2.24.4+** for `ports: !override []`, which
removes Vite's inherited host mapping. Only Nginx publishes `127.0.0.1:8080`.
Direct mode publishes Vite on port 8080. The manager sets `WEB_BASE_HREF` to `/`
or `/sheriff-scheduling/` for the selected mode. The proxy suffix is independent
of debug mode; both debug commands retain the existing API hot reload and debugger.

Nginx preserves the prefix and WebSocket upgrades; Vite removes the API/Hangfire prefix
before forwarding to `api:5000`. Unprefixed application requests receive 404.
`./manage proxy` remains available for Compose inspection commands such as logs.
The previous `web-release` service and `PROXY_WEB_IMAGE` override are retired.

To switch, invoke any startup command above; add `-d` to detach. Keep the same
`COMPOSE_PROJECT_NAME` to reuse the API, database, and volumes. Switching removes
the optional proxy and recreates Vite when its frontend mode changes, keeping
its normal name (`unified-scheduling-web-1` by default). The API is also recreated
to apply or remove the Hangfire dashboard prefix and update the PDF frontend URL. Compose recreates
the API only when its effective configuration or image changes. Existing temporary names
are repaired on the next startup. Repeated starts reuse unchanged Vite/API/database
containers. Use the default service list or include `web` for frontend switching.
`./manage stop` includes the proxy and preserves volumes; `down`/`rm` delete volumes.

Both proxy commands set `Hangfire:DashboardPrefixPath=/sheriff-scheduling` through
the local Compose override; direct mode retains an empty prefix. To enable the
dashboard without starting the background job server:

```bash
./manage start-proxy -d HangfireEnabled=true HangfireDashboardEnabled=true HangfireServerEnabled=false
# Or use debug-proxy with the same flags.
```

Open `http://localhost:8080/sheriff-scheduling/hangfire` as an authorized user and
check dashboard navigation, styles, scripts, and polling. Repeat the flags on each
startup unless they are already configured in your local environment.

The identity-provider client must allow
`http://localhost:8080/sheriff-scheduling/api/auth/signin-oidc` and the corresponding
prefixed sign-out callback. Keep existing direct-mode callback entries. Existing
`SameSite=None`/`Secure` cookies may limit authentication over HTTP localhost.
TLS, SiteMinder, and external justice-proxy behavior are not reproduced.

Manual checks (run these yourself):

```bash
./manage start-proxy -d
curl -i 'http://localhost:8080/sheriff-scheduling/'
curl -i 'http://localhost:8080/' # Expected outer 404
curl -i 'http://localhost:8080/api/users/11111111-1111-1111-1111-111111111111/photo' # Expected outer 404
./manage proxy logs --tail=100 proxy web api
./manage debug-proxy -d
./manage debug -d
./manage start -d
./manage stop
```

In both proxy modes, check nested routes, frontend hot reload, photo uploads and
prefixed image GETs with an authorized user, and login/logout redirects. In both
debug modes, verify backend hot reload and debugger attachment. After switching,
check container names, port 8080 ownership, an unchanged database container ID,
and persisted data. Schedule PDF rendering uses the mode-specific internal frontend URL automatically.

### Schedule PDF regression checks

Run from `docker/` with no explicit `ScheduleDistributionFrontendBaseUrl` override to check all four
startup commands, repeated starts, and direct -> proxy -> direct switching. Each iteration prints the
API container ID and its applied PDF frontend URL. Repeated startup with unchanged configuration
should retain the API container ID; switching frontend modes should recreate the API.

```bash
for mode in start debug start-proxy debug-proxy debug-proxy start start; do
  ./manage "$mode" -d
  docker compose -p "${COMPOSE_PROJECT_NAME:-unified-scheduling}" -f docker-compose.yaml ps -q api
  docker compose -p "${COMPOSE_PROJECT_NAME:-unified-scheduling}" -f docker-compose.yaml \
    exec -T api printenv ScheduleDistribution__FrontendBaseUrl
done
```

To inspect the resolved Compose value before startup:

```bash
docker compose -f docker-compose.yaml config | grep 'ScheduleDistribution__FrontendBaseUrl:'
./manage proxy config | grep 'ScheduleDistribution__FrontendBaseUrl:'
```

Repeat the configuration checks with an explicit `ScheduleDistributionFrontendBaseUrl` in `.env`
or the environment and confirm both resolve to that override.

For each frontend mode, sign in at its browser URL, open Calendar's Schedule or Assignments view,
and use **Distribute Schedule** to send to a test recipient through the existing CHES configuration.
Confirm the attached PDF renders successfully. The API should request `http://web:8080/calendar?...`
in direct mode and `http://web:8080/sheriff-scheduling/calendar?...` in proxy mode. Repeat after
switching back to direct mode. Browser printing alone does not exercise server-side Playwright.

# Dev Container

The VS Code devcontainer starts through `.devcontainer/docker-compose.yaml`. Docker Compose creates the
`unified-scheduling-dev` network and a local `db` service automatically, so a fresh developer does not need to
create the Docker network manually before opening the devcontainer. Inside the devcontainer, the database is
reachable at host `db` on port `5432`.
