using System.ComponentModel.DataAnnotations;
using Mapster;
using Unified.Db.Models.Abstract;

namespace Unified.Db.Models.UserManagement;

[AdaptTo("[name]Dto")]
public class Role : BaseEntity, ISoftDeletable
{
    [Key]
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public Guid? DeletedById { get; set; } = null;

    public DateTimeOffset? DeletedOn { get; set; } = null;

    public virtual ICollection<UserRole> UserRoles { get; set; } = [];

    public virtual ICollection<RolePermission> RolePermissions { get; set; } = [];
}
