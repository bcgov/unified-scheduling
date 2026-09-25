using Unified.Db.Models.Abstract;

namespace Unified.Db.Models.Training;

public class TrainingProfile : BaseEntity
{
    public int Id { get; set; }

    public int TrainingId { get; set; }

    public int TrainingProfileTypeId { get; set; }

    public virtual Training Training { get; set; } = null!;

    public virtual TrainingProfileType TrainingProfileType { get; set; } = null!;
}
