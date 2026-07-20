using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Unified.Common.Filters;

/// <summary>
/// Applies <see cref="RequestFormLimitsAttribute"/> with a <c>MultipartBodyLengthLimit</c>
/// derived from a named property on <typeparamref name="TOptions"/> at request time.
/// This prevents oversized multipart uploads from being buffered in memory before the action runs.
/// </summary>
/// <typeparam name="TOptions">The options class registered with <see cref="IOptions{TOptions}"/>.</typeparam>
/// <param name="propertyName">
/// The name of the property on <typeparamref name="TOptions"/> that holds the size limit.
/// Use <c>nameof(...)</c> to avoid magic strings.
/// </param>
/// <param name="multiplier">
/// Factor applied to the property value to convert it to bytes (e.g. 1024 for KB → bytes).
/// Defaults to 1 when the property is already in bytes.
/// </param>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public sealed class RequestFormLimitsFromOptionsAttribute<TOptions>(string propertyName, long multiplier = 1)
    : Attribute,
        IFilterFactory
    where TOptions : class
{
    public bool IsReusable => false;

    public IFilterMetadata CreateInstance(IServiceProvider serviceProvider)
    {
        var options = serviceProvider.GetRequiredService<IOptions<TOptions>>().Value;
        var property =
            typeof(TOptions).GetProperty(propertyName)
            ?? throw new InvalidOperationException($"Property '{propertyName}' not found on {typeof(TOptions).Name}.");

        var rawValue = property.GetValue(options);
        var limitBytes = Convert.ToInt64(rawValue) * multiplier;

        // RequestFormLimitsAttribute is itself only an IFilterFactory (not a filter),
        // so its own CreateInstance must be invoked to obtain the real IResourceFilter
        // that reads the request body limits and applies them before model binding.
        var formLimitsAttribute = new RequestFormLimitsAttribute { MultipartBodyLengthLimit = limitBytes };
        return ((IFilterFactory)formLimitsAttribute).CreateInstance(serviceProvider);
    }
}
