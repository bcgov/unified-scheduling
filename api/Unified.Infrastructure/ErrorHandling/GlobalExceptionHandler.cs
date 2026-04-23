using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

namespace Unified.Infrastructure.ErrorHandling;

public class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
    {
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken
    )
    {
        if (exception is OperationCanceledException)
        {
            httpContext.Response.StatusCode = 499; // Client Closed Request
            return true;
        }

        object problemDetails = exception switch
        {
            ValidationException ex => HandleValidationException(ex, httpContext),
            KeyNotFoundException ex => HandleKeyNotFoundException(ex, httpContext),
            InvalidOperationException ex => HandleInvalidOperationException(ex, httpContext),
            DbUpdateConcurrencyException ex => HandleConcurrencyException(ex, httpContext),
            OpenIdConnectProtocolException ex => HandleAuthenticationException(ex, httpContext),
            _ => HandleUnknownException(exception, httpContext),
        };

        await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);
        return true;
    }

    private ValidationProblemDetails HandleValidationException(ValidationException ex, HttpContext httpContext)
    {
        _logger.LogInformation(ex, "Validation failed: {Message}", ex.Message);
        httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;

        var errors = ex
            .Errors.GroupBy(f => f.PropertyName)
            .ToDictionary(g => g.Key, g => g.Select(f => f.ErrorCode).Distinct().ToArray());

        return new ValidationProblemDetails(errors)
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "Validation failed.",
        };
    }

    private ProblemDetails HandleKeyNotFoundException(KeyNotFoundException ex, HttpContext httpContext)
    {
        _logger.LogInformation(ex, "Resource not found: {Message}", ex.Message);
        httpContext.Response.StatusCode = StatusCodes.Status404NotFound;

        return new ProblemDetails
        {
            Status = StatusCodes.Status404NotFound,
            Title = "Resource not found.",
            Detail = ex.Message,
        };
    }

    private ProblemDetails HandleInvalidOperationException(InvalidOperationException ex, HttpContext httpContext)
    {
        _logger.LogInformation(ex, "Invalid operation: {Message}", ex.Message);
        httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;

        return new ProblemDetails
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "Invalid request.",
            Detail = ex.Message,
        };
    }

    private ProblemDetails HandleConcurrencyException(DbUpdateConcurrencyException ex, HttpContext httpContext)
    {
        _logger.LogWarning(ex, "Concurrency conflict: {Message}", ex.Message);
        httpContext.Response.StatusCode = StatusCodes.Status409Conflict;

        return new ProblemDetails
        {
            Status = StatusCodes.Status409Conflict,
            Title = "Conflict.",
            Detail = "The resource was modified by another request. Please retry.",
        };
    }

    private ProblemDetails HandleAuthenticationException(Exception ex, HttpContext httpContext)
    {
        _logger.LogWarning(ex, "Authentication error: {Message}", ex.Message);
        httpContext.Response.StatusCode = StatusCodes.Status401Unauthorized;

        return new ProblemDetails
        {
            Status = StatusCodes.Status401Unauthorized,
            Title = "Unauthorized.",
            Detail = "Authentication is required to access this resource.",
        };
    }

    private ProblemDetails HandleUnknownException(Exception ex, HttpContext httpContext)
    {
        _logger.LogError(ex, "An unhandled exception occurred");
        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;

        return new ProblemDetails
        {
            Status = StatusCodes.Status500InternalServerError,
            Title = "An error occurred while processing your request.",
            Detail = ex.Message,
        };
    }
}
