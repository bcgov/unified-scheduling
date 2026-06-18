using System.ComponentModel.DataAnnotations;
using Unified.Db.Models.Abstract;
using Unified.Db.Models.Lookup;

namespace Unified.Db.Models.UserManagement;

public class UserActingPosition : BaseEntity
{
    [Key]
    public int Id { get; set; }

    [Required]
    public Guid UserId { get; set; }

    public virtual User User { get; set; } = null!;

    [Required]
    public int PositionTypeId { get; set; }

    public virtual PositionType PositionType { get; set; } = null!;

    [Required]
    public DateTimeOffset EffectiveDate { get; set; }

    public DateTimeOffset? ExpiryDate { get; set; }

    public string? ExpiryReason { get; set; }

    public string? Comment { get; set; }
}
