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
# Evolve: 4 processes (one per core), every genome flown twice per generation, 15 generations.
dotnet ../binBE/CosmicShore.dll --train train --workers 4 --evals 2 --generations 15 --seed 200 --quiet --train-out out

# Re-score the generation Unity already scored, twice, and report the disparity + noise floor.
dotnet ../binBE/CosmicShore.dll --train replay --repeats 2 --seed 7 --quiet --train-out out

# Fly fixed genomes against each other N times each; mean and standard error per genome.
dotnet ../binBE/CosmicShore.dll --train eval --genome a.json --genome b.json --flights 24 --quiet --train-out out
```

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
| `KEY.progress.csv` | One row per generation: flights, mean, best, hall of fame, wall time | — |
| `eval.csv` / `KEY.replay.json` | Per-flight numbers from eval / replay | — |

`KEY` is the scenario key, e.g. `Squirrel_SkimRace_I4`.

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

## Known gaps

- Headless runs have no Entities Graphics device, so prism clock animation is off (visual only;
  it does not touch colliders or gameplay).
- The per-flight fitness breakdown (components) is not carried across workers — only totals.
- Box colliders are world AABBs (rotation ignored) for both triggers and queries, as before.
