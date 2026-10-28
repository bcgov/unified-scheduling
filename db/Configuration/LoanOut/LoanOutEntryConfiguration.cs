using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Unified.Db.Models.LoanOut;

namespace Unified.Db.Configuration.LoanOut;

public class LoanOutEntryConfiguration : BaseEntityConfiguration<LoanOutEntry>
{
    public override void Configure(EntityTypeBuilder<LoanOutEntry> builder)
    {
        builder.Property(entity => entity.Id).HasIdentityOptions(startValue: 200);

        builder
            .HasOne(entity => entity.LoanOutSeries)
            .WithMany(series => series.LoanOutEntries)
            .HasForeignKey(entity => entity.LoanOutSeriesId)
            .OnDelete(DeleteBehavior.SetNull);
        builder
            .HasOne(entity => entity.Event)
            .WithMany()
            .HasForeignKey(entity => entity.EventId)
            .OnDelete(DeleteBehavior.Restrict);
        builder
            .HasOne(entity => entity.LoanOutLocation)
            .WithMany()
            .HasForeignKey(entity => entity.LoanOutLocationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(entity => entity.EventId).IsUnique();
        builder.HasIndex(entity => entity.LoanOutLocationId);
        builder.ToTable("LoanOutEntries");

        base.Configure(builder);
    }
}
