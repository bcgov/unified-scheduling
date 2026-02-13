# Unified.Infrastructure Module

Infrastructure layer providing authentication, authorization, and configuration management.

## Skills Used

This module follows patterns from the **[dotnet-10-csharp-14](https://github.com/bcgov/unified-scheduling/blob/main/.agents/skills/dotnet-10-csharp-14)** agent skill, specifically:

- **Options Pattern & Validation** - Uses `IOptions<T>` with `ValidateDataAnnotations()` and `ValidateOnStart()` for fail-fast configuration validation at application startup
- **.NET 10 Built-in Validation** - Leverages data annotations instead of manual validation logic

## Overview

The Infrastructure module centralizes authentication and authorization configuration using Keycloak with Cookie, OpenID Connect, and JWT Bearer authentication schemes. It includes automatic token refresh for long-lived sessions.

## Key Components

### KeycloakOptions
Configuration class for Keycloak settings with automatic validation at startup.

**Configuration Section:** `Keycloak`

**Required Properties:**
- `Authority` - Keycloak realm URL
- `Client` - OpenID Connect Client ID
- `Secret` - OpenID Connect Client Secret  
- `Audience` - JWT bearer token audience claim

**Optional Properties:**
- `TokenRefreshThreshold` - TimeSpan for when to refresh tokens (default: 5 minutes)
- `CallbackPath` - OIDC callback path (default: `/api/auth/signin-oidc`)
- `IdpHint` - Identity provider hint (default: `idir`)


### InfrastructureModule
Main module for dependency injection setup.

**Registration:**
```csharp
builder.Services.AddInfrastructureModule(builder.Configuration);
```

**What it configures:**
1. Validates Keycloak options at startup
2. Configures Cookie authentication with automatic token refresh
3. Configures OpenID Connect authentication (OIDC)
4. Configures JWT Bearer authentication
5. Enables authorization services
6. Registers HttpClient for token refresh operations
7. Adds HttpContextAccessor for accessing request context

## Authentication Schemes

### 1. Cookie Authentication
- Stores authentication tokens in secure HTTP-only cookies
- Automatically refreshes access tokens when they near expiration
- Uses modern `HttpClient` with `FormUrlEncodedContent` (no deprecated libraries)
- Configured with SameSite=None and SecurePolicy=Always for cross-origin support
- Cookie path restricted to `/api/auth` to minimize unnecessary cookie transmission

**Token Refresh Flow:**
- Checks token expiration on each authenticated request
- If within `TokenRefreshThreshold`, uses refresh token to get new access token
- Updates cookies with new tokens transparently
- Signs out user if refresh fails

### 2. OpenID Connect (OIDC)
- Handles interactive authentication flows (login redirects)
- Uses PKCE (Proof Key for Code Exchange) for enhanced security
- Signs into Cookie scheme after successful authentication
- Removes unnecessary claims to reduce payload size
- Supports identity provider hints (e.g., `idir` for BC Gov IDIR)
- Only saves essential tokens (access_token, refresh_token)

### 3. JWT Bearer
- **Primary default scheme** for API authentication
- Validates tokens against Keycloak authority
- Verifies issuer signature and audience claims
- Custom error handling (401 for auth failures, 403 for forbidden)
- 5-second clock skew tolerance for distributed systems
- Preserves `sub` claim by removing from default claim mapping

## Configuration Validation

Configuration is validated automatically at application startup using the Options pattern:

```csharp
services.AddOptions<KeycloakOptions>()
    .BindConfiguration(KeycloakOptions.ConfigurationSection)
    .ValidateDataAnnotations()  // Validates [Required], [Url] attributes
    .ValidateOnStart();         // Fails immediately if invalid
```

**Benefits:**
- ✅ Detects configuration errors at startup, not first use
- ✅ Clear error messages during deployment
- ✅ No runtime surprises in production

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
            .BindConfiguration(MyServiceOptions.ConfigurationSection)
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
