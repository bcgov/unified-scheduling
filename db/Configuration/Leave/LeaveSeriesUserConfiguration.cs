using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Unified.Db.Models.Leave;

namespace Unified.Db.Configuration.Leave;

public class LeaveSeriesUserConfiguration : BaseEntityConfiguration<LeaveSeriesUser>
{
    public override void Configure(EntityTypeBuilder<LeaveSeriesUser> builder)
    {
        builder.Property(entity => entity.Id).HasIdentityOptions(startValue: 200);

        // Physical column/table names predate this rename and are pinned explicitly below
        // so no new migration is required.
        builder.Property(entity => entity.LeaveSeriesId).HasColumnName("LeaveSeriesId");

        builder
            .HasOne(entity => entity.LeaveSeries)
            .WithMany(series => series.Users)
            .HasForeignKey(entity => entity.LeaveSeriesId)
            .OnDelete(DeleteBehavior.Cascade);
        builder
            .HasOne(entity => entity.User)
            .WithMany()
            .HasForeignKey(entity => entity.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(entity => entity.LeaveSeriesId);
        builder.HasIndex(entity => entity.UserId);
        builder.HasIndex(entity => new { entity.LeaveSeriesId, entity.UserId }).IsUnique();
        builder.ToTable("LeaveSeriesUsers");

        base.Configure(builder);
    }
}
