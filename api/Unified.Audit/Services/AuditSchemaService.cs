using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Unified.Audit.Interceptors;
using Unified.Audit.Models;
using Unified.Db;

namespace Unified.Audit.Services;

public interface IAuditSchemaService
{
    bool EntityTypeExists(string entityType);

    AuditEntityFieldsResponse? GetFields(string entityType);
}

/// <summary>
/// Derives the auditable-field schema for every entity in the EF Core model. Model metadata is
/// static for the lifetime of the process, so the result is computed once and cached in memory.
/// </summary>
public sealed partial class AuditSchemaService(
    UnifiedDbContext DB,
    IOptions<AuditRecordInterceptorOptions> options,
    IMemoryCache cache
) : IAuditSchemaService
{
    private const string CacheKey = "Unified.Audit.SchemaFieldsByEntityType";

    public bool EntityTypeExists(string entityType) => GetSchemaMap().ContainsKey(entityType);

    public AuditEntityFieldsResponse? GetFields(string entityType) =>
        GetSchemaMap().TryGetValue(entityType, out var response) ? response : null;

    private IReadOnlyDictionary<string, AuditEntityFieldsResponse> GetSchemaMap() =>
        cache.GetOrCreate(CacheKey, _ => BuildSchemaMap())!;

    private Dictionary<string, AuditEntityFieldsResponse> BuildSchemaMap()
    {
        var auditOptions = options.Value;
        var map = new Dictionary<string, AuditEntityFieldsResponse>(StringComparer.OrdinalIgnoreCase);

        foreach (var entityType in DB.Model.GetEntityTypes())
        {
            var name = entityType.ClrType.Name;
            var fields = entityType
                .GetProperties()
                .Where(property => !AuditPropertyExclusion.ShouldExclude(property, auditOptions))
                .Select(BuildField)
                .OrderBy(field => field.Name, StringComparer.Ordinal)
                .ToList();

            map[name] = new AuditEntityFieldsResponse { EntityType = name, Fields = fields };
        }

        return map;
    }

    private static AuditEntityFieldDto BuildField(IProperty property)
    {
        var name = property.Name;
        var label = property.PropertyInfo?.GetCustomAttribute<DisplayAttribute>()?.Name ?? ToDisplayLabel(name);
        var type = ResolveFieldType(property.ClrType);

        return new AuditEntityFieldDto
        {
            Name = name,
            Label = label,
            Type = type,
        };
    }

    /// <summary>Splits PascalCase into spaced words, e.g. "FirstName" -&gt; "First Name".</summary>
    private static string ToDisplayLabel(string propertyName) => PascalCaseBoundary().Replace(propertyName, " ");

    private static string ResolveFieldType(Type clrType)
    {
        var type = Nullable.GetUnderlyingType(clrType) ?? clrType;

        if (type == typeof(string))
        {
            return "string";
        }

        if (type == typeof(bool))
        {
            return "boolean";
        }

        if (type == typeof(DateTime) || type == typeof(DateTimeOffset) || type == typeof(DateOnly))
        {
            return "date";
        }

        if (type == typeof(Guid))
        {
            return "uuid";
        }

        if (IsNumericType(type))
        {
            return "number";
        }

        return "string";
    }

    private static bool IsNumericType(Type type) =>
        type == typeof(byte)
        || type == typeof(sbyte)
        || type == typeof(short)
        || type == typeof(ushort)
        || type == typeof(int)
        || type == typeof(uint)
        || type == typeof(long)
        || type == typeof(ulong)
        || type == typeof(float)
        || type == typeof(double)
        || type == typeof(decimal);

    [GeneratedRegex("(?<=[a-z0-9])(?=[A-Z])|(?<=[A-Z])(?=[A-Z][a-z])")]
    private static partial Regex PascalCaseBoundary();
}
