using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Unified.Db.Models.Stats;

namespace Unified.Db.Configuration.Stats;

public class SubCategoryMetricConfiguration : BaseEntityConfiguration<SubCategoryMetric>
{
    public override void Configure(EntityTypeBuilder<SubCategoryMetric> builder)
    {
        builder
            .HasOne(scm => scm.SubCategory)
            .WithMany()
            .HasForeignKey(scm => scm.SubCategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder
            .HasOne(scm => scm.Metric)
            .WithMany()
            .HasForeignKey(scm => scm.MetricId)
            .OnDelete(DeleteBehavior.Restrict);

        base.Configure(builder);
    }
}
