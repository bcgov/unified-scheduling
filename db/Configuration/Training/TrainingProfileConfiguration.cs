using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Unified.Db.Models.Training;

namespace Unified.Db.Configuration.Training;

public class TrainingProfileConfiguration : BaseEntityConfiguration<TrainingProfile>
{
    public override void Configure(EntityTypeBuilder<TrainingProfile> builder)
    {
        builder.ToTable("TrainingMandatoryTrainingProfiles");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).UseIdentityByDefaultColumn();

        builder.HasIndex(x => new { x.TrainingId, x.TrainingProfileTypeId }).IsUnique();

        builder.HasIndex(x => x.TrainingProfileTypeId);

        builder
            .HasOne(x => x.Training)
            .WithMany(x => x.TrainingProfiles)
            .HasForeignKey(x => x.TrainingId)
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasOne(x => x.TrainingProfileType)
            .WithMany(x => x.TrainingProfiles)
            .HasForeignKey(x => x.TrainingProfileTypeId)
            .OnDelete(DeleteBehavior.Cascade);

        base.Configure(builder);
    }
}
