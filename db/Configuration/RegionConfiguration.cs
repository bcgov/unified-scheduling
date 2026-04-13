using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Unified.Db.Models;

namespace Unified.Db.Configuration;

public class RegionConfiguration : BaseEntityConfiguration<Region>
{
    public override void Configure(EntityTypeBuilder<Region> builder)
    {
        builder.HasIndex(b => b.JustinId).IsUnique();

        base.Configure(builder);
    }
}
