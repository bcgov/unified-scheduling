using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Unified.Db.Models.TimeOff;

namespace Unified.Db.Configuration.TimeOff;

public class TimeOffEntryUserConfiguration : BaseEntityConfiguration<TimeOffEntryUser>
{
    public override void Configure(EntityTypeBuilder<TimeOffEntryUser> builder)
    {
        builder.Property(entity => entity.Id).HasIdentityOptions(startValue: 200);

        builder
            .HasOne(entity => entity.TimeOffEntry)
            .WithMany(entry => entry.Users)
            .HasForeignKey(entity => entity.TimeOffEntryId)
            .OnDelete(DeleteBehavior.Cascade);
        builder
            .HasOne(entity => entity.User)
            .WithMany()
            .HasForeignKey(entity => entity.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(entity => entity.TimeOffEntryId);
        builder.HasIndex(entity => entity.UserId);
        builder.HasIndex(entity => new { entity.TimeOffEntryId, entity.UserId }).IsUnique();
        builder.ToTable("TimeOffEntryUsers");

        base.Configure(builder);
    }
}
