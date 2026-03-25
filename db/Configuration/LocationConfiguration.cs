using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Unified.Db.Models;

namespace Unified.Db.Configuration;

public class LocationConfiguration : BaseEntityConfiguration<Location>
{
    public override void Configure(EntityTypeBuilder<Location> builder)
    {
        builder.Property(b => b.Id).HasIdentityOptions(startValue: 200);

        builder
            .HasOne(b => b.Region)
            .WithMany()
            .HasForeignKey(m => m.RegionId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(b => b.AgencyId).IsUnique();

        base.Configure(builder);
    }
}
