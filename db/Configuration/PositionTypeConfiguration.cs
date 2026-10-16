using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Unified.Db.Models.Lookup;

namespace Unified.Db.Configuration;

public class PositionTypeConfiguration : BaseEntityConfiguration<PositionType>
{
    public override void Configure(EntityTypeBuilder<PositionType> builder)
    {
        builder.Property(b => b.Id).HasIdentityOptions(startValue: 200);

        builder.Property(b => b.Code).HasMaxLength(50).IsRequired();
        builder.Property(b => b.Description).HasMaxLength(100).IsRequired();
        builder.Property(b => b.DisplayOrder).IsRequired();

        builder.HasIndex(b => b.Code).IsUnique();

        builder.ToTable(
            "PositionTypes",
            table =>
                table.HasCheckConstraint(
                    "CK_PositionTypes_DisplayOrderPositive",
                    "\"DisplayOrder\" > 0"
                )
        );

        base.Configure(builder);
    }
}
