using Microsoft.EntityFrameworkCore;
using Unified.Db;

namespace Unified.Stats.Seeders;

internal static class StatsSeedIdentitySequence
{
    // The SCM seed range crosses the original identity start value. Advance its
    // existing sequence without changing the model or ever moving the sequence back.
    internal static Task AdvanceAsync(UnifiedDbContext db, CancellationToken cancellationToken)
    {
        if (!db.Database.IsNpgsql())
            return Task.CompletedTask;

        return db.Database.ExecuteSqlRawAsync(
            """
            DO $seed$
            DECLARE
                sequence_name regclass := pg_get_serial_sequence('public."SubCategoryMetrics"', 'Id')::regclass;
                sequence_value bigint;
                maximum_id bigint;
            BEGIN
                LOCK TABLE public."SubCategoryMetrics" IN SHARE ROW EXCLUSIVE MODE;
                EXECUTE format('SELECT last_value FROM %s', sequence_name) INTO sequence_value;
                SELECT COALESCE(MAX("Id"), 0) INTO maximum_id FROM public."SubCategoryMetrics";
                PERFORM setval(sequence_name, GREATEST(sequence_value, maximum_id), true);
            END $seed$;
            """,
            cancellationToken
        );
    }
}
