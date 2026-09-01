using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Unified.Db.Models.Calendar;

namespace Unified.Db.Configuration.Calendar;

public sealed class CalendarConflictOverrideConfiguration
    : BaseEntityConfiguration<CalendarConflictOverride>
{
    public override void Configure(EntityTypeBuilder<CalendarConflictOverride> builder)
    {
        builder.Property(overrideEntity => overrideEntity.Note).HasMaxLength(2000).IsRequired();
        builder
            .Property(overrideEntity => overrideEntity.IsActive)
            .HasDefaultValue(true)
            .IsRequired();

        builder
            .HasOne(overrideEntity => overrideEntity.FirstEvent)
            .WithMany()
            .HasForeignKey(overrideEntity => overrideEntity.FirstEventId)
            .OnDelete(DeleteBehavior.Cascade);
        builder
            .HasOne(overrideEntity => overrideEntity.SecondEvent)
            .WithMany()
            .HasForeignKey(overrideEntity => overrideEntity.SecondEventId)
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasIndex(overrideEntity => new
            {
                overrideEntity.FirstEventId,
                overrideEntity.SecondEventId,
                overrideEntity.ResourceId,
            })
            .IsUnique();

        builder.ToTable(
            "CalendarConflictOverrides",
            table =>
                table.HasCheckConstraint(
                    "CK_CalendarConflictOverrides_NormalizedPair",
                    "\"FirstEventId\" < \"SecondEventId\""
                )
        );

        base.Configure(builder);
    }
}
