# ADR: Use a pre-push hook to confirm review readiness

- Status: Proposed
- Date: 2026-10-22
- Deciders: Unified Scheduling development team
- Technical area: Developer workflow and agentic development
- Related: None

## Context

Formatting and lint failures are often found only after a pull request is
opened, which slows review and adds fix-up commits. Work produced by coding
agents makes this more frequent, because agents need a fast, local signal that
their output meets repository standards. The check must run locally, finish
quickly, and apply the same rules to people and agents.

## Decision

We will use the committed `.githooks/pre-push` hook to run lightweight frontend
lint and Prettier checks and CSharpier checks for `api` and `db` before every
push. Checks are scoped to the areas changed by the pushed commits, so
docs-only pushes skip them. The Copilot instructions direct agents to enable
and follow it. Build and test runs remain in CI.

## Alternatives

- CI-only checks: no local setup is needed, but feedback arrives after the
  push and the pull request needs further commits.
- Pre-commit hook: gives earlier feedback, but runs on every commit and slows
  frequent, small agent commits.

## Consequences

- Benefit: Pull requests arrive formatted and linted, so reviewers and agents
  get faster turnaround.
- Trade-off: Each clone must run `git config core.hooksPath .githooks`, and
  `--no-verify` can bypass the hook.
- Trade-off: The working tree must match the pushed commit, so one push of
  several refs with differing content (e.g. `git push --all`) is blocked.
- Mitigation: CI workflows remain the authoritative build and test gate.

## Follow-up

- None

## References

- [Pre-push hook](../../.githooks/pre-push)
- [Copilot instructions](../../.github/copilot-instructions.md)
- [README hook setup](../../README.md)
