using System.ComponentModel.DataAnnotations;
using Unified.Db.Models.Abstract;
using Unified.Db.Models.UserManagement;

namespace Unified.Db.Models.Scheduling;

public class ShiftAssignmentEntryUser : BaseEntity
{
    [Key]
    public int Id { get; set; }

    public int ShiftAssignmentEntryId { get; set; }

    public ShiftAssignmentEntry? ShiftAssignmentEntry { get; set; }

    public Guid UserId { get; set; }

    public User? User { get; set; }
}
