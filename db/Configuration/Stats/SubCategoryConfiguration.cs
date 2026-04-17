using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Unified.Db.Models.Stats;

namespace Unified.Db.Configuration.Stats;

public class SubCategoryConfiguration : BaseEntityConfiguration<SubCategory>
{
    public override void Configure(EntityTypeBuilder<SubCategory> builder)
    {
        builder.Property(b => b.Id).HasIdentityOptions(startValue: 200);

        builder
            .HasOne(sc => sc.Category)
            .WithMany()
            .HasForeignKey(sc => sc.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        base.Configure(builder);
    }
}
