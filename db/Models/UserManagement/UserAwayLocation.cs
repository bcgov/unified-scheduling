using System.ComponentModel.DataAnnotations;
using Unified.Db.Models.Abstract;

namespace Unified.Db.Models.UserManagement;

public class UserAwayLocation : BaseUserEvent
{
    [Required]
    public int LocationId { get; set; }

    public virtual Location Location { get; set; } = null!;
}
