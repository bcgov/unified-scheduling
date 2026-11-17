# Migration operations API: implemented, production disabled

Only migration-owned code changed. Existing string permission policies and module
permission seeds are used; role request DTOs already accept string permission IDs.
No role is automatically granted either permission, and no auth framework or
application workflow was changed.

## Operator contract

All routes start `/api/data-migration/SS`. The authenticated identity must have a
resolved, nonempty Unified user ID and `DataMigrationOperate`. Rollback routes
additionally require `DataMigrationRollback`. Assign these using the existing role
permission mechanism. Unauthenticated/denied requests return 401/403. CASS is
explicitly deferred (501); unknown sources/jobs and mismatched run scopes are
rejected. Jobs are `MyTeam`, `Training`, `Scheduling`, `Report`.

| Method / relative route | Purpose |
|---|---|
| GET `status`, `runs`, `runs/{id}` | Source status/history, including operational receipts |
| GET `jobs/{job}/status`, `jobs/{job}/runs`, `jobs/{job}/runs/{id}` | Job-scoped state/history |
| POST `jobs/{job}/runs` | Body `{"dryRun":true}`; acquires stable run before Hangfire dispatch; 202 Location identifies that run |
| POST `control/{action}`, `jobs/{job}/control/{action}` | `pause`, `resume`, `abort`; persisted public controls and append-only action audit |
| POST `jobs/{job}/runs/{id}/recover` | Body `staleBeforeUtc`; existing stale recovery/fencing and dependency guards |
| GET `jobs/{job}/runs/{id}/findings/{kind}` | Paged `outcomes` or `failures` |
| GET `jobs/{job}/runs/{id}/export/{format}` | Complete `csv` or `markdown`, not a page |
| POST `jobs/{job}/runs/{id}/rollback-preview` | Current immutable run-bound plan/digest; no ordinary filesystem path |
| GET `jobs/{job}/runs/{id}/rollback-impact?digest=…` | Separately authorized restricted impact download; verifies current digest and unchanged owner-only export |
| POST `jobs/{job}/runs/{id}/rollback` | Existing `ConfirmDataLoss`, confirmed run ID and digest guards; atomic inverse and dependencies |

Resume does not enable configuration, bypass lineage, or clear a failure. Missing
or unsafe configuration prevents dispatch. A dry-run request cannot become apply;
an apply request cannot override configured DryRun. The worker revalidates the
acquired run/configuration, uses a per-run PG session lock to prevent duplicate
delivery, and executes that same stable run. Dispatch failure persists Failed on
the acquired ID. Public controls are checked even inside private dry-run schemas.
Existing apply/rollback rehearsal restrictions remain; this is not production
activation.

## Export and privacy

CSV/Markdown stream one repeatable-read snapshot: Run summary, every durable
outcome, every failure, then End/SnapshotComplete with actual emitted counts.
There is no UI-page cap. Interrupted streams are aborted and lack the complete
footer. Both formats are no-store, escaped for their format, and CSV spreadsheet
formulas are neutralized.

Known machine codes/entities are allowlisted; unknown strings become opaque
SHA256. Only canonical GUID/numeric legacy keys are exposed directly; other keys
are hashed. Raw failure messages/watermarks, connection strings, mapped personal
text, photos and protected filesystem paths are not ordinary output. Restricted
impact remains a separate sensitive download, not a findings attachment.
These exports are migration diagnostics, not business UI/report CSV parity.

Report loader completion alone is NotAccepted. A final Report verdict requires
Completed Report, actual positive SourceOracleValidation and no unresolved
primary failures; unknown/unclassified findings cannot silently produce Pass.

## Verification

- SDK10 API build: pass (28 warnings, zero errors).
- Focused suites: **99 migration +5 Hangfire, zero failed/skipped**. All prior
  71 migration tests retained; 28 new HTTP/adapter/export tests.
- Tests cover real existing permission policies, authenticated/denied identities,
  source/job/run isolation, additional rollback permission, guard routes, stable
  dispatch IDs, controls/audits, redaction, complete 257-outcome exports, escaping,
  pagination and Report verdict holds. No full release-security certification.
- Actual PG isolated `unified_ss_rehearsal_operations_v15`: real coordinator,
  worker, simulation DML, public pause, exports and footprint checks passed.
  Recording dispatch tests the queue boundary, not a live Hangfire server.
  Completed MyTeam `622dbdc2-4106-4db2-84b6-b40bdcf5afb8`; paused simulation
  `8067bf73-c2d4-46e2-bce8-ba011e044918` retained Failed without publishing candidates.
  Duplicate owner/redelivery refused; ordinary/applied/progress/sequence/audit
  footprint unchanged; no business audit escaped simulation.
- v14 retained: an initial proof interceptor did not observe raw search_path
  commands. Both runs completed; this was a failed harness attempt, not a pause
  proof. v15 checks actual current_schema before inducing public pause.

Production remains disabled. BL15, matched Report failures and broader independent
validation gaps are not waived by implementing the operator API.
