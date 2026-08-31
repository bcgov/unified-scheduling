using System.Reflection;
using Microsoft.EntityFrameworkCore.Metadata;
using Unified.Audit.Interceptors;

namespace Unified.Audit;

/// <summary>
/// Shared deny-list logic for which entity properties are audited. Used by the Audit.NET-backed
/// audit pipeline (<see cref="Interceptors.AuditRecordDataProvider"/>, what gets written to
/// <c>AuditRecord</c> rows) and the audit schema endpoints (which fields are exposed as
/// filterable/displayable), so the two stay consistent.
/// </summary>
public static class AuditPropertyExclusion
{
    public static bool ShouldExclude(IProperty property, AuditRecordInterceptorOptions options) =>
        ShouldExclude(property.ClrType, property.PropertyInfo, property.Name, options);

    private static bool ShouldExclude(
        Type clrType,
        PropertyInfo? propertyInfo,
        string propertyName,
        AuditRecordInterceptorOptions options
    )
    {
        if (clrType == typeof(byte[]))
        {
            return true;
        }

        if (propertyInfo?.GetCustomAttribute<global::Audit.EntityFramework.AuditIgnoreAttribute>() is not null)
        {
            return true;
        }

        if (
            options.ExcludedPropertyNames.Any(excluded =>
                string.Equals(excluded, propertyName, StringComparison.OrdinalIgnoreCase)
            )
        )
        {
            return true;
        }

        if (
            options.ExcludedPropertyNameContains.Any(pattern =>
                propertyName.Contains(pattern, StringComparison.OrdinalIgnoreCase)
            )
        )
        {
            return true;
        }

        return options.ExcludedPropertyNameEndsWith.Any(suffix =>
            propertyName.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)
        );
    }
}
