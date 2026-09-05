using System.ComponentModel.DataAnnotations;
using Unified.Db.Models.Abstract;
using Unified.Db.Models.UserManagement;

namespace Unified.Db.Models.Scheduling;

public class ShiftAssignmentSeriesLinkUser : BaseEntity
{
    [Key]
    public int Id { get; set; }

    public int ShiftAssignmentSeriesLinkId { get; set; }

    public ShiftAssignmentSeriesLink? ShiftAssignmentSeriesLink { get; set; }

    public Guid UserId { get; set; }

    public User? User { get; set; }
}
