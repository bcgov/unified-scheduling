using Unified.Db.Models.Abstract;

namespace Unified.Db.Models.Training;

public class TrainingMandatoryTrainingProfile : BaseEntity
{
    public int Id { get; set; }

    public int TrainingId { get; set; }

    public int TrainingProfileId { get; set; }

    public virtual Training Training { get; set; } = null!;

    public virtual TrainingProfile TrainingProfile { get; set; } = null!;
}
