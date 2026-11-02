using System.Text.Json;
using FluentValidation;
using Unified.Scheduling.Models;
using Unified.Scheduling.Validators;

namespace Unified.Tests.Scheduling.Validators;

public sealed class SchedulingConcurrencyRequestTests
{
    [Theory]
    [InlineData("{}")]
    [InlineData("{\"concurrencyToken\":null}")]
    [InlineData("{\"concurrencyToken\":-1}")]
    [InlineData("{\"concurrencyToken\":4294967296}")]
    [InlineData("{\"concurrencyToken\":1.5}")]
    [InlineData("{\"concurrencyToken\":\"invalid\"}")]
    public void UpdateRequests_WhenVersionIsMissingOrMalformed_RejectJson(string json)
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<ShiftEntryUpdateRequest>(json, options));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<ShiftSeriesUpdateRequest>(json, options));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<AssignmentEntryUpdateRequest>(json, options));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<AssignmentSeriesUpdateRequest>(json, options));
    }

    [Theory]
    [InlineData(0u, true)]
    [InlineData(1u, false)]
    [InlineData(uint.MaxValue, false)]
    public void UpdateValidators_WhenVersionIsZero_RejectVersion(uint version, bool hasError)
    {
        AssertVersion(new ShiftEntryUpdateRequestValidator(), new ShiftEntryUpdateRequest { ConcurrencyToken = version }, hasError);
        AssertVersion(new ShiftSeriesUpdateRequestValidator(), new ShiftSeriesUpdateRequest { ConcurrencyToken = version }, hasError);
        AssertVersion(new AssignmentEntryUpdateRequestValidator(), new AssignmentEntryUpdateRequest { ConcurrencyToken = version }, hasError);
        AssertVersion(new AssignmentSeriesUpdateRequestValidator(), new AssignmentSeriesUpdateRequest { ConcurrencyToken = version }, hasError);
    }

    private static void AssertVersion<T>(IValidator<T> validator, T request, bool hasError)
    {
        var result = validator.Validate(request);
        Assert.Equal(hasError, result.Errors.Any(error => error.PropertyName == "ConcurrencyToken"));
    }
}
