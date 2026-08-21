using Mapster;
using Unified.Db.Models.Scheduling;
using Unified.Scheduling.Models;

namespace Unified.Scheduling.Mappings;

internal static class AssignmentDefinitionMapper
{
    private static readonly TypeAdapterConfig ResponseConfig = BuildResponseConfig();

    public static AssignmentDefinitionResponse ToResponse(AssignmentDefinition definition) =>
        definition.Adapt<AssignmentDefinitionResponse>(ResponseConfig);

    private static TypeAdapterConfig BuildResponseConfig()
    {
        var config = new TypeAdapterConfig();
        config
            .NewConfig<AssignmentDefinition, AssignmentDefinitionResponse>()
            .Map(response => response.CategoryName, definition => definition.Category.Name)
            .Map(response => response.SubCategoryName, definition => definition.SubCategory.Name)
            .Map(
                response => response.DefaultStartTime,
                definition =>
                    definition.DefaultStartTime.HasValue
                        ? definition.DefaultStartTime.Value.ToString("HH:mm:ss")
                        : null
            )
            .Map(
                response => response.DefaultEndTime,
                definition =>
                    definition.DefaultEndTime.HasValue ? definition.DefaultEndTime.Value.ToString("HH:mm:ss") : null
            );

        return config;
    }
}
