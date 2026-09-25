using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Unified.Common.Calendar.Conflicts;
using Unified.Db.Models.Calendar;

namespace Unified.Db.Configuration.Calendar;

public sealed class CalendarConflictOverrideConfiguration
    : BaseEntityConfiguration<CalendarConflictOverride>
{
    public override void Configure(EntityTypeBuilder<CalendarConflictOverride> builder)
    {
        base.Configure(builder);

        builder.Property(overrideEntity => overrideEntity.CreatedById).IsRequired();
        builder
            .HasOne(overrideEntity => overrideEntity.CreatedBy)
            .WithMany()
            .HasForeignKey(overrideEntity => overrideEntity.CreatedById)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired();
        builder.Property(overrideEntity => overrideEntity.Note).HasMaxLength(2000).IsRequired();
        builder
            .Property(overrideEntity => overrideEntity.FirstSourceModule)
            .HasMaxLength(CalendarConflictEventIdentity.SourceModuleMaxLength)
            .IsRequired();
        builder
            .Property(overrideEntity => overrideEntity.FirstEventId)
            .HasMaxLength(CalendarConflictEventIdentity.EventIdMaxLength)
            .IsRequired();
        builder
            .Property(overrideEntity => overrideEntity.SecondSourceModule)
            .HasMaxLength(CalendarConflictEventIdentity.SourceModuleMaxLength)
            .IsRequired();
        builder
            .Property(overrideEntity => overrideEntity.SecondEventId)
            .HasMaxLength(CalendarConflictEventIdentity.EventIdMaxLength)
            .IsRequired();

        builder
            .HasIndex(overrideEntity => new
            {
                overrideEntity.FirstSourceModule,
                overrideEntity.FirstEventId,
                overrideEntity.SecondSourceModule,
                overrideEntity.SecondEventId,
                overrideEntity.ResourceId,
            })
            .IsUnique();

    }
}
