# QA session results

## Session

| Field | Value |
|---|---|
| Tester | akouroshm |
| Date | 2026-10-09 |
| Branch | bleeding-edge |
| Commit | `88478a6d` |
| Unity version | 6000.3.17f1 |
| Platform(s) | Editor |
| Submitted | yes — 2026-10-09 17:11 · 2 verdict(s), 2 new this run |
## Results

<!-- qa-results-table -->

| ID | Result | Notes |
|---|---|---|
| QA-AUDIT-TOOLS | FAIL | Auditors ran without throwing, but several report beyond the known exceptions. Audit Vessel Skimmers: **6 faults** (known exception is Serpent only). Validate Lifeform Crystals: **34 warnings**. Measure Cell Environment Baselines: **1 warning**. Game Mode Prefab Kit Validate: **75 warnings**. Pending Tool Changes: **~11 uncommitted files**. Ability Rows / Elemental Morphs / Speed Tunnel / Corridor Radii / Cell-Owned Visuals / Occlusion Corridor: ran clean. NOTE: the item's step 10 is STALE — End Game Conditions correctly shows Wildlife Liberation **30** (the mode was re-targeted 250 to 30) and Dog Fight 120; 30 is right, so that line is a doc bug in this item, not a tool fault (fixed in the item body). |
| QA-VESSEL-RHINO-SWORD | SKIP | Outdated test — a lot of what it asks no longer matches the current build (the energy sword was reworked to SLICE prisms, so "breaks differently from a hull hit" is now expected rather than a defect), and there is no findable way to trigger an Astro League field reset (step 5). Not run to a pass/fail; item revised to be runnable and the field-reset step flagged. Would give more viable results against the current sword behaviour (see QA-RHINO-SWORD-COMBOS). |

<!-- /qa-results-table -->

## Evidence

(none attached)

## Anything else

- **QA-VESSEL-RHINO-SWORD is stale.** It was written against PR #639 (sword point-velocity +
  the debris retune, plus an "Astro League field reset" step). Since then the sword was reworked
  (slice on destroy, super-shield bind, trigger-tap combos — covered by QA-RHINO-SWORD-COMBOS), so
  a tester now sees the sword break prisms differently from a hull and reasonably reads it as wrong.
  The Astro League "field reset" step has no findable trigger in the current build — likely a
  removed/renamed mechanic. The item body has been updated to say the slice is expected and to
  flag the field-reset step for design/engineering confirmation rather than failing on it.
- **QA-AUDIT-TOOLS step 10 was corrected** in the item body: Wildlife Liberation's end target is
  **30**, not 250.
- Exact test build for this session was not pinned by the tester; filed against the current
  bleeding-edge tip.
