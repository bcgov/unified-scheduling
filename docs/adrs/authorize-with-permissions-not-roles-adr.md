# ADR: Authorize with permissions rather than roles

- Status: Proposed
- Date: 2026-08-18
- Deciders: Not identified; issue #130 was opened by @BronzBierd
- Technical area: API authorization
- Related: [Issue #130](https://github.com/bcgov/unified-scheduling/issues/130)

## Context

Issue #130 proposes that application authorization check permissions rather than
roles, avoiding role-permission combinations that become difficult to scale.
The current authorization flow expands active roles into permission claims and
controllers use named permission policies. Roles remain useful for assigning
permissions and administering access, but endpoint authorization should have a
single capability-oriented contract.

## Decision

We will use permission claims and named permission policies for endpoint and
action authorization. Roles may group and assign permissions, but application
code should not gate access directly on role names.

## Alternatives

- Check roles directly: simpler for fixed roles, but couples code to role
  configuration and makes role changes disruptive.
- Check both roles and permissions: supports special cases, but multiplies
  policy combinations and makes access harder to explain.

## Consequences

- Benefit: Endpoint requirements describe capabilities and remain independent
  of role structure.
- Trade-off: Permissions need consistent names, assignments, and review.
- Mitigation: Keep contextual or resource-level rules separate from broad
  permissions and test authorized and forbidden paths.

## Follow-up

- Confirm approved exceptions for direct role inspection.
- Decide whether role claims remain available to all downstream code.
- Assign ownership for new permission constants and role assignments.

## References

- [Authorization module guidance](../../api/Unified.Authorization/README.md)
- [Permission authorization handler](../../api/Unified.Authorization/Requirements/PermissionAuthorizationHandler.cs)
- [Permission constants](../../api/Unified.Authorization/Permissions.cs)