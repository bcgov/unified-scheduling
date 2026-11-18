using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Unified.DataMigration.FeatureFlags;
using Unified.DataMigration.Options;
using Unified.DataMigration.Services;
using Unified.DataMigration.Sources;
using Unified.Db;

namespace Unified.Tests.TestHelpers;

internal static class SsMigrationTestServices
{
    internal static SsMigrationExecutor Executor(
        UnifiedDbContext db,
        IOptions<DataMigrationOptions> options,
        IOptions<DataMigrationFeatureFlags> flags,
        IConfiguration? configuration = null
    ) =>
        new(
            db,
            configuration ?? new ConfigurationBuilder().Build(),
            options,
            flags,
            new DataMigrationControlService(db),
            new SsSnapshotReader(options),
            new SsMyTeamWriter(db, new SsLeaveWriter(db)),
            new SsTrainingWriter(db),
            new SsSchedulingWriter(db)
        );
}
