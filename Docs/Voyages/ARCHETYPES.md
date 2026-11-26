# Voyages around roles and archetypes

Shombith, 2026-11-26: voyages ask for vessel roles and archetypes, and the starters' archetypes matter. This replaced
the seven Milestone 2 voyage classes (Guardian, Protector, Destroyer, Cultivator, Hunter, Shepherd, Pathfinder).
Design doc: https://claude.ai/artifact/CBSraD4VoGxfvc7in3E7MR. Board (the source of truth):
https://claude.ai/artifact/Duwb9xJ7XdAykxMKAnMyFe, copied to `Docs/Voyages/archetypes.json`.

An archetype never changes a vessel's kit (CLAUDE.md, a game mode never changes the vessel). It decides what a voyage
asks for, who counters whom, and what the squad is paid. Voyages keep difficulty 1-9. The Crystal Run
(`job.raid.crystals`) asks for nothing and is unchanged.

## 1 Names and data

| Role | Archetypes |
|---|---|
| Cultivator (increases allied prism volume) | Pumper, Builder, Burster |
| Destroyer (destroys enemy prisms) | Sapper, Specialist, Nuke |
| Guardian (protects allied prisms) | Harraser, Zoner, Enhancer |

- Enums: `VesselRole`, `VesselArchetype` (`Data/Voyages/VesselArchetype.cs`), fit rules in `VesselArchetypes`.
- Table: `Resources/Voyages/VoyageArchetypes` (`VoyageArchetypesSO`), written by
  `python3 Tools/Build/author_voyage_archetypes.py` (`--check` drift gate, `--self-test`, `--table` prints every ask
  and the starters' best fit). To take a new board state: save the board's JSON over `archetypes.json` and re-run.
- The board's ids are its first draft's (`scout` is Builder, `hunters` Harraser, `builders` Zoner, `enhancers`
  Enhancer). A board vessel not in `VesselClassType` (the Wombat) is skipped.
- Tertiary: where the board gives two secondaries (Sparrow, Dolphin, Jewel Wasp), the second listed is the tertiary.

## 2 The ask and fit

A voyage asks for three seats, each one archetype. A vessel meets a seat 1 on its primary and 0.5 on its secondary or
tertiary. Each vessel fills at most one seat, assigned for the best total: squad fit is 0 to 3 in halves
(`VesselArchetypes.Fit`, `VoyageArchetypesSO.Fit(job, voyageId, squad)`).

## 3 What each type asks for

| Type | Seats | Why |
|---|---|---|
| Salvage `job.salvage.drift` | Nuke, Sapper, Harraser | Hold bursts spill crystal inside wreck mass; rival skimmers |
| Escort the Ark `escort` | Enhancer, Harraser, Nuke | Fauna and raiders assault the Ark's hull |
| Bounty `job.bounty.crew` | Harraser, Specialist, Sapper | Silenced escorts cannot wall their Ark; her walled hull |
| Hold the Core `job.defence.aid` | Pumper, Burster, Sapper | The hold runs on control volume in the nucleus |

Every starter (Squirrel, Sparrow, Dolphin, Thresher, Tiger Moth, Swordfish) is a full seat on at least one type (the
generator fails otherwise). With starters only the best fit is 2.5 on Salvage and 2 elsewhere. No starter is a
Guardian primary, and no vessel is a Zoner, so Zoner is in no ask.

## 4 Counters

A named voyage that meets a crew swaps its third seat for a counter of the crew's lead hull (its first hull's primary):
the counter most vessels hold as primary, a tie to the archetype order, one already asked for passing to the next.
Bounty crews are the rivals' hulls (`voyage_slice.json` `rivals[].hulls`). Hold the Core voyages now name their retake
crew (`HOLD_CREWS` in the generator, `VoyageAsk.CrewHulls`), and `VoyageRun.Core.SendRetake` sends those hulls in that
order instead of the toy's one list. The giver's line is `CounterReason(voyageId)` ("The Quiet Spear fly Sappers.
Bring a Builder.").

## 5 What fit does

When a Salvage, Bounty or Hold voyage sets sail, its fit is recorded on the job (`VoyageJob.SquadFit`) and its pay
rises `fitPayPerPoint` (0.1) per point, once (`VoyageRun.PayFit`). The escort pays nothing today, so its fit is shown
only. Set `fitPayPerPoint` to 0 on the asset to turn fit pay off.

## 6 Not built yet

- The end board labels each seat with the archetype it filled and credits its role's measure (Cultivator
  `IRoundStats.VolumeCreated`, Destroyer `HostileVolumeDestroyed`, Guardian `VolumeRestored` plus silences).
- AI duties by archetype: Thresher, Tiger Moth and Swordfish have no escort AI of their own (`ArkEscortRoles.HasRole`),
  so three starters still need a person in a wingmate seat.
- The screens (ask under each role, the counter line, half segments) are the voyage UI thread's, on Frogma first; the
  pages read this table through the API above until they land.
