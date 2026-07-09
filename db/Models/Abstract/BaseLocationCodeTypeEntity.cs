using Unified.Db.Models;

namespace Unified.Db.Models.Abstract;

/// <summary>
/// Base entity for location-scoped code-table records with lifecycle dates.
/// </summary>
public abstract class BaseLocationCodeTypeEntity : BaseCodeTypeEntity
{
    public int LocationId { get; set; }

    public virtual Location Location { get; set; } = null!;
}
