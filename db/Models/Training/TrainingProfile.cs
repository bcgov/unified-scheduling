using System.ComponentModel.DataAnnotations;
using Unified.Db.Models.Abstract;
using Unified.Db.Models.UserManagement;

namespace Unified.Db.Models.Training;

public class TrainingProfile : BaseCodeTypeEntity
{
    [Key]
    public int Id { get; set; }

    public virtual ICollection<User> Users { get; set; } = [];

    public virtual ICollection<TrainingMandatoryTrainingProfile> MandatoryTrainings { get; set; } = [];
}
