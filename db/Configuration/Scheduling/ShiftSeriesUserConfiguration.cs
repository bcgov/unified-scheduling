using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Unified.Db.Models.Scheduling;

namespace Unified.Db.Configuration.Scheduling;

public class ShiftSeriesUserConfiguration : BaseEntityConfiguration<ShiftSeriesUser>
{
    public override void Configure(EntityTypeBuilder<ShiftSeriesUser> builder)
    {
        builder.Property(b => b.Id).HasIdentityOptions(startValue: 200);

        builder
            .HasOne(b => b.ShiftSeries)
            .WithMany(b => b.Users)
            .HasForeignKey(b => b.ShiftSeriesId)
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasOne(b => b.User)
            .WithMany()
            .HasForeignKey(b => b.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(b => b.ShiftSeriesId);
        builder.HasIndex(b => b.UserId);
        builder.HasIndex(b => new { b.ShiftSeriesId, b.UserId }).IsUnique();

        builder.ToTable("ShiftSeriesUsers");

        base.Configure(builder);
    }
}
