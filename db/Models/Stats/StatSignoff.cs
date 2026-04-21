using System.ComponentModel.DataAnnotations;
using Unified.Db.Models.Abstract;
using Unified.Db.Models.UserManagement;

namespace Unified.Db.Models.Stats;

public class StatSignoff : BaseEntity
{
    [Key]
    public int Id { get; set; }

    public Guid UserId { get; set; }

    public User? User { get; set; }

    public int LocationId { get; set; }

    public Location? Location { get; set; }

    public int Month { get; set; }

    public int Year { get; set; }

    public DateTimeOffset SignoffDate { get; set; }
}
