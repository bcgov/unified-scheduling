namespace Unified.Training.Services;

internal static class MandatoryTrainingApplicability
{
    public static IQueryable<Unified.Db.Models.Training.Training> WhereApplicableToTrainingProfile(
        this IQueryable<Unified.Db.Models.Training.Training> trainings,
        int? trainingProfileId
    ) =>
        trainings.Where(training =>
            !training.TrainingProfileRequirements.Any()
            || (
                trainingProfileId != null
                && training.TrainingProfileRequirements.Any(requiredProfile =>
                    requiredProfile.TrainingProfileTypeId == trainingProfileId
                )
            )
        );
}
