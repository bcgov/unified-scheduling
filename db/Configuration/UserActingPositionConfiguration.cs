using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Unified.Db.Models.UserManagement;

namespace Unified.Db.Configuration;

public class UserActingPositionConfiguration : BaseEntityConfiguration<UserActingPosition>
{
    public override void Configure(EntityTypeBuilder<UserActingPosition> builder)
    {
        builder.Property(b => b.Id).HasIdentityOptions(startValue: 1000);

        builder.Property(b => b.ExpiryReason).HasMaxLength(200);
        builder.Property(b => b.Comment).HasMaxLength(500);

        builder
            .HasOne(m => m.User)
            .WithMany()
            .HasForeignKey(m => m.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasOne(m => m.PositionType)
            .WithMany()
            .HasForeignKey(m => m.PositionTypeId)
            .OnDelete(DeleteBehavior.Restrict);

        base.Configure(builder);
    }
}
