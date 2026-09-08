using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Unified.Db.Models.UserManagement;

namespace Unified.Db.Configuration;

public class UserLeaveConfiguration : BaseEntityConfiguration<UserLeave>
{
    public override void Configure(EntityTypeBuilder<UserLeave> builder)
    {
        builder.Property(b => b.Id).HasIdentityOptions(startValue: 1000);

        builder
            .HasOne(m => m.Event)
            .WithMany()
            .HasForeignKey(m => m.EventId)
            .OnDelete(DeleteBehavior.Restrict);

        builder
            .HasOne(m => m.User)
            .WithMany()
            .HasForeignKey(m => m.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder
            .HasOne(m => m.LeaveType)
            .WithMany()
            .HasForeignKey(m => m.LeaveTypeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(b => b.EventId).IsUnique();
        builder.HasIndex(b => b.UserId);
        builder.HasIndex(b => b.LeaveTypeId);

        base.Configure(builder);
    }
}
