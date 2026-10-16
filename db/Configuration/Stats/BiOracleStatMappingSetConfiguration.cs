using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Unified.Db.Models.Stats;

namespace Unified.Db.Configuration.Stats;

public class BiOracleStatMappingSetConfiguration : BaseEntityConfiguration<BiOracleStatMappingSet>
{
    public override void Configure(EntityTypeBuilder<BiOracleStatMappingSet> builder)
    {
        builder.ToTable("BiOracleStatMappingSets", "etl");
        builder.HasIndex(mappingSet => mappingSet.EffectiveDate).IsUnique();
        builder.HasIndex(mappingSet => mappingSet.ExpiryDate);
        builder.ToTable(table =>
            table.HasCheckConstraint(
                "CK_BiOracleStatMappingSets_EffectivePeriod",
                "\"ExpiryDate\" IS NULL OR \"ExpiryDate\" >= \"EffectiveDate\""
            )
        );

        base.Configure(builder);
    }
}
