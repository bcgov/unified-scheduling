---
name: adrs
description: "Create and maintain architecture decision records (ADRs) in docs/adrs using the {topic}-adr.md naming convention and a consistent template. Use when a user needs to capture, review, supersede, or explain an architectural decision."
argument-hint: "Describe the architectural decision, topic, constraints, and any repository-specific ADR preferences"
---

# Architecture Decision Records

## Purpose

Use this skill when a user needs to document an architectural decision. The
deliverable is a completed Markdown file at
`docs/adrs/{topic}-adr.md`, unless the configuration below is explicitly
overridden.

Do not create a placeholder ADR when the topic or decision is unknown. Ask only
the focused questions needed to complete the record, and do not invent
deciders, constraints, evidence, or consequences.

## ADR Basics

### What is an ADR?

An architecture decision record is a short document that captures one
important architectural decision, the context that led to it, and its
consequences. An architecture decision is a significant software design
choice that addresses an important requirement or constraint.

An ADR is part of an architecture decision log: the collection of decisions
that explain how a system has evolved.

### Why are ADRs valuable?

- Preserve the reasoning behind a decision, not just the implementation that
  remains after the discussion is forgotten.
- Give developers, operators, reviewers, and stakeholders a shared reference
  for trade-offs and constraints.
- Reduce repeated debates and help new team members understand why the system
  works the way it does.
- Make consequences, risks, follow-up work, and assumptions visible early.
- Provide a durable point of reference when a decision is later revisited or
  superseded.

An ADR is useful only when it reflects the real decision. Treat it as a
communication and learning tool, not paperwork completed after the fact.

### When should a team create one?

Create an ADR when a choice has meaningful long-term, cross-cutting, costly,
risky, or difficult-to-reverse effects. Typical examples include choices about
data ownership, public interfaces, security, identity, persistence,
integration boundaries, deployment, major dependencies, or system-wide
patterns.

Usually do not create one for a local implementation detail, an obvious and
low-risk choice, a temporary experiment, or a decision already fully defined
by an adopted standard. When in doubt, prefer a concise ADR if future readers
are likely to ask, "Why was this chosen?"

## Create an ADR

Follow this workflow whenever the skill is invoked to create a record:

1. **Define the decision.** State the architectural question and reduce the
   scope to one decision. Split unrelated decisions into separate ADRs.
2. **Inspect nearby records.** Check `docs/adrs` for related, duplicate, or
   superseded decisions. Link to relevant records instead of silently
   contradicting them.
3. **Gather context.** Record the current situation, problem or opportunity,
   scope, constraints, assumptions, and relevant evidence in a short,
   solution-neutral summary.
4. **Compare options.** State the few criteria that matter, then list the
   selected option and one or two credible alternatives. Explain the choice
   and the alternatives briefly; do not create straw-man options.
5. **Record the decision.** Use a direct statement such as "We will ..." and
   explain how it satisfies the decision drivers. Mark the record `Draft` when
   the decision, evidence, alternatives, or deciders remain unresolved. Mark
   it `Proposed` only when it is ready for stakeholder review.
6. **Describe consequences.** Capture the main benefits, trade-offs, risks,
   mitigations, and follow-up work. Include inconvenient or uncertain effects.
7. **Complete metadata and links.** Add the date, status, deciders or roles,
   technical area, related records, and references. Use the current date for a
   newly created record unless the user supplies another date.
8. **Write the file.** Normalize the topic to lowercase kebab-case and create
   `docs/adrs/{topic}-adr.md`. Never overwrite an existing accepted ADR. Use a
   new record to supersede a previous decision.
9. **Review the result.** Apply the concise configuration targets and checklist
   below. Remove unresolved placeholders and unnecessary repetition.

### Statuses

Use one of these statuses unless the repository has a more specific convention:

- `Draft`: a working record that is not ready for stakeholder review or
  proposal.
- `Proposed`: under discussion or awaiting approval.
- `Accepted`: approved and applicable.
- `Rejected`: considered and explicitly declined.
- `Superseded`: replaced by a newer ADR; link the replacement.
- `Deprecated`: no longer applicable without a direct replacement.

Do not change an accepted decision merely to rewrite history. If a later
decision changes it, create a new ADR, set its `Supersedes` metadata, and mark
the earlier record `Superseded` when appropriate.

## ADR Template

Use this concise template for every new `{topic}-adr.md`. Replace every
bracketed placeholder with specific content. Use `None` only when a field
genuinely does not apply, and explain meaningful exceptions in the body.

```markdown
# ADR: [Imperative decision title]

- Status: Draft
- Date: YYYY-MM-DD
- Deciders: [names, roles, or accountable team]
- Technical area: [module, system, or architectural area]
- Related: [issue, ADR, or None]

## Context

[Describe the problem, current state, constraints, and the few criteria that
matter. Keep this short and solution-neutral.]

## Decision

We will [state one unambiguous decision].

[State why this choice fits the context and what is out of scope.]

## Alternatives

- [Alternative]: [Why it was considered and why it was not selected]
- [Alternative or defer/do nothing]: [Why it was not selected, or None]

## Consequences

- Benefit: [Main expected benefit]
- Trade-off: [Main cost, limitation, or risk we accept]
- Mitigation: [Mitigation or monitoring approach, if needed]

## Follow-up

- [Open decision, action, or review trigger, or None]

## References

- [Link to requirements, designs, issue, experiment, standard, or related ADR]
```

Use an imperative, descriptive title and a lowercase kebab-case topic, for
example `choose-postgresql-adr.md` or `define-calendar-event-ownership-adr.md`.
Keep the filename stable after creation.

## Configuration

The defaults below keep ADRs consistent. A user or repository instruction may
override them for a particular record; state the override in the ADR when it
changes the expected structure or review process.

| Setting            | Default                                                              | Guidance                                                              |
| ------------------ | -------------------------------------------------------------------- | --------------------------------------------------------------------- |
| `adrDirectory`     | `docs/adrs`                                                          | Directory where records are created.                                  |
| `filenamePattern`  | `{topic}-adr.md`                                                     | Keep the `-adr.md` suffix.                                            |
| `topicFormat`      | lowercase kebab-case                                                 | Use a short, descriptive, imperative topic; no spaces or underscores. |
| `defaultStatus`    | `Draft`                                                              | Use `Proposed` when the record is ready for stakeholder review.       |
| `dateFormat`       | `YYYY-MM-DD`                                                         | Use an unambiguous ISO-style date.                                    |
| `contextWords`     | 75-150                                                               | Describe the problem and constraints without padding.                 |
| `decisionWords`    | 20-50                                                                | State the choice and rationale directly.                              |
| `alternativeWords` | 25-75 per alternative                                                | Explain only why each credible alternative was not selected.          |
| `consequenceWords` | 50-125                                                               | Cover the main benefit, trade-off, risk, and follow-up work.          |
| `totalWords`       | 250-500                                                              | Advisory total for the body; clarity takes precedence over length.    |
| `requiredMetadata` | status, date, deciders, technical area                               | Do not leave these blank.                                             |
| `requiredSections` | Context, Decision, Alternatives, Consequences, Follow-up, References | Keep these headings stable for consistent review and search.          |

Word targets are ranges, not quotas. A short decision should stay short, and a
complex decision may exceed them when the additional evidence improves future
understanding. Do not pad an ADR or repeat information across sections.

## Quality Checklist

Before finishing, verify that:

- The file is under the configured ADR directory and follows the filename
  pattern.
- The record covers one decision and has a clear status and date.
- The context explains the problem, scope, constraints, and important criteria.
- At least one credible alternative is described and rejected or deferred for
  a stated reason.
- The decision is explicit and distinguishes chosen work from out-of-scope
  work.
- The main benefit, trade-off, risk or mitigation, and follow-up work are
  documented where applicable.
- Deciders, technical area, related records, and references are named or
  intentionally set to `None`.
- There are no unresolved bracketed placeholders, invented facts, or vague
  claims such as "best" without a criterion.
- The concise word targets are respected where practical, without omitting
  material information.
- An accepted decision is not overwritten; changes are represented by a new
  superseding ADR.

## Validation

After creating or changing an ADR:

1. Confirm the exact path is `docs/adrs/{topic}-adr.md` and that no existing
   record was overwritten.
2. Check that the Markdown contains the required metadata and headings.
3. Check the body against the concise word targets and remove repetition or
   placeholders.
4. Run `git diff --check -- docs/adrs/{topic}-adr.md` when the file is tracked
   in the current change.

For background and alternative templates, see the
[architecture-decision-record guide](https://github.com/architecture-decision-record/architecture-decision-record).
