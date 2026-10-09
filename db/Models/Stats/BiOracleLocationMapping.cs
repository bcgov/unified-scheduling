using System.ComponentModel.DataAnnotations;
using Unified.Db.Models.Abstract;

namespace Unified.Db.Models.Stats;

public class BiOracleLocationMapping : BaseEntity
{
    [Key]
    public int Id { get; set; }

    [Required]
    public string JustinLocationCode { get; set; } = string.Empty;

    public int CrtLocId { get; set; }

    public DateOnly EffectiveDate { get; set; }

    public DateOnly? ExpiryDate { get; set; }
}
