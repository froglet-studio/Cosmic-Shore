# Voyages Milestone 2: the shared base

Shombith's decisions of 2026-11-24 (the Milestone 2 page, https://claude.ai/artifact/41YTUVhkuX1wPDwJQhub3g, and the
Voyage Codex, https://claude.ai/artifact/S4aH7rcMZhd6xrSen9oZSD): four voyage types (Salvage, Escort the Ark, Hold the
Core, Bounty); Salvage collects loose crystals; a run averages 3 to 5 minutes over 1 to 6 cells; difficulty is how
dangerous the sea is, never a timer. A type is never a name: each voyage has its own (Codex v2).

The in-game UI and the world's look are defined on Frogma first; no new screen is built before Shombith picks one.

## 1 What is built (this pass)

| Piece | Where |
|---|---|
| Named voyages (the Codex's twelve) | `Docs/Voyages/voyage_slice.json` `voyages`, `NamedVoyage` in `VoyageContent.cs`, validated by `author_voyage_assets.py` |
| The rung ladder I1-I4 | `VoyageSeaRung` / `VoyageSea` (`Data/Voyages/VoyageSea.cs`), the table on `VoyageToyDefinitionSO` (`seaRungs`) |
| The sea laid on a run | `VoyageRun.Sea.cs` `LaySea` (when the job is taken), raiders in `VoyageRun.Assault.cs`, kaiju in `VoyageRun.Kaiju.cs` |
| The cell clock | `VoyageSea.ArkSpeedFor`, `VoyageRun.Sea.cs` `PacedArkSpeed`, `cellSeconds` / `namedAnchorSeconds` / `arkCrawlCeiling` |
| Salvage's loose crystals | `VoyageRun.Sea.cs` `SeedDrift`, `VoyageDriftCrystal.cs`, `driftCrystalsPerCell` / `driftClusterShare` / `driftSpeed` |
| The pause map's feed | `VoyageRun.TryGetCourseFix` -> `VoyageCourseFix` (no screen yet) |
| Tests | `Tests/Editor/VoyageSeaTests.cs` (also run by `Tools/Build/voyage_depth_harness/run.sh`) |

## 2 Named voyages

A named voyage is a type (a job template id, or `escort`), a name, where it sails to (and from, for an escort), its rung
and its cells, and the Codex's line. A port offers ONE named voyage of each type whose template is offered at every port
(today Salvage), picked by the day, so the board stays near six options. The job keeps the type's rules; the voyage gives
it the title, the summary, the intensity (rung), the cells and the goal (`GoalPerCell` x cells). Venture further sails the
next named voyage of the type; try again sails the same one when it starts here.

Bounty and Hold the Core are named in the content but stay off the board until those types are built. The escort voyages
are named but the escort card does not read them yet.

## 3 The rungs

| Rung | Held cells | Life | Breach line | Raiders out / home | Kaiju |
|---|---|---|---|---|---|
| I1 calm | none | 0.6 | none | no / no | none |
| I2 choppy | 1 in 3 | 1.0 | 10 % | no / yes | none |
| I3 hostile | 2 in 3 | 1.3 | 30 % | yes / yes | first crossing |
| I4 abyssal | every cell | 1.6 | 45 % | yes / yes | every crossing |

Which cells are held comes from the real map: the course's HyperSea cells' danger, cut into the run's cells, the most
dangerous held first (a tie goes to the later cell). The way home after a named voyage is as rough as it was.

## 4 The cell clock and Salvage's drift

A cell takes `cellSeconds` (50): the Ark's crawl speed is set so it crosses core to core in that less the anchor at the
core (`namedAnchorSeconds`, 15), under the Arkway's slow-near-the-core profile, never above `arkCrawlCeiling`. A Salvage
cell seeds `driftCrystalsPerCell` elemental crystals, part bunched where a hold burst, each drifting slowly and turning
back at the cell's rim; seeded by the day, the voyage and the cell, so a party sees the same field.

## 5 Next

Escort (the escort voyages onto its tiers), Hold the Core, Bounty (rival crews in the world), then the screens Shombith
picks on Frogma (the pause map reads `TryGetCourseFix`).
