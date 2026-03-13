using System.ComponentModel.DataAnnotations;
using System.Reflection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Unified.Infrastructure.Options;

/// <summary>
/// Validates required boolean properties are explicitly configured.
/// </summary>
public class RequiredBooleanOptionsValidator<TOptions> : IValidateOptions<TOptions>
    where TOptions : class
{
    private readonly ILogger<RequiredBooleanOptionsValidator<TOptions>> _logger;

    public RequiredBooleanOptionsValidator(ILogger<RequiredBooleanOptionsValidator<TOptions>> logger)
    {
        _logger = logger;
    }

    public ValidateOptionsResult Validate(string? name, TOptions options)
    {
        if (options is null)
        {
            return ValidateOptionsResult.Fail($"{typeof(TOptions).Name} options are required.");
        }

        List<string> failures = [];

        PropertyInfo[] properties = typeof(TOptions).GetProperties(BindingFlags.Public | BindingFlags.Instance);

        foreach (PropertyInfo property in properties)
        {
            Type propertyType = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
            object? value = property.GetValue(options);

            if (property.GetCustomAttribute<RequiredAttribute>() is not RequiredAttribute)
            {
                continue;
            }

            if (propertyType != typeof(bool))
            {
                continue;
            }

            if (value is not bool)
            {
                failures.Add($"{property.Name} is required and must be explicitly set to true or false.");
            }
        }

        return failures.Count > 0 ? ValidateOptionsResult.Fail(failures) : ValidateOptionsResult.Success;
    }
}
