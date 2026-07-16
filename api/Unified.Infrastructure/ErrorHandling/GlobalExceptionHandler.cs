using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
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
    private readonly IProblemDetailsService _problemDetailsService;

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger, IProblemDetailsService problemDetailsService)
    {
        _logger = logger;
        _problemDetailsService = problemDetailsService;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken
    )
    {
        if (exception is OperationCanceledException)
        {
            httpContext.Response.StatusCode = StatusCodes.Status499ClientClosedRequest;
            return true;
        }

        var problemDetails = exception switch
        {
            ValidationException ex => HandleValidationException(ex, httpContext),
            ForbiddenException ex => HandleForbiddenException(ex, httpContext),
            KeyNotFoundException ex => HandleKeyNotFoundException(ex, httpContext),
            InvalidDataException ex => HandleInvalidDataException(ex, httpContext),
            InvalidOperationException ex => HandleInvalidOperationException(ex, httpContext),
            DbUpdateConcurrencyException ex => HandleConcurrencyException(ex, httpContext),
            OpenIdConnectProtocolException ex => HandleAuthenticationException(ex, httpContext),
            _ => HandleUnknownException(exception, httpContext),
        };

        var wasWritten = await _problemDetailsService.TryWriteAsync(
            new ProblemDetailsContext
            {
                HttpContext = httpContext,
                Exception = exception,
                ProblemDetails = problemDetails,
            }
        );

        if (!wasWritten)
        {
            httpContext.Response.ContentType = "application/problem+json";
            await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);
        }

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
            Extensions = { ["traceId"] = httpContext.TraceIdentifier },
        };
    }

    private ProblemDetails HandleForbiddenException(ForbiddenException ex, HttpContext httpContext)
    {
        _logger.LogInformation(ex, "Access denied: {Message}", ex.Message);
        httpContext.Response.StatusCode = StatusCodes.Status403Forbidden;

        return new ProblemDetails
        {
            Status = StatusCodes.Status403Forbidden,
            Title = "Access denied.",
            Detail = "You do not have permission to perform this action.",
            Extensions = { ["traceId"] = httpContext.TraceIdentifier },
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
            Extensions = { ["traceId"] = httpContext.TraceIdentifier },
        };
    }

    private ProblemDetails HandleInvalidOperationException(InvalidOperationException ex, HttpContext httpContext)
    {
        _logger.LogError(ex, "Invalid operation: {Message}", ex.Message);
        httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;

        return new ProblemDetails
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "Invalid request.",
            Detail = ex.Message,
            Extensions = { ["traceId"] = httpContext.TraceIdentifier },
        };
    }

    private ProblemDetails HandleInvalidDataException(InvalidDataException ex, HttpContext httpContext)
    {
        _logger.LogInformation(ex, "Failed to read request body: {Message}", ex.Message);
        httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;

        return new ProblemDetails
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "The uploaded file could not be processed.",
            Detail = BuildFriendlyFormReadDetail(ex.Message),
            Extensions = { ["traceId"] = httpContext.TraceIdentifier },
        };
    }

    /// <summary>
    /// Converts framework messages like "Multipart body length limit 409600 exceeded." into a
    /// human-readable message (e.g. "The file exceeds the maximum allowed size of 400 KB.") for
    /// display to end users. Falls back to the original message if the pattern isn't recognized.
    /// </summary>
    private static string BuildFriendlyFormReadDetail(string message)
    {
        var match = Regex.Match(message, @"Multipart body length limit (\d+) exceeded");
        if (!match.Success || !long.TryParse(match.Groups[1].Value, out var limitBytes))
        {
            return message;
        }

        var limitKb = limitBytes / 1024;
        return $"The file exceeds the maximum allowed size of {limitKb} KB.";
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
            Extensions = { ["traceId"] = httpContext.TraceIdentifier },
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
            Extensions = { ["traceId"] = httpContext.TraceIdentifier },
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
            Detail = "An unexpected server error occurred.",
            Extensions = { ["traceId"] = httpContext.TraceIdentifier },
        };
    }
}
