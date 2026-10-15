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

### Start in debug mode (with hot reload)

```bash
./manage debug
```

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

Schedule distribution uses server-side Playwright against the application's canonical frontend route. Set `ScheduleDistributionFrontendBaseUrl` to the externally addressed frontend URL that is also valid for the application's authentication cookie. The API validates this setting at startup when the Scheduling module is enabled and requires HTTPS outside local/test environments.

```text
ScheduleDistributionFrontendBaseUrl=https://example.gov.bc.ca/unified-scheduling/
ScheduleDistributionDefaultViewportWidth=1440
ScheduleDistributionDefaultViewportHeight=1000
ScheduleDistributionRenderTimeoutSeconds=45
```

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

# Dev Container

The VS Code devcontainer starts through `.devcontainer/docker-compose.yaml`. Docker Compose creates the
`unified-scheduling-dev` network and a local `db` service automatically, so a fresh developer does not need to
create the Docker network manually before opening the devcontainer. Inside the devcontainer, the database is
reachable at host `db` on port `5432`.
