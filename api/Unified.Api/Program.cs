using Unified.Core;
using Unified.Stats;


var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
// builder.Services.AddOpenApi();

// Modules 
builder.Services.AddCoreModule();

if (builder.Configuration["Modules"]?.Contains("Stats") == true)
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
if (builder.Configuration["Modules"]?.Contains("Stats") == true)
{
    app.MapStatsEndpoints();
}

app.Run();
