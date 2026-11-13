# Unified.Infrastructure Module

Infrastructure layer providing authentication, authorization, and configuration management.

## Skills Used

This module follows patterns from the **[dotnet-10-csharp-14](https://github.com/bcgov/unified-scheduling/blob/main/.agents/skills/dotnet-10-csharp-14)** agent skill, specifically:

- **Options Pattern & Validation** - Uses `IOptions<T>` with `ValidateDataAnnotations()` and `ValidateOnStart()` for fail-fast configuration validation at application startup
- **.NET 10 Built-in Validation** - Leverages data annotations instead of manual validation logic

## Overview

The Infrastructure module configures Keycloak Cookie and OpenID Connect authentication, automatic token refresh, and encryption of shared Data Protection keys. Authorization is registered separately by the API's Authorization module.

## Key Components

### KeycloakOptions

Configuration class for Keycloak settings with automatic validation at startup.

**Configuration Section:** `Keycloak`

**Required Properties:**

- `Authority` - Keycloak realm URL
- `Client` - OpenID Connect Client ID
- `Secret` - OpenID Connect Client Secret
- `Audience` - Required by the existing options contract; no JWT bearer handler is currently registered

**Optional Properties:**

- `TokenRefreshThreshold` - TimeSpan before access-token expiration when refresh begins (default: 1 minute)
- `KcIdpHint` - Identity provider hint (default: `idir`)
- `CookieName` - Authentication cookie name (default: `UnifiedAuth`, with `.Development` appended in Development)

The OIDC callback is configured as `/api/auth/signin-oidc` in authentication registration; the `CallbackPath` option is not currently used there.

### InfrastructureModule

Main module for dependency injection setup.

**Registration:**

```csharp
builder.Services.AddInfrastructureModule(builder.Environment, builder.Configuration);

// Program.cs composes Infrastructure encryption and Db persistence.
builder.Services.AddUnifiedDataProtection(builder.Configuration)
    .PersistKeysToDbContext<DataProtectionKeyDbContext>();
```

**What it configures:**

1. Validates Keycloak options at startup
2. Configures Cookie authentication with automatic token refresh
3. Configures OpenID Connect authentication (OIDC)
4. Registers HttpClient for token refresh operations
5. Adds HttpContextAccessor for accessing request context

`AddUnifiedDataProtection` separately validates Data Protection options, sets the stable application name, and configures AES-256-GCM encryption. `Program.cs` selects the database repository. No temporary service provider is built during registration.

## Authentication Schemes

### 1. Cookie Authentication

- Stores authentication tokens in secure HTTP-only cookies
- Automatically refreshes access tokens when they near expiration
- Uses modern `HttpClient` with `FormUrlEncodedContent` (no deprecated libraries)
- Configured with SameSite=None and SecurePolicy=Always for cross-origin support
- Retains only the refresh token, ID token, and access-token expiration in the protected ticket

**Token Refresh Flow:**

- Checks token expiration on each authenticated request
- If within `TokenRefreshThreshold`, uses refresh token to get new access token
- Updates cookies with new tokens transparently
- Rejects invalid or revoked refresh tokens without deleting a replacement cookie that another concurrent request may have issued
- Applies a 10-second process-local cooldown after transient Keycloak failures; an expired access token is never accepted during the cooldown

Concurrent requests can still produce one successful refresh and one HTTP 401 when Keycloak rotates refresh tokens. The failed response no longer deletes the successful response's cookie, but the frontend may begin reauthentication if it handles the HTTP 401 before the successful response. The cooldown is process-local and is therefore best-effort across multiple API replicas.

Authentication-cookie tests use a conservative 16 KB combined `Cookie` request-header budget. Production nginx accepts request-header buffers up to 32 KB, but compatibility with every upstream ingress limit still requires deployment verification.

### 2. OpenID Connect (OIDC)

- Handles interactive authentication flows (login redirects)
- Uses PKCE (Proof Key for Code Exchange) for enhanced security
- Signs into Cookie scheme after successful authentication
- Maps IDIR claims from the user-info response
- Supports identity provider hints (e.g., `idir` for BC Gov IDIR)
- Saves only the refresh token, ID token, and access-token expiration in the authentication cookie

The default authenticate, challenge, and general authentication scheme is Cookies. OpenID Connect is used explicitly for interactive login; no JWT bearer authentication handler is currently registered.

## Configuration Validation

Configuration is validated automatically at application startup using the Options pattern:

```csharp
services.AddOptions<KeycloakOptions>()
    .BindConfiguration(KeycloakOptions.SectionName)
    .ValidateDataAnnotations()  // Validates [Required], [Url] attributes
    .ValidateOnStart();         // Fails immediately if invalid
```

**Benefits:**

- ✅ Detects configuration errors at startup, not first use
- ✅ Clear error messages during deployment
- ✅ No runtime surprises in production

## Shared Data Protection

Authentication tickets and OIDC state are protected with ASP.NET Core Data Protection. Instance-local keys prevent another replica from reading a cookie, and container replacement can lose those keys. Every API replica in an environment now uses the same PostgreSQL key ring, application name, and AES encryption secret so protected data survives replica changes and restarts.

### Storage and encryption

`DbModule` registers `DataProtectionKeyDbContext` against the existing `DatabaseConnectionString`. It inherits ordinary EF Core `DbContext`, implements `IDataProtectionKeyContext`, and maps only `DataProtectionKeys`. It does not inherit Audit.NET's `AuditDbContext`, apply business mappings, or attach registered business save-rule interceptors. EF history is isolated in `__DataProtectionMigrationsHistory`; the existing `UnifiedDbContext` history and migrations remain separate.

`Program.cs` connects `PersistKeysToDbContext<DataProtectionKeyDbContext>()` with Infrastructure's encryptor. ASP.NET Core creates, activates, expires, and rotates its own Data Protection keys automatically (the default key lifetime remains 90 days). Expired keys remain available for reading older protected data. No key-generation or renewal job is required.

The XML secret material inside each persisted key is encrypted using AES-256-GCM. Key IDs, dates, algorithm descriptors, and the decryptor type remain readable metadata. The implementation adapts [JASPER's AES-GCM envelope](https://github.com/bcgov/jasper/blob/master/api/Infrastructure/Encryption/AesGcmService.cs) and XML encryptor/decryptor pattern: a fresh cryptographically random 12-byte nonce and a 16-byte authentication tag accompany each ciphertext in Base64. The envelope stores little-endian nonce length, nonce, tag length, tag, and ciphertext. Decryption validates the envelope and authentication tag. Persisted XML names the public `AesGcmXmlDecryptor` type, which resolves the configured secret through DI after a restart. Preserve that type's identity or provide compatibility when changing it in future releases.

The AES secret is a long-lived **key-encryption key**, separate from ASP.NET Core's automatically rotated Data Protection keys. Normal Data Protection key rotation does not change this secret.

### Configuration

Committed `appsettings.json` contains only:

```json
{
  "DataProtection": {
    "ApplicationName": "UnifiedScheduling"
  }
}
```

`DataProtectionOptions` binds the `DataProtection` section using `BindConfiguration`, data annotations, and `ValidateOnStart`. Supply `DataProtection__EncryptionKey` through secret configuration. It must be the Base64 encoding of a securely generated, random **32-byte** value; a 32-character password is not sufficient. No operational encryption key belongs in source control, appsettings, logs, or test fixtures. Tests use deterministic test-only material.

Use the same application name, database/key-ring table, and secret across all replicas of one environment. Keep dev, test, and production in separate databases/key stores with separate AES secrets. A different application name also prevents payload sharing, but is not a substitute for separate key stores and secrets.

### Local Docker

Set `DataProtectionEncryptionKey` in the existing private `docker/.env`. The committed template contains an empty placeholder only:

```dotenv
# Data Protection
DataProtectionEncryptionKey=
```

Compose requires this variable and passes it to the API as `DataProtection__EncryptionKey`. A missing or empty value fails Compose interpolation. A malformed or incorrectly sized value fails API startup. Use the same value and PostgreSQL database for additional local API instances, and keep the PostgreSQL volume and private secret across container recreation. Supply distinct listening ports when running more than one local instance. Native API runs need the same .NET environment variable or an equivalent secret configuration provider.

### OpenShift / GitOps

The app uses standard .NET environment-variable binding. Deployment owners must inject the environment's Secret into **every API replica**, for example:

```yaml
env:
  - name: DataProtection__EncryptionKey
    valueFrom:
      secretKeyRef:
        name: unified-data-protection
        key: encryption-key
```

The Secret's decoded `encryption-key` value must itself contain the Base64 AES configuration string; OpenShift's Secret serialization is an additional encoding layer. Keep this Secret separate for dev/test/prod, and preserve it and the key-ring database through rolling deployments and restores. All replicas must use the same PostgreSQL key table and `DataProtection:ApplicationName`. Do not create a fresh encryption secret on each release.

The repository's deployment workflow points to `services/unified-scheduling/overlays/dev` in the tenant GitOps repository selected by license plate; the PR template also names `bcgov-c/tenant-gitops-cb6495`. That repository was not accessible during implementation (404), so existing Secret wiring has **not** been verified. Add/verify Secret references in the applicable dev/test/prod API deployments before promoting this change. No external repository was modified.

### Migrations and startup failures

`Program.cs` validates the AES configuration before database startup work. `MigrationAndSeedService` runs the existing business migrations, then the dedicated key-context migrations, then existing business seeders. The key context has no seeders. These migrations run before Data Protection key access, authentication middleware, hosted services, or request handling. Database migration failures retain the existing critical log and startup exception behavior; no fallback to local or plaintext keys is configured.

After migrations, startup initializes the encryptor for every persisted key to detect a mismatched secret or unreadable material before accepting requests. Invalid configuration produces non-sensitive validation errors. Missing migration artifacts produce an explicit startup error. A database permission/connectivity error, migration error, or unreadable persisted key stops startup. Startup validation checks every existing key, including expired keys, because they may still be needed to decrypt older payloads.

The dedicated design-time factory reads only `DatabaseConnectionString` from the process environment. It does not read private dotenv files or require an AES secret. Migration generation does not connect to a database or apply migrations, so a syntactically valid dummy connection string is sufficient for generation. Keep existing business migration order and snapshots unchanged. Every future EF command must select the intended context explicitly now that there are two contexts. Normal API startup applies the new context's migration once these artifacts are included in the release. The database user needs the same migration/schema permissions as the existing startup lifecycle, plus read/write access to the key table and its history table.

### Backup, recovery, and secret rotation

- Back up the key-ring table, its migration history, and the matching AES secret through the established database and secret-backup processes. Restrict access to both; possession of both permits decrypting protected authentication data.
- Retain old Data Protection keys while protected payloads may still reference them. Automatic rotation is not permission to delete expired keys. Restoring an older database backup can lose newer keys and invalidate sessions issued with them.
- Restore the matching secret, database key ring, and application name together. Preserve the secret through container restarts and rolling deployments. Test recovery in an isolated environment.
- Losing or replacing the AES secret makes previously persisted Data Protection keys unreadable and can invalidate existing sessions. This implementation fails startup on unreadable stored keys; restore the matching secret to recover. Do not delete the key ring simply to make a deployment start without explicitly accepting session loss.
- **Changing the AES secret is not a supported routine rotation mechanism** without migrating stored key-ring encryption or adding support for historical decryption keys. Neither feature is implemented here. A coordinated migration/recovery plan is required before changing this secret; updating replicas independently can make them unable to decrypt each other's keys.
- On first deployment, old instance-local keys are not imported automatically. If those keys cannot be recovered and migrated compatibly (including their application discriminator), existing cookies and in-flight OIDC state will be unreadable and users will need to sign in again. Mixed old/new replicas may reject each other's cookies during that rollout; coordinate the rollout accordingly. Later releases retain sessions when the shared store, secret, and application name are preserved.

### Verification

`DataProtectionTests` covers independent providers against a shared SQLite database, provider recreation, wrong-secret rejection, startup validation, encrypted XML, isolated database registration, cookie/OIDC configuration, automatic generation settings, replacement-key compatibility, nonce uniqueness, and tamper rejection. An optional PostgreSQL test follows the existing `UNIFIED_TEST_POSTGRES_CONNECTION_STRING` convention and uses only connection-local temporary tables; independent providers share that connection sequentially. It verifies the PostgreSQL repository without changing deployed schemas. Neither these tests nor SQLite verify generated PostgreSQL migrations or multi-process startup races. Existing authentication pipeline tests remain applicable. The API compilation required for EF generation succeeded with existing warnings; the migration artifacts were generated and reviewed, without applying them to a database. Tests and formatters were not run. The developer should run the focused and existing authentication tests, then verify two real API replicas with independent PostgreSQL connections and a rolling restart in the deployment environment before rollout.

## Modern Implementation Notes

### Token Refresh Without IdentityModel.Client

This implementation **does not use** the deprecated `IdentityModel.Client` library. Instead, it uses standard .NET `HttpClient` with `FormUrlEncodedContent`:

```csharp
// Modern approach (used in this module)
var content = new FormUrlEncodedContent(new Dictionary<string, string>
{
    ["grant_type"] = "refresh_token",
    ["client_id"] = keycloakOptions.Client,
    ["client_secret"] = keycloakOptions.Secret,
    ["refresh_token"] = refreshToken
});
var response = await httpClient.PostAsync(tokenEndpoint, content);
var tokenResponse = await response.Content.ReadFromJsonAsync<TokenResponse>();
```

**Benefits:**

- ✅ No external OAuth protocol libraries required
- ✅ Uses built-in .NET HTTP primitives
- ✅ JSON deserialization with System.Text.Json
- ✅ No deprecated dependencies

## For Developers

### Adding New Infrastructure Services

Follow this pattern for new infrastructure components:

```csharp
public static class InfrastructureModule
{
    public static IServiceCollection AddInfrastructureModule(...)
    {
        // 1. Register options with validation
        services.AddOptions<MyServiceOptions>()
            .BindConfiguration(MyServiceOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        // 2. Register services
        services.AddScoped<IMyService, MyService>();

        return services;
    }
}
```

### Testing Configuration

When testing, ensure Keycloak options are properly configured:

```csharp
var config = new ConfigurationBuilder()
    .AddInMemoryCollection(new Dictionary<string, string>
    {
        {"Keycloak:Authority", "https://test-keycloak/realms/test"},
        {"Keycloak:Client", "test-client"},
        {"Keycloak:Secret", "test-secret"},
        {"Keycloak:Audience", "test-api"}
    })
    .Build();
```

## Project Structure

```
Unified.Infrastructure/
├── InfrastructureModule.cs    # Main module for DI setup
├── Options/
│   └── KeycloakOptions.cs     # Configuration class with validation
└── README.md                   # This file
```
