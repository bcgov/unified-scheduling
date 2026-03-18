using System.Text.Json.Serialization;
using Unified.Api.Services;
using Unified.Core;
using Unified.Db;
using Unified.FeatureFlags;
using Unified.Infrastructure;
using Unified.Stats;
using Unified.UserManagement;

var builder = WebApplication.CreateBuilder(args);
var featureFlagsOptions =
    builder.Configuration.GetSection(FeatureFlags.SectionName).Get<FeatureFlags>() ?? new FeatureFlags();

{
    // Add services to the container.
    builder.Services.AddUnifiedErrorHandling();

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

    if (featureFlagsOptions.StatsModule)
    {
        builder.Services.AddStatsModule();
    }
}

var app = builder.Build();
{
    // Run database migrations
    var migrationService = app.Services.GetRequiredService<MigrationAndSeedService>();
    await migrationService.ExecuteMigrationsAndSeeds();

    // Configure the HTTP request pipeline.
    app.UseUnifiedErrorHandling();
    app.UseUnifiedOpenApi();
    app.UseHttpsRedirection();

    app.UseAuthentication().UseAuthorization();

    app.MapControllers();

    // Modules
    if (featureFlagsOptions.StatsModule)
    {
        app.MapStatsEndpoints();
    }
}

app.Run();
