# The Regatta race judge (the real game, headless)

**2026-11-16, Garrett's Regatta flywheel, round 1, worker W1.** Plan step: `time-arena-course`.

A course is scored by the game's own code: Amoebius boots the real game headless, launches Regatta through the normal
arcade flow (the editor's `RegattaHullBenchmark`, step for step), seats each hull's SHIPPED AI at Legendary with the
host seat idle in a hull nobody races, presses Ready, and timestamps every gate each AI threads. Nothing is modelled.

```sh
apt-get install -y dotnet-sdk-10.0                                     # once (dot.net is blocked in cloud sessions)
python3 Tools/Compute/evaluators/regatta_judge.py --self-test           # ~1 min: Urchin ~51 s, a 10 s limit is a DNF
python3 Tools/Compute/evaluators/regatta_judge.py --calibrate --seeds 1,2,3
python3 Tools/Compute/evaluators/regatta_judge.py --genome g.json --hulls Urchin,Manta --seeds 1,2 [--field]
```

From Python: `regatta_judge.judge(genome, seats, seeds, limit_s=400, backend="engine"|"model", field=False)`, the RESULT
shape of the flywheel's shared contract (`/mnt/project-files/regatta-flywheel/BRIEFS.md`). The engine side is
`Port/src/CosmicShore.Player/RaceJudge.cs` (`CosmicShore.dll --headless --race-judge SPEC --race-out RESULT`, optional
`--control-port N` to inspect a judged race live, `"trace": true` for a 4 Hz position and speed trace per seat).

## Speed

One process boots in ~20 s wall, then races its seeds back to back through Play Again: 4-20 s wall per race (5-12x real
time on one core). Alone mode runs one process per hull in parallel: **27 races (9 hulls x 3 seeds) in 3.5 min on 4
cores**, about 2 races per core-minute. A 6-hull field race: ~10 s wall per race after boot.

## Today's baseline (2026-11-16, bleeding-edge 51323414, after #2037 and #2038)

Each AI alone, I1, the shipped course with no features, Legendary, 4 seeds (`--calibrate --seeds 1,2,3,4`, 36 races in
3.3 min on 4 cores). **This is the reference the course search scores against**; the 11-03 editor column below is kept
for history.

| Hull | Mean | sd | Finished | vs Urchin |
|---|---|---|---|---|
| Rhino | 35.4 s | 2.6 | 4/4 | 0.69x |
| Stoat | 46.1 s | 5.4 | 4/4 | 0.90x |
| Scarab | 47.2 s | 4.9 | **2/4** | 0.93x |
| Urchin | 51.0 s | 0.3 | 4/4 | 1.00x |
| Squirrel | 80.1 s | 2.8 | 4/4 | 1.57x |
| Manta | 82.3 s | 0.8 | 4/4 | 1.61x |
| Sparrow | 90.5 s | 1.2 | 4/4 | 1.77x |
| Serpent | 107.0 s | 6.2 | 4/4 | 2.10x |
| Dolphin | 131.0 s | 23.8 | 4/4 | 2.57x |

Spread (slowest / fastest finisher): **3.7x**. Fixed today: the Stoat (239 s, its trampoline AI never pressed on the race
line: `StoatSlingConfig_Regatta` presses for the warp at 400 u, #2037) and the Dolphin (never finished: its race-line nose
lead 2.5 made it overshoot and circle; 1.0, #2038).

**Open: the Scarab** at its nose lead 2.5 is fast when it finishes (44-51 s) but stalls in half its races (15-16 of 24
gates at 400 s). At 1.0 it finishes 10 of 10 but in 88-173 s; at 0, 141-211 s. Left as it is: a choice between a fast,
fragile AI and a slow, reliable one.

**Determinism.** One process is deterministic for a (seed, race index) pair: two processes with the same seeds give the
same times to the millisecond. The arena's SegmentSpawner re-seeds `UnityEngine.Random` with the course seed while it lays
the course, so the judge seeds the race again as the turn starts; the seed then feeds the race's own draws.

## Calibration against the 11-03 editor benchmark (first run, before #2037 and #2038; 3 seeds)

| Hull | Port (mean, sd) | Editor 2026-11-03 | Ratio | Why |
|---|---|---|---|---|
| Urchin | 50.9 s (0.4) | 51.0 | 1.00 | matches |
| Squirrel | 80.5 s (3.3) | 75.0 | 1.07 | matches within the noise band |
| Serpent | 108.7 s (6.4) | 100.0 | 1.09 | matches within the noise band |
| Manta | 82.5 s (0.8) | 67.0 | 1.23 | **game change**: Soar is the Manta's Time level-5 upgrade since the 2026-11 overhaul (`MantaAnalogTurnBoostExecutor.soarUnlockedBy = Time`); at Regatta's starting Time the upgrade is off, so the AI never boosts and holds 180 u/s (inspected live: `SoarUnlocked = False`) |
| Sparrow | 91.1 s (2.3) | 133.0 | 0.68 | not traced yet |
| Rhino | 35.2 s (3.2) | 95.0 | 0.37 | **game change**: since 2026-11-07 the Rhino's hull is a plough that shoves rail prisms aside (`RhinoPloughPrismEffectSO`); the 11-03 Rhino rammed and stalled (78% of an I4 race at 50 u/s). Now it cruises at ~440 u/s and shoves 200-500 prisms a race |
| Stoat | 238.7 s (1.0) | 70.0 | 3.4 | **game change**: since 2026-11-07 the Stoat's sling is a trampoline whose autopilot presses only when its steering target is 35+ degrees off the nose (`AutopilotTrampoline`); on Regatta's race line the target sits one look-ahead dead ahead, so it never presses and flies at its 60 u/s base (inspected live: `_aiLastSlingTime = -Infinity`) |
| Dolphin | DNF (3-21 of 24 gates in 400 s) | 97.5 | - | circles near rings at 50-100 u/s; Yash's editor flight records showed an AI Dolphin stuck at 0 u/s too (`AI_BOOST.md`). Not traced to a cause yet |
| Scarab | 43.9 s (2 of 3; one timed out at 14 gates) | - | - | no editor reference |

**Reading it.** The three hulls whose kits did not change since 2026-11-03 match the editor within 10%. Every large gap
traced so far is the game changing since that benchmark, not the port differing from Unity: the 11-03 numbers are
stale for the Manta, Rhino and Stoat. The calibration that would settle the rest is a FRESH editor run of
`RegattaHullBenchmark` (branch `claude/regatta-overnight-balance`, `-executeMethod ...RegattaHullBenchmark.RunBatch`) on
today's bleeding-edge.

**What the judge already says about Regatta today** (worth Garrett's eyes before any course search): alone on the I1
course the Rhino (35 s) and Scarab (44 s) beat the Urchin (51 s); the Stoat (239 s) cannot race because its AI never
uses its sling; the Dolphin AI does not finish. The course search balances whatever the AIs do, so a broken AI (Stoat,
Dolphin) is a link-3 problem to fix before the search can mean anything for those hulls.

## Time level 5 for everyone (2026-11-16, Garrett: "do both with and without the L5")

`--start card | rest | L5 | '{"Manta": {"Time": 5}}'` publishes the starting element levels the way the card's
`StartingElements` is published (no card or vessel asset edited); each seat's levels at the start are in its
`start_levels`. `card` is what a player gets: Manta Time -0.5 (the game reads level -4), Serpent, Sparrow, Scarab and
Stoat Time 10 at I1. `rest` is every hull at 0. `L5` is every hull at Time 5, so every Time level-5 upgrade is on.
Alone, I1, the shipped course, Legendary, 4 seeds each, bleeding-edge 628de300:

| Hull | rest (all 0) | L5 (all Time 5) | card (today) | What Time 5 buys it (rest to L5) |
|---|---|---|---|---|
| Rhino | 35.4 s | 32.8 s | 35.4 s | 7% faster |
| Urchin | 51.0 s | 51.0 s | 51.0 s | nothing: its AI's time does not move with Time |
| Scarab | 68.3 s | 50.7 s (sd 0.2) | 47.2 s, **2/4 finish** | 26% faster; at Time 10 it is faster again but stalls in half its races |
| Squirrel | 80.1 s | 77.2 s | 80.1 s | 4% faster |
| Manta | 80.3 s | **103.9 s (sd 22.8)** | 82.3 s | 29% SLOWER: Soar unlocks and the AI flies it worse than without it |
| Sparrow | 119.8 s | 103.4 s | 90.5 s | 14% faster (Time 10 another 12%) |
| Dolphin | 131.0 s | 119.9 s | 131.0 s | 8% faster |
| Serpent | 161.1 s | 132.2 s | 107.0 s | 18% faster (Time 10 another 19%) |
| Stoat | **363 s, 1/4 finish** | 180.6 s | 46.1 s | 2x faster; its warp needs Time: at rest it cannot race, Time 10 makes it the second fastest |
| **Spread** | 10x (4.5x without the Stoat) | 5.5x (4.0x without the Stoat) | 3.7x | |

Reading it. Time is the lever the card already pulls, and it pulls hard on four hulls (Stoat, Serpent, Sparrow,
Scarab) and barely on the rest. Two AIs fail at higher Time, which is an AI fault, not a vessel fact: the Manta flies
worse with Soar, and the Scarab stalls at Time 10 where it finishes every race at Time 5. Raw: 
`/mnt/project-files/regatta-flywheel/w1-time-ai/l5_{rest,L5,card}.json`.

## Time games (`--mode`)

The judge races any gate-race Time game: `--mode Switchback|SerpentTime|Breakwater|Redline|ScarabRally ... --intensity N`.
Every game but Regatta draws a new course per match, so the judge forces course seed = race seed
(`CourseSeedOverride.Forced`; `course_seeds` in the spec names others). `overrides` (asset fields) and
`controller_overrides` (the scene's race controller, e.g. `{"aiNoseLead.0.Gain": 1.0}`) change the AI's numbers in
memory for one run, which is how `Tools/Compute/evaluators/time_ai_search.py` searches an AI in its own game. `"trace": true`
adds the AI's objective, steering target, nose and state flags per sample, and what is within 30 u whenever the hull is
under 50 u/s. The player is rebuilt when any C# under `Assets/_Scripts` or `Port/src` is newer than it. A process takes
about 2.5 GB: more than 4 at once on a 16 GB machine is killed for memory.

Time-game baseline, each AI alone, Legendary, 3 random courses per rung (`w1-time-ai/baseline_time.json`):

| Hull, game | I1 | I2 | I3 | I4 |
|---|---|---|---|---|
| Dolphin, Switchback | 93-105 s | 143-164 s | 165-168 s | 227-259 s |
| Serpent, Serpent Time | 128-135 s | 130-149 s | 147-171 s | 177-187 s |
| Sparrow, Breakwater | 106-164 s, 1 DNF | 105-248 s | 106-332 s | 266 s, 2 DNF |
| Manta, Redline | 64-66 s | 66-71 s | 73-124 s | 83-120 s |
| Scarab, Scarab Rally | 62-65 s | 71-75 s | 70-71 s | 138-142 s |

The Sparrow's DNFs are its Breakwater nose lead (2.0): at its 35 u/s base speed the lead spins the hull in open air
(traced: nose in a new direction every half second, nothing within 30 u but its own trail), it never lines up to boost,
and it can circle for minutes. At lead 1.0 it finished 12 of 12 (100-133 s, one 299 s) against 8 of 12 at 2.0.

## Field races

`field: true` races every seat at once (two domains, the host idle). The race ends when the first pilot finishes (the
mode's own end), so the others report gates, not times. Two 6-hull test fields (Urchin, Manta, Rhino, Squirrel, Serpent,
Sparrow): the Rhino won both in ~34 s; the Urchin had 15-16 of 24 gates.

## Limits

- The genome: until the game has `RegattaCourseGenome` (W2), the judge reads the genome's `intensity`, `seed` and
  `features` and says in `notes` what it ignored; once that type exists it hands it the whole genome.
- `prism_hits` is prisms destroyed (friendly + hostile) from `RoundStats`, not contacts.
- Headless has no GPU: prism clock animation is off (visual only).
- The boot fix (`UnattendedBoot`) closes the first-run privacy overlay unanswered and skips the username step, exactly
  as the game's soak runners do, and only in `--train` and `--race-judge` runs.
