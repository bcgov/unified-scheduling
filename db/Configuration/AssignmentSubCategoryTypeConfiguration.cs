using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Unified.Db.Models.Lookup;

namespace Unified.Db.Configuration;

public class AssignmentSubCategoryTypeConfiguration
    : BaseEntityConfiguration<AssignmentSubCategoryType>
{
    public override void Configure(EntityTypeBuilder<AssignmentSubCategoryType> builder)
    {
        builder.Property(b => b.Id).HasIdentityOptions(startValue: 200);
        builder.Property(b => b.Code).HasMaxLength(50).IsRequired();
        builder.Property(b => b.Description).HasMaxLength(100).IsRequired();
        builder.Property(b => b.ParentCodeTypeId).HasColumnName("AssignmentCategoryTypeId");

        builder.HasIndex(b => b.ParentCodeTypeId);
        builder.HasIndex(b => new { b.ParentCodeTypeId, b.Code }).IsUnique();

        builder.ToTable("AssignmentSubCategoryTypes");
        base.Configure(builder);
    }
}
