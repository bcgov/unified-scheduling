using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Unified.Db.Models.DataMigration;

namespace Unified.Db.Configuration.DataMigration;

public sealed class DataMigrationSimulationCandidateConfiguration
    : IEntityTypeConfiguration<DataMigrationSimulationCandidate>
{
    public void Configure(EntityTypeBuilder<DataMigrationSimulationCandidate> b)
    {
        b.ToTable("DataMigrationSimulationCandidates");
        b.HasKey(x => new
        {
            x.RunId,
            x.EntityType,
            x.LegacyId,
        });
        b.Property(x => x.Source).HasMaxLength(16).IsRequired();
        b.Property(x => x.EntityType).HasMaxLength(128).IsRequired();
        b.Property(x => x.LegacyId).HasMaxLength(128).IsRequired();
        b.Property(x => x.TargetType).HasMaxLength(128).IsRequired();
        b.Property(x => x.TargetKey).HasMaxLength(128).IsRequired();
        b.Property(x => x.PayloadHash).HasMaxLength(64).IsRequired();
        b.HasIndex(x => new
        {
            x.NamespaceId,
            x.EntityType,
            x.LegacyId,
        });
        b.HasOne<DataMigrationSimulation>()
            .WithMany(x => x.Candidates)
            .HasForeignKey(x => x.RunId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class DataMigrationSimulationConfiguration
    : IEntityTypeConfiguration<DataMigrationSimulation>
{
    public void Configure(EntityTypeBuilder<DataMigrationSimulation> b)
    {
        b.ToTable("DataMigrationSimulations");
        b.HasKey(x => x.RunId);
        b.Property(x => x.SourceHash).HasMaxLength(64).IsRequired();
        b.Property(x => x.BaselineHash).HasMaxLength(64).IsRequired();
        b.Property(x => x.ConfigurationHash).HasMaxLength(64).IsRequired();
        b.Property(x => x.CandidateHash).HasMaxLength(64).IsRequired();
        b.HasIndex(x => new { x.NamespaceId, x.MyTeamRunId });
        b.HasOne<DataMigrationRun>()
            .WithMany()
            .HasForeignKey(x => x.RunId)
            .OnDelete(DeleteBehavior.Restrict);
        b.HasOne<DataMigrationRun>()
            .WithMany()
            .HasForeignKey(x => x.MyTeamRunId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class DataMigrationJournalConfiguration
    : IEntityTypeConfiguration<DataMigrationJournal>
{
    public void Configure(EntityTypeBuilder<DataMigrationJournal> b)
    {
        b.ToTable("DataMigrationJournals");
        b.HasKey(x => x.Id);
        b.Property(x => x.RowKey).HasColumnType("jsonb");
        b.Property(x => x.SchemaName).HasMaxLength(128);
        b.Property(x => x.TableName).HasMaxLength(128);
        b.Property(x => x.Operation).HasMaxLength(1);
        b.Property(x => x.AfterHash).HasMaxLength(64);
        b.Property(x => x.AfterVersion).HasMaxLength(32);
        b.HasIndex(x => new { x.RunId, x.Id });
        b.HasOne<DataMigrationRun>()
            .WithMany()
            .HasForeignKey(x => x.RunId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class DataMigrationWorkItemConfiguration
    : IEntityTypeConfiguration<DataMigrationWorkItem>
{
    public void Configure(EntityTypeBuilder<DataMigrationWorkItem> b)
    {
        b.ToTable("DataMigrationWorkItems");
        b.HasKey(x => x.Id);
        b.Property(x => x.Source).HasMaxLength(16);
        b.Property(x => x.Job).HasMaxLength(32);
        b.Property(x => x.Stream).HasMaxLength(64);
        b.Property(x => x.EntityType).HasMaxLength(128);
        b.Property(x => x.LegacyId).HasMaxLength(128);
        b.Property(x => x.Outcome).HasMaxLength(32);
        b.Property(x => x.Reason).HasMaxLength(128);
        b.HasIndex(x => new
            {
                x.Source,
                x.Job,
                x.Stream,
                x.EntityType,
                x.LegacyId,
            })
            .IsUnique();
        b.HasIndex(x => new
        {
            x.Source,
            x.Job,
            x.Stream,
            x.PendingRetry,
        });
    }
}

public sealed class DataMigrationCheckpointConfiguration
    : IEntityTypeConfiguration<DataMigrationCheckpoint>
{
    public void Configure(EntityTypeBuilder<DataMigrationCheckpoint> b)
    {
        b.ToTable("DataMigrationCheckpoints");
        b.HasKey(x => x.Id);
        b.Property(x => x.Source).HasMaxLength(16);
        b.Property(x => x.Job).HasMaxLength(32);
        b.Property(x => x.Stream).HasMaxLength(64);
        b.Property(x => x.EntityType).HasMaxLength(128);
        b.Property(x => x.LegacyId).HasMaxLength(128);
        b.Property(x => x.UpperLegacyId).HasMaxLength(128);
        b.Property(x => x.IdKind).HasMaxLength(16);
        b.HasIndex(x => new
            {
                x.Source,
                x.Job,
                x.Stream,
                x.EntityType,
            })
            .IsUnique();
    }
}

public sealed class DataMigrationOutcomeConfiguration
    : IEntityTypeConfiguration<DataMigrationOutcome>
{
    public void Configure(EntityTypeBuilder<DataMigrationOutcome> builder)
    {
        builder.ToTable("DataMigrationOutcomes");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.EntityType).HasMaxLength(128).IsRequired();
        builder.Property(x => x.LegacyId).HasMaxLength(128).IsRequired();
        builder.Property(x => x.Outcome).HasMaxLength(32).IsRequired();
        builder.Property(x => x.Reason).HasMaxLength(128);
        builder
            .HasIndex(x => new
            {
                x.RunId,
                x.EntityType,
                x.LegacyId,
            })
            .IsUnique();
        builder
            .HasOne<DataMigrationRun>()
            .WithMany()
            .HasForeignKey(x => x.RunId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class DataMigrationRunConfiguration : IEntityTypeConfiguration<DataMigrationRun>
{
    public void Configure(EntityTypeBuilder<DataMigrationRun> builder)
    {
        builder.ToTable("DataMigrationRuns");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Source).HasMaxLength(16).IsRequired();
        builder.Property(entity => entity.Job).HasMaxLength(32).IsRequired();
        builder.Property(entity => entity.Status).HasMaxLength(16).IsRequired();
        builder
            .HasIndex(entity => new { entity.Source, entity.Job })
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
        builder.Property(entity => entity.AfterHash).HasMaxLength(128);
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
