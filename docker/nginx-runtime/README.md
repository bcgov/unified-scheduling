# Nginx Runtime for Unified Scheduling

This directory contains the nginx runtime configuration for deploying the Unified Scheduling web application in production (OpenShift).

## Overview

The nginx runtime serves the built Vue.js application and proxies API requests to the backend service.

## Structure

```
nginx-runtime/
├── Dockerfile              # nginx base image with OpenShift S2I support
├── nginx.conf.template     # nginx configuration template
└── s2i/
    └── bin/
        ├── assemble        # S2I build script (minimal)
        ├── assemble-runtime # S2I runtime script (minimal)
        └── run             # Main entrypoint - configures and starts nginx
```


## Environment Variables

### Required
- `API_URL` - URL of the backend API (e.g., `http://api:5000/api/`)

### Optional
- `WEB_BASE_HREF` - Base path for the application (default: `/`)
- `HTTP_BASIC_USERNAME` - Username for HTTP Basic Auth
- `HTTP_BASIC_PASSWORD` - Password for HTTP Basic Auth
- `RealIpFrom` - IP range for real IP detection (default: `172.51.0.0/16`)
- `API_SERVICE_NAME` - OpenShift service name for API (e.g., `api`)
- `API_PATH` - API path prefix (default: `/api/`)
- `USE_SELF_SIGNED_SSL` - Enable self-signed SSL certificates
- `IncludeSiteminderHeaders` - Allow headers with underscores
- `PUBLIC_HOST_ALLOWLIST` - Comma-separated approved public hosts, optionally with `:443` (default: empty)

### Runtime base path (`WEB_BASE_HREF`)

The release web image (`docker/web/Dockerfile.release`) does **not** bake the base path in at build
time; the `WEB_BASE_HREF` build-arg is ignored. The bundle is built with the placeholder base
`/__WEB_BASE_HREF__/` and stored in `/opt/app/dist-template`. On every container start,
`vue-on-nginx/s2i/bin/fix-base-url` normalizes `WEB_BASE_HREF` (default `/`, leading and trailing slash
enforced), regenerates `/tmp/app/dist` from the template, and replaces the placeholder with the
runtime value. If any placeholder is left after the replacement, startup fails. Set `WEB_BASE_HREF` per
deployment to the public path prefix, e.g. `/sheriff-scheduling/` behind
`https://dev.jag.gov.bc.ca/sheriff-scheduling/`. nginx also accepts requests with that prefix stripped,
so the direct route at `/` keeps working.

### Public host and OIDC

Set `PUBLIC_HOST_ALLOWLIST` on the web deployment to the exact, application-controlled public
authority supplied by the gateway in `X-Forwarded-Server`, for example `dev.jag.gov.bc.ca`.
This requires a separately approved deployment configuration change; when unset, the direct host
is retained and the public login host mismatch remains.

Use comma-separated ASCII hostnames, either bare or followed by `:443`, without spaces, wildcards,
schemes, or paths. Other ports and invalid or duplicate entries fail startup. Hostnames may be up
to 253 characters. Matching is case-insensitive, but a bare host and its `:443` form are distinct
entries; list the form actually sent by the gateway. A missing, unlisted, or combined header falls
back to the direct host with existing direct-route scheme and port handling. For a match, nginx
forwards HTTPS on port 443. `WEB_BASE_HREF` supplies the public path prefix.

Verify that the gateway sends the matching single authority and routes callbacks to the same
application. The allowlist does not authenticate the header: a direct client can forge an
approved host and disrupt its own host-only correlation cookies. Allowlist only origins under
the application's control; verify trusted proxy/header handling separately.

In Keycloak, register the exact public and direct-route callback URLs, including their base paths
and any direct-route port: `/api/auth/signin-oidc` under valid redirect URIs and
`/api/auth/signout-callback-oidc` under valid post-logout redirect URIs.

## Building the Image

```bash
cd docker
docker build -t unified-scheduling-web-runtime -f nginx-runtime/Dockerfile nginx-runtime/
```

## Running Locally

First, build your Vue application:

```bash
cd web
npm run build
```

Then run the nginx container:

```bash
docker run -p 8080:8080 \
  -v $(pwd)/web/dist:/tmp/app/dist:ro \
  -e API_URL=http://host.docker.internal:5000/api/ \
  unified-scheduling-web-runtime
```

Access the application at http://localhost:8080

## OpenShift Deployment

This image is designed to work with OpenShift's S2I build process:

1. Build the Vue application (`npm run build`)
2. Copy the `dist/` folder to `/tmp/app/dist` in the container
3. Set environment variables (API_URL, etc.)
4. nginx starts and serves the application

### Example OpenShift Configuration

```yaml
apiVersion: v1
kind: DeploymentConfig
metadata:
  name: unified-scheduling-web
spec:
  template:
    spec:
      containers:
        - name: web
          image: unified-scheduling-web-runtime:latest
          ports:
            - containerPort: 8080
          env:
            - name: API_URL
              value: "http://api:5000/api/"
            - name: WEB_BASE_HREF
              value: "/"
          volumeMounts:
            - name: app-dist
              mountPath: /tmp/app/dist
              readOnly: true
```

## Security

- Runs as non-root user (UID 104)
- Security headers configured (CSP, HSTS, X-Frame-Options, etc.)
- Rate limiting enabled
- Optional HTTP Basic Authentication
- Server tokens disabled

## Logging

- Access logs: `/var/log/nginx/access.log`
- Error logs: `/var/log/nginx/error.log`
- Health check endpoint: `/nginx_status`

## Troubleshooting

### Check nginx configuration
```bash
docker exec <container> cat /etc/nginx/nginx.conf
```

### View nginx logs
```bash
docker logs <container>
```

### Test configuration
```bash
docker exec <container> nginx -t
```

## References

- [OpenShift nginx guide](https://torstenwalter.de/openshift/nginx/2017/08/04/nginx-on-openshift.html)
- Based on jasper project's nginx-runtime implementation
