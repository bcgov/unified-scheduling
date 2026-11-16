# Original extractor boundary and all 53 noncomparable cells

**Original production SQL execution is blocked on missing input, not passed.**
The supplied `ss-shape-extracts.md` says its producer was
`ss-shape-extractor.sql`. That SQL file and the original EX/AG/AD/SP/OD extractors
are not present in the supplied repo-local inputs. No original query was executed
against v10 in this validity pass. No substitute query is described as original.
Provide the original files, preserve their SHA256, then execute them read-only
against `ss_shape_v10`, documenting every clock/parameter/environment substitution.
Band boundaries and last-12-month upper-bound semantics remain unverified.

The [actual local measurement](2026-11-16-shape-profile.md) remains:
1,565 reference rows / 5,906 cells = **4,631 non-suppressed comparable** within
the unchanged ±20% bounds + **1,222 suppressed count bounds** in 1..4 +
**53 noncomparable cells**, zero local failures. This is not a full T060 pass.
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
