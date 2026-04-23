using System.IO;
using System.Linq;
using System.Text.Json;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Unified.Infrastructure.ErrorHandling;

namespace Unified.Tests.Infrastructure.ErrorHandling;

public class GlobalExceptionHandlerTests
{
    [Fact]
    public async Task TryHandleAsync_Should_Map_ValidationException_To_400_With_Grouped_Errors()
    {
        // Arrange
        var context = CreateHttpContext();
        var handler = CreateHandler(context);
        var exception = new ValidationException(
            new[]
            {
                new ValidationFailure("Name", "Name is required") { ErrorCode = "NotEmptyValidator" },
                new ValidationFailure("Name", "Name is too short") { ErrorCode = "LengthValidator" },
                new ValidationFailure("Code", "Code is required") { ErrorCode = "NotEmptyValidator" },
            }
        );

        // Act
        var handled = await handler.TryHandleAsync(context, exception, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(handled);
        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);

        var body = await ReadJsonBodyAsync(context);
        Assert.Equal("Validation failed.", body.RootElement.GetProperty("title").GetString());

        var errors = body.RootElement.GetProperty("errors");
        var nameErrors = errors.GetProperty("Name").EnumerateArray().Select(x => x.GetString()).ToArray();
        var codeErrors = errors.GetProperty("Code").EnumerateArray().Select(x => x.GetString()).ToArray();

        Assert.Contains("NotEmptyValidator", nameErrors);
        Assert.Contains("LengthValidator", nameErrors);
        Assert.Single(codeErrors);
        Assert.Equal("NotEmptyValidator", codeErrors[0]);
    }

    [Fact]
    public async Task TryHandleAsync_Should_Map_DbUpdateConcurrencyException_To_409()
    {
        // Arrange
        var context = CreateHttpContext();
        var handler = CreateHandler(context);
        var exception = new DbUpdateConcurrencyException("conflict");

        // Act
        var handled = await handler.TryHandleAsync(context, exception, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(handled);
        Assert.Equal(StatusCodes.Status409Conflict, context.Response.StatusCode);

        var body = await ReadJsonBodyAsync(context);
        Assert.Equal("Conflict.", body.RootElement.GetProperty("title").GetString());
    }

    [Fact]
    public async Task TryHandleAsync_Should_Map_Unknown_Exception_To_500_Without_Leaking_Message()
    {
        // Arrange
        var context = CreateHttpContext();
        var handler = CreateHandler(context);
        var exception = new Exception("Database password is secret");

        // Act
        var handled = await handler.TryHandleAsync(context, exception, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(handled);
        Assert.Equal(StatusCodes.Status500InternalServerError, context.Response.StatusCode);

        var body = await ReadJsonBodyAsync(context);
        Assert.Equal(
            "An error occurred while processing your request.",
            body.RootElement.GetProperty("title").GetString()
        );
        Assert.Equal("An unexpected server error occurred.", body.RootElement.GetProperty("detail").GetString());
        Assert.True(body.RootElement.TryGetProperty("traceId", out _));
    }

    [Fact]
    public async Task TryHandleAsync_Should_Map_OperationCanceledException_To_499()
    {
        // Arrange
        var context = CreateHttpContext();
        var handler = CreateHandler(context);
        var exception = new OperationCanceledException();

        // Act
        var handled = await handler.TryHandleAsync(context, exception, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(handled);
        Assert.Equal(StatusCodes.Status499ClientClosedRequest, context.Response.StatusCode);
    }

    private static GlobalExceptionHandler CreateHandler(HttpContext context)
    {
        var services = new ServiceCollection();
        services.AddProblemDetails();

        var provider = services.BuildServiceProvider();
        context.RequestServices = provider;

        var problemDetailsService = provider.GetRequiredService<IProblemDetailsService>();

        return new GlobalExceptionHandler(NullLogger<GlobalExceptionHandler>.Instance, problemDetailsService);
    }

    private static DefaultHttpContext CreateHttpContext()
    {
        return new DefaultHttpContext { Response = { Body = new MemoryStream() } };
    }

    private static async Task<JsonDocument> ReadJsonBodyAsync(HttpContext context)
    {
        context.Response.Body.Seek(0, SeekOrigin.Begin);
        return await JsonDocument.ParseAsync(context.Response.Body);
    }
}
