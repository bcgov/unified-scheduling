using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Unified.Db.Models.Abstract;

namespace Unified.Db.Configuration;

/// <summary>
/// Shared EF Core configuration for all <see cref="BaseUserEvent"/> derived entities.
/// Wires the User FK and delegates base audit fields to <see cref="BaseEntityConfiguration{TBase}"/>.
/// </summary>
public abstract class BaseUserEventConfiguration<TEvent> : BaseEntityConfiguration<TEvent>
    where TEvent : BaseUserEvent
{
    public override void Configure(EntityTypeBuilder<TEvent> builder)
    {
        builder.Property(b => b.ExpiryReason).HasMaxLength(200);

        builder
            .HasOne(m => m.User)
            .WithMany()
            .HasForeignKey(m => m.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        base.Configure(builder);
    }
}
