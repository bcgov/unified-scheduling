using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TrainingEntity = Unified.Db.Models.Training.Training;

namespace Unified.Db.Configuration.Training;

public class TrainingConfiguration : BaseEntityConfiguration<TrainingEntity>
{
    public override void Configure(EntityTypeBuilder<TrainingEntity> builder)
    {
        builder.Property(b => b.Id).HasIdentityOptions(startValue: 200);

        builder.Property(b => b.Code).HasMaxLength(50).IsRequired();
        builder.Property(b => b.Description).HasMaxLength(200).IsRequired();

        builder.HasIndex(b => b.Code).IsUnique();
        builder.HasIndex(b => b.TrainingCategoryId);

        builder
            .HasOne(b => b.TrainingCategory)
            .WithMany(c => c.Trainings)
            .HasForeignKey(b => b.TrainingCategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        base.Configure(builder);
    }
}
