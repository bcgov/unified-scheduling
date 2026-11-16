# SS Migration Validation Evidence

**Session started 2026-11-16. No runtime validity pass yet.**

## Planning Checks

| Check | Evidence / result |
|---|---|
| Active feature | `.specify/feature.json` points to `specs/001-data-migration-jobs` |
| Existing app branch | `Feature/SS-872-ss-migration-job`; no branch created/switched |
| Initial worktree | Pre-existing deleted `docs/data-migration/ss-mapping-contracts.md`; preserve and do not stage |
| Workspace Git ownership | Workspace specs/documentation are outside a Git repo; versioned mirror needed for requested milestone history |
| Plan hooks | `.specify` has no extensions.yml; no hooks apply |
| Setup script | RTK/Bash Windows path invocation failed; active feature resolved directly, plan deliberately reconciled in place |
| Source findings | Final worksheet through G-19/BL-6, split/cutover/report/dry-run/rollback design, production SH extract read |
| Runtime/tooling | Prior inspection: host SDK8; target needs .NET10 container. Existing `ss-synth-pg` stopped; persistence/contents not yet verified |

## Runtime Checks

Pending implementation evidence. Record exact commands, counts and artifact paths here.
Do not promote old fixture checks, code inspection or unit-test success to migration validity.

| Gate | Current result |
|---|---|
| Persistent source schema/health | Pending |
| Production-shape fit +/-20% | Pending |
| Actual SS report oracle | Pending |
| MyTeam identity/auth-field persistence | Pending |
| Training exact facts/report comparison | Pending |
| Scheduling intervals/links/publish parity | Pending |
| Full outcome coverage / accepted verdict | Pending |
| Replay/conflicts/retry/backfill | Pending |
| Dry-run business rollback | Pending |
| Confirmed rollback/footprint proof | Pending |
| Build/tests/EF/format | Pending |

Use SS-V001..057 from the [stable validation register](../../documentation/data-migration/ss-job-notes/validation-tests.md).
SS-V040/041 are superseded, not unimplemented ownership work.
SS-V036's old app-default edit is now BL-9; migration-only explicit-false preflight remains required.
