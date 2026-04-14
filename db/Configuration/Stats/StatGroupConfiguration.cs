using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Unified.Db.Models.Stats;

namespace Unified.Db.Configuration.Stats;

public class StatGroupConfiguration : BaseEntityConfiguration<StatGroup>
{
    public override void Configure(EntityTypeBuilder<StatGroup> builder)
    {
        builder.Property(b => b.Id).HasIdentityOptions(startValue: 200);

        base.Configure(builder);
    }
}
