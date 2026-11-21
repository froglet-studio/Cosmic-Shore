# QA session results

## Session

| Field | Value |
|---|---|
| Tester | akouroshm |
| Date | 2026-10-12 |
| Branch | bleeding-edge |
| Commit | `5ba3e941` |
| Unity version | 6000.3.17f1 |
| Platform(s) | Editor |
| Submitted | yes — 2026-10-12 20:33 · 3 verdict(s), 3 new this run |
## Results

<!-- qa-results-table -->

| ID | Result | Notes |
|---|---|---|
| QA-EDITMODE-TESTS | FAIL | EditMode Run All: **2685 pass, 35 fail** (total ~2720 — far above the item's stale "≈762" expectation, which is now corrected in the item body). All **11** named step-4 suites are GREEN. The 35 failures span 14 suites, several entirely red (FollowHeightScale 6/6, RaceRankToastDriver 7/7, RearViewLaw 5/5, SparrowCombatTier 3/3, PartyInviteSystem 3/3) — a whole-suite-red pattern that usually means a shared regression or stale assertions, not 35 independent bugs. Full list in DT-003; screenshot attached: evidence/2026-10-12-akouroshm/QA-EDITMODE-TESTS-35-failures.webp |
| QA-SPARROW-PROJECTILE-POOL | FAIL | Full-auto shots have launch sound; **the skyburst missiles have NO launch sound**. Both shot types still interact with prisms (live colliders), and no NullReferenceException was reported. So this is specifically a missing missile launch SFX, not a dud projectile or a pool/injection crash. Likely an empty/unwired `EventReference` on the skyburst launch path (per the FMOD exposed-field convention, an empty slot ships silent). |
| QA-FLORA-LEAFSIZE | PASS | Compiles clean; Hesperides flora grow leaves at the authored size. |

<!-- /qa-results-table -->

## Evidence

- `evidence/2026-10-12-akouroshm/QA-EDITMODE-TESTS-35-failures.webp` — the Test Runner tree showing all 35 failing EditMode tests.

## Anything else

- **QA-EDITMODE-TESTS "≈762" is stale.** The suite has grown to ~2720 tests; the item body's total
  expectation is corrected to "well over 2000" so a future tester doesn't flag the count itself.
- **QA-SPARROW-PROJECTILE-POOL** found a real but narrow defect: the missile launch is silent while
  everything else about the projectile (collision, no NRE) works. If the skyburst launch
  `EventReference` is simply unwired, that is a one-field authoring fix on the skyburst bay/gun
  action; see the FMOD convention (every ability gets its own exposed event, shipped empty = silent).
- The 35 EditMode failures are the headline — a red edit-mode suite on bleeding-edge is exactly what
  QA-EDITMODE-TESTS exists to catch. DT-003 lists every failing test by suite for engineering to group
  and triage.
