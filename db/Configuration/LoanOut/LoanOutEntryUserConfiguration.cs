using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Unified.Db.Models.LoanOut;

namespace Unified.Db.Configuration.LoanOut;

public class LoanOutEntryUserConfiguration : BaseEntityConfiguration<LoanOutEntryUser>
{
    public override void Configure(EntityTypeBuilder<LoanOutEntryUser> builder)
    {
        builder.Property(entity => entity.Id).HasIdentityOptions(startValue: 200);

        // Physical column/table names predate this rename and are pinned explicitly below
        // so no new migration is required.
        builder.Property(entity => entity.LoanOutEntryId).HasColumnName("LoanOutEntryId");

        builder
            .HasOne(entity => entity.LoanOutEntry)
            .WithMany(entry => entry.Users)
            .HasForeignKey(entity => entity.LoanOutEntryId)
            .OnDelete(DeleteBehavior.Cascade);
        builder
            .HasOne(entity => entity.User)
            .WithMany()
            .HasForeignKey(entity => entity.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(entity => entity.LoanOutEntryId);
        builder.HasIndex(entity => entity.UserId);
        builder.HasIndex(entity => new { entity.LoanOutEntryId, entity.UserId }).IsUnique();
        builder.ToTable("LoanOutEntryUsers");

        base.Configure(builder);
    }
}
