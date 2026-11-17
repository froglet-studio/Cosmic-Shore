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
  `cross_line`: whether it may stand on the race line, and `cross_rail`: whether it may stand on or across a rail
  section.

Every prism a placement lays is carved, one by one, out of the ring mouths, the rails, the nucleus and the membrane,
away from the placements before it, out of the race line's tube unless the placement has `cross_line`, and off the
rails unless it has `cross_rail`; the ring mouths, the nucleus and the membrane are always carved. At most
6,000 placement prisms per course. A placement is sized by the radius that holds 90% of its prisms, so a projecting
spawner's few far outliers (the Hopf fibration's) do not shrink the rest below the finest prism.

**Why `cross_line`.** The first judge run (2026-11-16: 5 random genomes against their placement-free twins, Urchin,
Squirrel, Serpent and Rhino alone) gave the same finish times to the millisecond: about 9,000 prisms were laid, but the
carve kept the tube round the line open and every AI flies that line, so no placement ever met a pilot. A course whose
genes cannot touch the race cannot be searched for balance. With `cross_line` the obstacle stands on the line; the ring
mouths stay open, so every ring can still be threaded. `cross_rail` does the same for the rails, so a rail rider (the
Urchin) must leave its rail or go round.

**The cell's phase ladder, and how much a course may hold** (Garrett, 2026-11-16: "if you make more then set a higher
limit. but that also might be a flag that you are making too much"). A Regatta cell's `PhaseThresholds` are measured on
the rails' laid volume. `SpawnableRegattaRails` reports what its placements laid (`ICellPhaseBaseline`) and the cell
raises every boundary by it, so a course opens in the phase the measured one does and the band left for players' trails
is unchanged. A course is capped at 6,000 placement prisms (about three rail sets), and the search sees `est_prisms`, so
a loss can charge for content as well as reward balance.

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

## Which genes move which hull (judge, 2026-11-16)

I1 default circuit, each hull alone, seed 1, finish seconds (prism hits in brackets). A gene that moves no hull's time is
dead for the search; every hull below has at least one live gene.

| Genome | Urchin | Rhino | Serpent | Squirrel |
|---|---|---|---|---|
| none (I1) | 51.1 (0) | 31.6 (515) | 102.1 (898) | 79.0 (3) |
| 2 SuperShielded placements, `cross_line` + `cross_rail` | 51.1 | 31.6 (520) | 102.1 | 84.6 |
| 2 Plain placements on the rail | 51.1 | 31.6 (671) | 102.1 | 91.9 |
| 2 Danger placements on the rail | 51.1 | 31.6 (671) | 100.2 | 83.8 |
| 2 Plain placements on the line | 51.1 | 31.6 (671) | 102.1 | 96.4 |
| rail gaps (every leg, 30-70%) | 59.4 | 35.6 | 214.4 | 84.1 |
| rail detour 150 u (every leg) | 60.8 | no finish (7 of 24 rings) | 165.8 | 150.8 |
| Plain rails | 53.3 | 31.6 (610) | 101.6 (2103) | 182.9 |
| neutral rails | 287.9 | 31.6 | 102.1 | 89.1 |

What this says: placements reach only the Squirrel (and the Serpent a little through Danger). The Urchin rides its rail at
a steady 300 u/s and never touches them; the Rhino ploughs through every kind, SuperShielded included, at no cost in
time. The rail features are the live levers for all four: neutral rails take the Urchin's ride away (+237 s), gaps and
detours cost every hull, and Plain rails cost the Squirrel most. The search should keep rail features in every genome and
treat placements as the Squirrel's and Serpent's lever until a placement kind is found that slows a Rhino. One seed;
the detour Rhino timeout needs a second look (it may be the Rhino AI, not the course).

## Wider training courses for the AIs (round 4)

Several AIs broke on Regatta situations their own Time game never showed them (FINDINGS 4, 5, 7, 13). The genome now
widens those games for training, without touching Regatta (the AIs train in their own Time games, `AI_PLAN.md`):

- `TrainingCourseOverlay.Override` (null in shipped play) is a course genome that `GateRaceController.ApplyCourse`
  lays round ANY gate race's own gates, after the mode's own geometry. It uses the same builder and carve:
  - the ring mouths are always kept open;
  - the line tube (the gates' polyline) is carved unless a placement has `cross_line`;
  - the frame is taken about the gates' centroid;
  - legs are rescaled from eight gates to the mode's count;
  - the shell is the mode's own.
  Rail features, flora and circuit genes are Regatta's only.
- The judge sets it from a spec's `genome` whenever `mode` is not Regatta, and records `training_prisms` per race.
- `regatta_genome.py --random N --curriculum W --for-mode <Card>` (`training_genome`) runs from narrow to wide:
  - W=0 is the mode's own course.
  - Up to W=1, the number of placements, their size, their share on the line, the share of hard prisms (Danger,
    Shielded, SuperShielded) and the tightness of the line tube all grow with W.
  - For Regatta, rail sections, alternate rings and circuit changes grow with W too.

Proof (judge, 2026-11-16, Dolphin alone in Switchback I1, seed 1): its own course took 101.4 s. Two wide genomes
(`--curriculum 1 --for-mode Switchback`) laid 5,719 and 808 prisms round its gates; the Dolphin still threaded all 26
gates, in 132.1 s and 155.0 s.

## Blocking draws across the line (round 5, 2026-11-17)

W3's L5 trace (regatta-flywheel FINDINGS row 24) found that the archive's random placements touched no hull. Every
hull ran a course in the same time as its placement-free twin, to the millisecond. A sparse shape on the line leaves a
hollow to fly through. A shape off the line has its line prisms carved away, unless `cross_line` is on. Designed
courses with two Walls per leg at offset 0, cross_line on and Danger prisms did slow the fast group: Stoat +43 s,
Urchin +13 s, Squirrel and Serpent +29 s each, Scarab +34%. The Rhino got 0.9 s faster, because Danger is its ram
reward.

So `regatta_genome.py` now does three things:
- **Blocking draws.** `BLOCKING_SHARE` (0.3) of free placement draws are a `blocking_placement`. That is one of
  `BLOCKING_KINDS` (Wall, Panes, Flatline, Jackstraw), at radius 0-40 u, with cross_line on, scaled to block a leg's
  width, and Danger with probability `BLOCKING_DANGER` (0.6, otherwise any prism kind). This applies in
  `random_genome`, mutate's add and kind-swap, and crossover's children. `training_genome` keeps its own curriculum.
- **Descriptors.** `danger_on_line_share`, `dense_on_line_count` and `on_line_by_prism` count placements on the line.
  On the line means cross_line is on and the radius is below the placement's scale. MAP-Elites can bin by these, so
  blocking courses keep cells of their own (`w2:` keys in regatta_qd).
- **Corner bias.** `CORNER_TIGHTEN` (0.25) of circuit-number mutations only tighten a corner, by lowering
  CornerFloorRadius or RingRadius. A floor of 100 with a ring radius of 50 cost the Scarab +16 s (it also made the
  Rhino AI stick, which is an AI fault, link 3).

The self-test checks the blocking share, the Danger share and the descriptors. Its negative control sets the share to
0, and then no blocking draw appears.

## Known gaps

- A spawner that lays a prism at infinity is skipped prism by prism (Linked Rings did, with 20+ rings of 64 blocks,
  until its slide was fixed on 2026-11-16; the carve's grid looped forever on such a point).
- Lifeform species still come from the cell config's spawn profile; `flora_preset` is recorded only.
- Spawners that need an authored Vector3 scale or waypoints are left out (reading prefab values is the v2 idea).
