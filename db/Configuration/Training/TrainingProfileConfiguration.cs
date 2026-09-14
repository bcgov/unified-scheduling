using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Unified.Db.Models.Training;

namespace Unified.Db.Configuration.Training;

public class TrainingProfileConfiguration : BaseEntityConfiguration<TrainingProfile>
{
    public override void Configure(EntityTypeBuilder<TrainingProfile> builder)
    {
        builder.Property(b => b.Id).HasIdentityOptions(startValue: 200);

        builder.Property(b => b.Code).HasMaxLength(50).IsRequired();
        builder.Property(b => b.Description).HasMaxLength(200).IsRequired();

        builder.HasIndex(b => b.Code).IsUnique();

        base.Configure(builder);
    }
}
