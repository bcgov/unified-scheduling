using System.Text.Json.Serialization;
using Microsoft.FeatureManagement;
using Unified.Auth;
using Unified.Core;
using Unified.Infrastructure;
using Unified.Stats;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<RouteOptions>(options => options.LowercaseUrls = true);
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.NumberHandling = JsonNumberHandling.Strict;
});


builder.Services.AddInfrastructureModule();

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddFeatureManagement();

builder.Services.AddUnifiedOpenApi();

// Modules
builder.Services.AddCoreModule();
builder.Services.AddAuthModule();

if (builder.Configuration.GetValue<bool>("FeatureManagement:Stats"))
{
    builder.Services.AddStatsModule();
}

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseUnifiedOpenApi();

app.UseHttpsRedirection();

app.UseAuthentication();

app.UseAuthorization();

app.MapControllers();

// Modules
if (await app.Services.GetRequiredService<IFeatureManager>().IsEnabledAsync("Stats"))
{
    app.MapStatsEndpoints();
}

app.Run();
