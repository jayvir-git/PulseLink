# List access and indexes

Incident list is the hot path: role filter, then `ORDER BY UpdatedAtUtc DESC, Id DESC`, then a page of rows with Agency and destination Hospital included. `GET /api/incidents` also runs `CountAsync` on the same filter on every request.

Indexes below match that filter-then-order shape. They were measured on SQL Server 2022 Express LocalDB (`16.0.1000.6`), not Docker. Logical reads come from showplan `ActualLogicalReads` on each operator. STATISTICS IO line parsing is not used here; it under-counted this workload.

## Load-test seed (opt-in)

Demo seed is unchanged. A large, skewed set is opt-in only:

```bash
sqllocaldb start MSSQLLocalDB
cd backend/PulseLink.Api
dotnet run --launch-profile http-localdb -- --seed-load-test --capture-load-test-plans
```

`--capture-load-test-plans` writes `artifacts/load-test-plans/` (gitignored) and exits. Do not enable this in CI.

Bulk insert bypasses `SaveChanges`, so it never runs `SyncIncidentUpdatedAtUtc()`. The seeder sets `UpdatedAtUtc` on every row and spreads timestamps across ~180 days. After insert it asserts zero default timestamps and a minimum distinct-day count. `LoadTestSeederTests` repeats that on an 800-row SQLite seed so a regression cannot land as “ordering still works, every timestamp is default.”

Measured on this machine after the full seed:

| | |
| --- | --- |
| Incidents | 70,350 (`LT-` prefix) |
| Vitals / interventions / audits | 21,015 / 7,163 / 70,350 |
| `UpdatedAtUtc` min | 2026-02-15 00:13:53Z |
| `UpdatedAtUtc` max | 2026-08-15 00:33:16Z |
| Distinct UTC days | 182 |
| Default `UpdatedAtUtc` rows | 0 |
| Hospital list matches (metro + Transporting/Arrived/HandedOff) | 15,051 |

Skew: one metro agency ~54% of rows, one metro hospital the plurality of destinations, a long tail of small agencies/hospitals, plus a few cross-agency `CreatedByUserId` rows so the paramedic `OR` is not identical to `AgencyId` alone.

## Access patterns

| Pattern | Query | Filter |
| --- | --- | --- |
| **a** | Hospital staff list | `DestinationHospitalId = @hospital AND Status IN (3, 4, 5)` then order/page, plus `Include` Agency and Hospital |
| **a count** | Same filter, `COUNT(*)` | Runs on every list request |
| **a deep** | Same list at page 150, `pageSize` 100 (`OFFSET 14900`) | Same filter |
| **b** | Paramedic list | `AgencyId = @agency` **UNION** `CreatedByUserId = @user` (distinct), then order/page |
| **c** | Detail by `Id` | PK plus child collections |
| **d** | Vitals / audits by `IncidentId` | Existing FK indexes |

## Indexes added

Migrations: SQLite `20260814231436_AddListAccessIndexes`, SQL Server `20260814231449_AddListAccessIndexes`.

| Index | Keys | Notes |
| --- | --- | --- |
| `IX_Incidents_HospitalList` | `(DestinationHospitalId, UpdatedAtUtc DESC, Id DESC)` filtered `Status IN (3, 4, 5)` | Covers hospital list filter **and** order |
| `IX_Incidents_Agency_UpdatedAtUtc` | `(AgencyId, UpdatedAtUtc DESC, Id DESC)` | Replaces `IX_Incidents_AgencyId` (AgencyId is still the leading key) |
| `IX_Incidents_CreatedBy_UpdatedAtUtc` | `(CreatedByUserId, UpdatedAtUtc DESC, Id DESC)` | Second arm of the paramedic predicate |

`IX_Incidents_DestinationHospitalId` stays. The filtered hospital index does not include drafts (or other non-transport statuses) that still have a destination.

Rollback is `Down()` on those migrations: drop the three new indexes, restore `IX_Incidents_AgencyId`.

Write cost: every incident insert/update that touches `AgencyId`, `CreatedByUserId`, `DestinationHospitalId`, `Status`, `UpdatedAtUtc`, or `Id` maintains extra nonclustered keys. Child tables (vitals, interventions, audits) were already indexed by `IncidentId`; no new indexes there.

## Before / after (SQL Server LocalDB)

Times are wall-clock for one captured execution after `UPDATE STATISTICS … WITH FULLSCAN`. Include joins are part of the page queries.

### a — hospital list

| Query | Before | After | Sort gone? |
| --- | --- | --- | --- |
| Page 1 (`pageSize` 50) + Includes | Clustered scan **15,051** rows / **3,006** reads, **Sort** (Top-N, actual 50), 88 MB grant, **94 ms** | **Index seek** `IX_Incidents_HospitalList` **50** rows / **3** reads, **50** clustered lookups (126 reads), Agency/Hospital PK seeks 50 rows each, **0** grant, **34 ms** | **Yes** |
| `CountAsync` | Clustered scan 15,051 / 3,006 reads, **38 ms** | Index seek 15,051 keys / **89** reads, **11 ms** | n/a (no order) |
| Deep page 150 (`OFFSET 14900`, `pageSize` 100) | Clustered scan 15,051 / 3,006, **Sort actual 15,000**, 88 MB grant, **89 ms** | **Index seek** walks **15,000** keys (89 reads), then **15,000** clustered lookups (**43,108** reads), **0** grant, **88 ms** | **Yes** |

Page 1 Includes are PK seeks on Agency and Hospital (50 rows). They are not the expensive part; the clustered scan + sort was.

**Count vs page (large hospital tenant):** count is cheaper both before (38 vs 94 ms) and after (11 vs 34 ms). Same access path I/O before indexes (both scanned 3,006 pages). After indexes, count still costs less than the page query. Options if list latency later needs another cut (not implemented): skip `totalCount` after page 1, cache it per tenant, or return `hasMore` instead of an exact total. Do not change the envelope without a measured need — count is not the larger of the two queries here.

**Deep OFFSET:** removing Sort is not enough. Page 1 does 50 key lookups; page 150 does 15,000. I/O is *higher* than the pre-index scan. `Id` is already the stable tie-breaker, so keyset (`WHERE (UpdatedAtUtc, Id) < (@lastUtc, @lastId) ORDER BY … FETCH`) is available. Hospital UI is page 1. Keep `OFFSET` until a product path pages this deep.

### b — paramedic list

`List()` now uses `IncidentRoleQueries.ForParamedic`: `Union` of the two equality predicates (SQL `UNION`, not `UNION ALL`). Own-agency creates match both arms; distinct keeps them once.

| Query | After indexes, still OR | After UNION rewrite | Sort gone? |
| --- | --- | --- | --- |
| Page 1 + Includes | Clustered scan **39,204** / **3,006** reads, **Sort**, 154 MB grant, **133 ms** | **Index seek** `IX_Incidents_Agency_UpdatedAtUtc` 50 keys / 3 reads + **index seek** `IX_Incidents_CreatedBy_UpdatedAtUtc` 7 keys / 3 reads, merge, **no Sort**, **32 ms**, 0 grant | **Yes** |
| `CountAsync` | Index union: Agency seek 38,854 (225 reads) + CreatedBy seek 5,207 (54 reads), Hash Match distinct, **58 ms** | Two clustered scans (38,854 + 5,207, 3,006+3,006 reads), merge distinct, **79 ms** | n/a |

Both list-access indexes are used on the page query. The `PK_Hospitals` scan in that plan is the 10-row hospital table (nested-loop prefetch), not Incidents. Indexes stay.

`CountAsync` on the UNION is slower than the OR count (79 vs 58 ms) because EF unions the full incident row before counting. Combined list request is still faster: **111 ms** (79+32) vs **191 ms** (58+133). Envelope unchanged.

Before either index existed, OR page was 135 ms and OR count 43 ms.

### c / d — detail and children

PK seek on Incidents plus `IX_VitalSigns_IncidentId` / `IX_Interventions_IncidentId` / `IX_AuditEvents_IncidentId`. No new indexes. Plans unchanged (~5–36 ms).

## What we did not change

- Hospital list filter, `CanAccess` (single-row), and `IncidentStatusMachine` are unchanged.
- Pagination envelope `{ items, page, pageSize, totalCount }` is unchanged.
- No keyset pagination.

## How to re-measure

1. Apply migrations to LocalDB (`http-localdb`).
2. `--seed-load-test` (idempotent once 70k `LT-` rows exist; incomplete seeds are deleted and rebuilt).
3. `--capture-load-test-plans` (updates statistics, runs the queries, writes XML plans).
4. Confirm the report header: `defaults=0` and `distinctDays` well above 1 before trusting any plan.
