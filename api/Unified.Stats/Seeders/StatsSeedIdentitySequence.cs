using Microsoft.EntityFrameworkCore;
using Unified.Db;

namespace Unified.Stats.Seeders;

internal static class StatsSeedIdentitySequence
{
    // These explicit seed ranges now cross the original identity start values. Advance
    // the existing sequences without changing the model or ever moving a sequence back.
    internal static Task AdvanceAsync(
        UnifiedDbContext db,
        string schema,
        string table,
        CancellationToken cancellationToken
    )
    {
        if (!db.Database.IsNpgsql())
            return Task.CompletedTask;
        if ((schema, table) is not (("public", "SubCategoryMetrics") or ("etl", "BiOracleStatMappings")))
            throw new ArgumentException("Unsupported Stats seed sequence.");

        return db.Database.ExecuteSqlRawAsync(
            $$"""
            DO $seed$
            DECLARE
                sequence_name regclass := pg_get_serial_sequence('{{schema}}."{{table}}"', 'Id')::regclass;
                sequence_value bigint;
                maximum_id bigint;
            BEGIN
                LOCK TABLE {{schema}}."{{table}}" IN SHARE ROW EXCLUSIVE MODE;
                EXECUTE format('SELECT last_value FROM %s', sequence_name) INTO sequence_value;
                SELECT COALESCE(MAX("Id"), 0) INTO maximum_id FROM {{schema}}."{{table}}";
                PERFORM setval(sequence_name, GREATEST(sequence_value, maximum_id), true);
            END $seed$;
            """,
            cancellationToken
        );
    }
}
