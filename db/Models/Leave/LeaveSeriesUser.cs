using System.ComponentModel.DataAnnotations;
using Unified.Db.Models.Abstract;
using Unified.Db.Models.UserManagement;

namespace Unified.Db.Models.Leave;

public class LeaveSeriesUser : BaseEntity
{
    [Key]
    public int Id { get; set; }

    public int LeaveSeriesId { get; set; }

    public LeaveSeries? LeaveSeries { get; set; }

    public Guid UserId { get; set; }

    public User? User { get; set; }
}
