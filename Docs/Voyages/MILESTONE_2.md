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
| The sea's danger by difficulty 1-9 | `VoyageSeaDanger` / `VoyageSea` (`Data/Voyages/VoyageSea.cs`), the table on `VoyageToyDefinitionSO` (`seaDanger`) |
| The sea laid on a run | `VoyageRun.Sea.cs` `LaySea` (when the job is taken), raiders in `VoyageRun.Assault.cs`, kaiju in `VoyageRun.Kaiju.cs` |
| The cell clock | `VoyageSea.ArkSpeedFor`, `VoyageRun.Sea.cs` `PacedArkSpeed`, `cellSeconds` / `namedAnchorSeconds` / `arkCrawlCeiling` |
| Salvage's loose crystals | `VoyageRun.Sea.cs` `SeedDrift`, `VoyageDriftCrystal.cs`, `driftCrystalsPerCell` / `driftClusterShare` / `driftSpeed` |
| The pause map's feed | `VoyageRun.TryGetCourseFix` -> `VoyageCourseFix` (no screen yet) |
| Tests | `Tests/Editor/VoyageSeaTests.cs` (also run by `Tools/Build/voyage_depth_harness/run.sh`) |

## 2 Named voyages

Crystal Run (`job.raid.crystals`, Milestone 1) stays on the board exactly as it was (Shombith, 2026-11-24: keep it in the
lot, change nothing about it). The Salvage voyages are their own type, `job.salvage.drift`, beside it; unnamed work never
reads the sea's danger table, the cell clock or the drift.

A named voyage is a type (a job template id, or `escort`), a name, where it sails to (and from, for an escort), its difficulty
and its cells, and the Codex's line. A port offers ONE named voyage of each type whose template is offered at every port
(today `job.salvage.drift`), picked by the day, so the board stays near six options. The job keeps the type's rules; the voyage gives
it the title, the summary, the difficulty (and the board's pricing from it), the cells and the goal (`GoalPerCell` x cells). Venture further sails the
next named voyage of the type; try again sails the same one when it starts here.

Bounty and Hold the Core are named in the content but stay off the board until those types are built. The escort voyages
are named but the escort card does not read them yet.

### Givers

Each voyage type has a giver on the quay (`voyageTypes` in `voyage_slice.json`, `VoyageTypeInfo` in `VoyageContent.cs`,
`content.VoyageType(type)`): the crew pilot who offers it (Cass Morrow, GALLEON, for Salvage; Captain Theo Lark, WINGMAN,
for the escort; Juno Rask, BANDIT, for Bounty; Marshal Edda Kroll, RAMPART, for Hold the Core), what they say, the type's
lore, and the faction types keen, reluctant or refusing to sail it. The three classes a type wants are the shell's
(`VoyageClassesSO`, `Tools/Build/author_voyage_classes.py`).

## 3 Difficulty 1-9

A voyage has one difficulty, 1 to 9 (Shombith, 2026-11-24: "There is no intensity difficulty on voyages, it is just
difficulty from 1-9"). A named voyage is offered at its own (the Codex's), the crew may pick another on the voyage page,
and the sea's danger follows the pick (`VoyageSea.DifficultyOf`, `VoyageSeaDanger.Defaults`):

| Difficulty | Held cells | Life | Breach line | Raiders out / home | Kaiju |
|---|---|---|---|---|---|
| 1 | none | 0.5 | none | no / no | none |
| 2 | none | 0.6 | none | no / no | none |
| 3 | 1 in 3 | 0.9 | 10 % | no / yes | none |
| 4 | 1 in 3 | 1.0 | 15 % | no / yes | none |
| 5 | 1 in 2 | 1.15 | 25 % | yes / yes | first crossing |
| 6 | 2 in 3 | 1.3 | 30 % | yes / yes | first crossing |
| 7 | 5 in 6 | 1.45 | 38 % | yes / yes | first crossing |
| 8 | every cell | 1.6 | 45 % | yes / yes | every crossing |
| 9 | every cell | 1.8 | 55 % | yes / yes | every crossing |

Which cells are held comes from the real map: the course's HyperSea cells' danger, cut into the run's cells, the most
dangerous held first (a tie goes to the later cell). The way home after a named voyage is as rough as it was. The HUD
names the sea by the difficulty ("Cell 2 of 4 - difficulty 6").

## 4 The cell clock and Salvage's drift

A cell takes `cellSeconds` (50): the Ark's crawl speed is set so it crosses core to core in that less the anchor at the
core (`namedAnchorSeconds`, 15), under the Arkway's slow-near-the-core profile, never above `arkCrawlCeiling`. A Salvage
cell seeds `driftCrystalsPerCell` elemental crystals, part bunched where a hold burst, each drifting slowly and turning
back at the cell's rim; seeded by the day, the voyage and the cell, so a party sees the same field.

## 5 Next

Escort (the escort voyages onto its tiers), Hold the Core, Bounty (rival crews in the world), then the screens Shombith
picks on Frogma (the pause map reads `TryGetCourseFix`).
