using System.Reflection;
using Microsoft.EntityFrameworkCore.Metadata;
using Unified.Audit.Interceptors;
using Unified.Db.Audit;

namespace Unified.Audit;

/// <summary>
/// Shared deny-list logic for which entity properties are audited. Used by both
/// <see cref="AuditRecordInterceptor"/> (what gets written to <c>AuditRecord</c> rows) and
/// the audit schema endpoints (which fields are exposed as filterable/displayable), so the
/// two stay consistent.
/// </summary>
public static class AuditPropertyExclusion
{
    public static bool ShouldExclude(IProperty property, AuditRecordInterceptorOptions options)
    {
        if (property.ClrType == typeof(byte[]))
        {
            return true;
        }

        if (property.PropertyInfo?.GetCustomAttribute<AuditExcludeAttribute>() is not null)
        {
            return true;
        }

        var propertyName = property.Name;

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
