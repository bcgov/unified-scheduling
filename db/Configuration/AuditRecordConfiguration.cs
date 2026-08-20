using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Unified.Db.Models;

namespace Unified.Db.Configuration;

public class AuditRecordConfiguration : IEntityTypeConfiguration<AuditRecord>
{
    public void Configure(EntityTypeBuilder<AuditRecord> builder)
    {
        builder.Property(b => b.OccurredOn).HasDefaultValueSql("now()").IsRequired();
        builder.Property(b => b.Action).HasMaxLength(20).IsRequired();
        builder.Property(b => b.EntityType).HasMaxLength(200).IsRequired();
        builder.Property(b => b.TableName).HasMaxLength(200).IsRequired();

        builder.Property(b => b.ActorName).HasMaxLength(200);
        builder.Property(b => b.SourceModule).HasMaxLength(100);
        builder.Property(b => b.CorrelationId).HasMaxLength(200);

        // JSONB columns
        builder.Property(b => b.KeyValues).HasColumnType("jsonb").IsRequired();
        builder.Property(b => b.OldValues).HasColumnType("jsonb");
        builder.Property(b => b.NewValues).HasColumnType("jsonb");

        // text[] column
        builder.Property(b => b.ChangedColumns).HasColumnType("text[]");

        // No FK constraints — audit records must survive entity deletion
        builder.HasNoKey();
        builder.Property(b => b.Id).ValueGeneratedOnAdd();

        // Restore PK after HasNoKey() override
        builder.HasKey(b => b.Id);

        builder.HasIndex(b => new { b.EntityType, b.KeyValues }).HasDatabaseName("ix_audit_entity");
        builder
            .HasIndex(b => new { b.ActorUserId, b.OccurredOn })
            .IsDescending(false, true)
            .HasDatabaseName("ix_audit_actor");
        builder.HasIndex(b => b.OccurredOn).IsDescending().HasDatabaseName("ix_audit_occurred");
        builder.HasIndex(b => b.TableName).HasDatabaseName("ix_audit_table");
    }
}
