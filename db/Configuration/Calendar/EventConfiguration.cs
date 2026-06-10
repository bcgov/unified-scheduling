using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Unified.Db.Models.Calendar;
using Unified.Db.Models.Lookup;

namespace Unified.Db.Configuration.Calendar;

public class EventConfiguration : BaseEntityConfiguration<Event>
{
    public override void Configure(EntityTypeBuilder<Event> builder)
    {
        builder.Property(b => b.Id).HasIdentityOptions(startValue: 200);

        builder.Property(b => b.Title).HasMaxLength(200).IsRequired();
        builder.Property(b => b.Description).HasMaxLength(2000);
        builder.Property(b => b.Notes).HasMaxLength(4000);
        builder.Property(b => b.Color).HasMaxLength(100);
        builder.Property(b => b.TimeZoneId).HasMaxLength(100);
        builder.Property(b => b.IsException).HasDefaultValue(false).IsRequired();
        builder
            .Property(b => b.EventTypeCode)
            .HasMaxLength(50)
            .HasDefaultValue(CalendarEventTypeCodes.General)
            .IsRequired();
        builder
            .Property(b => b.StatusTypeCode)
            .HasMaxLength(50)
            .HasDefaultValue(CalendarEventStatusTypeCodes.Draft)
            .IsRequired();
        builder.Property(b => b.CancellationReason).HasMaxLength(2000);
        builder.Property(b => b.SourceModule).HasMaxLength(50).IsRequired();
        builder.Property(b => b.Status).HasMaxLength(50);

        builder
            .HasOne(b => b.EventSeries)
            .WithMany(b => b.Events)
            .HasForeignKey(b => b.EventSeriesId)
            .OnDelete(DeleteBehavior.SetNull);

        builder
            .HasOne(b => b.EventType)
            .WithMany()
            .HasForeignKey(b => b.EventTypeCode)
            .HasPrincipalKey(nameof(EventType.Code))
            .OnDelete(DeleteBehavior.Restrict);

        builder
            .HasOne(b => b.StatusType)
            .WithMany()
            .HasForeignKey(b => b.StatusTypeCode)
            .HasPrincipalKey(nameof(EventStatusType.Code))
            .OnDelete(DeleteBehavior.Restrict);

        builder
            .HasOne(b => b.CancelledByUser)
            .WithMany()
            .HasForeignKey(b => b.CancelledByUserId)
            .OnDelete(DeleteBehavior.SetNull);

        builder
            .HasOne(b => b.Location)
            .WithMany()
            .HasForeignKey(b => b.LocationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(b => b.EventSeriesId);
        builder.HasIndex(b => b.EventTypeCode);
        builder.HasIndex(b => b.StatusTypeCode);
        builder.HasIndex(b => b.CancelledByUserId);
        builder.HasIndex(b => b.LocationId);
        builder.HasIndex(b => new { b.SourceModule, b.StartAtUtc });

        builder.ToTable(
            "Events",
            table =>
                {
                    table.HasCheckConstraint(
                        "CK_Events_EndAfterStart",
                        "\"EndAtUtc\" IS NULL OR \"EndAtUtc\" > \"StartAtUtc\""
                    );
                }
        );

        base.Configure(builder);
    }
}
