using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Unified.Db.Models.Stats;

namespace Unified.Db.Configuration.Stats;

public class BiOracleLocationMappingConfiguration : BaseEntityConfiguration<BiOracleLocationMapping>
{
    public override void Configure(EntityTypeBuilder<BiOracleLocationMapping> builder)
    {
        builder.ToTable("BiOracleLocationMapping", "etl");

        builder.Property(mapping => mapping.JustinLocationCode).HasMaxLength(4);

        builder.HasIndex(mapping => mapping.JustinLocationCode).IsUnique();
        builder.HasIndex(mapping => mapping.CrtLocId).IsUnique();
        builder.HasIndex(mapping => mapping.ExpiryDate);

        builder.ToTable(table =>
        {
            table.HasCheckConstraint(
                "CK_BiOracleLocationMapping_JustinLocationCode",
                "length(trim(\"JustinLocationCode\")) = 4"
            );
            table.HasCheckConstraint(
                "CK_BiOracleLocationMapping_EffectivePeriod",
                "\"ExpiryDate\" IS NULL OR \"ExpiryDate\" >= \"EffectiveDate\""
            );
        });

        base.Configure(builder);
    }
}
