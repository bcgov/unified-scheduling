using Microsoft.FeatureManagement;
using Unified.Core;
using Unified.Stats;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
builder.Services.AddFeatureManagement();

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
// builder.Services.AddOpenApi();

// Modules
builder.Services.AddCoreModule();

if (builder.Configuration.GetValue<bool>("FeatureManagement:Stats"))
{
    builder.Services.AddStatsModule();
}

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    // app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

// Modules
if (await app.Services.GetRequiredService<IFeatureManager>().IsEnabledAsync("Stats"))
{
    app.MapStatsEndpoints();
}

app.Run();
