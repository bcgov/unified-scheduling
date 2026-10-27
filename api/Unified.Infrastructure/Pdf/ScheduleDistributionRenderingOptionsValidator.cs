using Microsoft.Extensions.Options;

namespace Unified.Infrastructure.Pdf;

public sealed class ScheduleDistributionRenderingOptionsValidator : IValidateOptions<ScheduleDistributionRenderingOptions>
{
    public ValidateOptionsResult Validate(string? name, ScheduleDistributionRenderingOptions options)
    {
        var failures = new List<string>();

        if (
            !Uri.TryCreate(options.FrontendBaseUrl, UriKind.Absolute, out var frontendBaseUri)
            || (frontendBaseUri.Scheme != Uri.UriSchemeHttps && frontendBaseUri.Scheme != Uri.UriSchemeHttp)
            || !string.IsNullOrEmpty(frontendBaseUri.UserInfo)
            || !string.IsNullOrEmpty(frontendBaseUri.Query)
            || !string.IsNullOrEmpty(frontendBaseUri.Fragment)
        )
        {
            failures.Add(
                $"{ScheduleDistributionRenderingOptions.SectionName}:{nameof(options.FrontendBaseUrl)} must be an absolute HTTP(S) URL without credentials, a query, or a fragment."
            );
        }

        ValidateRange(
            failures,
            nameof(options.DefaultViewportWidth),
            options.DefaultViewportWidth,
            ScheduleDistributionRenderingOptions.MinimumViewportWidth,
            ScheduleDistributionRenderingOptions.MaximumViewportWidth
        );
        ValidateRange(
            failures,
            nameof(options.DefaultViewportHeight),
            options.DefaultViewportHeight,
            ScheduleDistributionRenderingOptions.MinimumViewportHeight,
            ScheduleDistributionRenderingOptions.MaximumViewportHeight
        );
        ValidateRange(failures, nameof(options.RenderTimeoutSeconds), options.RenderTimeoutSeconds, 5, 120);
        ValidateRange(failures, nameof(options.MaximumConcurrentRenders), options.MaximumConcurrentRenders, 1, 10);

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }

    private static void ValidateRange(List<string> failures, string propertyName, int value, int minimum, int maximum)
    {
        if (value < minimum || value > maximum)
        {
            failures.Add(
                $"{ScheduleDistributionRenderingOptions.SectionName}:{propertyName} must be between {minimum} and {maximum}."
            );
        }
    }
}
