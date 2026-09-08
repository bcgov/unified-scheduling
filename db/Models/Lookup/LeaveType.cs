using System.ComponentModel.DataAnnotations;
using Unified.Db.Models.Abstract;

namespace Unified.Db.Models.Lookup;

public class LeaveType : BaseCodeTypeEntity
{
    [Key]
    public int Id { get; set; }

    // Drives overtime calculations, which need to know whether time on this leave type is paid.
    public bool IsPaid { get; set; }
}
