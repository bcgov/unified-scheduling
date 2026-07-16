using System;
using System.ComponentModel.DataAnnotations;
using Mapster;
using Unified.Db.Models.Abstract;

namespace Unified.Db.Models;

[AdaptTo("[name]Dto")]
public class CourtRoom : BaseEntity
{
    [Key]
    public int Id { get; set; }

    public string Room { get; set; } = string.Empty;

    public DateTimeOffset? EffectiveDate { get; set; }

    public DateTimeOffset? ExpiryDate { get; set; }

    [AdaptIgnore]
    public virtual Location? Location { get; set; }
    public int? LocationId { get; set; }
}
