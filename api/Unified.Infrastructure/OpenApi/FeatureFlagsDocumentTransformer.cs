using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
using Unified.Common.FeatureFlags;

namespace Unified.Infrastructure.OpenApi;

public sealed class FeatureFlagsDocumentTransformer(IEnumerable<IFeatureFlags> featureFlags)
    : IOpenApiDocumentTransformer
{
    private const string FeatureFlagsResponseSchemaName = "FeatureFlagsResponse";

    public async Task TransformAsync(
        OpenApiDocument document,
        OpenApiDocumentTransformerContext context,
        CancellationToken cancellationToken
    )
    {
        if (document.Components?.Schemas is null)
            return;

        if (!document.Components.Schemas.TryGetValue("ConfigResponse", out var configResponseSchema))
            return;

        if (configResponseSchema.Properties is null)
            return;

        var properties = new Dictionary<string, IOpenApiSchema>(StringComparer.Ordinal);

        foreach (var flag in featureFlags.DistinctBy(flag => flag.Source, StringComparer.Ordinal))
        {
            var flagType = flag.GetType();
            var schemaName = flagType.Name;
            var schema = await context.GetOrCreateSchemaAsync(flagType, parameterDescription: null, cancellationToken);

            document.Components.Schemas[schemaName] = schema;
            properties[flag.Source] = new OpenApiSchemaReference(schemaName, document, externalResource: null);
        }

        document.Components.Schemas[FeatureFlagsResponseSchemaName] = new OpenApiSchema
        {
            Type = JsonSchemaType.Object,
            Properties = properties,
        };

        configResponseSchema.Properties["featureFlags"] = new OpenApiSchemaReference(
            FeatureFlagsResponseSchemaName,
            document,
            externalResource: null
        );

        return;
    }
}
