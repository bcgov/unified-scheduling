using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Unified.Db.Models.LoanOut;

namespace Unified.Db.Configuration.LoanOut;

public class LoanOutSeriesConfiguration : BaseEntityConfiguration<LoanOutSeries>
{
    public override void Configure(EntityTypeBuilder<LoanOutSeries> builder)
    {
        builder.Property(entity => entity.Id).HasIdentityOptions(startValue: 200);

        builder
            .HasOne(entity => entity.EventSeries)
            .WithMany()
            .HasForeignKey(entity => entity.EventSeriesId)
            .OnDelete(DeleteBehavior.Restrict);
        builder
            .HasOne(entity => entity.LoanOutLocation)
            .WithMany()
            .HasForeignKey(entity => entity.LoanOutLocationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(entity => entity.EventSeriesId).IsUnique();
        builder.HasIndex(entity => entity.LoanOutLocationId);
        builder.ToTable("LoanOutSeries");

        base.Configure(builder);
    }
}
