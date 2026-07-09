using System.ComponentModel.DataAnnotations;
using Unified.Db.Models.Abstract;

namespace Unified.Db.Models.Lookup;

public class AssignmentType : BaseCodeTypeEntity
{
    [Key]
    public int Id { get; set; }
}
