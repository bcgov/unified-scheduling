using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Unified.Authorization;
using Unified.Authorization.Claims;
using Unified.Db.Models.UserManagement;
using Unified.Infrastructure.OpenApi;
using Unified.UserManagement.Controllers;

namespace Unified.Tests.UserManagement.Controllers;

public sealed class AuthResponseContractTests
{
    [Fact]
    public async Task UserResponseAndOpenApi_ExposeMigrationPermissionsAsStrings()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { ApplicationName = "Unified.Api" });
        builder.WebHost.UseTestServer();
        builder.Services.ConfigureHttpJsonOptions(options =>
            options.SerializerOptions.NumberHandling = JsonNumberHandling.Strict
        );
        builder
            .Services.AddControllers()
            .ConfigureApplicationPartManager(parts =>
            {
                parts.ApplicationParts.Clear();
                parts.ApplicationParts.Add(new AssemblyPart(typeof(AuthController).Assembly));
            });
        builder.Services.AddAuthorization();
        builder.Services.AddSingleton<IUserAccountResolutionService, StubUserAccountResolutionService>();
        builder.Services.AddUnifiedOpenApi();
        await using var app = builder.Build();
        app.Use(
            async (context, next) =>
            {
                context.User = new ClaimsPrincipal(
                    new ClaimsIdentity(
                        [
                            new Claim(UnifiedClaimTypes.UserId, Guid.NewGuid().ToString()),
                            new Claim(UnifiedClaimTypes.Permission, "DataMigrationOperate"),
                            new Claim(UnifiedClaimTypes.Permission, "DataMigrationRollback"),
                            new Claim(UnifiedClaimTypes.Permission, "UnknownPermission"),
                        ],
                        "TestAuth"
                    )
                );
                await next();
            }
        );
        app.UseAuthorization();
        app.MapControllers();
        app.MapOpenApi();
        await app.StartAsync(TestContext.Current.CancellationToken);

        using var client = app.GetTestClient();
        using var response = await client.GetAsync("/api/auth/user", TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
        using var user = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)
        );
        Assert.True(user.RootElement.GetProperty("isAuthenticated").GetBoolean());
        Assert.True(user.RootElement.GetProperty("isRegistered").GetBoolean());
        Assert.Equal(
            ["DataMigrationOperate", "DataMigrationRollback"],
            user.RootElement.GetProperty("permissions").EnumerateArray().Select(value => value.GetString()).ToArray()
        );

        using var openApiResponse = await client.GetAsync("/openapi/v1.json", TestContext.Current.CancellationToken);
        openApiResponse.EnsureSuccessStatusCode();
        var openApiJson = await openApiResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        using var openApi = JsonDocument.Parse(openApiJson);
        var schemas = openApi.RootElement.GetProperty("components").GetProperty("schemas");
        Assert.Equal(
            "#/components/schemas/Permissions",
            schemas
                .GetProperty("UserInfo")
                .GetProperty("properties")
                .GetProperty("permissions")
                .GetProperty("items")
                .GetProperty("$ref")
                .GetString()
        );
        var permissions = schemas
            .GetProperty("Permissions")
            .GetProperty("enum")
            .EnumerateArray()
            .Select(value => value.GetString())
            .ToArray();
        Assert.Contains("DataMigrationOperate", permissions);
        Assert.Contains("DataMigrationRollback", permissions);
    }

    private sealed class StubUserAccountResolutionService : IUserAccountResolutionService
    {
        public Task UpdateCurrentUserLastLoginAsync(
            ClaimsPrincipal principal,
            CancellationToken cancellationToken = default
        ) => Task.CompletedTask;

        public Task<User?> ResolveCurrentUserAsync(
            ClaimsPrincipal principal,
            bool recordLogin = false,
            CancellationToken cancellationToken = default
        ) => Task.FromResult<User?>(null);
    }
}
