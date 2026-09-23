using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Unified.Db.Models.Training;

namespace Unified.Db.Configuration.Training;

public class TrainingProfileTypeConfiguration : BaseEntityConfiguration<TrainingProfileType>
{
    public override void Configure(EntityTypeBuilder<TrainingProfileType> builder)
    {
        builder.Property(b => b.Id).HasIdentityOptions(startValue: 200);

        builder.Property(b => b.Code).HasMaxLength(50).IsRequired();
        builder.Property(b => b.Name).HasMaxLength(100).IsRequired();

        builder.HasIndex(b => b.Code).IsUnique();

        base.Configure(builder);
    }
}