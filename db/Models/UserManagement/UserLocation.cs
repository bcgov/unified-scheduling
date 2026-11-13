using System.ComponentModel.DataAnnotations;
using Unified.Db.Models.Abstract;

namespace Unified.Db.Models.UserManagement;

public class UserLocation : BaseEntity
{
    [Key]
    public int Id { get; set; }
    public Guid UserId { get; set; }
    public virtual User User { get; set; } = null!;

    public int LocationId { get; set; }
    public virtual Location Location { get; set; } = null!;

    public DateTimeOffset? EffectiveFrom { get; set; }

    public DateTimeOffset EffectiveUntil { get; set; }
}
