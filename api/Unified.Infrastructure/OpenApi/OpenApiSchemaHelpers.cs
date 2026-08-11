using System.Reflection;
using Microsoft.OpenApi;

namespace Unified.Infrastructure.OpenApi;

public static class OpenApiSchemaHelpers
{
    private static readonly NullabilityInfoContext NullabilityInfoContext = new();

    public static OpenApiSchema BuildSchema(Type type, HashSet<Type> visited)
    {
        var resolvedType = Nullable.GetUnderlyingType(type) ?? type;

        if (resolvedType == typeof(string))
            return new OpenApiSchema { Type = JsonSchemaType.String };

        if (resolvedType == typeof(bool))
            return new OpenApiSchema { Type = JsonSchemaType.Boolean };

        if (resolvedType.IsPrimitive || resolvedType == typeof(decimal))
            return new OpenApiSchema { Type = JsonSchemaType.Number };

        if (!visited.Add(resolvedType))
            return new OpenApiSchema { Type = JsonSchemaType.Object };

        var properties = resolvedType
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.GetMethod is not null && !p.GetMethod.IsStatic)
            .ToDictionary(p => ToCamelCase(p.Name), p => (IOpenApiSchema)BuildSchema(p.PropertyType, visited));

        var required = resolvedType
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.GetMethod is not null && !p.GetMethod.IsStatic)
            .Where(p => !IsNullable(p))
            .Select(p => ToCamelCase(p.Name))
            .ToHashSet(StringComparer.Ordinal);

        visited.Remove(resolvedType);

        return new OpenApiSchema
        {
            Type = JsonSchemaType.Object,
            Properties = properties,
            Required = required,
        };
    }

    public static bool IsNullable(PropertyInfo property)
    {
        if (Nullable.GetUnderlyingType(property.PropertyType) is not null)
            return true;

        if (property.PropertyType.IsValueType)
            return false;

        return NullabilityInfoContext.Create(property).ReadState == NullabilityState.Nullable;
    }

    public static string ToCamelCase(string name) =>
        string.IsNullOrEmpty(name) || char.IsLower(name[0]) ? name : char.ToLowerInvariant(name[0]) + name[1..];
}
