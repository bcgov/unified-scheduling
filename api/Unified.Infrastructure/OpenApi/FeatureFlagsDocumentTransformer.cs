using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
using Unified.Common.FeatureFlags;

namespace Unified.Infrastructure.OpenApi;

public sealed class FeatureFlagsDocumentTransformer(IEnumerable<IFeatureFlags> featureFlags)
    : IOpenApiDocumentTransformer
{
    public Task TransformAsync(
        OpenApiDocument document,
        OpenApiDocumentTransformerContext context,
        CancellationToken cancellationToken
    )
    {
        if (document.Components?.Schemas is null)
            return Task.CompletedTask;

        if (!document.Components.Schemas.TryGetValue("ConfigResponse", out var configResponseSchema))
            return Task.CompletedTask;

        if (configResponseSchema.Properties is null)
            return Task.CompletedTask;

        configResponseSchema.Properties["featureFlags"] = new OpenApiSchema
        {
            Type = JsonSchemaType.Object,
            Properties = featureFlags
                .DistinctBy(flag => flag.Source, StringComparer.Ordinal)
                .ToDictionary(
                    flag => flag.Source,
                    flag => (IOpenApiSchema)OpenApiSchemaHelpers.BuildSchema(flag.GetType(), new HashSet<Type>())
                ),
        };

        return Task.CompletedTask;
    }
}
