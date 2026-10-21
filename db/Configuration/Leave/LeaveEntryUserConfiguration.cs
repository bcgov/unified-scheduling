using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Unified.Db.Models.Leave;

namespace Unified.Db.Configuration.Leave;

public class LeaveEntryUserConfiguration : BaseEntityConfiguration<LeaveEntryUser>
{
    public override void Configure(EntityTypeBuilder<LeaveEntryUser> builder)
    {
        builder.Property(entity => entity.Id).HasIdentityOptions(startValue: 200);

        // Physical column/table names predate this rename and are pinned explicitly below
        // so no new migration is required.
        builder.Property(entity => entity.LeaveEntryId).HasColumnName("LeaveEntryId");

        builder
            .HasOne(entity => entity.LeaveEntry)
            .WithMany(entry => entry.Users)
            .HasForeignKey(entity => entity.LeaveEntryId)
            .OnDelete(DeleteBehavior.Cascade);
        builder
            .HasOne(entity => entity.User)
            .WithMany()
            .HasForeignKey(entity => entity.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(entity => entity.LeaveEntryId);
        builder.HasIndex(entity => entity.UserId);
        builder.HasIndex(entity => new { entity.LeaveEntryId, entity.UserId }).IsUnique();
        builder.ToTable("LeaveEntryUsers");

        base.Configure(builder);
    }
}
