# GitHub Actions Workflows

This directory contains CI/CD workflows for the Unified Scheduling project.

## Overview

The project uses 3 automated checks that run on every pull request:

1. **Build and Test API** - Lints, builds and tests the .NET backend
2. **Build and Test Web** - Builds and tests the Vue.js frontend  
3. **Merge Gate** - Ensures all checks pass before allowing merge

## CI/CD Pipeline Flow

```
┌─────────────────────────────────────────┐
│  Pull Request Created/Updated           │
└─────────────────┬───────────────────────┘
                  │
         ┌────────┼──────────────┐
         ▼        ▼              ▼
   ┌──────────┐ ┌─────────┐ ┌──────────┐
   │ Build API│ │Build Web│ │Merge Gate│
   │ (includes│ │         │ │          │
   │  lint)   │ │         │ │          │
   └────┬─────┘ └────┬────┘ └─────┬────┘
        │            │            │
        │ (checks    │ (checks    │ (waits for
        │  api/**,   │  web/**    │  all checks)
        │  db/**)    │  changes)  │
        │            │            │
        ▼            ▼            ▼
┌─────────────────────────────────────────┐
│    All checks passed                    │
│    Ready to merge!                      │
└─────────────────────────────────────────┘
```

## Workflows

### 1. build-and-test-api.yml
**Purpose**: Lint, build and test the .NET API with EF Core database layer

**Triggers**:
- All pull requests to `main`
- Pushes to `main` (only when `api/**` or `db/**` changes)
- Manual via `workflow_dispatch`

**Path Detection**:
- Checks if files changed in: `api/**`, `db/**`, workflow files
- If no changes detected: passes immediately with skip message
- If changes detected: runs full lint, build and test

### 2. build-and-test-web.yml
**Purpose**: Build and test the Vue.js web application

**Triggers**:
- All pull requests to `main`
- Pushes to `main` (only when `web/**` changes)
- Manual via `workflow_dispatch`

**Path Detection**:
- Checks if files changed in: `web/**`, workflow files
- If no changes detected: passes immediately with skip message
- If changes detected: runs full build and test

### 3. merge-gatekeeper.yml
**Purpose**: Final check ensuring all required workflows have passed

**Triggers**:
- Pull requests to `main`
- Merge groups (for merge queues)


## Local Development

Run quality checks locally before pushing:

### API
```bash
cd api

# Restore dependencies
dotnet restore

# Format code
dotnet csharpier format .

# Check formatting (what CI runs)
dotnet csharpier check .

# Check style
dotnet format style --verify-no-changes

# Check analyzers
dotnet format analyzers --verify-no-changes

# Build
dotnet build
```

### Web
```bash
cd web

# Install dependencies
npm ci

# Lint code
npm run lint

# Format code
npm run prettier:write

# Check formatting (what CI runs)
npm run prettier:check

# Run tests
npm run test

# Build
npm run build
```

## Troubleshooting

### "Required check is pending"
- The workflow is waiting for file change detection
- Should complete within 10-20 seconds

### "Check was skipped"  
- This is normal! The workflow detected no relevant file changes
- The check should still show as passed

### "Merge Gate is failing"
- One or more of the other 3 checks failed
- Click on "Merge Gate" to see which checks are failing
- Fix the failing check and push a new commit

### Formatting failures
Run these commands locally to auto-fix:
```bash
# API
dotnet csharpier format ./api
dotnet csharpier format ./db

# Web  
cd web
npm run prettier:write
npm run lint:fix
```

## Best Practices

1. **Always run local checks before pushing**
   - Saves CI/CD time
   - Catches issues earlier
   - Faster feedback loop

2. **Keep PRs focused**
   - Smaller PRs = faster checks
   - Only relevant workflows run
   - Easier code review

3. **Watch for workflow failures**
   - GitHub sends email notifications
   - Fix failures promptly
   - **Don't force-merge**