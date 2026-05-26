using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Unified.Db.Models.UserManagement;

namespace Unified.Db.Configuration;

public class PermissionConfiguration : BaseEntityConfiguration<Permission>
{
    public override void Configure(EntityTypeBuilder<Permission> builder)
    {
        builder.Property(b => b.Id).HasMaxLength(100);
        builder.Property(b => b.Group).HasConversion<string>().HasMaxLength(100);
        builder.Property(b => b.Description).IsRequired().HasMaxLength(500);

        base.Configure(builder);
    }
}
