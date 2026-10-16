using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Unified.Db.Models.TimeOff;

namespace Unified.Db.Configuration.TimeOff;

public class TimeOffSeriesConfiguration : BaseEntityConfiguration<TimeOffSeries>
{
    public override void Configure(EntityTypeBuilder<TimeOffSeries> builder)
    {
        builder.Property(entity => entity.Id).HasIdentityOptions(startValue: 200);

        builder
            .HasOne(entity => entity.EventSeries)
            .WithMany()
            .HasForeignKey(entity => entity.EventSeriesId)
            .OnDelete(DeleteBehavior.Restrict);
        builder
            .HasOne(entity => entity.LeaveType)
            .WithMany()
            .HasForeignKey(entity => entity.LeaveTypeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(entity => entity.EventSeriesId).IsUnique();
        builder.HasIndex(entity => entity.LeaveTypeId);
        builder.ToTable("TimeOffSeries");

        base.Configure(builder);
    }
}
