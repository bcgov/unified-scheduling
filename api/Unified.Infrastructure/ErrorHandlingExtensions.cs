using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Scalar.AspNetCore;

namespace Unified.Infrastructure;

public static class ErrorHandlingExtensions
{
    public static IServiceCollection AddUnifiedErrorHandling(this IServiceCollection services)
    {
        services.AddProblemDetails(options =>
        {
            options.CustomizeProblemDetails = context =>
            {
                context.ProblemDetails.Extensions["traceId"] = context.HttpContext.TraceIdentifier;

                if (context.HttpContext.Response.StatusCode == StatusCodes.Status404NotFound)
                {
                    context.ProblemDetails.Title = "The requested resource was not found.";
                    context.ProblemDetails.Detail = "Please check the URL and try again.";
                }
            };
        });
        return services;
    }

    public static WebApplication UseUnifiedErrorHandling(this WebApplication app)
    {
        app.UseExceptionHandler();

        return app;
    }
}
