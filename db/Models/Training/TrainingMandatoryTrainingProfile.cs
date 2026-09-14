namespace Unified.Db.Models.Training;

public class TrainingMandatoryTrainingProfile
{
    public int TrainingId { get; set; }

    public int TrainingProfileId { get; set; }

    public virtual Training Training { get; set; } = null!;

    public virtual TrainingProfile TrainingProfile { get; set; } = null!;
}
