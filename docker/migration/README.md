# Isolated SS migration rehearsal

This is **not production deployment configuration**. No host database ports are published.
Trust authentication is restricted to the synthetic-only Docker network; never expose these
services or reuse this authentication policy for production.

Run from the repository root with Docker and `rtk` available:

```powershell
rtk docker compose -f docker/migration/compose.yml up -d source target
rtk docker compose -f docker/migration/compose.yml ps
rtk docker compose -f docker/migration/compose.yml run --rm runner schema
rtk docker compose -f docker/migration/compose.yml run --rm runner bootstrap
rtk docker compose -f docker/migration/compose.yml run --rm runner MyTeam
rtk docker compose -f docker/migration/compose.yml run --rm runner evidence
```

Default jobs are dry runs. The adverse fixture deliberately contains rejecting cases and
unsupported future gaps; a nonzero migration exit is expected. Never remove those cases to
manufacture acceptance. `bootstrap` refuses non-empty users/crosswalks and accepts only
explicitly named isolated rehearsal databases. It calls real application reference seeders;
it is never called by migration execution or app startup.

`source-init.sh` initializes only a new named volume, refuses filled source generation through
the supplied generator guard, creates a read-only migration role, and marks the database
`SS SYNTHETIC` for the separately owned legacy oracle. Initialization failures must be
investigated; **do not run down -v** to hide them. Repeated up/start preserves all data.

The original `ss-synth-pg` container/anonymous volume are not part of this Compose project.

## Separate minimal ready fixture

This smoke fixture is not SH-fit or complete acceptance coverage. Create new databases
`ss_ready_v2` on source and `unified_ss_rehearsal_ready_v2` on target with `createdb`.
On source, execute `/fixture/ss-schema-noseed.sql`, then `/fixture-ready.sql` with
`psql -v ON_ERROR_STOP=1`. Do not clone or repair the adverse database.

Then use both Compose files for `schema`, `bootstrap`, `MyTeam`, `Training`, `Scheduling`,
`queries` and `evidence`:

```powershell
rtk docker compose -f docker/migration/compose.yml -f docker/migration/ready.yml run --rm runner MyTeam
```

The ready overlay explicitly applies data, but only to the separately named ready target.
First-rehearsal `ss_ready` databases are retained as failure evidence. Neither overlay
overwrites them. Scheduling remains NeedsAction because recurrence/full reconciliation are
not implemented; Report cannot pass.

## Operations and limitations

- Configuration: `DataMigration:Sources:SS:Jobs:{MyTeam|Training|Scheduling|Report}` has
  Enabled, CronSchedule, DryRun (default true), BatchSize. Source-wide Enabled/connection,
  fixed CutoverDate and HistoryStartDate remain required.
- Migration controls use source keys `SS` and `SS:{Job}`. Paused/abort controls fence starts;
  run status/row locks fence pre-write execution.
- CLI `recover` reads `DataMigrationRecovery__RunId` and
  `DataMigrationRecovery__StaleBeforeUtc`. It requires an explicitly stale Running SS run;
  NOWAIT refuses a live business-transaction fence. Independent concurrency tests remain open.
- CLI `queries` runs actual target training and working-hours queries, emitting only IDs,
  dates, statuses and metrics. It is not the final reconciliation report.
- `docker/migration/.env.oracle.local` is ignored. Provide oracle acknowledgment/connection
  using this env-file, never secret CLI values or committed credentials.
- There is no completed rollback operation. Do not treat schema Down, Docker volume deletion
  or a database restore as an implemented migration rollback.

See [measured results](../../docs/data-migration/reports/2026-11-16-rehearsal.md) and
[all remaining gaps](../../docs/data-migration/implementation-status.md).
