using System.ComponentModel.DataAnnotations;
using Unified.Db.Models.Abstract;

namespace Unified.Db.Models.Stats;

public class StatCategory : BaseEntity
{
    [Key]
    public int Id { get; set; }

    public int GroupId { get; set; }

    public StatGroup? Group { get; set; }

    [Required]
    public string Name { get; set; } = string.Empty;

    public bool IsArchived { get; set; }

    public bool IsHighSecurity { get; set; }

    public int DisplayOrder { get; set; }
}
