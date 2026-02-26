# Unified API

## Mapster DTO generation

From repository root:

1. Restore local tools:
	- `dotnet tool restore`
2. Generate DTOs for `Unified.Auth`:
	- `dotnet build api/Unified.Auth/Unified.Auth.csproj -t:GenerateMapsterDtos`

Generated files are written to:

- `api/Unified.Auth/Models/Generated`

