using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Unified.Db.Models.Scheduling;

namespace Unified.Db.Configuration.Scheduling;

public sealed class AssignmentDefinitionConfiguration
    : BaseEntityConfiguration<AssignmentDefinition>
{
    public override void Configure(EntityTypeBuilder<AssignmentDefinition> builder)
    {
        builder.Property(x => x.Name).HasMaxLength(100).IsRequired();
        builder.Property(x => x.NormalizedName).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(500);
        builder.Property(x => x.Color).HasMaxLength(100);
        builder.Property(x => x.DefaultCapacity).IsRequired();
        builder.Property(x => x.EffectiveDateUtc).IsRequired();
        builder.HasIndex(x => new { x.LocationId, x.NormalizedName }).IsUnique();
        builder.HasIndex(x => new { x.EffectiveDateUtc, x.ExpiryDateUtc });
        builder
            .HasOne(x => x.Location)
            .WithMany()
            .HasForeignKey(x => x.LocationId)
            .OnDelete(DeleteBehavior.Restrict);
        builder
            .HasOne(x => x.Category)
            .WithMany()
            .HasForeignKey(x => x.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);
        builder
            .HasOne(x => x.SubCategory)
            .WithMany()
            .HasForeignKey(x => x.SubCategoryId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.ToTable(
            "AssignmentDefinitions",
            table =>
            {
                table.HasCheckConstraint(
                    "CK_AssignmentDefinitions_DefaultCapacityAtLeastOne",
                    "\"DefaultCapacity\" >= 1"
                );
                table.HasCheckConstraint(
                    "CK_AssignmentDefinitions_ExpiryAfterEffective",
                    "\"ExpiryDateUtc\" IS NULL OR \"ExpiryDateUtc\" > \"EffectiveDateUtc\""
                );
                table.HasCheckConstraint(
                    "CK_AssignmentDefinitions_DefaultEndAfterStart",
                    "\"DefaultStartTime\" IS NULL OR \"DefaultEndTime\" IS NULL OR \"DefaultEndTime\" > \"DefaultStartTime\""
                );
            }
        );
        base.Configure(builder);
    }
}
