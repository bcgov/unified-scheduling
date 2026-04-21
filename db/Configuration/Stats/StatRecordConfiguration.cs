using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Unified.Db.Models.Stats;

namespace Unified.Db.Configuration.Stats;

public class StatRecordConfiguration : BaseEntityConfiguration<StatRecord>
{
    public override void Configure(EntityTypeBuilder<StatRecord> builder)
    {
        builder
            .HasOne(r => r.Location)
            .WithMany()
            .HasForeignKey(r => r.LocationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder
            .HasOne(r => r.SubCategoryMetric)
            .WithMany()
            .HasForeignKey(r => r.SubCategoryMetricId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(r => r.Value).HasColumnType("numeric(18,4)");

        builder.HasIndex(r => r.LocationId);
        builder.HasIndex(r => r.SubCategoryMetricId);
        builder.HasIndex(r => new { r.DateFrom, r.DateTo });

        base.Configure(builder);
    }
}
