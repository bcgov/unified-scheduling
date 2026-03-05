using System.Text.Json.Serialization;
using Microsoft.FeatureManagement;
using Unified.Auth;
using Unified.Core;
using Unified.Infrastructure;
using Unified.Stats;

var builder = WebApplication.CreateBuilder(args);
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
    builder.Services.AddFeatureManagement();

    // Modules
    builder.Services.AddInfrastructureModule().AddCoreModule().AddAuthModule();

    builder.Services.AddUnifiedOpenApi();

    if (builder.Configuration.GetValue<bool>("FeatureManagement:Stats"))
    {
        builder.Services.AddStatsModule();
    }
}

var app = builder.Build();
{
    // Run database migrations
    await app.MigrateAuthDatabaseAsync();

    // Configure the HTTP request pipeline.
    app.UseUnifiedErrorHandling();
    app.UseUnifiedOpenApi();
    app.UseHttpsRedirection();

    app.UseAuthentication().UseAuthorization();

    app.MapControllers();

    // Modules
    if (await app.Services.GetRequiredService<IFeatureManager>().IsEnabledAsync("Stats"))
    {
        app.MapStatsEndpoints();
    }
}

app.Run();
