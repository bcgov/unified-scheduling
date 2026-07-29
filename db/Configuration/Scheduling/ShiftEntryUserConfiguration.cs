using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Unified.Db.Models.Scheduling;

namespace Unified.Db.Configuration.Scheduling;

public class ShiftEntryUserConfiguration : BaseEntityConfiguration<ShiftEntryUser>
{
    public override void Configure(EntityTypeBuilder<ShiftEntryUser> builder)
    {
        builder.Property(b => b.Id).HasIdentityOptions(startValue: 200);

        builder
            .HasOne(b => b.ShiftEntry)
            .WithMany(b => b.Users)
            .HasForeignKey(b => b.ShiftEntryId)
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasOne(b => b.User)
            .WithMany()
            .HasForeignKey(b => b.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(b => b.ShiftEntryId);
        builder.HasIndex(b => b.UserId);
        builder.HasIndex(b => new { b.ShiftEntryId, b.UserId }).IsUnique();

        builder.ToTable("ShiftEntryUsers");

        base.Configure(builder);
    }
}
