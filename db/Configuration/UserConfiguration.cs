using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Unified.Db.Models.UserManagement;

namespace Unified.Db.Configuration;

public class UserConfiguration : BaseEntityConfiguration<User>
{
    public override void Configure(EntityTypeBuilder<User> builder)
    {
        builder.Property(b => b.Id).HasIdentityOptions(startValue: 200);
        builder.Property(b => b.PendingRegistration).HasDefaultValue(false);
        builder.HasIndex(b => b.IdirName).IsUnique();
        builder.HasIndex(b => b.IdirId).IsUnique();
        builder.HasIndex(b => b.KeyCloakId).IsUnique();

        // @TODO: Enable after adding User Roles
        // builder
        //     .HasMany(m => m.UserRoles)
        //     .WithOne(m => m.User)
        //     .HasForeignKey(m => m.UserId)
        //     .OnDelete(DeleteBehavior.ClientCascade);

        // @TODO: Enable after adding Location
        // builder
        //     .HasOne(l => l.HomeLocation)
        //     .WithMany()
        //     .HasForeignKey(m => m.HomeLocationId)
        //     .OnDelete(DeleteBehavior.SetNull);

        base.Configure(builder);
    }
}
