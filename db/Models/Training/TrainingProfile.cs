using System.ComponentModel.DataAnnotations;
using Unified.Db.Models.Abstract;
using Unified.Db.Models.UserManagement;

namespace Unified.Db.Models.Training;

public class TrainingProfile : BaseEntity
{
    [Key]
    public int Id { get; set; }

    public string Code { get; set; } = string.Empty;

    public virtual ICollection<User> Users { get; set; } = [];

    public virtual ICollection<TrainingMandatoryTrainingProfile> MandatoryTrainings { get; set; } = [];
}
