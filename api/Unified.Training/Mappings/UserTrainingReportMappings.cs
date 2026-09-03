using Mapster;
using Unified.Training.Services.Reporting;

namespace Unified.Training.Mappings;

internal static class UserTrainingReportMappings
{
    private static readonly TypeAdapterConfig Config = BuildConfig();

    public static UserTrainingReportItem ToReportRowValue(UserTrainingReportRow row, TrainingComplianceStatus status)
    {
        return row.Adapt<UserTrainingReportItem>(Config) with { Status = status.ToDisplayValue() };
    }

    private static TypeAdapterConfig BuildConfig()
    {
        var config = new TypeAdapterConfig();

        config
            .NewConfig<UserTrainingReportRow, UserTrainingReportItem>()
            .Map(dest => dest.UserDisplayName, src => BuildUserDisplayName(src.FirstName, src.LastName))
            .Map(dest => dest.TrainingId, src => src.TrainingId)
            .Map(dest => dest.TrainingCode, src => src.TrainingCode)
            .Map(dest => dest.TrainingDescription, src => src.TrainingDescription)
            .Map(dest => dest.AwardedOn, src => src.AwardedOn)
            .Map(dest => dest.EndingOn, src => src.EndingOn)
            .Map(dest => dest.ExpiryDate, src => src.ExpiryDate)
            .Map(dest => dest.Version, src => src.Version)
            .Map(dest => dest.NoticeState, src => src.NoticeState)
            .Map(dest => dest.Notes, src => src.Notes)
            .Map(dest => dest.HasMissingMandatoryTrainingAssignment, src => src.IsMissingMandatoryTrainingAssignment)
            .Map(dest => dest.Status, src => string.Empty);

        return config;
    }

    private static string BuildUserDisplayName(string firstName, string lastName)
    {
        var normalizedFirstName = firstName.Trim();
        var normalizedLastName = lastName.Trim();

        if (string.IsNullOrWhiteSpace(normalizedLastName))
        {
            return normalizedFirstName;
        }

        if (string.IsNullOrWhiteSpace(normalizedFirstName))
        {
            return normalizedLastName;
        }

        return $"{normalizedLastName}, {normalizedFirstName}";
    }
}
