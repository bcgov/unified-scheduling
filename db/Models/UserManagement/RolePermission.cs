using System.ComponentModel.DataAnnotations;
using Mapster;
using Unified.Db.Models.Abstract;

namespace Unified.Db.Models.UserManagement;

[AdaptTo("[name]Dto")]
public class RolePermission : BaseEntity
{
    [Key]
    public int Id { get; set; }

    public virtual Role Role { get; set; } = null!;

    [Required]
    public int RoleId { get; set; }

    public virtual Permission Permission { get; set; } = null!;

    [Required]
    public string PermissionId { get; set; } = string.Empty;
}
