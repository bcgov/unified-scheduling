using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Unified.Db.Models.Lookup;

namespace Unified.Db.Configuration;

public class AssignmentTypeConfiguration : BaseEntityConfiguration<AssignmentType>
{
    public override void Configure(EntityTypeBuilder<AssignmentType> builder)
    {
        builder.Property(b => b.Id).HasIdentityOptions(startValue: 200);
        builder.Property(b => b.Code).HasMaxLength(50).IsRequired();
        builder.Property(b => b.Description).HasMaxLength(100).IsRequired();
        builder.HasIndex(b => b.Code).IsUnique();
        builder.ToTable("AssignmentTypes");
        base.Configure(builder);
    }
}
