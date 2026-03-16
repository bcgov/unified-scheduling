# Unified API

ASP.NET Core API for the Unified Scheduling platform. It is composed of modular projects, each responsible for a distinct domain.

## Project Structure

| Project | Description |
|---|---|
| `Unified.Api` | Entry point — wires up modules, middleware, and routing |
| `Unified.Core` | Shared core services registered via `AddCoreModule()` |
| `Unified.Auth` | Authentication and OpenID Connect integration |
| `Unified.Infrastructure` | Cross-cutting concerns (error handling, OpenAPI, options) |
| `Unified.UserManagement` | User CRUD domain: controllers, services, models |
| `Unified.Stats` | Optional stats module, enabled via feature flag |
| `Unified.Tests` | Unit and integration tests |
 status |

## Feature Flags

The `StatsModule` is conditionally registered based on configuration:

```json
{
  "FeatureFlags": {
    "StatsModule": true
  }
}
```

## Running Locally

See the [docker README](../docker/README.md) for running the API and database with Docker Compose. 