# unified-scheduling

A full-stack scheduling platform consisting of an ASP.NET Core API and a Vue 3 frontend.

## Repository Structure

```
unified-scheduling/
├── api/          # ASP.NET Core modular API
├── db/           # EF Core database project
├── web/          # Vue 3 + Vite frontend
└── docker/       # Docker Compose setup
```

## Getting Started

The recommended way to run the full stack locally is via Docker Compose.

### Prerequisites

- [Docker](https://docs.docker.com/get-docker/)
- [Node.js](https://nodejs.org/) (for frontend development)
- [.NET SDK](https://dotnet.microsoft.com/download) (see `global.json` for the required version)

### Running with Docker

The `docker/manage` script wraps Docker Compose with convenient commands.

```bash
cd docker

# Build all containers
./manage build

# Start all services
./manage start

# Start with hot reload (debug mode)
./manage debug

# Stop services
./manage stop

# Remove containers and volumes
./manage down
```

Once running:

| URL | Description |
|-----|-------------|
| http://localhost:8080/ | Frontend |
| http://localhost:8080/api/ | Swagger / API docs |
| http://localhost:5000/api/ | API direct |

---

## Frontend (`web/`)

Built with **Vue 3**, **Vite**, **Pinia**, **Vuetify**, and **TypeScript**.

### Setup

```bash
cd web
npm install
```

### Dev server

```bash
npm run dev
```

### Other commands

| Command | Description |
|---------|-------------|
| `npm run build` | Type-check and build for production |
| `npm run test` | Run unit tests (Vitest) |
| `npm run test:e2e` | Run end-to-end tests (Playwright) |
| `npm run lint` | Lint with oxlint + ESLint |
| `npm run format` | Format with Prettier |
| `npm run gen:api` | Regenerate API client from OpenAPI spec (Orval) |
| `npm run pre:push` | Lint, format, build, and test before pushing |

### IDE Setup

[VS Code](https://code.visualstudio.com/) with the [Vue (Official)](https://marketplace.visualstudio.com/items?itemName=Vue.volar) extension. Disable Vetur if installed.

---

## Backend (`api/`)

Built with **ASP.NET Core** using a modular monolith architecture.

### Project Structure

| Project | Description |
|---------|-------------|
| `Unified.Api` | Entry point — wires up modules, middleware, and routing |
| `Unified.Core` | Shared core services |
| `Unified.Infrastructure` | Cross-cutting concerns (error handling, OpenAPI, options) |
| `Unified.UserManagement` | User CRUD domain |
| `Unified.Stats` | Optional stats module (feature flag controlled) |
| `Unified.Tests` | Unit and integration tests |

### Feature Flags

```json
{
  "FeatureFlags": {
    "StatsModule": true
  }
}
```

### Running Locally

See [api/README.md](api/README.md) and [docker/README.md](docker/README.md) for detailed setup instructions.

---

## Documentation

Additional references and learning resources are in [docs/References.md](docs/References.md).
