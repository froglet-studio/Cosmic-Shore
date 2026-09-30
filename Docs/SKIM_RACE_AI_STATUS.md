# Skim Race AI — Status Report

Snapshot of branch `feat/ai-genetic-training` after the Prompt 6 run of
2026-09-29 (Editor `6000.3.17f1`). The population is at generation 2
(72 evaluations), and that genome is deployed. Normal arcade races now
finish, but **Garrett's bar is unmet**: no race scored 110 or better. The
best winning score was 207.00. The 70-second target for collecting all
crystals is also unmet; the fastest finish took 206.8 s. Garrett has not
flown this archive, so this document does not record a human result, and
it does not claim the AI beat Garrett.

Skim Race is `GameModes.HexRace (33)` in `MinigameSkimRace`. The vessel is
locked to Squirrel. The training key is `Squirrel_SkimRace_I4`.

---

## 0. Prompt 6 — measured against Garrett's bar

Garrett's bar is a Skim Race score of 110 or better (golf rules: the score
is the finish time, and lower is better), or a win against Garrett. The
training fitness is not that score.

### Steering fix found before the run

The user tested the generation-1 archive before this prompt. The AIs were
not idle; they flew past the crystals. The cause was in
`TargetSeekingPolicy.Decide`. It divided the steering cross product by the
raw squared range to the target. `AIPilot` caps that divisor at its
`_maxDistance` (50). Without the cap, a crystal 100 units away produced
less than a degree of stick input. The policy now clamps the divisor to
`[1, 50]` (`TargetSeekingPolicy.MaxSteerDivisor`). This is still
input-only: sticks only, no course, pose or score writes.

### Learn pass

Learn stayed on. Squirrel, intensity 4, population 24, elite 4, episode cap
120 s, time scale 8. The scenario key was `Squirrel_SkimRace_I4`. The pass
resumed from the saved session at 48 episodes / generation 1 and stopped
after 24 more evaluations.

| Field | Value |
|---|---|
| Episodes completed | 72 (48 → 72) |
| Generation | 2 |
| Hall-of-fame fitness | **974.2953** (evaluation 61, Ruby, 11 crystals, t = 120.0 s) |
| Crystals per evaluation, this generation | Ruby 2–11, Gold 5–10, Jade 0–1 |
| Archive entry | Squirrel / SkimRace (33) / intensity 4, fitness 974.2953, generation 2, notes `Deployed after 72 episodes` |
| Export | `Assets/_SO_Assets/AI Training/Exports/SkimRace_Squirrel_I4.json` (re-exported) |
| Proof YAML | untouched |

Every evaluation still ended on the 120 s cap. No training episode reached
the 54-crystal target.

### Normal arcade races

These were run the way Garrett will run them. Arcade Skim Race card,
Squirrel, intensity 4, player count 4 (1 human + 3 AI backfill), **Use
archive in normal play** on, **Store a genome per intensity** off.
`IsTraining` was false. Learn was not pressed, and neither was **Play
against trained AI**. Every AI seat logged `[Deploy] … flies the archive
(Squirrel, SkimRace, play intensity 4)`. Crystal target: 54. An editor
script launched each race and pressed Ready once. **The host seat
(`Joseph`) had no input**, so its results are not a human result.

Results with the generation-2 archive, after the steering fix:

| Race | Winner | Winning score | Ruby AI seats (crystals) | Gold AI seat | Host (Jade) | AI stalled at 0? |
|---|---|---|---|---|---|---|
| 1 | Ruby | 223.73 | 36 + 18 = 54 | 2 (score 10052) | 1 (10053) | No |
| 2 | Ruby | 207.00 | 41 + 13 = 54 | 4 (10050) | 0 (10054) | No |
| 3 | Ruby | 208.26 | 37 + 17 = 54 | 3 (10051) | 0 (10054) | No |

Races 1 and 2 took 223.5 s and 206.8 s of game time. Race 3 took 208.4 s.

Results with the generation-1 archive, before the fix (fitness 277.0955,
capped by the script at 240 s):

| Race | Finished | AI crystals | Score at cap | AI stalled at 0? |
|---|---|---|---|---|
| A | No | 0, 0, 0 | 240.02 (race clock) | Yes. All three AIs were moving (speed ≈ 30) but collected nothing. |
| B | No | 1, 1, 1 | 239.92 (race clock) | No, 1 each |

**Verdict: benchmark unmet.** Scores after the fix: 223.73, 207.00, 208.26.
None of them is 110 or better, and no race finished within 70 s.

### What the races show

- The two Ruby seats share a domain, so their crystals add up toward 54.
  One seat carries the load (36–41) and the other collects 13–18.
- The lone Gold seat collected only 2–4 crystals per race, at logged
  speeds of 178–260. The Ruby seats flew at 30–67. The Gold seat appears
  to be boosting past crystals it cannot turn into. This is observed, not
  yet diagnosed.
- In training, the third seat (Jade) collected 0–1 crystals in every
  match. A genome evaluated on that seat scores about −129 regardless of
  its genes, which adds noise to selection.

---

## 1. The generation-0 run (earlier record)

Learn was pressed with the `AITraining` log channel on. The host seat
`Joseph` was on autopilot. The runner pressed the public Ready button
(`MiniGameControllerBase.OnReadyClicked`), the countdown started, and the
ships flew through `TrainingPilot` only.

| Field | Value |
|---|---|
| Generation | 0 (`evaluationsThisGen` 24; `Evolve` did not run) |
| Episodes completed | 24 finished evaluations (8 matches × 3 genomes) |
| Best fitness | **277.09552** |
| Best rollout | generation 0, evaluation 5, domain Gold, 4 crystals, t = 120.1 s |
| Breakdown | `Crystals=400.0(4.00) GolfScore=-2.8(-28.21) TimePenalty=-120.1(-120.08)` |
| Any crystal collected | Yes. 11 of 24 evaluations collected at least one. The most in one evaluation was 4. The 24 evaluations together collected 15. |
| SessionState LastWriteUtc | 2026-09-29T15:08:32Z |
| Archive entry | Squirrel / HexRace / intensity 4, fitness 277.09552, generation 0, notes `Auto-deploy after 24 episodes` |
| Export | `Assets/_SO_Assets/AI Training/Exports/SkimRace_Squirrel_I4.json` |

`TrainingGenome.Clone()` zeros `Fitness`, `EvaluationCount`, and
`NoveltyScore` on the copy stored inside the archive entry and the hall of
fame. The score that deployment and this document use is the archive
**entry** fitness, 277.09552, which matches the population member
`ReturnFitness` updated (evaluation count 1, novelty 15.75). The JSON
carries that population member, genes included. The genes in the archive
match it.

The three fitness terms were not tied. Totals in this generation ran from
277.10 down to about −131.84. No fitness component was added. The profile
is still crystals × 100, negated golf score × 0.1, and time penalty × 1.

`Population.generation` stayed 0 because the session stopped when
`EpisodesCompleted` hit the operator target of 24, which is the end of
generation 0's evaluations and before the next checkout, where `Evolve`
breeds generation 1.

### Why the finish benchmark is unmet

Every one of the 24 evaluations ended at about 120.0–120.3 seconds of
**game** time. The trainer's episode cap fired. No `[TIMEOUT]` watchdog
line was logged, and no episode ended because a domain reached the crystal
target.

Intensity 4's authored target is 54 crystals (27 waypoints × 2 laps;
`EndConditionOverrides` leaves HexRace on that auto-calc). The best single
pilot collected 4. In that same match the other two seats collected 1 and
0, on other domains. A domain sum of 4 is short of 54.

Golf numbers in the rollout lines are the live race clock HexRace writes
onto round stats during the race, then negated. They are small and they
grew across the session. They are not a winner's finish time and they are
not the 10000-plus-remaining loser score, which is written only when a
domain actually finishes. The cap ended the match while the race was still
open.

The other candidate explanations, checked against this log:

- **Host seat not on autopilot.** False for this run. The launcher logged
  `Host seat 'Joseph' is on autopilot for this rollout.`
- **All pilots tied.** False. Fitness spread from 277.10 to about −131.84,
  and crystal counts were 0, 1, 2, or 4.
- **Archive not installed for a normal match.** False after the save.
  `Archive.asset` holds `Squirrel_SkimRace_I4`. `DeployArchiveInNormalPlay`
  is on. Intensity 4 flies that genome with no dither. Garrett's steps are
  in `Docs/SKIM_RACE_GARRETT_TEST.md`. Those races have not been played
  here.

`SimulationTimeScale` was 8 for the run (the clamp ceiling). The `t=120s`
figures are game time, about 15 wall-clock seconds per episode. The whole
generation took about two minutes of wall clock. Those operator fields were
put back afterwards: time scale 0, target episodes −1, mute audio off,
camera rendering on. `WatchdogSeconds` stays 120.

### The discarded zero run

An earlier Learn pass in the same editor session recorded nine evaluations
at total 0.00 with an empty-looking breakdown (crystals 0, time 0) at
t = 120. HexRace sits on Ready until a human presses it. With nobody
pressing it, every vessel stayed stationary, `TrainingPilot` returned
before it built a context, and `EpisodeTime` stayed 0. That pass was wiped
by copying the population-3 / 20-second proof YAML back onto
`SessionState.asset` and `Archive.asset` before the scored run. The proof
copies themselves were kept:

- `Assets/_SO_Assets/AI Training/Exports/SessionState_proof_pop3_20s.yaml`
- `Assets/_SO_Assets/AI Training/Exports/Archive_proof_pop3_20s.yaml`

The scored run is the one in the live assets. Two additions made it able
to score: the runner presses Ready once per episode (the same public button
the HUD wires; no score write, no crystal RPC, no course write), and
`EpisodeTime` is filled from the episode clock when the pilot never stamped
it. Harvest order is unchanged: the context is read before `EndEpisode()`.

---

## 2. What is configured now

| Source | PopulationSize | EliteCount | MaxEpisodeSeconds |
|---|---|---|---|
| `Scenario_HexRace.asset` | **24** | **4** | **120** |
| `TrainingModeCatalog` HexRace row | 24 | — | 120 |

Vessel Squirrel (6), mode HexRace (33), intensity 4. The old 3 / 2 / 20
numbers were the reload-loop proof. They live in the YAML copies above.
The live scenario is the catalog contract.

Other scenario fields were left as authored: numeric mutation 0.3 / 0.18,
structural mutation 0.04, novelty weight 0.15, opponent count 3,
`OpponentsUseTrainedGenome` off, `UseResetForReplay` on, minimum episode
5 seconds.

`TrainingControl` after the run: `DeployArchiveInNormalPlay` 1,
`UseStoredGenomeForLowerIntensity` 0, `TargetEpisodes` −1,
`WatchdogSeconds` 120, `AutoStartOnPlay` 0, `HumanPlaysThisLaunch` 0,
`SimulationTimeScale` 0.

### Input-only pilot

`TrainingPilot` writes `IInputStatus` sticks and
`PerformShipControllerActions` / `StopShipControllerActions`. It does not
write `Course`, pose, teleport, speed, rigidbody, transform, or score.
`InputOnlyContractTests` enforces that. HexRace rules, the crystal target,
and golf scoring were not changed.

### Fitness recipe (`FitnessProfile_HexRace`)

| Component | Weight | Raw value |
|---|---|---|
| Crystals collected | 100 | count |
| GolfScore | 0.1 | HexRace score, negated |
| TimePenalty | 1 | −EpisodeTime (seconds) |

Best-genome arithmetic: 4 × 100 + 0.1 × (−28.21) + (−120.08) = 277.10.

---

## 3. What is still open

1. **No domain finished a race.** The 120-second cap arrived with the best
   pilot at 4 crystals and the intensity-4 target at 54. A longer episode,
   or more generations of the same 120-second episodes, is a later
   decision. This generation did not breed a second one.
2. **Garrett has not flown the archive.** A normal Skim Race at intensity 4
   is how that gets recorded. See `Docs/SKIM_RACE_GARRETT_TEST.md`.
3. **Prompt 3's catalog failure is stale.** That run saw
   `Scenario_HexRace.PopulationSize` at 3. The asset is now 24. The catalog
   test was not re-run after the change.
4. Deferred from the consolidation notes, still true where they were true
   before this run: `PrismSensor` uses `OverlapSphere`; there is no GA flag
   separate from `IsTraining`. Learn rollouts now go through
   `CSLogChannel.AITraining` (off unless the Learn hook or the logging
   window turns the bit on).

---

## 4. Failed approaches

- **Reading fitness after the context was cleared.** Every episode scored
  0.00. Harvest now reads the context first. The scored run's breakdown
  lines are the live proof.
- **Leaving HexRace on Ready.** Nine evaluations scored 0.00 because the
  ships never left the stationary gate. Ready is now pressed by the
  trainer. Those nine scores were discarded; the proof YAML is the record
  of the session that existed before that pass.
- **Treating 3 pilots / 20 seconds as a champion.** That size proved the
  reload loop. The live scenario is 24 / 120.
- **Course, teleport, pose, or score writes.** Banned. They would record a
  result the vessel did not fly.

---

## 5. Prompt log

1. Status report written from the then-empty archive.
2. Garrett's match doc written while the archive was empty.
3. Editor `6000.3.17f1` compiled. Six named tests passed. The catalog test
   failed at population 3, which was the asset at that time.
4. The fallback package was committed locally as `de854a9fe`. The push in
   that pass failed authentication. This document's later commit is the
   one that carries the trained archive.
5. Scenario set to 24 / 4 / 120. One generation of 24 evaluations
   saved. Best fitness 277.09552. Crystals were collected. The finish
   benchmark was unmet because every episode hit the time cap first.
6. Steering divisor capped in `TargetSeekingPolicy`. Learn resumed from
   48 to 72 episodes (generation 2, fitness 974.2953), then deployed and
   re-exported. Three normal arcade races finished with Ruby winning at
   223.73 / 207.00 / 208.26. Garrett's bar (110) and the 70-second target
   are both unmet.
