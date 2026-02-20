using Microsoft.AspNetCore.OpenApi;
using Microsoft.FeatureManagement;
using Unified.Auth;
using Unified.Core;
using Unified.Infrastructure;
using Unified.Stats;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddInfrastructureModule();

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddFeatureManagement();

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

// Modules
builder.Services.AddCoreModule();
builder.Services.AddAuthModule();

if (builder.Configuration.GetValue<bool>("FeatureManagement:Stats"))
{
    builder.Services.AddStatsModule();
}

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/openapi/v1.json", "v1");
    });
}

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
