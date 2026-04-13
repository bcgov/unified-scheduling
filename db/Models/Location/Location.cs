using System;
using System.ComponentModel.DataAnnotations;
using Mapster;
using Unified.Db.Models.Abstract;

namespace Unified.Db.Models;

[AdaptTo("[name]Dto")]
public class Location : BaseEntity
{
    [Key]
    public int Id { get; set; }

    [Required]
    public string AgencyId { get; set; }
    public string Name { get; set; }
    public string? JustinCode { get; set; }
    public int? ParentLocationId { get; set; }

    [AdaptIgnore]
    public virtual Region Region { get; set; }
    public DateTimeOffset? ExpiryDate { get; set; }
    public int? RegionId { get; set; }
    public string Timezone { get; set; }
}
