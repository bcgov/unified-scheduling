using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Unified.Db.Models.UserManagement;

namespace Unified.Db.Configuration;

public class UserLocationConfiguration : BaseEntityConfiguration<UserLocation>
{
    public override void Configure(EntityTypeBuilder<UserLocation> builder)
    {
        builder.Property(location => location.Id).HasIdentityOptions(startValue: 5000);
        builder.Property(location => location.EffectiveUntil).IsRequired();
        builder.HasIndex(location => new { location.UserId, location.LocationId, location.EffectiveFrom });
        builder.ToTable(table => table.HasCheckConstraint(
            "CK_UserLocations_EffectiveRange",
            "\"EffectiveFrom\" IS NULL OR \"EffectiveUntil\" > \"EffectiveFrom\""
        ));

        builder.HasOne(location => location.User)
            .WithMany(user => user.UserLocations)
            .HasForeignKey(location => location.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(location => location.Location)
            .WithMany()
            .HasForeignKey(location => location.LocationId)
            .OnDelete(DeleteBehavior.Restrict);

        base.Configure(builder);
    }
}
