using Mapster;
using Unified.Db.Models.Training;
using Unified.Training.Models;

namespace Unified.Training.Mappings;

public static class UserTrainingMappings
{
    public static readonly TypeAdapterConfig ResponseConfig = BuildResponseConfig();
    public static readonly TypeAdapterConfig RequestToEntityConfig = BuildRequestToEntityConfig();

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

    private static TypeAdapterConfig BuildRequestToEntityConfig()
    {
        var config = new TypeAdapterConfig();

        config
            .NewConfig<UserTrainingRequest, UserTraining>()
            .Map(dest => dest.UserId, src => src.UserId)
            .Map(dest => dest.TrainingId, src => src.TrainingId)
            .Map(dest => dest.AwardedOn, src => src.AwardedOn)
            .Map(dest => dest.EndingOn, src => src.EndingOn)
            .Map(dest => dest.Notes, src => src.Notes == null ? null : src.Notes.Trim());

        return config;
    }
}
