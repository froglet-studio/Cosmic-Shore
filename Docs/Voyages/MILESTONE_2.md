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

All four types are built and on the board (Hold the Core and Bounty through `voyageTypes[].onBoard`).

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

## 5 Escort the Ark

The escort voyages (Spindrift, Lanterns Home, Tollmoor, ...) are offered on the board beside the plain escort, from the port
they set out from (`VoyageJobsView.NamedEscortsFrom`). Each card shows the voyage's own name and line and recommends its own
difficulty; the crew may pick another 1-9, and the escort's tier is that pick (`ArkEscortSO.TierFor`). It sails the voyage's
own cells (`ArkEscortVariety.Roll(..., worlds)`), and the party course carries the voyage id so friends sail the same one
(`VoyageCourse.VoyageId`). The HUD kicker and the end board carry the voyage's name. Venture further sails the next named
escort from where the Ark berthed, at that voyage's own difficulty; try again sails the same one.

## 6 Hold the Core

The three Hold the Core voyages (The Verdigris Vigil 1, Ironmouth Holds 4, The Ninefold Stand 8; type `job.defence.aid`)
are on the board: the type is built (`onBoard` on its `voyageTypes` entry, `VoyageTypeInfo.OnBoard`), so every port offers
one of them, picked by the day, beside Salvage and Crystal Run. The news-brought defence jobs are unchanged.

The Ark sails the voyage's cells to the core, which is contested at any difficulty (`VoyageSea.ContestLast`), and anchors
under the nucleus. The hold clock (`VoyageCoreHold`, `VoyageRun.Core.cs`) runs only while the squad's domain holds more of
the nucleus than any other (`Cell.GetControlVolume`, the volume that decides the cell); an empty nucleus is nobody's. Once
the core has been yours, it turning theirs starts a 15 s countdown, and the hold is lost if it stays theirs. Wave 1 comes
at the anchor and wave k when the clock passes (k - 1) / waves of the hold; in each wave's window (`coreWaveSeconds`, 25)
the core's fauna may assault the anchored Ark through the assault clock. From difficulty 3 the other domain's crew comes
with wave 2 to retake the core: AI hulls of the domain that leads the rest of the nucleus (`coreRetakeHulls`), which build
and fight by the rules every AI plays. The Ark falling, or its breach line, ends it as anywhere.

| Difficulty | Hold | Waves | Retake crew |
|---|---|---|---|
| 1-2 | 45 s | 1 | none |
| 3-4 | 60 s | 2 | 1 hull |
| 5-6 | 75 s | 2 | 2 hulls |
| 7-9 | 90 s | 3 | 3 hulls |

The objective box names the voyage, gives the goal ("Keep the core of Ironmouth yours for 1:00") and, at the core, the
clock and one line: build (not yours), "Yours 61% - wave 2 of 2", or the turning countdown. The end board (`Kind.CoreHold`)
shows the hold and the wave reached; Venture further sails the next Hold the Core voyage from here, Finish sails home.

Not built yet: the kaiju in the last wave at 7-9, the distress call next door, and the Codex's "Wall of the Voyage" award.

## 7 Bounty

Built 2026-11-24 (`Data/Voyages/VoyageBounty.cs`, `Controller/Voyages/VoyageRun.Bounty.cs`), on Frogma Rev 137's rules
(Shombith: "Lets make rival ark thats cooler and makes sense there can be runaway crews as well ... how do i defeat
theses players?"). A Bounty voyage (`job.bounty.crew`, given by Juno Rask, BANDIT) names the crew it hunts
(`voyages[].rival`). The squad sails its cells to where the crew is; there the crew is met, and the warrant is served by
beating it before it makes port. Every crew carries an Ark or runaway flag (`rivals[].ark`: her name, empty for a
runaway crew):

- **A rival Ark** (Black Tide's *Undertow*, the Rusted Grin's *Old Jaw*; they sail The Crossing and Aurum Banks): a
  smaller Ark in the crew's domain sails across the cell for its port, her escorts the crew's hulls (AI vessels that
  fight by the rules every AI plays). Her escorts wall her hull (super-shielded); **silenced escorts cannot wall**, so
  with enough of them silenced at once her hull is open. Break her to her break line: her hold bursts as loose crystals
  and the crew turns back.
- **A runaway crew** (the Quiet Spear; runaways roam Carmine Drift and The Deep): three hulls, no Ark. **Silence all
  three at the same moment** and they rout, dropping their haul as loose crystals.
- If the clock runs out first, the crew makes port and the warrant goes unserved. The Ark falling ends it as anywhere.

| Difficulty | Clock | Hold (spills) | Breaks at | Escorts silenced to open her |
|---|---|---|---|---|
| 1-2 | 110-105 s | 12-14 | 50 % | 2 |
| 3-4 | 100-95 s | 18-20 | 50 % | 2 |
| 5-6 | 90 s | 24-26 | 45 % | 2 |
| 7-9 | 85-75 s | 30-36 | 40-35 % | 3 |

The rival plate, the feed's ArkMeet / ArkOpen / ArkBreak / Runaway / Rout toasts and the pause chart read the crew
through `IVoyageScreenSource.GetRivals`. The objective box names the voyage and the crew ("Serve the warrant on Black
Tide at Gullbone Landing"), then the fight: a runaway crew's count, a rival Ark's escorts and clock, or "Undertow is open
for 0:04. Break her hull!". The end board (`Kind.Bounty`) says the warrant was served or the crew got away; its Rival
Breaker share is each seat's part of the rival hull broken (a rival Ark's plates by who broke them; a runaway's silences
by the seat nearest each hull, since a silence carries no attacker). The warrant served catches the crew
(`VoyageRivals.Catch`: grudge +1, off the sea a while). Venture further sails the next Bounty from here.

Not built yet: the crew's wake on the pause map before it is met, the crew splitting or calling a second crew at 7-9,
and the fight on a party friend's machine (the host's run decides it; a friend sees the rival Ark and hulls).

## 8 Next

Hold the Core's kaiju and distress call, and the screens' remaining feeds (events next door, the detour vote).
