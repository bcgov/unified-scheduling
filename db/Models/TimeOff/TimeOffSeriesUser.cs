using System.ComponentModel.DataAnnotations;
using Unified.Db.Models.Abstract;
using Unified.Db.Models.UserManagement;

namespace Unified.Db.Models.TimeOff;

public class TimeOffSeriesUser : BaseEntity
{
    [Key]
    public int Id { get; set; }

    public int TimeOffSeriesId { get; set; }

    public TimeOffSeries? TimeOffSeries { get; set; }

    public Guid UserId { get; set; }

    public User? User { get; set; }
}
