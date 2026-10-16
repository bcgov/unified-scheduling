using System.ComponentModel.DataAnnotations;
using Unified.Db.Models.Abstract;
using Unified.Db.Models.UserManagement;

namespace Unified.Db.Models.Leave;

public class LeaveEntryUser : BaseEntity
{
    [Key]
    public int Id { get; set; }

    public int LeaveEntryId { get; set; }

    public LeaveEntry? LeaveEntry { get; set; }

    public Guid UserId { get; set; }

    public User? User { get; set; }
}
