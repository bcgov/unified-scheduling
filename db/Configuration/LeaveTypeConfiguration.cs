using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Unified.Db.Models.Lookup;

namespace Unified.Db.Configuration;

public class LeaveTypeConfiguration : BaseEntityConfiguration<LeaveType>
{
    public override void Configure(EntityTypeBuilder<LeaveType> builder)
    {
        builder.Property(b => b.Id).HasIdentityOptions(startValue: 400);

        builder.Property(b => b.Code).HasMaxLength(50).IsRequired();
        builder.Property(b => b.Description).HasMaxLength(100).IsRequired();

        builder.HasIndex(b => b.Code).IsUnique();

        base.Configure(builder);
    }
}
