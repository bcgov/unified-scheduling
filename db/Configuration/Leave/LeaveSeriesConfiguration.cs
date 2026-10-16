using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Unified.Db.Models.Leave;

namespace Unified.Db.Configuration.Leave;

public class LeaveSeriesConfiguration : BaseEntityConfiguration<LeaveSeries>
{
    public override void Configure(EntityTypeBuilder<LeaveSeries> builder)
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
        builder.ToTable("LeaveSeries");

        base.Configure(builder);
    }
}
