using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Unified.Db.Models.DataMigration;

namespace Unified.Db.Configuration.DataMigration;

public sealed class DataMigrationRunConfiguration : IEntityTypeConfiguration<DataMigrationRun>
{
    public void Configure(EntityTypeBuilder<DataMigrationRun> builder)
    {
        builder.ToTable("DataMigrationRuns");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Source).HasMaxLength(16).IsRequired();
        builder.Property(entity => entity.Status).HasMaxLength(16).IsRequired();
        builder
            .HasIndex(entity => new { entity.Source, entity.Status })
            .IsUnique()
            .HasFilter("\"Status\" = 'Running'");
        builder
            .HasOne<DataMigrationRun>()
            .WithMany()
            .HasForeignKey(entity => entity.RollbackOfRunId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class DataMigrationRecordConfiguration : IEntityTypeConfiguration<DataMigrationRecord>
{
    public void Configure(EntityTypeBuilder<DataMigrationRecord> builder)
    {
        builder.ToTable("DataMigrationRecords");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Source).HasMaxLength(16).IsRequired();
        builder.Property(entity => entity.EntityType).HasMaxLength(128).IsRequired();
        builder.Property(entity => entity.LegacyId).HasMaxLength(128).IsRequired();
        builder.Property(entity => entity.TargetType).HasMaxLength(128).IsRequired();
        builder.Property(entity => entity.TargetKey).HasMaxLength(128).IsRequired();
        builder.Property(entity => entity.PayloadHash).HasMaxLength(128).IsRequired();
        builder
            .HasIndex(entity => new
            {
                entity.Source,
                entity.EntityType,
                entity.LegacyId,
            })
            .IsUnique();
        builder
            .HasOne(entity => entity.LastAppliedRun)
            .WithMany()
            .HasForeignKey(entity => entity.LastAppliedRunId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class DataMigrationFailureConfiguration
    : IEntityTypeConfiguration<DataMigrationFailure>
{
    public void Configure(EntityTypeBuilder<DataMigrationFailure> builder)
    {
        builder.ToTable("DataMigrationFailures");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Source).HasMaxLength(16).IsRequired();
        builder.Property(entity => entity.EntityType).HasMaxLength(128);
        builder.Property(entity => entity.LegacyId).HasMaxLength(128);
        builder.Property(entity => entity.ErrorType).HasMaxLength(128).IsRequired();
        builder.Property(entity => entity.ErrorMessage).HasMaxLength(2048).IsRequired();
        builder.Property(entity => entity.Watermark).HasMaxLength(512);
        builder
            .HasIndex(entity => new { entity.RunId, entity.OccurredOn })
            .IsDescending(false, true);
        builder
            .HasOne(entity => entity.Run)
            .WithMany(entity => entity.Failures)
            .HasForeignKey(entity => entity.RunId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class DataMigrationMutationConfiguration
    : IEntityTypeConfiguration<DataMigrationMutation>
{
    public void Configure(EntityTypeBuilder<DataMigrationMutation> builder)
    {
        builder.ToTable("DataMigrationMutations");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Operation).HasMaxLength(32).IsRequired();
        builder.Property(entity => entity.AfterHash).HasMaxLength(128).IsRequired();
        builder.Property(entity => entity.Version).HasMaxLength(128);
        builder.HasIndex(entity => new { entity.RunId, entity.Sequence }).IsDescending(false, true);
        builder
            .HasOne(entity => entity.Run)
            .WithMany(entity => entity.Mutations)
            .HasForeignKey(entity => entity.RunId)
            .OnDelete(DeleteBehavior.Cascade);
        builder
            .HasOne(entity => entity.DataMigrationRecord)
            .WithMany(entity => entity.Mutations)
            .HasForeignKey(entity => entity.DataMigrationRecordId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class DataMigrationControlConfiguration
    : IEntityTypeConfiguration<DataMigrationControl>
{
    public void Configure(EntityTypeBuilder<DataMigrationControl> builder)
    {
        builder.ToTable("DataMigrationControls");
        builder.HasKey(entity => entity.Source);
        builder.Property(entity => entity.Source).HasMaxLength(16);
        builder.Property(entity => entity.ChangedBy).HasMaxLength(256);
    }
}

public sealed class DataMigrationWatermarkConfiguration
    : IEntityTypeConfiguration<DataMigrationWatermark>
{
    public void Configure(EntityTypeBuilder<DataMigrationWatermark> builder)
    {
        builder.ToTable("DataMigrationWatermarks");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Source).HasMaxLength(16).IsRequired();
        builder.Property(entity => entity.EntityType).HasMaxLength(128).IsRequired();
        builder.Property(entity => entity.LegacyId).HasMaxLength(128).IsRequired();
        builder.HasIndex(entity => new { entity.Source, entity.EntityType }).IsUnique();
        builder.HasIndex(entity => entity.LastCompletedRunId);
        builder
            .HasOne(entity => entity.LastCompletedRun)
            .WithMany()
            .HasForeignKey(entity => entity.LastCompletedRunId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
