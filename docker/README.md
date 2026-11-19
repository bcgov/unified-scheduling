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

For local Docker Compose development, `ScheduleDistributionFrontendBaseUrl` defaults to
`http://web:8080/`, which is the frontend service address reachable from the API container.
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

Use `http://localhost:3000/sheriff-scheduling/` to investigate photo requests through
browser -> outer Nginx -> release web/Nginx -> existing development API -> existing
database. The override's `proxy-repro` profile adds only `web-release` and `proxy`,
using `unified-scheduling-dev`. Normal `./manage start` does not start these services;
the Vite workflow remains at `http://localhost:8080/`.

From the repository root, use your configured Docker development environment. Build
development images with `./manage build` first if needed. Keep your normal Compose
project name (default `unified-scheduling`) to reuse containers and database volumes:

```bash
cd docker
./manage start -d
./manage proxy pull web-release proxy
./manage proxy up -d --no-deps web-release proxy
```

`./manage proxy` loads the usual development configuration and forwards its arguments
to Docker Compose with both files and the `proxy-repro` profile. It requires a Compose
version that supports profiles. Use normal Compose command/option syntax after `proxy`;
set image overrides in the environment rather than adding `NAME=value` arguments.

The normal stack must be running: `--no-deps` starts only the added services. The
outer proxy is bound to `127.0.0.1:3000`; the release web app is also available
directly at `http://localhost:9080/sheriff-scheduling/`, bound to `127.0.0.1:9080`.
Use the proxy on port 3000 for login and reproduction so forwarded headers describe
the browser's connection. Both containers still listen internally on port 8080.
The normal application's `WEB_BASE_HREF` is unchanged.

The proxy's expected login callback is now
`http://localhost:3000/sheriff-scheduling/api/auth/signin-oidc`; the development
Keycloak client must allow that exact URI. Existing redirect entries are retained.

The default image is GitOps production's `ghcr.io/bcgov/unified-scheduling/web:1.3.4`
(release `v1.3.4`, image tag without `v`); no release-web build is needed. Registry
access, image availability, and the live deployment's digest are unverified. Resolve
a failed pull before starting. Select another compatible image or deployed digest
with `PROXY_WEB_IMAGE`, then repeat pull/start:

```bash
export PROXY_WEB_IMAGE='ghcr.io/bcgov/unified-scheduling/web:1.3.4'
```

The [v1.3.4 release runtime](https://github.com/bcgov/unified-scheduling/blob/v1.3.4/docker/vue-on-nginx/s2i/bin/fix-base-url)
substitutes frontend base-path placeholders at startup. This checkout's `fix-base-url`
only starts Nginx; its release Dockerfile does not apply `WEB_BASE_HREF` to Vite.
Local builds from this checkout are therefore unsupported substitutes. Alternative
images must support runtime base substitution; record their version/digest and
distinguish results from production `1.3.4`. The API always remains this checkout's
development API.

Production settings verified from `tenant-gitops-cb6495/services/unified-scheduling/`
are `WEB_BASE_HREF=/sheriff-scheduling/`, `API_PATH=/api/`,
`API_URL=http://unified-scheduling-api:8080/api/`, and
`API_SERVICE_NAME=unified-scheduling-api`. Locally the API is `http://api:5000/api/`,
without Kubernetes discovery. The production web service uses port 8080; its route
is `sheriff-unified-web-cb6495-prod.apps.emerald.devops.gov.bc.ca`, with edge TLS and
HTTP-to-HTTPS redirection.

The outer proxy preserves the prefix. The existing release runtime removes it,
routes `/api`, sends `X-Base-Href: /sheriff-scheduling/`, and rewrites cookie paths
from `/api/auth` to `/sheriff-scheduling/api/auth`. All paths outside the prefix,
including `/api/*` and `/`, receive an outer 404 without forwarding or SPA fallback.

GitOps configures `PUBLIC_HOST_ALLOWLIST` with `jag.gov.bc.ca` and the production
route hostname. In the [v1.3.4 Nginx configuration](https://github.com/bcgov/unified-scheduling/blob/v1.3.4/docker/nginx-runtime/nginx.conf.template),
approved `X-Forwarded-Server` values select HTTPS/443; otherwise the runtime uses the
request host and forwarded protocol/port. Locally the allowlist is empty and that
header is cleared. The proxy forwards the browser host, `X-Real-IP`,
`X-Forwarded-For`, `X-Forwarded-Host`, `X-Forwarded-Proto: http`, and port `3000`.

The outer proxy matches the release runtime's bounded header buffers: incoming
headers use `large_client_header_buffers 4 32k`, and upstream responses use
`proxy_buffer_size 128k`, `proxy_buffers 4 256k`, and `proxy_busy_buffers_size 256k`.
This prevents large response headers, such as renewed session cookies, from causing
an outer-proxy 502 (`upstream sent too big header`) after the API returns 200.

External justice proxy rewrite rules, headers, and cookie handling are absent from
GitOps. Trusted-proxy IP settings are secret references and were not read. TLS,
SiteMinder, public-host mapping, and trusted client-IP behavior are not reproduced.
Production's rejection behavior and incident cause remain unproven. All `.env*`
files, including `docker/.env.template`, were left unread under project instructions.

Inspect the generated runtime, assets, and logs with:

```bash
./manage proxy exec web-release cat /etc/nginx/nginx.conf
./manage proxy exec web-release cat /tmp/app/dist/index.html
./manage proxy exec web-release sh -c 'if grep -rlF /__WEB_BASE_HREF__/ /tmp/app/dist; then echo "ERROR: unresolved frontend base placeholder"; exit 1; fi'
./manage proxy logs -f proxy web-release api
```

Manual validation for the developer (no builds, startup, or validation were run):

1. Load `/sheriff-scheduling/` and reload a nested route. Confirm JS, CSS, dynamic
   imports, and images load under the prefix, with no unresolved base placeholder.
2. Confirm `/` and `/api/users/{id}/photo` return 404 with `upstream_status=-` and
   `upstream_addr=-` in outer logs, proving no forwarding.
3. Confirm `/sheriff-scheduling/api/users/{id}/photo` reaches the API using logs;
   authorization and missing-photo responses are valid routing outcomes.
4. Upload a valid small photo through a user profile. Inspect the POST to
   `/sheriff-scheduling/api/users/{id}/upload-photo` and subsequent image GET in
   browser Network. Record whether upload succeeds but the GET uses `/api/...`
   and is rejected by the outer proxy. Preserve existing photo behavior.
5. If the local auth flow sets a cookie, inspect its Path for
   `/sheriff-scheduling/api/auth` and redirects for HTTP/localhost:3000. Record
   unexpected authentication behavior without changing auth configuration.
6. Confirm the normal `http://localhost:8080/` development workflow still works.

Stop and optionally remove only the added containers, retaining the normal stack
and all database volumes:

```bash
./manage proxy stop proxy web-release
./manage proxy rm -f proxy web-release
```

Continue normal development at `http://localhost:8080/`; repeat the optional start
commands to resume reproduction. Use the commands above instead of `./manage down`
or `./manage rm`, which delete project volumes.

# Dev Container

The VS Code devcontainer starts through `.devcontainer/docker-compose.yaml`. Docker Compose creates the
`unified-scheduling-dev` network and a local `db` service automatically, so a fresh developer does not need to
create the Docker network manually before opening the devcontainer. Inside the devcontainer, the database is
reachable at host `db` on port `5432`.
