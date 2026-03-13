using System.ComponentModel.DataAnnotations;
using Unified.Db.Models.Abstract;

namespace Unified.Db.Models.UserManagement;

public class User : BaseEntity
{
    public static readonly Guid SystemUser = new("00000000-0000-0000-0000-000000000001");

    [Key]
    public Guid Id { get; set; }
    public string IdirName { get; set; } = string.Empty;
    public Guid? IdirId { get; set; }
    public Guid? KeyCloakId { get; set; }
    public bool IsEnabled { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public int? HomeLocationId { get; set; }
    public Gender Gender { get; set; }
    public string? BadgeNumber { get; set; }
    public string? Rank { get; set; }
    public DateTimeOffset? LastLogin { get; set; }
}
