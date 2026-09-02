using Microsoft.EntityFrameworkCore.Metadata;
using Unified.Audit.Options;

namespace Unified.Audit;

/// <summary>
/// Shared deny-list logic for which entity properties are audited by the audit schema endpoints
/// (which fields are exposed as filterable/displayable).
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
