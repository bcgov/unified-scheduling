using Mapster;
using Unified.Db.Models.Training;
using Unified.Training.Models;

namespace Unified.Training.Mappings;

public static class UserTrainingMappings
{
    public static readonly TypeAdapterConfig ResponseConfig = BuildResponseConfig();

    private static TypeAdapterConfig BuildResponseConfig()
    {
        var config = new TypeAdapterConfig();

        config
            .NewConfig<UserTraining, UserTrainingResponse>()
            .Map(dest => dest.TrainingCode, src => src.Training.Code)
            .Map(
                dest => dest.TrainingCategoryName,
                src => src.Training.TrainingCategory != null ? src.Training.TrainingCategory.Name : string.Empty
            );

        return config;
    }
}
