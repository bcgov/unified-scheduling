using System.ComponentModel.DataAnnotations;
using Unified.Db.Models.Abstract;

namespace Unified.Db.Models.Stats;

public class SubCategory : BaseEntity
{
    [Key]
    public int Id { get; set; }

    public int CategoryId { get; set; }

    public StatCategory? Category { get; set; }

    [Required]
    public string Name { get; set; } = string.Empty;

    public int DisplayOrder { get; set; }
}
