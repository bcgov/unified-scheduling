# Mock BIPRDZ destination: source and unresolved items

## Confirmed and represented

- The local destination schema is `COURTS` in `FREEPDB1`.
- The destination tables are `COURTS.SHERIFF_SRVCES` and `COURTS.SHERIFF_SRVCES_HOURS`.
- Column names, Oracle data types, and column order come from the `BCSS_Table_Mapping` worksheet in `BCSS Tables and Fields (Historic Mapping) CURRENT_20260916.xlsx`.
- The worksheet contains 201 sequential columns for `SHERIFF_SRVCES` and 129 sequential columns for `SHERIFF_SRVCES_HOURS`, with no duplicate column names. The generated DDL has the same counts and order.
- The documented types used by these tables are `NUMBER(6)`, `NUMBER(8)`, `NUMBER(10,2)`, `FLOAT(126)`, `DATE`, `VARCHAR2(1 BYTE)`, and `VARCHAR2(30 BYTE)`.
- The legacy loader's logical destination row key is `MONTH` + `CRT_LOC_ID` + `SUPERVISOR_YN`. It updates a statistic column when that logical row exists and inserts a row when it does not.
- `COURTS` is a passwordless `NO AUTHENTICATION` schema owner with no roles or system privileges. The existing `ETL_TARGET` login receives direct `SELECT`, `INSERT`, `UPDATE`, and `DELETE` grants on both tables; `INSERT` and `UPDATE` permit `MERGE`.

No discrepancy was found between the two relevant worksheet row sets and the generated column names, types, order, or counts.

## Not confirmed from live BIPRDZ

- Production Oracle version, character set, and NLS settings.
- Column nullability.
- Primary keys, unique constraints, and whether the logical row key has a physical unique constraint.
- Indexes, including an index on `MONTH`, `CRT_LOC_ID`, and `SUPERVISOR_YN`.
- Foreign keys, including any location reference or crosswalk table.
- Triggers, defaults, virtual or generated columns, and sequences.
- The exact Oracle-side implementation, if any, of workbook fields described as `Auto Sum`.

The baseline mock therefore adds none of these objects or behaviours. A mock-only unique constraint on (`MONTH`, `CRT_LOC_ID`, `SUPERVISOR_YN`) may help concurrency testing, but it requires an explicit decision and must not be represented as production metadata.

## ETL decisions still required

- The workbook's `Sum (Regular + Overtime Hours)` and `Auto Sum` fields are ordinary columns with their documented data types. No trigger, virtual column, or generated expression calculates them. The legacy Access extract calculates regular-plus-overtime totals before export, while category-specific Oracle `Auto Sum` behaviour remains unconfirmed.
- The known location mapping is `BCSS Location_ID` / `CSBMD_LOC_CD` to `CRT_LOC_ID` / `CORIN_CODE`. Decide whether Unified `LocationId` or `PerformedAtLocationId` determines the reporting location before implementing the transformation. This does not change the destination DDL.
- `SHF_LOCATIONS` is not created. It is not required for the initial two-table destination contract and may be considered later as a reference or crosswalk table.

Oracle initialization scripts execute only on first database initialization. An existing `oracle-data` volume does not receive these files automatically; use an approved migration path instead of removing a volume that must be preserved.