using System.ComponentModel.DataAnnotations;
using Unified.Db.Models.Abstract;
using Unified.Db.Models.Calendar;

namespace Unified.Db.Models.LoanOut;

public class LoanOutEntry : BaseEntity
{
    [Key]
    public int Id { get; set; }

    public int EventId { get; set; }

    public int LoanOutLocationId { get; set; }

    public Location? LoanOutLocation { get; set; }

    public Event? Event { get; set; }

    public ICollection<LoanOutEntryUser> Users { get; set; } = new List<LoanOutEntryUser>();
}
