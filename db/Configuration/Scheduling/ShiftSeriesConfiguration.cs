using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Unified.Db.Models.Scheduling;

namespace Unified.Db.Configuration.Scheduling;

public class ShiftSeriesConfiguration : BaseEntityConfiguration<ShiftSeries>
{
    public override void Configure(EntityTypeBuilder<ShiftSeries> builder)
    {
        builder.Property(b => b.Id).HasIdentityOptions(startValue: 200);

        builder
            .HasOne(b => b.EventSeries)
            .WithMany()
            .HasForeignKey(b => b.EventSeriesId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(b => b.EventSeriesId).IsUnique();

        builder.ToTable("ShiftSeries");

        base.Configure(builder);
    }
}
