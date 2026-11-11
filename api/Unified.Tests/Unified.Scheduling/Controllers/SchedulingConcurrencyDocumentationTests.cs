using System.Reflection;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Unified.Calendar.Models;
using Unified.Scheduling.Controllers;
using Unified.Scheduling.OpenApi;

namespace Unified.Tests.Scheduling.Controllers;

public sealed class SchedulingConcurrencyDocumentationTests
{
    [Theory]
    [InlineData(typeof(ShiftController), nameof(ShiftController.UpdateShiftSeries), typeof(ProblemDetails))]
    [InlineData(
        typeof(AssignmentController),
        nameof(AssignmentController.UpdateAssignmentSeries),
        typeof(ProblemDetails)
    )]
    [InlineData(
        typeof(ShiftController),
        nameof(ShiftController.UpdateShiftEntry),
        typeof(CalendarConflictRejectionResponse)
    )]
    [InlineData(
        typeof(AssignmentController),
        nameof(AssignmentController.UpdateAssignmentEntry),
        typeof(CalendarConflictRejectionResponse)
    )]
    public void UpdateEndpoints_DeclareOneConflictResponseWithoutReplacingCalendarConflicts(
        Type controller,
        string method,
        Type body
    )
    {
        var response = Assert.Single(
            controller.GetMethod(method)!.GetCustomAttributes<ProducesResponseTypeAttribute>(),
            attribute => attribute.StatusCode == StatusCodes.Status409Conflict
        );

        Assert.Equal(body, response.Type);
    }

    [Fact]
    public async Task OpenApi_EntryUpdatesDocumentBothCalendarAndConcurrencyConflictBodies()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder
            .Services.AddControllers()
            .ConfigureApplicationPartManager(parts =>
            {
                parts.ApplicationParts.Clear();
                parts.ApplicationParts.Add(new AssemblyPart(typeof(ShiftController).Assembly));
            });
        builder.Services.AddAuthorization();
        builder.Services.AddOpenApi(
            "v1",
            options => options.AddOperationTransformer<SchedulingConcurrencyOperationTransformer>()
        );
        await using var app = builder.Build();
        app.UseAuthorization();
        app.MapControllers();
        app.MapOpenApi();
        await app.StartAsync(TestContext.Current.CancellationToken);

        using var response = await app.GetTestClient()
            .GetAsync("/openapi/v1.json", TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)
        );
        foreach (var entity in new[] { "shifts", "assignments" })
        {
            var conflict = document
                .RootElement.GetProperty("paths")
                .GetProperty($"/api/scheduling/{entity}/entries/{{id}}")
                .GetProperty("put")
                .GetProperty("responses")
                .GetProperty("409")
                .GetProperty("content");
            Assert.Contains(
                "CalendarConflictRejectionResponse",
                conflict.GetProperty("application/json").GetProperty("schema").GetProperty("$ref").GetString()
            );
            var problem = conflict.GetProperty("application/problem+json").GetProperty("schema");
            Assert.True(
                problem.TryGetProperty("$ref", out var reference)
                    ? reference.GetString()!.Contains("ProblemDetails")
                    : problem.GetProperty("properties").TryGetProperty("title", out _)
            );
        }
    }
}
