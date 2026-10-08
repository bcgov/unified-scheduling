using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Unified.Db.Models.TimeOff;

namespace Unified.Db.Configuration.TimeOff;

public class TimeOffSeriesUserConfiguration : BaseEntityConfiguration<TimeOffSeriesUser>
{
    public override void Configure(EntityTypeBuilder<TimeOffSeriesUser> builder)
    {
        builder.Property(entity => entity.Id).HasIdentityOptions(startValue: 200);

        builder
            .HasOne(entity => entity.TimeOffSeries)
            .WithMany(series => series.Users)
            .HasForeignKey(entity => entity.TimeOffSeriesId)
            .OnDelete(DeleteBehavior.Cascade);
        builder
            .HasOne(entity => entity.User)
            .WithMany()
            .HasForeignKey(entity => entity.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(entity => entity.TimeOffSeriesId);
        builder.HasIndex(entity => entity.UserId);
        builder.HasIndex(entity => new { entity.TimeOffSeriesId, entity.UserId }).IsUnique();
        builder.ToTable("TimeOffSeriesUsers");

        base.Configure(builder);
    }
}
