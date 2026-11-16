# The Regatta course genome

Regatta flywheel W2 (Garrett, 2026-11-16: "really mutate the heck out of the course and find tons of different
solutions in a vast space"). Plan step: `time-arena-course`.

A course is a JSON genome. The empty genome `{}` is today's course. A genome can change:

- the circuit (`circuit`: base radius, ring radius, swing, spread, jitter, ...; `corner_profile`),
- the rails (`lane_offset`, `twist_turns_per_lap`, `prism_spacing`),
- the rail features (`features`: sections, alternate rings, line crystals) and the line tube (`line_clearance`),
- the lifeform planting sites (`flora`),
- **placements**: up to 24 of any of the 39 placeable segment spawners and cell worlds, each with its own leg,
  cylindrical offset about the spine (`radius`, `bearing`, `along`), size, rotation, team colour, prism kind
  (Plain, Danger, Shielded, SuperShielded), seed, density and every one of the spawner's own parameters.

Every prism a placement lays is carved, one by one, out of the race line's tube, the ring mouths, the rails, the
nucleus and the membrane, and away from the placements before it; at most 12,000 placement prisms per course.

## The pieces

| Piece | What it is |
|---|---|
| `Assets/Resources/Regatta/RegattaGeneCatalogue.json` | every spawner, its genes and their ranges; written by `Tools/Build/regatta_gene_catalogue.py` (`--check`, `--describe`), which also says why each left-out spawner is left out |
| `RegattaCourseGenome.cs` | the genome, `Repair` (clamps, never invents), JSON, `Override` for harnesses |
| `RegattaPlacementBuilder.cs`, `RegattaPlacementCarve.cs` | lay a placement through the spawner's own code, then carve it |
| `SpawnableRegattaRails.courseGenome` | a pool entry carries its genome here; empty is today's course |
| `Tools/Compute/evaluators/regatta_genome.py` | the Python mirror: `random_genome`, `mutate`, `crossover`, `repair`, `descriptors`; `--self-test` holds it to the C# on 41 genomes |
| `Tools/Build/regatta_genome_harness/run.sh` | the game's code with no editor: `lay`, `repair`, `check`, `fixture`, `gate` |
| `Tools/Build/regatta_genome_render.py` | stills of laid genomes |

## Known gaps

- HopfFibration often lays nothing: random parameters make its prisms too fine.
- Lifeform species still come from the cell config's spawn profile; `flora_preset` is recorded only.
- Placement volume is not counted in the cell's phase thresholds yet.
- Spawners that need an authored Vector3 scale or waypoints are left out (reading prefab values is the v2 idea).
