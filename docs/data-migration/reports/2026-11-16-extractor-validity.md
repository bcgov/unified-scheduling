# Original extractor boundary and all 53 noncomparable cells

**2026-11-17 correction: exact original SH execution completed.**
The implementation copied unchanged workspace SQL and production Markdown itself;
there is no pending parent handoff gate. Exact `psql -X -f` executions on v10,
its independent rebuild and the preserved v20 adverse source all exited0,
read-only, with empty stderr. Both shape databases match all4,631 numeric and
1,222 suppressed bounds, retaining all53 noncomparables and zero unexpected buckets.
See [original-file hashes, complete inventory, commands and actual adverse fit
failure](2026-11-17-original-sh-execution.md). Band boundaries and rolling
last-12-month upper-bound semantics are now verified without substitutions.
Other EX/AG/AD/SP/OD/LX originals were not executed; no inference is made for them.

The [actual local measurement](2026-11-16-shape-profile.md) remains:
1,565 reference rows / 5,906 cells = **4,631 non-suppressed comparable** within
the unchanged ±20% bounds + **1,222 suppressed count bounds** in 1..4 +
**53 noncomparable cells**, zero local failures. The new exact original-query
execution independently confirms this complete table; neither is full SS acceptance.
The exact 22-table inventory, source integrity, independently generated rebuild
digest and filled-database refusal are measured separately in that report.

## Why 53 cells cannot be compared

These are withheld production metric values, not missing synthetic measurements.
The synthetic SQL computed actual values. A suppressed production count `<5`
does not disclose an exact proportion or percentile. A null production metric
must not be treated as zero, invented, or counted as a pass.

| Profile | Explicit suppressed reference buckets | Withheld metrics | Cells |
|---|---|---|---:|
| SH02 Shift | Hours 0, 1, 3, 21 | Share | 4 |
| SH02 Duty | Hours 0, 2, 20, 23 | Share | 4 |
| SH02 DutySlot | Hours 0, 1, 20 | Share | 3 |
| SH02 SheriffLeave | Hours 1, 2, 3, 18 | Share | 4 |
| SH02 SheriffTraining | Hours 3, 5, 19 | Share | 3 |
| SH02 SheriffAwayLocation | Hours 4, 5, 12, 15 | Share | 4 |
| SH02 SheriffActingRank | Hours 9, 10, 14 | Share | 3 |
| SH03 Shift | `06 8.5-10h` | Share, Median | 2 |
| SH03 DutySlot | `00 <=0`, `07 10-12h` | Share, Median | 4 |
| SH03 SheriffTraining | `06 8.5-10h`, `07 10-12h` | Share, Median | 4 |
| SH03 SheriffAwayLocation | `01 <1h` | Share, Median | 2 |
| SH03 SheriffActingRank | `03 4-8h`, `05 8-8.5h`, `07 10-12h` | Share, Median | 6 |
| SH04 SheriffActingRank | Weekday 7 | Share | 1 |
| SH05 SheriffAwayLocation | Year 2027 | p10, p50, p90 | 3 |
| SH05 SheriffActingRank | Years 2019, 2020 | p10, p50, p90 | 6 |
| **Total** | **SH02 25 + SH03 18 + SH04 1 + SH05 9** | | **53** |

No aggregate or threshold was altered to remove these cells. Original SQL may
clarify the reference definitions, but cannot recover intentionally withheld
production values. Any further comparison must retain that privacy boundary.
