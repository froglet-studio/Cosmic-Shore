# Headless AI training in the port

The game's AI genetic training (`Assets/_Scripts/Utility/AITraining`, bleeding-edge) runs in
the port **unmodified**: the same `TrainingAutoLauncher`, `TrainingSessionRunner`,
`TrainingPilot`, policies, sensors, fitness profiles and `TrainingPopulation`. The port adds
only the thing the editor's **Learn** button does — create the launcher, hand it the
`TrainingControl` asset — plus a clock that runs as fast as the CPU allows, and processes that
fly several matches at once.

It is the port's first real utility, and it doubles as its fidelity gauge: a genome Unity
scored can be re-scored here, genome for genome, and the gap between the two numbers is a
measurement of how far the port's simulation is from Unity's.

## Build against a tree that has the training code

The training framework exists on `bleeding-edge`. Check it out as a separate worktree (no merge
into the port's branch) and point the Live compile at its `Assets/`:

```sh
git worktree add --detach --no-checkout ../be origin/bleeding-edge
(cd ../be && git sparse-checkout set --no-cone '/Assets/' '/ProjectSettings/' '/Packages/' && git checkout --detach origin/bleeding-edge)
dotnet build src/CosmicShore.Player -c Release -o ../binBE \
    -p:LiveAssetsDir=$(realpath ../be/Assets) -p:LiveSrcDir=obj/live-src-be
export COSMIC_SHORE_PROJECT=$(realpath ../be)   # the content (scenes, prefabs, SOs) must match the code
export COSMIC_SHORE_NET=off
```

`LiveAssetsDir` picks the `Assets/` tree the Live assembly compiles; `LiveSrcDir` keeps its
synced sources apart from the default tree's, so the two builds never share a sync directory.

## Commands

All modes read `Assets/_SO_Assets/AI Training/TrainingControl.asset` and its scenario, session
state and archive — exactly the assets Learn uses. `--scenario Scenario_HexRace` picks another
scenario asset by name.

```sh
# Evolve: 4 processes (one per core), every genome flown three times per generation, 12 generations.
dotnet ../binBE/CosmicShore.dll --train train --workers 4 --evals 3 --generations 12 --seed 300 --quiet --train-out out

# Re-score the generation Unity already scored, twice, and report the disparity + noise floor.
dotnet ../binBE/CosmicShore.dll --train replay --repeats 2 --seed 7 --quiet --train-out out

# Fly fixed genomes against each other N times each; mean and standard error per genome.
dotnet ../binBE/CosmicShore.dll --train eval --genome a.json --genome b.json --flights 24 --quiet --train-out out

# Final tournament: fly EVERY genome of a trained population (plus controls) many times; the
# winner is written to out/eval_best.json. Run several processes with different --seed and pool.
dotnet ../binBE/CosmicShore.dll --train eval --population out/KEY.state.json --genome control.json --flights 6 --seed 51 --quiet --train-out t1
```

`--resume-run` (train mode) continues a run that was killed, from its checkpoints (see "Long runs").
`--train export --genome FILE --fitness F [--note TEXT] --train-out DIR` deploys a genome file
(a tournament's `eval_best.json`) into the archive without flying anything: the scenario's entry
is upserted with the genome, the fitness you name (the tournament mean, not the in-training
number) and the genome's own generation, and `KEY.Archive.asset` + `KEY.archive.json` are
written, the same drop-ins the trainer writes for its hall of fame.
`--seed` makes a run reproducible (two runs with one seed are bit-identical). A train run always
has one; its workers fly different matches (the gameplay seed is offset per worker) but evolve
under the shared seed.

## What comes back to Unity

After every generation worker 0 writes, under `--train-out`:

| File | What it is | How Unity takes it |
|---|---|---|
| `KEY.json` | The hall-of-fame genome, via the game's own `GenomeJson` | **FrogletTools ▸ Training window ▸ Archive ▸ Import JSON**, then it deploys to AI seats like any archive entry (`DeployArchiveInNormalPlay`) |
| `KEY.robust.json` | The best mean among genomes flown ≥ max(4, 2K) times | Same import. Prefer this one (see "Noise" below) |
| `KEY.archive.json` | `TrainingArchiveSO` as `JsonUtility` JSON, with the entry upserted | `JsonUtility.FromJsonOverwrite` onto the archive asset |
| `KEY.state.json` | `TrainingSessionStateSO` (population, history, hall of fame) | `JsonUtility.FromJsonOverwrite` onto the session asset — Learn then continues from the port's generation |
| `KEY.SessionState.asset` | The same session state, **as the Unity YAML the editor writes** | **Drop-in**: copy over `Assets/_SO_Assets/AI Training/SessionState.asset` (guid and script reference kept) |
| `KEY.Archive.asset` | The archive, as Unity YAML | Drop-in over the archive asset the control asset references |
| `KEY.progress.csv` | One row per generation: flights, mean, best, hall of fame, wall time | — |
| `eval.csv` / `KEY.replay.json` | Per-flight numbers from eval / replay | — |

`KEY` is the scenario key, e.g. `Squirrel_SkimRace_I4`.

The `.asset` files come from `UnityYamlWriter`, which writes a ScriptableObject the way the
editor serializes it: base-class fields first, integer arrays as one hex blob, a null
serializable class as its default instance, the editor's string quoting and exponent spelling,
and the document header copied from the asset being replaced. Checked against the real assets:
all seven AI Training assets on bleeding-edge (session state, archive, control, scenario,
fitness profile, schedule, telemetry) round-trip **byte-identical**
(`CosmicShore.dll --yaml-roundtrip ASSET OUT`), so an exported asset shows in version control as
a diff of the trained numbers only.

## How the parallel trainer keeps the algorithm the game's

Each worker is the full game plus the runner, serving one slice of the population (a whole
number of matches). Per-flight fitness is recovered from the runner's running mean between
matches (`x = F_{n-1} + (F_n - F_{n-1})·n`), and only those numbers are exchanged. Every worker
then restores the generation as it started and replays every flight in (genome, flight) order
through `TrainingPopulation.ReturnFitness` and `TrainingSessionStateSO.RecordEpisode`, and calls
`TrainingPopulation.Evolve` under a random state seeded from (run seed, generation). Every
process therefore ends the generation holding the identical population, novelty archive and
hall of fame, with no population ever shipped between them. The runner's own
end-of-generation evolve is held off (its evaluation counter is parked far below the population
size) so the merge is the only thing that evolves.

## Long runs: supervision and recycling

A train run's launching process is a supervisor that flies nothing: it starts every worker as
a child (worker 0's console is echoed; each writes `run/wN.log`). After a generation's merge
every worker holds the identical evolved population, so a worker whose working set is past
`--recycle-mb` (default 2500) writes that population to `run/ckpt_wN.json`, exits with code 75,
and is restarted with `--resume`. The others simply wait for its next results as for any slow
worker. A resumed worker continues the run's generation and episode limits (`run/meta.txt`)
and re-seeds its gameplay stream from the generation it resumes at, so it never replays its
previous incarnation's tracks. Every worker still logs the identical merged generation across
recycles (checked); a recycled run is reproducible under its seed and limit, but it flies
different tracks from an un-recycled one after the first recycle, by design.

**A run that died resumes.** Every worker also checkpoints at every generation boundary, so when
the OS kills a worker (measured 2026-10-10: the memory cgroup of a 16 GB box killed a 2.4 GB
worker at generation 8 of 10, with three agents' players alongside the four workers) the same
command plus `--resume-run` keeps the run directory, removes the in-flight generation's result
files and starts every worker from its own checkpoint at the last completed generation; the
generation and episode limits are the run's (`run/meta.txt`), as after a recycle. `--workers`
must match the killed run (the merge needs every slot). Size `--workers x --recycle-mb` under
the memory the box can actually give the run.

Why recycling is needed: the game leaks across scene reloads, and Unity retains the same references
(there its engine objects are near-empty shells; in the port they carry managed data).
`COSMIC_SHORE_LEAKSCAN=1` attributes every destroyed prism still reachable to the first live
holder on its reference chain. After 10 SkimRace matches, ~27,700 destroyed prisms:

| holder | destroyed prisms | status |
|---|---|---|
| `SquirrelVesselTelemetry` in `SkimmerStealPrismEffectSO.OnSkimmerStolenPrism` (turn hooks released only at a turn end a watchdog-ended match never raises) | found first | **fixed** in `VesselTelemetry` (base owns the hook pairing; releases on disable) |
| `VesselOvertakeBySkimmerEffectSO._lastEffectTime` — a static dictionary keyed by `ResourceSystem`, never pruned; each dead key holds its vessel and trail (its danger-prism, skimmer and explosion debuff twins had the same shape: the danger-prism table held 16,886 of the 27,587 destroyed prisms reachable after 10 matches, the overtake table 2,581) | 11,874 | **fixed** 2026-10-10 (game code: the four tables share `VesselEffectCooldowns`, which prunes destroyed vessels on first sight of a new one; measured on this scan, 12 matches of the deployed genome: 27,587 -> 12,623 destroyed prisms reachable, the two tables gone from the growers, live heap at sample 12 1,190 -> 1,145 MB; the remaining holder is the engine's `GameLoop.Current`) |
| a pending `Delay` continuation held by the score tracker: `TimePlayedScoring.UpdateScoreLoop`, whose token is cancelled only by a turn end, so a tracker destroyed without one (a watchdog-ended match, a quit, a host-loss bounce) kept the loop pending in the scheduler, and the loop kept adding time score to the shared round stats from the menu | 10,268 (12,053 on the 2026-10-10 scan) | **fixed** 2026-10-10 (game code: `BaseScoreTracker.OnDestroy` unsubscribes every scorer; measured on the same 12-match scan: 12,623 -> **570** destroyed prisms reachable, those 570 the cooldown tables' current-match keys before their next prune, live heap at sample 12 1,149 -> 1,075 MB; from the first scan of the day, 27,587 -> 570) |
| the port's scene-load history | 5,549 | bounded to the current load |

The port's own leaks the same investigation found are fixed (`LongRunMemoryTests`): the renderer
registry, the FMOD start log, the scene-load history, and a destroyed transform's world cache.

## Speed (Skim Race, Squirrel, intensity 4, 3 racers, 120 s watchdog matches)

| | wall per match | simulated / wall |
|---|---|---|
| Unity editor Learn | ~125 s | ~1× (real time) |
| port, 1 process, first cut | 27 s | 4.5× |
| port, 1 process, query snapshot | 12–14 s | 10× |
| port, 4 processes | 4 matches at once | ~35× aggregate |

A 24-genome generation flown once each is 8 matches: ~17 minutes in the editor, ~70 s here.

## Fidelity: the disparity metric

`--train replay` takes the generation stored in the session asset (24 genomes Unity flew once
each), flies every one again in the port, and reports per genome and in aggregate. Skim Race
fitness is `100·crystals − episode seconds − 0.1·score`, and every match ran to the 120 s
watchdog in both engines, so the comparable quantity is **crystals per racer per match**.

| Port state | crystals / racer / match | runs |
|---|---|---|
| Unity (the reference) | **0.625 ± 0.18** | 24 flights |
| port, triggers once per rendered frame, FixedUpdate at 50 Hz | 0.79 ± 0.09 | 96 flights |
| + physics on the project's 0.04 s step, triggers in the step | 1.01 ± 0.07 | 144 flights |
| + OnTriggerStay, queries on the last step's poses | 0.875 ± 0.07 | 144 flights |

Per genome, one port round agrees with Unity at rank correlation 0.45–0.66; two port rounds of
the same genomes agree with each other at 0.41–0.78. **Port-vs-Unity already sits inside the
port's own round-to-round noise**, so at 24 Unity flights the metric cannot resolve anything
finer. The reference, not the port, is now the limiting error bar: every additional Unity
evaluation tightens it. The cheapest way to buy precision is to have Learn score one more
generation in Unity (without evolving), export that session asset, and replay it here.

Each fix the metric drove was a documented Unity contract the port had missed, not a tuning:

- `ProjectSettings/TimeManager.asset` was never read — FixedUpdate ran at the engine default
  0.02 s where this project says 0.04 s.
- Triggers were resolved once per rendered frame instead of in the physics step, sampling
  contacts at 60 Hz instead of 25 Hz.
- `OnTriggerStay` was never dispatched (the skimmer's crystal vacuum and the Astro League ball's
  vessel handler never ran).
- Physics queries saw live transforms; with `autoSyncTransforms` off Unity answers from the
  last step's poses.
- Unity's hex-blob primitive arrays did not deserialize (the session asset's fitness ring
  buffer loaded empty and the runner threw at the first episode).

## Noise, and why the robust export exists

One genome's flights in Skim Race range from 0 to 12 crystals: tracks are random and three
racers compete for the same crystals. The game's hall of fame records the best running mean *at
the moment of a flight*, so a genome's first lucky flight can hold it permanently. Evaluate
before deploying — `--train eval` with `--flights 24` or more gives a standard error small
enough to tell genomes apart — and prefer `KEY.robust.json`. `--evals 2` or more per
generation is the in-loop version of the same cure; the port's speed is what makes it
affordable.

### Result: 12 generations, seat-balanced, evaluated

Run: 4 workers, 3 flights per genome per generation, 12 generations, seed 300, recycling on
(864 flights in 1,016 s wall). Then eval, 24 flights per genome, 4 processes:

| genome | crystals / flight |
|---|---|
| a typical Unity generation-0 genome (control) | 3.25 ± 0.60 |
| T3 robust best (6-flight in-training mean 392) | 2.17 ± 0.51 |
| Unity's hall of fame | 1.79 ± 0.38 |
| T4 hall of fame (one 880 flight) | 1.25 ± 0.12 |
| T4 robust best (6-flight in-training mean 147) | 0.96 ± 0.07 |

And the final tournament over T4's whole last generation (24 genomes + the same control, 24
flights each): every genome lands at 1.1-1.4 crystals per flight (s.e. ~0.15), the control
among them at 1.25. Absolute crystal counts move with who else is in the race, so only rows
from one eval compare.

**What it says:** on Skim Race, with 3 flights per genome per generation, the trainer's
selection is dominated by noise. Its in-training picks show the winner's curse (a mean over a
few flights of a 0-12 crystal distribution is mostly luck) and 12 generations did not produce a
genome measurably better than an ordinary one. That is a property of the training
configuration, and Unity's own run has the same one (its deployed best flies worse than a
typical genome). The port's contribution is that both the diagnosis and the cure are now
cheap: a tournament of 600 flights costs ~12 minutes, and `--evals` can go to 8+ per genome
per generation (a Unity-editor generation at that budget is 64 matches, ~2.2 hours; here ~2.5 minutes on 4 cores).

### Result: 7 generations at 8 flights per genome, seat-shuffled slices (2026-10-10)

The cure above, applied: `--train train --workers 4 --evals 8 --generations 10 --seed 2026`
(Release build, this branch at 48d84486f, worker slices shuffled so every flight lands in a random
seat against random opponents). The memory cgroup of the 16 GB box killed a worker at generation
8 (four 2.4 GB workers beside three agents' players), so the run stands at **7 generations, 1,344
flights, 55 minutes wall** under heavy shared load; `--resume-run` now exists for the next one.

| generation | flights | mean flight | best genome mean (8 flights) | hall of fame (one flight) | wall s |
|---|---|---|---|---|---|
| 1 | 192 | -49.9 | 39.6 | 277.1 | 469 |
| 2 | 192 | -40.6 | 52.1 | 277.1 | 307 |
| 3 | 192 | 5.3 | 138.1 | 280.0 | 409 |
| 4 | 192 | 42.8 | 163.1 | 380.0 | 417 |
| 5 | 192 | 58.4 | 185.4 | 380.0 | 452 |
| 6 | 192 | 69.8 | 227.1 | 680.0 | 690 |
| 7 | 192 | 91.7 | 250.6 | 680.0 | 519 |

The population mean (192 flights a generation, the least noisy number in the table) climbs every
generation, -50 to +92; the best 8-flight mean climbs 40 to 251. Then the **seat-balanced
tournament**: every genome of the generation-7 population plus the deployed control (the archive's
genome, `Exports/SkimRace_Squirrel_I4.json`, Unity's hall of fame), 8 flights per genome in each of
three processes (seeds 51, 52, 53), pooled to 24 flights per genome, 1,800 flights in 36 minutes
on three cores:

| genome | flights | mean | s.e. | crystals / flight |
|---|---|---|---|---|
| pop13 (gen 7) | 24 | **221.4** | 29.1 | **3.46** |
| pop18 (gen 7) | 24 | 208.9 | 37.4 | 3.33 |
| pop01 (gen 5) | 24 | 208.9 | 37.8 | 3.33 |
| pop14 | 24 | 197.9 | 26.1 | 3.21 |
| pop15 | 24 | 188.1 | 32.8 | 3.12 |
| ... 15 more trained genomes between 177 and 59 ... | | | | |
| **deployed control** (Unity hall of fame) | 24 | **47.2** | 30.5 | **1.67** |
| pop09, pop03, pop10, pop07 | 24 | 38 to 21 | | 1.5 to 1.6 |

**What it says.** With 8 flights per genome and shuffled seats the trainer's selection is no
longer noise: 20 of the 24 trained genomes beat the deployed genome, and the top five sit four
standard errors above it (221 against 47, pooled s.e. about 42), collecting twice the crystals per
flight. This is the first run on this harness that produced a genome measurably better than the
one Unity deployed, and the difference between it and the 12-generation run above is the
evaluation budget, not the algorithm: 8 flights per genome per generation instead of 3, and a
random seat for every flight. The in-training hall of fame (one 680 flight) is still a poor pick:
its genome is not in the tournament's top five. Deploy from the tournament (`eval_best.json`,
pop13), never from the hall of fame.

**Cost.** 1,344 training flights plus 1,800 tournament flights, about 95 minutes of wall time on
four shared cores; the editor would need about 26 hours for the same flights at real time.

**Deployed 2026-10-10** with `--train export` (fitness 221.4, the tournament note): the project's
`Archive.asset` entry for Squirrel_SkimRace_I4 is now pop13 (born generation 7), replacing the
hall-of-fame genome that scored 47.2 in the same tournament; `SessionState.asset` is the
generation-7 population, so the editor's Learn continues from generation 8; and
`Exports/SkimRace_Squirrel_I4.json` is pop13 for a manual Import JSON. Both assets parse and
re-write losslessly (`cs-asset roundtrip`, `serialization-audit` 0 dropped, 0 extra). What this
changes in play: nothing in Skim Race itself yet, because `SkimRaceAIDeployment.Claims` hands
Skim Race and Regatta seats to the rule-based Skim Race pilot while its config's
`DeployInNormalPlay` is on, and `TrainingDeploymentService` stands down for a claimed seat; the
archive entry is what deploys the moment that flag is off, and a head-to-head of the two pilots is
the next measurement worth taking.

## Seats: what the trainer was actually selecting

The runner hands genomes to `gameData.Players` in checkout order, so in the game's own loop
genome *i* always flies seat *i* mod 3 of its match, against the same neighbours. **Seats are
not equal.** Over the replay data (fixed seats):

| | seat 0 | seat 1 | seat 2 |
|---|---|---|---|
| port, crystals / match (144 flights each) | 1.10 | 1.03 | **0.51** |
| Unity, crystals / match (8 flights each) | 0.62 | 1.12 | **0.12** |

The two engines agree on the asymmetry, which is itself a fidelity result. And because
`Evolve` re-sorts elites into indices 0–3 every generation, an elite keeps its seat forever:
its fitness measures the seat as much as the genome.

A seat-balanced head-to-head (`--train eval`, each genome rotated through every seat and
opponent, 24 flights each, 4 processes):

| genome | crystals / match | |
|---|---|---|
| Unity's hall of fame (one 277 flight) | 0.50 ± 0.13 | |
| port 15-generation hall of fame (one 1080 flight) | 1.25 ± 0.23 | |
| port 15-generation "robust best" (3.9 over 16 flights in training) | 0.54 ± 0.13 | seat-inflated |
| a typical Unity generation-0 genome (-21 in Unity) | **1.46 ± 0.16** | |

**Unity's deployed best genome flies worse than an ordinary random one** once seats are
balanced. This is a property of the trainer, not of either engine. The parallel trainer now
shuffles each worker's slice before every pass (the merge still replays in the original order,
and flights are harvested by genome identity), so every flight lands in a random seat against
random opponents.

## Known gaps

- Headless runs have no Entities Graphics device, so prism clock animation is off (visual only;
  it does not touch colliders or gameplay).
- The per-flight fitness breakdown (components) is not carried across workers — only totals.
- Box colliders are world AABBs (rotation ignored) for both triggers and queries, as before.
