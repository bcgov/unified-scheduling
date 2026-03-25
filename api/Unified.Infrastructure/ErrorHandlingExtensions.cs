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

        services.Configure<ApiBehaviorOptions>(options =>
        {
            options.InvalidModelStateResponseFactory = context =>
            {
                var errors = context
                    .ModelState.Where(x => x.Value is { Errors.Count: > 0 })
                    .ToDictionary(
                        x => ToCamelCaseFieldName(x.Key),
                        x =>
                            x.Value!.Errors.Select(e =>
                                    string.IsNullOrWhiteSpace(e.ErrorMessage)
                                        ? ApiValidationErrorCodes.Invalid
                                        : e.ErrorMessage
                                )
                                .Distinct()
                                .ToArray()
                    );

                var problemDetails = new ProblemDetails
                {
                    Status = StatusCodes.Status400BadRequest,
                    Title = "One or more validation errors occurred.",
                    Type = "https://datatracker.ietf.org/doc/html/rfc9110#section-15.5.1",
                };

                problemDetails.Extensions["traceId"] = context.HttpContext.TraceIdentifier;
                problemDetails.Extensions["errors"] = errors;

                return new BadRequestObjectResult(problemDetails);
            };
        });

        return services;
    }

    public static WebApplication UseUnifiedErrorHandling(this WebApplication app)
    {
        app.UseExceptionHandler();

        return app;
    }

    private static string ToCamelCaseFieldName(string fieldName)
    {
        if (string.IsNullOrWhiteSpace(fieldName))
        {
            return fieldName;
        }

        var segments = fieldName.Split('.', StringSplitOptions.RemoveEmptyEntries);
        var leafName = segments.Length > 0 ? segments[^1] : fieldName;

        return leafName.Length switch
        {
            0 => fieldName,
            1 => leafName.ToLowerInvariant(),
            _ => char.ToLowerInvariant(leafName[0]) + leafName[1..],
        };
    }
}
