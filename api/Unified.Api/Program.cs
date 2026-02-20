using Microsoft.AspNetCore.OpenApi;
using Microsoft.FeatureManagement;
using Scalar.AspNetCore;
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
builder.Services.AddOpenApi("v1");

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
    app.MapOpenApi("/openapi/{documentName}.json");
    app.MapScalarApiReference(
        "/openapi",
        (options) =>
        {
            options
                .WithTitle("Unified.API")
                .DisableAgent()
                .HideClientButton()
                .HideDeveloperTools()
                .ShowOperationId();
        }
    );
    app.MapGet("/swagger", () => Results.Redirect("/openapi"));
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
