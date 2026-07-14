using System.ComponentModel.DataAnnotations;
using Mapster;
using Unified.Db.Models.Abstract;

namespace Unified.Db.Models.Lookup;

[AdaptTo("[name]Dto")]
public class CourtRoom : BaseCodeTypeEntity
{
    [Key]
    public int Id { get; set; }

    [AdaptIgnore]
    public virtual Location Location { get; set; }
    public int? LocationId { get; set; }
}
