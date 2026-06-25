using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Unified.Db.Models.Training;

namespace Unified.Db.Configuration.Training;

public class TrainingCategoryConfiguration : BaseEntityConfiguration<TrainingCategory>
{
    public override void Configure(EntityTypeBuilder<TrainingCategory> builder)
    {
        builder.Property(b => b.Id).HasIdentityOptions(startValue: 200);

        builder.Property(b => b.Name).HasMaxLength(100).IsRequired();
        builder.Property(b => b.Description).HasMaxLength(500);

        builder.HasIndex(b => b.Name).IsUnique();

        base.Configure(builder);
    }
}
