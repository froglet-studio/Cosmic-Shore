# Skim Race AI — Status Report

Snapshot of the working tree on branch `feat/ai-genetic-training`, 2026-09-29. Read-only
report: nothing here was retuned, and none of it is committed.

**Bottom line:** the genetic-training loop runs end to end, but it has not yet produced a
trained Squirrel for Skim Race. The deployment archive is empty, so "Play against trained AI"
currently installs nothing and the AI opponents are the stock `AIPilot`. Nobody has shown this
AI beating a human.

Skim Race is `GameModes.HexRace (33)` in `MinigameHexRace`. The vessel is locked to Squirrel,
and the training key is `Squirrel_HexRace_I4`.

---

## 1. Current behavior

### What Learn does

1. `TrainingAutoLauncher` boots Menu_Main and launches an all-AI HexRace with
   `GameDataSO.IsTraining` set.
2. The host seat is flipped to autopilot, and `TrainingSessionRunner` checks out genomes from
   the population (round-robin) into `TrainingPilot`s.
3. An episode ends when `OnMiniGameEnd`/`OnMiniGameTurnEnd` fires or `MaxEpisodeSeconds`
   elapses. `EndEpisodeInternal` then does three things:
   - scores each pilot against `FitnessProfile_HexRace`;
   - calls `TrainingPopulation.ReturnFitness` and `state.RecordEpisode`;
   - updates the hall of fame, runs `SaveAssets`, and requests a replay (a scene reload).
4. Once every member of a generation has been evaluated, `Evolve` produces the next
   generation.

### What a rollout records

For each pilot in each episode:
- the fitness total, which is the sum of each component's raw value times its weight;
- a running-mean fitness on that genome, plus its evaluation count;
- a 32-bit behavior hash, which goes into the novelty archive;
- the episode count and fitness history in `SessionState.asset`.

### What "Play against trained AI" installs

`TrainingDeploymentService` listens on `OnPlayerPairInitialized`. It skips in three cases:
`DeployArchiveInNormalPlay` is off, `IsTraining` is set, or the seat is the human's.

For any other seat it installs a genome only when `Archive.asset` has a matching entry. Locked
modes resolve their hull through `TrainingModeCatalog`. At intensity 4 the genome is used raw;
intensities 1–3 get a dithered copy.

`TrainingControl.asset` has `DeployArchiveInNormalPlay: 1`, so deployment is switched on.

### Is there a Squirrel HexRace I4 genome in the Archive?

**No.** `Archive.asset` contains `Entries: []`. The lookup key is `Squirrel_HexRace_I4`
(`TrainingArchiveSO.MakeKey`: vessel, mode, intensity). With nothing to install, the AI
seats in a normal Skim Race stay on `AIPilot`.

### Export

`Assets/_SO_Assets/AI Training/Exports/SkimRace_Squirrel_I4.json` was **not written**.

The archive has no HexRace Squirrel intensity-4 entry. The fallback, `SessionState`
`HallOfFameBest`, is also empty (`geneNames` / `geneValues` / `enabledModules` are empty
lists, fitness −Infinity, episodes completed 0). No genome was invented. The 24 genomes
in the saved population were left in `SessionState.asset`; none has been evaluated, and
none was copied out as an opponent.

Playtest steps for that empty-archive match: `Docs/SKIM_RACE_GARRETT_TEST.md`.

### Deploy path (confirmed, unchanged)

A normal HexRace does not set `IsTraining`. `TrainingDeploymentService` then runs only
when `TrainingControlSO.DeployArchiveInNormalPlay` is on (it is: `1` in
`TrainingControl.asset`) and only for seats with `IsInitializedAsAI`. The human host
seat is skipped.

`ArchiveDeployment.TryInstall` looks up `Squirrel_HexRace_I4` (HexRace is vessel-locked
to Squirrel in `TrainingModeCatalog`; `UseStoredGenomeForLowerIntensity` is off, so the
lookup intensity is always 4). No entry returns false and leaves `AIPilot` running.
An entry stops `AIPilot` (`StopAIPilot`, then `enabled = false`) before
`TrainingPilot` is added. Play intensity 4 sets `TrainingPilot.Intensity` to 4.
`IntensityDitherer` level 4 is identity: dropout, noise, reaction delay, and ability
skip are 0, and throttle scale is 1. The pilot still writes only sticks and
`PerformShipControllerActions` / `StopShipControllerActions`.

### Key configured values (as they stand on disk)

| Source | PopulationSize | EliteCount | MaxEpisodeSeconds |
|---|---|---|---|
| `Scenario_HexRace.asset` | **3** | **2** | **20** (Min 5) |
| `TrainingModeCatalog` (HexRace row, per the catalog test) | 24 | — | 120 |
| `SessionState.asset` saved population | 24 | 4 | — |

`Scenario_HexRace` at population 3 / 20 seconds is the loop proof, not a trained champion. It shows the trainer can launch, fly, score, and advance a generation. It is not a genome that has raced, and it is not a claim that the AI beats a human.

Other `Scenario_HexRace` values:

| Field | Value |
|---|---|
| NumericMutationRate | 0.3 |
| NumericMutationStrength | 0.18 |
| StructuralMutationRate | 0.04 |
| NoveltyWeight | 0.15 |
| OpponentCount | 3 |
| OpponentsUseTrainedGenome | 0 |
| UseResetForReplay | 1 |
| DelayBetweenEpisodes | 1 |

`TrainingControl` has `WatchdogSeconds: 120` and `TargetEpisodes: -1` (unbounded).

### State of `SessionState.asset`

| Field | Value |
|---|---|
| ScenarioKey | `Squirrel_HexRace_I4` |
| LastWriteUtc | 2026-09-29T13:21:43Z |
| EpisodesCompleted / EpisodesRequested | 0 / 0 |
| Generation / evaluationsThisGen / nextCheckoutIndex | 0 / 0 / 0 |
| Genomes | 24, every one with EvaluationCount 0 and Fitness 0 |
| HallOfFameBestFitness | −Infinity |

So no scored rollout has been saved.

The saved population size (24/4) does not match the scenario (3/2). There are two possible
explanations, and I did not confirm either:
- The runner's `overrideScenarioDefaults` path, which defaults to 24/4, was used.
- The state was written by something other than a Learn run using this scenario.

**The whole `Assets/_SO_Assets/AI Training/` folder is untracked in git**, including the
scenario, profile, archive, state and control assets.

---

## 2. Training approach

### Genetic algorithm (`TrainingPopulation`)

- **Selection:** population members are ranked by `Fitness + NoveltyScore × noveltyWeight`.
  - The top `eliteCount` members carry over unchanged. The field default is 4; the scenario
    sets 2.
  - The remaining slots are filled by tournament selection with `tournamentSize` 3.
- **Crossover:** takes two tournament winners and produces a child genome.
- **Mutation:**
  - Numeric: each gene is perturbed with probability rate 0.3 by an amount scaled by strength
    0.18.
  - Structural: rate 0.04.
- **Fitness bookkeeping:** after each generation, non-elite fitness is reset to 0, so every
  member is re-evaluated.
- **Novelty:** the archive holds up to 256 behavior hashes. A genome's score is its mean
  Hamming distance to that archive, weighted by 0.15.

### HexRace fitness recipe (`FitnessProfile_HexRace`)

| Component | Weight | Raw value |
|---|---|---|
| Crystals collected | 100 | count |
| GolfScore | 0.1 | HexRace score, **negated** (golf rules: lower is better) |
| TimePenalty | 1 | −EpisodeTime (seconds) |

### Input-only pilot (`TrainingPilot`)

- The pilot's only outputs are `IInputStatus` stick values and
  `PerformShipControllerActions` / `StopShipControllerActions`, which is exactly what a human
  controls.
- It has no write access to `Course`, `SetPose`, teleport, `SetInitialSpeed`, rigidbody,
  transform or score.
- `InputOnlyContractTests` enforces this.

---

## 3. Known blockers

1. **The harvest fix is tested but has not been confirmed in a live run.**
   - Previously, `EndEpisodeInternal` read fitness *after* `pilot.EndEpisode()` had cleared
     the context, so every rollout recorded a total of 0.00.
   - The reorder (`GetCurrentContextOrNull()` before `EndEpisode()`) is in the working tree.
     It is uncommitted and covered by two new tests, both passing:
     `EndEpisode_ClearsContextThatHarvestMustReadFirst` and
     `FitnessHarvest_ReadsContextBeforeEndEpisode`.
   - No Learn run since the fix has shown non-zero fitness in the console or in
     `SessionState`.
2. **The scenario's sizes are proof-of-concept sizes, not training sizes.**
   `Scenario_HexRace` is 3 pilots / 20 s, while the catalog expects 24 / 120.
   - A 3-member population with 2 elites has one non-elite slot per generation, which leaves
     almost no selection pressure.
   - 20 s is too short to complete a Skim Race lap at intensity 4.
3. **`Catalog_EveryLiveMode_HasScenarioAndProfileMatchingApplyFor` fails** with
   `HexRace Expected: 24 But was: 3`. This is the asset mismatch from item 2. It was
   deliberately left unfixed; the asset has not been changed.
4. **A 20 s timeout with zero crystals flattens the fitness landscape.**
   - When no pilot collects a crystal, the score is dominated by TimePenalty ≈ −20, and every
     pilot lands on roughly the same value.
   - At that point the GA is ranking mostly on novelty and noise, not on racing.
5. **No playtest against a human.** The README playtest matrix still reads "Not flown", and
   the overnight queue has not been flown either. Nothing shows this AI beats Garrett, or any
   human, in Skim Race.
6. **Nothing is deployed.** The Archive is empty, and the hall of fame is empty, so no
   export file was written (see §1). `Docs/SKIM_RACE_GARRETT_TEST.md` is the match a
   human can start anyway; the AI seats in that match are stock `AIPilot`.
7. **The assets are not in version control.** Everything under `Assets/_SO_Assets/AI Training/`
   is untracked, so the configuration would not reach another machine.

Deferred items carried from the consolidation doc:
- `PrismSensor` uses `OverlapSphere` instead of `PrismSpatialIndex`.
- There is no separate GA flag apart from `IsTraining`.
- `Debug.Log` traces remain instead of `CSLogChannel`.

---

## 4. Failed approaches

- **Reading fitness after the context was cleared.** Every episode scored 0.00, so the GA was
  effectively selecting at random. Fixed in the working tree (§3.1).
- **Treating the 3-pilot / 20 s proof run as training.** That configuration proves only that
  the loop mechanics work: launch, pilots, eval counter, replay. It cannot produce a
  competitive racer. The README itself says fitness will be near zero at that size.
- **Any Course / teleport / pose / score write.** These are banned. They would let the AI
  "win" without flying the vessel the way a human does, and `InputOnlyContractTests` rejects
  them. Do not reintroduce them.

---

## 5. Next steps (in order)

1. **Export and document (Prompt 2) — done, with nothing to export.**
   - Archive and hall of fame are both empty. `Exports/SkimRace_Squirrel_I4.json` was not
     created. SessionState and Archive were not overwritten.
   - Garrett’s match steps are in `Docs/SKIM_RACE_GARRETT_TEST.md`. Until an archive entry
     exists, that match is against `AIPilot`.
2. **Verify (Prompt 3) — done.** Editor `6000.3.17f1` compiled and ran the named
   tests. Six passed. `Catalog_EveryLiveMode_HasScenarioAndProfileMatchingApplyFor`
   failed as expected (`HexRace Expected: 24 But was: 3`). `Scenario_HexRace` was
   not edited. Full list: §6.
3. **Commit and push the fallback package (Prompt 4).** This covers the harvest fix, the
   tests, this doc, and the `AI Training` assets folder.
4. **Real Learn run (Prompt 5).**
   - Scenario at 24 population / 4 elites / 120 s. This needs explicit sign-off to edit
     `Scenario_HexRace`, and it also clears the Catalog test failure.
   - Confirm non-zero, rising fitness across generations.
   - Promote the best genome into the Archive under `Squirrel_HexRace_I4`.
   - Confirm it is installed in normal play at intensity 4.
   - Commit.
5. **Validate against Garrett.** Run repeated normal-rules Skim Races and record the results.
   Until that happens, make no claim that the AI wins.

---

## 6. Editor verification (Prompt 3)

Run in the open editor on 2026-09-29. `GET /api/editor_status` reported
`status: ready`, `compiling: false`, `playMode: stopped`,
`unityVersion: 6000.3.17f1`, project `/Users/studyholic/Cosmic-Shore`.

`unity command run_tests` was refused: this Editor's Unity Pipeline package is
too old to parse command lines. The tests were posted to the local exec API
as edit-mode `run_tests` with a `testName` filter. `com.unity.pipeline` was
not upgraded. Each filter matched exactly one test.

| Test | Result |
|---|---|
| `EndEpisode_ClearsContextThatHarvestMustReadFirst` | Passed |
| `FitnessHarvest_ReadsContextBeforeEndEpisode` | Passed |
| `Population_EvolveMonotonicInExpectation` | Passed |
| `Loop_ThreeMatchesAtPopulationSize_IncrementsGeneration` | Passed |
| `InputOnly_PilotPoliciesAndSensors_DoNotCallBannedApis` | Passed |
| `HexRace_ScoreFromRoundStats_IsNegated` | Passed |
| `Catalog_EveryLiveMode_HasScenarioAndProfileMatchingApplyFor` | Failed |

Catalog failure message: `HexRace Expected: 24 But was: 3`. That is
`Scenario_HexRace.PopulationSize` still at 3. The asset was not changed.

The two harvest tests passing in this editor is the compile-and-load proof
that the reorder (`GetCurrentContextOrNull()` before `EndEpisode()`) is in
the running domain. It is still not a live Learn run: `SessionState` remains
at 0 episodes and fitness has not been shown non-zero in play.
