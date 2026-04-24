using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Unified.Db.Models.UserManagement;

namespace Unified.Db.Configuration;

public class RoleConfiguration : BaseEntityConfiguration<Role>
{
    public override void Configure(EntityTypeBuilder<Role> builder)
    {
        builder.Property(b => b.Id).HasIdentityOptions(startValue: 50);

        base.Configure(builder);
    }
}
