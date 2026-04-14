using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Unified.Db.Models.Stats;

namespace Unified.Db.Configuration.Stats;

public class StatMetricConfiguration : BaseEntityConfiguration<StatMetric>
{
    public override void Configure(EntityTypeBuilder<StatMetric> builder)
    {
        builder.Property(b => b.Id).HasIdentityOptions(startValue: 200);

        base.Configure(builder);
    }
}
