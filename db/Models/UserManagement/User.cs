using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Unified.Db.Models;
using Unified.Db.Models.Abstract;
using Unified.Db.Models.Training;

namespace Unified.Db.Models.UserManagement;

public class User : BaseEntity
{
    public static readonly Guid SystemUser = new("00000000-0000-0000-0000-000000000001");

    private string _idirName = string.Empty;

    [Key]
    public Guid Id { get; set; }
    public string IdirName
    {
        get => _idirName;
        set => _idirName = NormalizeIdirName(value) ?? string.Empty;
    }
    public Guid? IdirId { get; set; }
    public Guid? KeyCloakId { get; set; }
    public bool IsEnabled { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public Gender Gender { get; set; }
    public int? HomeLocationId { get; set; }
    public virtual Location? HomeLocation { get; set; }
    public string? BadgeNumber { get; set; }
    public string? Rank { get; set; }
    public DateTimeOffset? LastLogin { get; set; }
    public byte[]? Photo { get; set; }
    public bool PendingRegistration { get; set; }
    public DateTimeOffset? LastPhotoUpdate { get; set; }

    public virtual ICollection<UserRole> UserRoles { get; set; } = [];
    public virtual ICollection<UserTraining> UserTrainings { get; set; } = [];

    [NotMapped]
    public virtual IReadOnlyList<UserRole> ActiveUserRoles =>
        UserRoles
            .Where(ur =>
                ur.EffectiveDate <= DateTimeOffset.UtcNow
                && (ur.ExpiryDate == null || ur.ExpiryDate > DateTimeOffset.UtcNow)
            )
            .ToList();

    [NotMapped]
    public virtual IReadOnlyList<Permission> Permissions =>
        ActiveUserRoles
            .SelectMany(ur => ur.Role.RolePermissions)
            .Select(rp => rp.Permission)
            .DistinctBy(p => p.Id)
            .ToList();

    public static string? NormalizeIdirName(string? idirName)
    {
        return string.IsNullOrWhiteSpace(idirName) ? null : idirName.Trim().ToLowerInvariant();
    }
}
