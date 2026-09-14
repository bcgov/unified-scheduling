using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Unified.Db.Models.Training;

namespace Unified.Db.Configuration.Training;

public class TrainingMandatoryTrainingProfileConfiguration
    : IEntityTypeConfiguration<TrainingMandatoryTrainingProfile>
{
    public void Configure(EntityTypeBuilder<TrainingMandatoryTrainingProfile> builder)
    {
        builder.HasKey(x => new { x.TrainingId, x.TrainingProfileId });

        builder.HasIndex(x => x.TrainingProfileId);

        builder
            .HasOne(x => x.Training)
            .WithMany(x => x.MandatoryTrainingProfiles)
            .HasForeignKey(x => x.TrainingId)
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasOne(x => x.TrainingProfile)
            .WithMany(x => x.MandatoryTrainings)
            .HasForeignKey(x => x.TrainingProfileId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
