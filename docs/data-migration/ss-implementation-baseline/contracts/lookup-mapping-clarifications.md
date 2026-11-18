# SS Lookup Decision Index

**Reconciled 2026-11-16.** This index replaces stale Pending/bookmark labels.
The [canonical SS contract](./ss-mapping-contracts.md) governs implementation.
Deeper decision/evidence history stays in the linked notes, not duplicated here.

| Type | Final rule | Evidence / remaining validation |
|---|---|---|
| 0 CourtRoom | Exact room/resolved location; link unchanged or insert missing active/assignment-referenced room; expired unreferenced missing room excluded | SS-V008..013/048; JC expiry flags explicitly false |
| 1 CourtRole | Exact reviewed classification, then independent Assignment -> definition mapping | G-1..16; no direct lookup-to-definition identity |
| 2 JailRole | Same two-step plan; preserve type distinctions through definition merge key | G-8 cross-type suffix; no name-only cross-type merge |
| 3 EscortRun | Same; real loan-outs migrate through away-location events, not these codes | G-5 |
| 4 OtherAssignment | Same; Test and Leave Waitlist excluded with counts/IDs | G-3; BL-3 |
| 5 LeaveType | 44 snapshot mappings, seven standard links/37 retained paid expired; earlier Special/STIP/Annual aliases retained; null-only scope, trim ends, unknown/collision exception | Q-L1..6 closed; SS-V015 and future editing still require evidence |
| 6 TrainingType | Exact untrimmed Code; null-only scope; approved conversions/append order/dates; category client gate cleared 2026-11-15 | SS-V016/042..047; category labels exact, no replacement catalogue |
| 7 Rank | Exact Code; unique source Description fallback for person text; ignore Location/SubCode; append order, generated IDs | SS-V017; no guessed rank |
| LookupSortOrder | No rows copied. Types 0/1-4/5 omit/count; 6/7 append without changing existing rows | G-14/Q-S1/Q-L5; BL-5 |

No Stats seeds are changed. Group 3 is never a classification target. Imperfect secondary
classifications may use documented logical decisions under the current user authorization;
they must not hide lost business scheduling data.

## Evidence Links

- [Final worksheet](../../../documentation/data-migration/ss-job-notes/mapping-decision-worksheet.md)
- [Leave snapshot](../../../documentation/data-migration/ss-job-notes/leave-mapping-review.md)
- [Training fields](../../../documentation/data-migration/ss-job-notes/training-field-review.md)
- [Rank resolver](../../../documentation/data-migration/ss-job-notes/rank-mapping-review.md)
- [Validation IDs](../../../documentation/data-migration/ss-job-notes/validation-tests.md)
- [Backlog](../backlog.md)

Implementation authorization is not production-write approval or a test-pass claim.
