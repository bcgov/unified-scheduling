using System.Text.Json;
using FluentValidation;
using Unified.Audit.Models;

namespace Unified.Audit.Validators;

public class AuditHistoryQueryParamsValidator : AbstractValidator<AuditHistoryQueryParams>
{
    public static readonly string[] ValidActions = ["Added", "Modified", "Deleted"];
    public static readonly string[] ValidSortDirections = ["asc", "desc"];

    public AuditHistoryQueryParamsValidator()
    {
        RuleFor(x => x.Page)
            .GreaterThanOrEqualTo(1)
            .WithMessage("Page must be at least 1.")
            .When(x => x.Page.HasValue);

        RuleFor(x => x.PageSize)
            .GreaterThanOrEqualTo(1)
            .WithMessage("PageSize must be at least 1.")
            .LessThanOrEqualTo(100)
            .WithMessage("PageSize must not exceed 100.")
            .When(x => x.PageSize.HasValue);

        RuleFor(x => x.Action)
            .Must(action => ValidActions.Contains(action, StringComparer.OrdinalIgnoreCase))
            .WithMessage($"Action must be one of: {string.Join(", ", ValidActions)}.")
            .When(x => !string.IsNullOrEmpty(x.Action));

        RuleFor(x => x.SortDirection)
            .Must(direction => ValidSortDirections.Contains(direction, StringComparer.OrdinalIgnoreCase))
            .WithMessage("SortDirection must be 'asc' or 'desc'.")
            .When(x => !string.IsNullOrEmpty(x.SortDirection));

        RuleFor(x => x.EntityKey)
            .Must(BeValidJson)
            .WithMessage("EntityKey must be valid JSON.")
            .When(x => !string.IsNullOrEmpty(x.EntityKey));

        RuleFor(x => x)
            .Must(x => !x.From.HasValue || !x.To.HasValue || x.From <= x.To)
            .WithMessage("From must be on or before To.");
    }

    private static bool BeValidJson(string? json)
    {
        try
        {
            using var _ = JsonDocument.Parse(json!);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
