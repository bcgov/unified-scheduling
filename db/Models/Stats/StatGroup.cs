using System.ComponentModel.DataAnnotations;
using Unified.Db.Models.Abstract;

namespace Unified.Db.Models.Stats;

public class StatGroup : BaseEntity
{
    [Key]
    public int Id { get; set; }

    [Required]
    public string Name { get; set; } = string.Empty;

    public int DisplayOrder { get; set; }

    /// <summary>
    /// When true, records in this group are per-location (no employee assignment required).
    /// </summary>
    public bool IsLocationLevel { get; set; }
}
