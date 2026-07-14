using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Unified.Db.Models;

namespace Unified.Db.Configuration;

public class CourtRoomConfiguration : BaseEntityConfiguration<CourtRoom>
{
    public override void Configure(EntityTypeBuilder<CourtRoom> builder)
    {
        builder.Property(b => b.Id).HasIdentityOptions(startValue: 1000);

        builder.Property(b => b.Code).HasMaxLength(50).IsRequired();

        builder.HasIndex(b => new { b.Code, b.LocationId }).IsUnique();

        base.Configure(builder);
    }
}
