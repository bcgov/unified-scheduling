using System.ComponentModel.DataAnnotations;
using Unified.Db.Models.Abstract;
using Unified.Db.Models.UserManagement;

namespace Unified.Db.Models.Scheduling;

public class ShiftEntryUser : BaseEntity
{
    [Key]
    public int Id { get; set; }

    public int ShiftEntryId { get; set; }

    public ShiftEntry? ShiftEntry { get; set; }

    public Guid UserId { get; set; }

    public User? User { get; set; }
}
