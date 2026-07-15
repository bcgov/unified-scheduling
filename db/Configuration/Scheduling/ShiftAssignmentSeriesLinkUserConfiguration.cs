using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Unified.Db.Models.Scheduling;

namespace Unified.Db.Configuration.Scheduling;

public class ShiftAssignmentSeriesLinkUserConfiguration : BaseEntityConfiguration<ShiftAssignmentSeriesLinkUser>
{
    public override void Configure(EntityTypeBuilder<ShiftAssignmentSeriesLinkUser> builder)
    {
        builder.Property(b => b.Id).HasIdentityOptions(startValue: 200);

        builder
            .HasOne(b => b.ShiftAssignmentSeriesLink)
            .WithMany(b => b.Users)
            .HasForeignKey(b => b.ShiftAssignmentSeriesLinkId)
            .OnDelete(DeleteBehavior.Cascade);
        builder
            .HasOne(b => b.User)
            .WithMany()
            .HasForeignKey(b => b.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(b => b.ShiftAssignmentSeriesLinkId);
        builder.HasIndex(b => b.UserId);
        builder.HasIndex(b => new { b.ShiftAssignmentSeriesLinkId, b.UserId }).IsUnique();

        builder.ToTable("ShiftAssignmentSeriesLinkUsers");

        base.Configure(builder);
    }
}
