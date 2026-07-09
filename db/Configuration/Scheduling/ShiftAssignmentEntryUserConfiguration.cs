using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Unified.Db.Models.Scheduling;

namespace Unified.Db.Configuration.Scheduling;

public class ShiftAssignmentEntryUserConfiguration
    : BaseEntityConfiguration<ShiftAssignmentEntryUser>
{
    public override void Configure(EntityTypeBuilder<ShiftAssignmentEntryUser> builder)
    {
        builder.Property(b => b.Id).HasIdentityOptions(startValue: 200);

        builder
            .HasOne(b => b.ShiftAssignmentEntry)
            .WithMany(b => b.Users)
            .HasForeignKey(b => b.ShiftAssignmentEntryId)
            .OnDelete(DeleteBehavior.Cascade);
        builder
            .HasOne(b => b.User)
            .WithMany()
            .HasForeignKey(b => b.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(b => b.ShiftAssignmentEntryId);
        builder.HasIndex(b => b.UserId);
        builder.HasIndex(b => new { b.ShiftAssignmentEntryId, b.UserId }).IsUnique();

        builder.ToTable("ShiftAssignmentEntryUsers");

        base.Configure(builder);
    }
}
