# Vessel roles and archetypes (the fleet's one table)

Shombith, 2026-11-26. Vessel **roles** (Cultivator, Destroyer, Guardian) and their nine **archetypes** describe what a
vessel is for. The source of truth is Shombith's board (https://claude.ai/artifact/Duwb9xJ7XdAykxMKAnMyFe), copied to
[`archetypes.json`](archetypes.json).

| Role | Goal | Archetypes |
|---|---|---|
| Cultivator | Increases allied prism volume | Pumper, Builder, Burster |
| Destroyer | Destroy enemy prisms | Sapper, Specialist, Nuke |
| Guardian | Protects allied prisms | Harraser, Zoner, Enhancer |

Each vessel has a primary archetype, a secondary and sometimes a tertiary. Where the board lists two secondaries
(Sparrow, Dolphin, Jewel Wasp), the second is the tertiary (Shombith, 2026-11-26). A vessel's role is its primary's role.
The board's arrows are counters. An archetype never changes a vessel's kit.

## The one copy

- `Assets/_Scripts/Data/VesselArchetype.cs`: `VesselRole`, `VesselArchetype` (fixed values) and the `VesselArchetypes`
  rules (role of an archetype, seat meets, squad fit).
- `VesselArchetypesSO` at `Resources/Vessels/VesselArchetypes`: `Load()`, `ArchetypesOf(vessel)` (primary first),
  `PrimaryOf`, `RoleOf`, `VesselsOf(archetype)` (with rank), `Counters(a)`, `CounteredBy(a)`, and the authored names and
  lines (`RoleName`, `RoleLine`, `ArchetypeName`, `ArchetypeLine`).
- Written by `python3 Tools/Build/author_vessel_archetypes.py` (`--check` drift gate, `--self-test`, `--table`). The
  vessel and counter tables are the script's; the names and lines are seeded from the board once and kept after, so edit
  them in the Inspector. The Wombat is on the board but not in `VesselClassType` yet, so it is skipped.

Readers: the Hangar (the kicker over the vessel's name, `HangarArchetypeWords`), the ecology page, and voyages
(PR #2406 on `voyages` carries its own copy of these types and should read this table once it meets bleeding-edge).

To change an assignment: change the board, copy its JSON here, run the script.
