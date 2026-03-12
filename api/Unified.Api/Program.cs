using System.Text.Json.Serialization;
using Unified.Core;
using Unified.Db;
using Unified.Db.Services.EF;
using Unified.Infrastructure;
using Unified.Infrastructure.Options;
using Unified.Stats;
using Unified.UserManagement;

var builder = WebApplication.CreateBuilder(args);
var featureFlagsOptions =
    builder.Configuration.GetSection(FeatureFlagsOptions.SectionName).Get<FeatureFlagsOptions>()
    ?? new FeatureFlagsOptions();

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
