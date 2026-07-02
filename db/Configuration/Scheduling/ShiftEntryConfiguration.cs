using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Unified.Db.Models.Scheduling;

namespace Unified.Db.Configuration.Scheduling;

public class ShiftEntryConfiguration : BaseEntityConfiguration<ShiftEntry>
{
    public override void Configure(EntityTypeBuilder<ShiftEntry> builder)
    {
        builder.Property(b => b.Id).HasIdentityOptions(startValue: 200);

        builder
            .HasOne(b => b.ShiftSeries)
            .WithMany(b => b.ShiftEntries)
            .HasForeignKey(b => b.ShiftSeriesId)
            .OnDelete(DeleteBehavior.SetNull);

        builder
            .HasOne(b => b.Event)
            .WithMany()
            .HasForeignKey(b => b.EventId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(b => b.ShiftSeriesId);
        builder.HasIndex(b => b.EventId).IsUnique();

        builder.ToTable("ShiftEntries");

        base.Configure(builder);
    }
}
