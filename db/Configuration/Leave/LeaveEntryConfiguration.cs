using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Unified.Db.Models.Leave;

namespace Unified.Db.Configuration.Leave;

public class LeaveEntryConfiguration : BaseEntityConfiguration<LeaveEntry>
{
    public override void Configure(EntityTypeBuilder<LeaveEntry> builder)
    {
        builder.Property(entity => entity.Id).HasIdentityOptions(startValue: 200);

        // Physical column/table names predate this rename and are pinned explicitly below
        // so no new migration is required.
        builder.Property(entity => entity.LeaveSeriesId).HasColumnName("LeaveSeriesId");

        builder
            .HasOne(entity => entity.LeaveSeries)
            .WithMany(series => series.LeaveEntries)
            .HasForeignKey(entity => entity.LeaveSeriesId)
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

        builder.HasIndex(entity => entity.LeaveSeriesId);
        builder.HasIndex(entity => entity.LeaveTypeId);
        builder.HasIndex(entity => entity.EventId).IsUnique();
        builder.ToTable("LeaveEntries");

        base.Configure(builder);
    }
}
