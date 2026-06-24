using System.ComponentModel.DataAnnotations;
using Unified.Db.Models.Abstract;
using Unified.Db.Models.Lookup;

namespace Unified.Db.Models.UserManagement;

public class UserActingPosition : BaseUserEvent
{
    [Required]
    public int PositionTypeId { get; set; }

    public virtual PositionType PositionType { get; set; } = null!;
}
