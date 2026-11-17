# Data Migration PR Stack

Historical PR stack; current work remains on `Feature/SS-872-ss-migration-job`.
The 2026-11-16 request authorizes local milestone commits only: no pushes, PR creation
or branch switching. The [current plan](./plan.md) and [tasks](./tasks.md) supersede old
mapping-blocked statuses and the proposed additional branches below.

## Rules

- Each PR targets the prior PR branch, except the first PR, which targets `main`.
- Keep every PR independently buildable and safe with both migration sources disabled.
- Do not include secrets, connection strings, source payloads, or values from local `.env` files.
- Update the PR number, commit SHA, validation, and merge state before opening the next PR.
- Rebase or retarget child PRs after a parent merges. Record the new parent below.

## Stack

| Order | Branch | PR | Parent | Scope | Required checks | State |
|---|---|---|---|---|---|---|
| 1 | `Feature/SS-872-Data-migration` | [#351](https://github.com/bcgov/unified-scheduling/pull/351) | `main` | T001-T013: disabled module, target migration state, EF migration, source contracts, and foundation tests | CSharpier, `dotnet format`, focused migration tests, EF migration apply on local Compose PostgreSQL | Open |
| 2 | `Feature/SS-872-ss-migration-job` | TBD | PR 1 | T016, T014-T015, T017-T018, T022-T023: approved SS contract, recurring job, read-only adapter, and SS tests | SS isolation, disabled schedule, watermark replay, idempotence, batch rollback, one-active-run tests | Branch created; gates/preflight tested; full execution blocked by mapping approval |
| 3 | `Feature/SS-872-ss-migration-mappings` | TBD | PR 2 | T019-T021: approved SS reference, people/event, and roster mappings | Approved table contracts, mapping acceptance checks, reconciliation evidence | Not started |
| 4 | `Feature/SS-872-migration-operations` | TBD | PR 3 | T034-T040: source-scoped controls, rollback, API, permissions, logging, and tests | Authorization, redaction, abort boundary, conflict-safe rollback, OWASP review | Not started |
| 5 | `Feature/SS-872-ss-migration-gitops` | TBD | PR 3 or PR 4 | T041-T045: CB6495 SS secret reference, egress, overlays, and render validation | Render dev/test overlays; verify no CASS settings or `tenant-gitops-ac1cc8` changes | Not started |
| 6 | `Feature/SS-872-migration-release-readiness` | TBD | All required parents | T046-T050: runbook, full validation, and release readiness | Full build/test, EF script, security review, documented evidence | Not started |

## PR Record Template

Copy this section for each opened PR.

### PR 1 - Data Migration Foundation

- **Branch**: `Feature/SS-872-Data-migration`
- **Parent branch / PR**: `main`
- **GitHub PR**: [#351](https://github.com/bcgov/unified-scheduling/pull/351)
- **Head commit**: `TBD`
- **Task IDs**: `T001-T013`
- **Scope**: Disabled module, target migration state, EF migration, source contracts, and foundation tests.
- **Validation run**: CSharpier, `dotnet format`, 12 focused migration tests, and PostgreSQL Compose EF migration apply.
- **Human review gate**: Review migration schema, active-run constraint, transaction boundary, and disabled-by-default configuration.
- **Merge state**: `Open`
- **Retargeted after parent merge**: `No`
- **Notes / follow-up**: Do not enable SS or CASS. PR 2 remains blocked by the approved SS mapping contract.

### PR N - Title TBD

- **Branch**: `TBD`
- **Parent branch / PR**: `TBD`
- **GitHub PR**: `TBD`
- **Head commit**: `TBD`
- **Task IDs**: `TBD`
- **Scope**: `TBD`
- **Validation run**: `TBD`
- **Human review gate**: `TBD`
- **Merge state**: `Not opened | Open | Changes requested | Approved | Merged`
- **Retargeted after parent merge**: `No`
- **Notes / follow-up**: `TBD`

## Current Gate

PR 1 is historical foundation context; its remote state was not rechecked this session.
Mapping reconciliation is complete for implementation; production write approval remains.

Do not enable an SS or CASS source in any PR in this stack. SS write enablement requires the later dry-run reconciliation and human approval gates in [quickstart.md](./quickstart.md).