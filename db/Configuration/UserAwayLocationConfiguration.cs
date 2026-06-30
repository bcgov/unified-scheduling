using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Unified.Db.Models.UserManagement;

namespace Unified.Db.Configuration;

public class UserAwayLocationConfiguration : BaseUserEventConfiguration<UserAwayLocation>
{
    public override void Configure(EntityTypeBuilder<UserAwayLocation> builder)
    {
        builder.Property(b => b.Id).HasIdentityOptions(startValue: 1000);

        builder
            .HasOne(m => m.Location)
            .WithMany()
            .HasForeignKey(m => m.LocationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(b => new { b.StartAtUtc, b.EndAtUtc });

        base.Configure(builder);
    }
}
