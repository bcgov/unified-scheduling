using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Unified.Db.Models;

namespace Unified.Db.Configuration;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.IdirName).HasMaxLength(200).IsRequired();
        builder.Property(x => x.IsEnabled).IsRequired();
        builder.Property(x => x.FirstName).HasMaxLength(150).IsRequired();
        builder.Property(x => x.LastName).HasMaxLength(150).IsRequired();
        builder.Property(x => x.Email).HasMaxLength(320).IsRequired();
        builder.Property(x => x.HomeLocationId);
        builder.Property(x => x.LastLogin);

        builder.HasIndex(x => x.IdirName).IsUnique();
        builder.HasIndex(x => x.Email).IsUnique();
    }
}
