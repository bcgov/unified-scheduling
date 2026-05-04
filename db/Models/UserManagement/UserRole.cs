using System.ComponentModel.DataAnnotations;
using Mapster;
using Unified.Db.Models.Abstract;

namespace Unified.Db.Models.UserManagement;

[AdaptTo("[name]Dto")]
public class UserRole : BaseEntity
{
    [Key]
    public int Id { get; set; }

    [Required]
    public Guid UserId { get; set; }

    public virtual User User { get; set; } = null!;

    [Required]
    public int RoleId { get; set; }

    public virtual Role Role { get; set; } = null!;

    [Required]
    public DateTimeOffset EffectiveDate { get; set; }

    public DateTimeOffset? ExpiryDate { get; set; }

    public string? ExpiryReason { get; set; }
}
