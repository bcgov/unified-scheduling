# CHES email integration

This infrastructure adapter implements `Unified.Core.Email.IEmailService` using the BC Government Common Hosted Email Service (CHES). Feature modules depend only on the shared interface and models; they do not use generated CHES types or authentication directly.

## Configuration

The integration is opt-in through the `Ches` configuration section. When `Ches:Enabled` is `false`, CHES services are not registered and CHES-specific configuration is not validated. When enabled, all required settings are validated at startup.

Credentials must come from the application's deployment secret mechanism, normally through `Ches__ClientId` and `Ches__ClientSecret` environment variables.

`AllowedAttachmentTypes` is a list of approved extension/MIME-type pairs. Both values must match the same pair; listing an extension and MIME type in different entries does not approve that combination.

Attachment content is supplied as a caller-owned readable stream. The caller must keep the stream open until `SendAsync` completes and remains responsible for disposing it. The adapter never loads attachment content from a filesystem path. HTTP endpoints can pass a stream opened from `IFormFile` without adding an ASP.NET dependency to the shared email contract.

Default locally enforced limits are below, but can be overriden:

- 500 recipients per message;
- 20 MB per attachment;
- PDF attachments only (`.pdf` with `application/pdf`).

CHES also documents operational limits of 10,000 recipients per day and 30 messages per minute.

## OpenAPI client generation

- Official source: <https://ches.api.gov.bc.ca/api/v1/docs/api-spec.yaml>
- Checked-in specification: `OpenApi/api-spec.yaml`
- NSwag configuration: `OpenApi/nswag-ches.json`
- Generated client: `OpenApi/ChesClient.cs`
- Pinned tool: `NSwag.ConsoleCore` 14.7.1 in the repository `.config/dotnet-tools.json`

Restore repository tools once from the repository root:

```bash
dotnet tool restore
```

The verified regeneration command is:

```bash
cd api/Unified.Infrastructure/Email/Ches/OpenApi
dotnet nswag run nswag-ches.json
```

The generator reads only the checked-in `api-spec.yaml`; normal builds do not download the specification. Do not edit `ChesClient.cs` manually. To update CHES, deliberately replace `api-spec.yaml` from the official URL currently (https://ches.api.gov.bc.ca/api/v1/docs/api-spec.yaml), regenerate, and review the generated diff before committing it.
