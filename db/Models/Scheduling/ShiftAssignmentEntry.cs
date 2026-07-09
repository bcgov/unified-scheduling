using System.ComponentModel.DataAnnotations;
using Unified.Db.Models.Abstract;

namespace Unified.Db.Models.Scheduling;

public class ShiftAssignmentEntry : BaseEntity
{
    [Key]
    public int Id { get; set; }

    public int ShiftEntryId { get; set; }

    public ShiftEntry? ShiftEntry { get; set; }

    public int AssignmentEntryId { get; set; }

    public AssignmentEntry? AssignmentEntry { get; set; }

    public ICollection<ShiftAssignmentEntryUser> Users { get; set; } = [];
}
