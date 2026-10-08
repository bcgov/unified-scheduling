using System.ComponentModel.DataAnnotations;
using Unified.Db.Models.Abstract;
using Unified.Db.Models.UserManagement;

namespace Unified.Db.Models.TimeOff;

public class TimeOffEntryUser : BaseEntity
{
    [Key]
    public int Id { get; set; }

    public int TimeOffEntryId { get; set; }

    public TimeOffEntry? TimeOffEntry { get; set; }

    public Guid UserId { get; set; }

    public User? User { get; set; }
}
