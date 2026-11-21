# Regatta lifeforms as a course setting (2026-11-06, `Plan step: time-arena-course`)

Brief 11 of the Regatta balancing work (Garrett, 2026-11-06: perturb Regatta "in much more interesting ways ...
lifeforms, etc."). Lifeforms are now a setting a course carries and a search can move, **off on every shipped
course**: the four Regatta cell configs, their Barren spawn profile and the race are byte-for-byte what they were.

## What a course can now say

| Setting | Where it lives | Default (today) |
|---|---|---|
| Planting sites per leg (8 legs) | `SpawnableRegattaRails.lifeforms.sitesPerLeg` | none |
| Distance from the spine (min, max) | `offsetMin`, `offsetMax` | 90-160 u (inert without sites) |
| Side of the spine (centre, half-width) | `sideDegrees`, `sideSpreadDegrees` (0 = outward, 180 = toward the nucleus) | 0, 180 |
| Where along a leg | `legWindowStart`, `legWindowEnd` | 0.2-0.8 |
| Clearance from every ring | `ringClearance` (beyond the mouth radius) | 60 u |
| Plant species and counts | the course's own spawn profile + species configs | Barren (none) |
| Creature species and counts | same | none |
| Fauna kept off the racing line | `faunaCorridorRadius` (`FaunaCorridor` round the spine) | 0 (no corridor) |

Catalogue: plants are Tollway's networked race-court species, Spire (40 prisms, 488 volume a plant), Cacti (40, 3,000)
and Borromean (342, 2,512), read from the shipped assets the way `author_tollway_assets.py` reads them (the volumes in
`TOLLWAY.md`'s table predate later element-asset edits; these are today's). Copied per course with only the counts
changed and `GrowthPerOffspring 0`, so a plant stays on its site and neither Tollway nor Thresher Time is touched.
No lattice species: a lattice colony births its next plant at its frontier, off the sites, and could grow across
the line. Creatures: the Skim Race tadpole (a grazer), copied the same way.

`python3 Tools/Build/regatta_lifeforms.py` prints the catalogue and four presets (none ships):

| Preset | Plants | Sites | Forest | Offset | Creatures |
|---|---|---|---|---|---|
| groves | 16 Spire | 24 | 640 prisms | 110-180 u, outside | none |
| thicket | 12 Cacti + 12 Spire | 32 | 960 prisms | 60-120 u, all round | none |
| reef | 6 Borromean + 10 Spire | 24 | 2,452 prisms | 120-260 u, inside | 8 tadpoles, free to cross the line |
| reef-fenced | the same | 24 | 2,452 prisms | the same | 8 tadpoles behind a 110 u corridor |

To fly one: set an entry of `LIFEFORMS` in `author_regatta_assets.py` to `RL.PRESETS[...]` (or `RL.decode(x)` from a
search) and run the generator. It forks that course's species configs and spawn profile and raises the course's
phase ladder by the forest's measured prisms and volume (I3 with `reef-fenced`: +2,452 count, +19,956 volume on every
threshold). Verified as a dry run with all four presets, one per intensity: validation passes, the cell config
swaps the Barren profile for its own, the prefab carries the sites; then reverted (nothing of it is committed).

## For the search (briefs 10 and 12)

`regatta_lifeforms.SPACE` is 12 dimensions in the unit cube (site density, odd/even leg bias, offset, depth, side,
spread, fill, three species shares, creatures, corridor); `decode(x)` turns any point into a setting that passes
`validate()` (the self-test decodes 511 points, the corners included). `descriptors(s)` gives what a MAP-Elites
archive can bin by: plants per leg, forest prisms, mean offset, all-round share, species, creatures, whether
creatures may cross the line.

**These are descriptors, never scores.** `race_models.py` has no lifeform term and this work adds none on purpose:
what a plant costs a hull is real-game only. The search can carry lifeforms as diversity (one pool entry a grove,
one a thicket, one bare) and Garrett judges them by flying.

## What each dimension can reach, by hull (expectations to test in the real game, not measurements)

| Dimension | Who it touches, and how |
|---|---|
| Plants close to the line (thicket) | Every hull brushes them; a Rhino's sword and every ability that cuts prisms can clear them; a Squirrel may skim their prisms for boost energy; the Urchin on its rail is untouched (sites never sit on a rail) |
| Plants far out (groves, reef) | Reading aids only, until someone leaves the line for them |
| Hearts (each plant drops an elemental crystal when it withers or is jousted) | A human can take the element levels; the racing AI never detours (`AIDetoursForCrystals` stays false, Yash's rule). This can widen a human-vs-AI gap and is the first thing to watch |
| Creatures free to cross the line | Moving obstacles on the line; tadpoles graze the plants, so a forest thins and re-seeds |
| Creatures behind a corridor | Scenery off the line; the forest still thins |

## What is not verified, and the real-game checks

- **No Unity editor ran.** The C# was compiled with `Tools/Build/unity_refcompile` and EditMode tests were written
  (`RegattaLifeformSitesTests`) but not run; `bash Tools/Build/verify_unity.sh` on a full checkout would run them.
- **Network**: plant PLACEMENT is replicated (every species is `NetworkSynced`, and the sites are a pure function of
  the course seed), but a plant's grown shape is local per peer by design (`FloraNetworkSync`'s fidelity contract),
  so two machines can see a slightly different branch where a pilot brushes it. Creatures are opted in to
  `FaunaNetworkSync` on the copied tadpole config: that is a rollout step beyond the shipped Shark
  (`Docs/ECOSYSTEM_NETWORK_SYNC.md` §5) for Shombith to check before any creature preset is flown online.
- **Site reuse**: the cell deals sites round-robin and the ring wraps, so after many re-seeds a new plant can take a
  site a living one still holds (the platform's garden behaviour, Hesperides too). Validation keeps plants under 75%
  of the sites asked for, which makes it rarer, not impossible.
- Real-game checks, in order: (1) `groves` at I1, alone: plants appear beside the straights, nothing in a mouth, the
  AI's lap time unchanged; (2) `thicket` in a six-AI grid: missed mouths, stalls, which hulls lose time; (3) `reef`
  vs `reef-fenced`: whether tadpoles on the line matter; (4) the same in a two-player party (placement agrees,
  shapes may differ).

## What changed in code

- `RegattaLifeformSites.cs` (new, pure): the settings and the sowing rule (stratified along each leg, hashed, no
  Unity or `System.Random` randomness; skips a site inside any ring's mouth plus clearance, nearer any leg's spine
  than the lane offset + 30 u, or outside the 450-1150 u shell; each site's up points away from the line).
- `SpawnableRegattaRails`: sows the sites through the base's `Sow`, declares `PreparesPlantingSites`, implements
  `ICellFaunaCorridorSource`. Rails, rings and course generator (`RegattaCourse.cs`) untouched.
- `Cell`: adopts an environment's corridor (`AdoptFaunaCorridor`) and releases only one it adopted.
- `IntensityWiseLifeSpawner`: plants through `PlantFlora` (it ignored planting sites before, so no IntensityWise cell
  could use them; `Docs/ECOSYSTEM.md` §68) and waits for the environment only when the world declares sites.
- `regatta_lifeforms.py` (new) and `author_regatta_assets.py`'s `LIFEFORMS` table; the four rails prefabs gained the
  default `lifeforms` block.
