using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Scalar.AspNetCore;
using Unified.Common.Validation;

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

                // Handle authentication errors
                if (context.HttpContext.Response.StatusCode == StatusCodes.Status401Unauthorized)
                {
                    context.ProblemDetails.Title = "Unauthorized";
                    context.ProblemDetails.Detail = "Authentication is required to access this resource.";
                }

                // Handle OpenIdConnect and authentication protocol exceptions
                if (context.Exception is OpenIdConnectProtocolException)
                {
                    context.HttpContext.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    context.ProblemDetails.Status = StatusCodes.Status401Unauthorized;
                    context.ProblemDetails.Title = "Authentication Failed";
                    context.ProblemDetails.Detail = "An authentication error occurred. Please try again.";
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
