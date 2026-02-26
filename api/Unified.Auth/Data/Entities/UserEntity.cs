using System.ComponentModel.DataAnnotations;
using Mapster;

namespace Unified.Auth.Data.Entities;

[AdaptTo("[name]Dto")]
public class UserEntity
{
    [AdaptIgnore]

    public static readonly Guid SystemUser = new("00000000-0000-0000-0000-000000000001");

    [Key]
    public Guid Id { get; set; }

    [AdaptIgnore]
    public string IdirName { get; set; } = string.Empty;
    
    [AdaptIgnore]
    public Guid? IdirId { get; set; }
    
    [AdaptIgnore]
    public Guid? KeyCloakId { get; set; }

    public bool IsEnabled { get; set; }

    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public int? HomeLocationId { get; set; }

    public DateTimeOffset? LastLogin { get; set; }
}
