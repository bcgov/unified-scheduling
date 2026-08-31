using Microsoft.EntityFrameworkCore.Metadata;
using Unified.Audit.Interceptors;

namespace Unified.Audit;

/// <summary>
/// Shared deny-list logic for which entity properties are audited. Used by both the interceptor
/// that writes <c>AuditRecord</c> rows and the audit schema endpoints (which fields are exposed
/// as filterable/displayable), so the two stay consistent.
/// </summary>
public static class AuditPropertyExclusion
{
    public static bool ShouldExclude(IProperty property, AuditRecordOptions options)
    {
        if (property.ClrType == typeof(byte[]))
        {
            return true;
        }

        var propertyName = property.Name;

        return options.ExcludedPropertyNames.Any(excluded =>
            string.Equals(excluded, propertyName, StringComparison.OrdinalIgnoreCase)
        );
    }
}
