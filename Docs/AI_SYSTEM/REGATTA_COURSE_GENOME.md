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
  (Plain, Danger, Shielded, SuperShielded), seed, density, every one of the spawner's own parameters, and
  `cross_line`: whether it may stand on the race line.

Every prism a placement lays is carved, one by one, out of the ring mouths, the rails, the nucleus and the membrane,
away from the placements before it, and (unless the placement has `cross_line`) out of the race line's tube; at most
12,000 placement prisms per course. A placement is sized by the radius that holds 90% of its prisms, so a projecting
spawner's few far outliers (the Hopf fibration's) do not shrink the rest below the finest prism.

**Why `cross_line`.** The first judge run (2026-11-16: 5 random genomes against their placement-free twins, Urchin,
Squirrel, Serpent and Rhino alone) gave the same finish times to the millisecond: about 9,000 prisms were laid, but the
carve kept the tube round the line open and every AI flies that line, so no placement ever met a pilot. A course whose
genes cannot touch the race cannot be searched for balance. With `cross_line` the obstacle stands on the line; the ring
mouths stay open, so every ring can still be threaded.

**The cell's phase ladder** (held for the Ecology lane). A Regatta cell's `PhaseThresholds` are measured on the rails'
laid volume, and the placements add to it. `SpawnableRegattaRails.PhaseBaselineVolume` / `PhaseBaselinePrisms` report
what they laid; raising the ladder by it touches `Cell` and `CellPhaseThresholds`, an occupied lane, so that half waits
as a patch (`/mnt/project-files/regatta-flywheel/w2/phase_baseline.patch`) for its occupants' yes. Until then a course
with many placements can open Restless or Frenzy; a Regatta cell is Barren, so no fauna act on it.

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
| `Tools/Compute/evaluators/regatta_player_prior.py`, `Tools/Compute/regatta/player_prior.json` | genes from play (below) |

## Genes from play

The flywheel's link from people back to the search. Every editor race is recorded (`HumanFlightRecorder`) and sent to a
`bugledger/<user>` branch. `regatta_player_prior.py --build` reads every Regatta flight there and measures, per
intensity and leg, the human's leg time over the median AI rival's in the same race (`ratio`), the line they flew in a
placement's own coordinates (`radius`, `bearing` about the spine), and the rail prisms they broke. A leg with at least
three passes gets a role (skill below 0.9, pain above 1.3, else even) and a weight (`(1 / ratio)^2`, held to 0.25-4).

- `seed_genomes(prior, rng, intensity, n)`: courses made from play on the shipped circuit. Structure stands opposite
  the side people chose on skill legs, alternate rings sit on lines people found off the spine, and pain legs get a
  wider tube.
- `regatta_genome.mutate(g, rng, prior=P)`: half the placements added or moved land on a leg drawn by weight, beside a
  line people flew. Without a prior the stream is exactly what it was.

The prior proposes; the judge scores. It stores hulls, counts and file stamps, never a name. Built 2026-11-16 from 110
usable flights of 189 (the rest threaded no ring). On I1 and I3 people beat the median AI on most legs; on I2 they
trail it everywhere. v1 compares with the median of every rival, whatever its hull; a per-hull ratio is the next step.

## Known gaps

- Lifeform species still come from the cell config's spawn profile; `flora_preset` is recorded only.
- The cell phase ladder does not count placements yet (above).
- Spawners that need an authored Vector3 scale or waypoints are left out (reading prefab values is the v2 idea).
