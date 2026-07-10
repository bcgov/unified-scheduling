using System.ComponentModel.DataAnnotations;
using Unified.Db.Models.Abstract;

namespace Unified.Db.Models.Lookup;

public class AssignmentCategoryType : ParentCodeTypeEntity<AssignmentSubCategoryType>
{
    [Key]
    public int Id { get; set; }
}
