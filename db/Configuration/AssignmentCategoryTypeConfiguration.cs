using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Unified.Db.Models.Lookup;

namespace Unified.Db.Configuration;

public class AssignmentCategoryTypeConfiguration : BaseEntityConfiguration<AssignmentCategoryType>
{
    public override void Configure(EntityTypeBuilder<AssignmentCategoryType> builder)
    {
        builder.Property(b => b.Id).HasIdentityOptions(startValue: 200);
        builder.Property(b => b.Code).HasMaxLength(50).IsRequired();
        builder.Property(b => b.Description).HasMaxLength(100).IsRequired();
        builder.HasIndex(b => b.Code).IsUnique();
        builder.ToTable("AssignmentCategoryTypes");
        base.Configure(builder);
    }
}
