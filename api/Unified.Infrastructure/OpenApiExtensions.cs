using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Scalar.AspNetCore;

namespace Unified.Infrastructure;

public static class OpenApiExtensions
{
    public static IServiceCollection AddUnifiedOpenApi(this IServiceCollection services)
    {
        services.AddOpenApi("v1");
        return services;
    }

    public static WebApplication UseUnifiedOpenApi(this WebApplication app)
    {
        if (!app.Environment.IsDevelopment())
            return app;

        app.MapOpenApi("/openapi/{documentName}.json");

        app.MapScalarApiReference(
            "/openapi",
            options =>
            {
                options.WithTitle("Unified.API").DisableAgent().HideClientButton().ShowOperationId();
            }
        );

        app.MapGet("/", () => Results.Redirect("/openapi")).ExcludeFromDescription();
        app.MapGet("/swagger", () => Results.Redirect("/openapi")).ExcludeFromDescription();

        return app;
    }
}