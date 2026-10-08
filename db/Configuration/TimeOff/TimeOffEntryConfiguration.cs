using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Unified.Db.Models.TimeOff;

namespace Unified.Db.Configuration.TimeOff;

public class TimeOffEntryConfiguration : BaseEntityConfiguration<TimeOffEntry>
{
    public override void Configure(EntityTypeBuilder<TimeOffEntry> builder)
    {
        builder.Property(entity => entity.Id).HasIdentityOptions(startValue: 200);

        builder
            .HasOne(entity => entity.TimeOffSeries)
            .WithMany(series => series.TimeOffEntries)
            .HasForeignKey(entity => entity.TimeOffSeriesId)
            .OnDelete(DeleteBehavior.SetNull);
        builder
            .HasOne(entity => entity.LeaveType)
            .WithMany()
            .HasForeignKey(entity => entity.LeaveTypeId)
            .OnDelete(DeleteBehavior.Restrict);
        builder
            .HasOne(entity => entity.Event)
            .WithMany()
            .HasForeignKey(entity => entity.EventId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(entity => entity.TimeOffSeriesId);
        builder.HasIndex(entity => entity.LeaveTypeId);
        builder.HasIndex(entity => entity.EventId).IsUnique();
        builder.ToTable("TimeOffEntries");

        base.Configure(builder);
    }
}
