using System.ComponentModel.DataAnnotations;
using Unified.Db.Models.Abstract;
using Unified.Db.Models.UserManagement;

namespace Unified.Db.Models.Scheduling;

public class ShiftSeriesUser : BaseEntity
{
    [Key]
    public int Id { get; set; }

    public int ShiftSeriesId { get; set; }

    public ShiftSeries? ShiftSeries { get; set; }

    public Guid UserId { get; set; }

    public User? User { get; set; }
}
