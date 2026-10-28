using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Unified.Db.Models.LoanOut;

namespace Unified.Db.Configuration.LoanOut;

public class LoanOutSeriesUserConfiguration : BaseEntityConfiguration<LoanOutSeriesUser>
{
    public override void Configure(EntityTypeBuilder<LoanOutSeriesUser> builder)
    {
        builder.Property(entity => entity.Id).HasIdentityOptions(startValue: 200);

        // Physical column/table names predate this rename and are pinned explicitly below
        // so no new migration is required.
        builder.Property(entity => entity.LoanOutSeriesId).HasColumnName("LoanOutSeriesId");

        builder
            .HasOne(entity => entity.LoanOutSeries)
            .WithMany(series => series.Users)
            .HasForeignKey(entity => entity.LoanOutSeriesId)
            .OnDelete(DeleteBehavior.Cascade);
        builder
            .HasOne(entity => entity.User)
            .WithMany()
            .HasForeignKey(entity => entity.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(entity => entity.LoanOutSeriesId);
        builder.HasIndex(entity => entity.UserId);
        builder.HasIndex(entity => new { entity.LoanOutSeriesId, entity.UserId }).IsUnique();
        builder.ToTable("LoanOutSeriesUsers");

        base.Configure(builder);
    }
}
