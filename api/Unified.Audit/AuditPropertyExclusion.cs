using System.Reflection;
using Microsoft.EntityFrameworkCore.Metadata;
using Unified.Audit.Options;

namespace Unified.Audit;

/// <summary>
/// Shared deny-list logic for which entity properties are audited. Used by
/// <see cref="AuditRecordEntityAction"/> (what gets written to <c>AuditRecord</c> rows) and the
/// audit schema endpoints (which fields are exposed as filterable/displayable), so the two stay
/// consistent.
/// </summary>
public static class AuditPropertyExclusion
{
    public static bool ShouldExclude(IProperty property, AuditRecordOptions options) =>
        ShouldExclude(property.ClrType, property.PropertyInfo, property.Name, options);

    private static bool ShouldExclude(
        Type clrType,
        PropertyInfo? propertyInfo,
        string propertyName,
        AuditRecordOptions options
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

        return options.ExcludedPropertyNames.Any(excluded =>
            string.Equals(excluded, propertyName, StringComparison.OrdinalIgnoreCase)
        );
    }
}
