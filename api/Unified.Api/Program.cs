using System.Text.Json.Serialization;
using Microsoft.AspNetCore.HttpOverrides;
using Unified.Api.Services;
using Unified.Core;
using Unified.Db;
using Unified.FeatureFlags;
using Unified.Infrastructure;
using Unified.Infrastructure.Options;
using Unified.Stats;
using Unified.UserManagement;

var builder = WebApplication.CreateBuilder(args);
var featureFlagsOptions =
    builder.Configuration.GetSection(FeatureFlags.SectionName).Get<FeatureFlags>() ?? new FeatureFlags();

{
    // Add services to the container.
    builder.Services.AddUnifiedErrorHandling();

    // Configure forwarded headers so the app sees the external scheme/host
    // from the nginx reverse proxy. Required for OIDC token exchange to
    // construct the correct redirect_uri.
    builder.Services.Configure<ForwardedHeadersOptions>(options =>
    {
        options.ForwardedHeaders =
            ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost;
        // Trust all proxies (nginx runs in the same pod / overlay network)
        options.KnownIPNetworks.Clear();
        options.KnownProxies.Clear();
    });

    builder
        .Services.Configure<RouteOptions>(options => options.LowercaseUrls = true)
        .ConfigureHttpJsonOptions(options =>
        {
            options.SerializerOptions.NumberHandling = JsonNumberHandling.Strict;
        });

    builder.Services.AddControllers();

    // Modules
    builder
        .Services.AddInfrastructureModule()
        .AddCoreModule()
        .AddDbModule(builder.Configuration)
        .AddUserManagementModule();

    builder.Services.AddSingleton<MigrationAndSeedService>();
    builder.Services.AddTransient(typeof(SeederFactory<>));

    builder.Services.AddUnifiedOpenApi();

    // CORS — mirrors the Probate pattern; origins are comma-separated in config
    var corsOptions = builder.Configuration.GetSection(CorsOptions.SectionName).Get<CorsOptions>();

    builder.Services.AddCors(options =>
    {
        options.AddPolicy(
            "UnifiedCorsPolicy",
            policy =>
                policy
                    .WithOrigins(corsOptions.AllowedOrigins.Split(','))
                    .AllowAnyMethod()
                    .AllowAnyHeader()
                    .AllowCredentials()
        );
    });

    // Authentication & Authorization
    builder.Services.AddUnifiedAuthentication(builder.Environment, builder.Configuration);
    builder.Services.AddAuthorization();

    if (featureFlagsOptions.StatsModule)
    {
        builder.Services.AddStatsModule();
    }
}

var app = builder.Build();
{
    // Must be first so Request.Scheme, Request.Host, etc. reflect the
    // external URL for all downstream middleware (OIDC, cookies, etc.).
    app.UseForwardedHeaders();

    // Base path support
    var basePath = builder.Configuration["WEB_BASE_HREF"]?.Trim();
    if (!string.IsNullOrEmpty(basePath))
    {
        if (!basePath.StartsWith("/"))
            basePath = "/" + basePath;

        basePath = basePath.TrimEnd('/');

        if (!string.IsNullOrEmpty(basePath) && basePath != "/")
            app.UsePathBase(basePath);
    }

    // Run database migrations
    var migrationService = app.Services.GetRequiredService<MigrationAndSeedService>();
    await migrationService.ExecuteMigrationsAndSeeds();

    // Configure the HTTP request pipeline.
    app.UseUnifiedErrorHandling();
    app.UseUnifiedOpenApi();
    app.UseHttpsRedirection();
    app.UseRouting();

    // CORS must be between UseRouting and UseAuthentication
    app.UseCors("UnifiedCorsPolicy");

    app.UseAuthentication();
    app.UseAuthorization();

    app.MapControllers();

    if (featureFlagsOptions.StatsModule)
    {
        app.MapStatsEndpoints();
    }
}

app.Run();
