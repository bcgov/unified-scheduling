using System.ComponentModel.DataAnnotations;
using Unified.Db.Models.Abstract;
using Unified.Db.Models.UserManagement;

namespace Unified.Db.Models.Training;

public class TrainingProfileType : BaseCodeTypeEntity
{
    [Key]
    public int Id { get; set; }

    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public virtual ICollection<User> Users { get; set; } = [];

    public virtual ICollection<TrainingProfile> TrainingProfiles { get; set; } = [];
}
