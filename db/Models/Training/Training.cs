using System.ComponentModel.DataAnnotations;
using Unified.Db.Models.Abstract;

namespace Unified.Db.Models.Training;

public class Training : BaseCodeTypeEntity
{
    [Key]
    public int Id { get; set; }

    public bool Mandatory { get; set; }

    // Signifies how long the training is valid for in days. If null, always valid.
    public int? ValidityDays { get; set; }

    public int? AdvanceNoticeDays { get; set; }

    public bool Rotating { get; set; }

    public int? TrainingCategoryId { get; set; }

    public int Order { get; set; }

    public virtual TrainingCategory? TrainingCategory { get; set; }

    public virtual ICollection<UserTraining> UserTrainings { get; set; } = [];
}
