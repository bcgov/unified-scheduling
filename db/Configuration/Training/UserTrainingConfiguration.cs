using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Unified.Db.Models.Training;

namespace Unified.Db.Configuration.Training;

public class UserTrainingConfiguration : BaseEntityConfiguration<UserTraining>
{
    public override void Configure(EntityTypeBuilder<UserTraining> builder)
    {
        builder.Property(b => b.Id).HasIdentityOptions(startValue: 200);

        builder.Property(b => b.NoticeState).HasMaxLength(50).IsRequired();
        builder.Property(b => b.Notes).HasMaxLength(2000);

        builder
            .HasOne(b => b.User)
            .WithMany(u => u.UserTrainings)
            .HasForeignKey(b => b.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasOne(b => b.Training)
            .WithMany(t => t.UserTrainings)
            .HasForeignKey(b => b.TrainingId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(b => b.UserId);
        builder.HasIndex(b => b.TrainingId);
        builder.HasIndex(b => b.ExpiryDate);
        builder.HasIndex(b => new { b.UserId, b.TrainingId, b.AwardedOn });

        base.Configure(builder);
    }
}
