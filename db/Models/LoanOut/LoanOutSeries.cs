using System.ComponentModel.DataAnnotations;
using Unified.Db.Models.Abstract;
using Unified.Db.Models.Calendar;

namespace Unified.Db.Models.LoanOut;

public class LoanOutSeries : BaseEntity
{
    [Key]
    public int Id { get; set; }

    public int EventSeriesId { get; set; }

    public EventSeries? EventSeries { get; set; }

    public int LoanOutLocationId { get; set; }

    public Location? LoanOutLocation { get; set; }

    public ICollection<LoanOutSeriesUser> Users { get; set; } = new List<LoanOutSeriesUser>();

    public ICollection<LoanOutEntry> LoanOutEntries { get; set; } = new List<LoanOutEntry>();
}
