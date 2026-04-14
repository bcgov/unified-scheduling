using System.ComponentModel.DataAnnotations;
using Unified.Db.Models.Abstract;

namespace Unified.Db.Models.Lookup;

public class PositionType : BaseCodeTypeEntity
{
    [Key]
    public int Id { get; set; }
}
