# QA session results

## Session

| Field | Value |
|---|---|
| Tester | akouroshm |
| Date | 2026-10-15 |
| Branch | bleeding-edge |
| Commit | `fb4cd7f97` |
| Unity version | 6000.3.17f1 |
| Platform(s) | Editor |
| Submitted | yes — 2026-10-15 17:54 · 1 verdict(s), 1 new this run |
## Results

<!-- qa-results-table -->

| ID | Result | Notes |
|---|---|---|
| QA-ARCADE-ROSTER-SMOKE | FAIL | Two findings. (1) **Scurry forces the Squirrel** with no vessel picker, but its card `ArcadeGameScurry.asset` authors **3 vessels** and this item expects it pickable (Sparrow/Manta/Squirrel) — a real regression (DT-005), most likely the collapsed single-panel arcade launch dropping the picker for a multi-vessel card. (2) The **roster has grown past the item's expected 24**: five new cards are now registered (GrizzlyCharge, GrizzlyTime on **Grizzly**; Slingshot, Warpline on the new **Stoat** vessel; ThresherTime on the new **Thresher** vessel — all confirmed in `ArcadeGames.asset`, which now holds 30 entries incl. Maelstrom; VesselClassType adds Thresher=14, Stoat=15). The item's 24-card count and ship table are stale (updated here); those 5 new in-development modes + 2 new vessels are themselves untested and need their own QA items. The listed existing cards were not reported as failing; the 5 new cards' per-card launch was not verified this run. |

<!-- /qa-results-table -->

## Evidence

(none attached)

## Anything else

- **The real product bug is Scurry's vessel pick (DT-005).** Its card lists three vessels; at
  runtime only the Squirrel is flyable. Suspected cause: the arcade launch was collapsed into one
  panel on the premise that "every arcade mode locks to one hull" (`Docs/ArcadeLaunch/`), but Scurry
  is a multi-vessel card, so the picker was lost and it falls back to a default hull. Any other
  multi-vessel arcade card would have the same symptom — worth checking the whole roster for cards
  with more than one entry in `Vessels`.
- **The item QA-ARCADE-ROSTER-SMOKE is stale and was updated in this pass:** card count 24 → ~29,
  and the ship-per-mode table now includes GrizzlyCharge/GrizzlyTime (Grizzly), Slingshot/Warpline
  (Stoat) and ThresherTime (Thresher), flagged as in-development.
- **New untested work for a future `/qa-backlog scan`:** five new arcade modes (GameModes 62–66:
  GrizzlyCharge, GrizzlyTime, Slingshot, Warpline, ThresherTime) and two new vessels (Stoat=15,
  Thresher=14) have landed and have no dedicated QA items yet. They need the usual new-mode / new-
  vessel smoke items (scene opens, forces the right hull, reaches fly, scores, ends).
