using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Unified.Db.Models.Scheduling;

namespace Unified.Db.Configuration.Scheduling;

public class ShiftAssignmentSeriesLinkConfiguration
    : BaseEntityConfiguration<ShiftAssignmentSeriesLink>
{
    public override void Configure(EntityTypeBuilder<ShiftAssignmentSeriesLink> builder)
    {
        builder.Property(b => b.Id).HasIdentityOptions(startValue: 200);

        builder
            .HasOne(b => b.ShiftSeries)
            .WithMany(b => b.ShiftAssignmentSeriesLinks)
            .HasForeignKey(b => b.ShiftSeriesId)
            .OnDelete(DeleteBehavior.Restrict);
        builder
            .HasOne(b => b.AssignmentSeries)
            .WithMany(b => b.ShiftAssignmentSeriesLinks)
            .HasForeignKey(b => b.AssignmentSeriesId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(b => b.AssignmentSeriesId);
        builder.HasIndex(b => new { b.ShiftSeriesId, b.AssignmentSeriesId }).IsUnique();

        builder.ToTable("ShiftAssignmentSeriesLinks");

        base.Configure(builder);
    }
}
