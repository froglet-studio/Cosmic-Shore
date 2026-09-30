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

## 0f. Playtest follow-up — one AI at 0 / collection too slow (IN SOURCE; playtest pending)

### Prompt 4 verification attempt — 2026-09-30 (BLOCKED; no new race measurements)

Requested setup: normal arcade Skim Race, Squirrel, intensity 4, 4 players,
`DeployArchiveInNormalPlay: 1`, no Learn or "Play against trained AI".
The host may idle after the separate human Blue-pickup check.

**No race was run in this attempt.** `unity pipeline list --json` found the
project's running Editor (PID 7721) with Pipeline `0.8.0-exp.1`, but reported
`pipelineServer.isReachable: false` and `reachableServers: 0`. Both
`unity status` and `unity command` could not connect, including a status check
outside the filesystem/network sandbox. An attempt to open the project did
not establish a reachable connection. Runtime measurements are unavailable;
the V1–V3 results below are historical, before the Blue collection fix.

| Required measurement | Measured count / time | Verdict |
|---|---|---|
| Human after flying through a Blue crystal | Not measured | Pending |
| Each AI seat's crystals at race finish | Not measured | Pending |
| Each AI seat's crystals at 70 s; race finish time | Not measured | Stretch pending |

**Collection change present in the working tree: TeamCrystal Blue rule;
no prefab swap.** `TeamCrystalImpactor.AdmitsShipDomain` delegates to
`Crystal.IsCollectableBy` (Blue OR own domain). `MinigameSkimRace` still
references `BigCrystalVariant` (guid `fced5b15da3ad1d41b4e3b7385609e37`),
with `spawnCrystalWithPlayerDomain: 0` and 8 extra crystals. This is source
evidence, not a measured human pickup or confirmation that the running
Editor loaded the change.

Prompt 3 also added one null-Vessel warning per AI seat per turn while
readiness retries continue. The source assertions of
`TrainingDeployment_EnsuresAiReadyOnTurnStart` and
`TrainingPilot_SourceClearsStationaryDuringEpisode` passed; an Editor compile
and NUnit run were unavailable. Archive deployment is still enabled and the
archive contains the Squirrel (6), SkimRace (33), intensity-4 genome.

To close this bar, restore the Editor's Pipeline connection and run the
requested normal race. Record the human's before/after Blue pickup counts,
every AI seat's counts at 70 s and at finish, and elapsed turn time. An idle
host alone cannot establish the human flying-through pickup requirement.

### Shipping review — 2026-09-30 (NO-GO)

The source-only readiness assertions do not cover episode lifecycle. Review
found that `HandleTurnEnded` calls `EndEpisodes`, which clears `s_Installed`,
but `Update` continues to call `InstallOnAllAi` every 30 frames whenever
`ShouldDeploy` is true. That predicate checks deployment/training flags,
not whether a turn is running. `ArchiveDeployment.Install` calls
`BeginEpisode`, and the active pilot clears `IsStationary` each frame.
Consequently the next installation sweep can restart an ended episode.
Pair-init also calls `ReadyAllAiSeats` without a live-turn check. Shipping
requires explicit turn lifecycle ownership so ended/awaiting-ready seats
stay parked while late AI seats in a running turn still become ready.

The deployment service also has no player-build asset resolution:
`FirstAsset<T>` returns null under `#else`, so `Boot` exits for missing
control/GameData outside the Editor. This limitation predates this review;
the rewritten service still carries it. Provide runtime dependency wiring
before claiming normal-play archive deployment in a player build.

Evidence commands: `rg -n 'HandleTurnEnded|ShouldDeploy|InstallOnAllAi|s_Installed.Clear|FirstAsset|return null' Assets/_Scripts/Utility/AITraining/Pilot/TrainingDeploymentService.cs`
locates the ungated installation sweep, cache clearing and null resolver;
`rg -n 'BeginEpisode|IsStationary = false' Assets/_Scripts/Utility/AITraining/Pilot/ArchiveDeployment.cs Assets/_Scripts/Utility/AITraining/Pilot/TrainingPilot.cs`
locates installation's episode start and the active pilot's unpark write.

Remaining iterations before shipping:

1. Fix turn lifecycle ownership and verify turn end, pair-init before Ready,
   late pair-init during a turn, and a second turn.
2. Wire deployment assets for a player build.
3. Restore Pipeline connectivity, compile, run the named readiness tests and
   `TeamCrystalImpactorDomainTests`, then measure Prompt 4's human pickup and
   each AI seat's counts. The 70-second stretch remains an open benchmark.
4. Reconcile the branch with fetched `origin/bleeding-edge` (`5e7a37af4`,
   one base commit ahead at review), and keep unrelated Editor changes out
   of the shipping commits. The new base changes a Squirrel icon meta and
   build scene list that also have local modifications.

Tool-output review: no added/changed editor menu tools or Python/shell
generators were found in the reviewed branch commits or working diff.
The existing machine-local tool ledger names AI Training assets and build
settings, but is not evidence those dirty outputs have been committed.
No tool was retired. No commit, push or PR was made on this NO-GO.

Reusable skill candidate: word-presence readiness tests cannot prove that
turn end stays ended; trace the install cache, background sweep and
installation's `BeginEpisode` together. No new refactor rows were opened;
the two source findings above are correctness follow-ups, not cleanup.

**Symptom (Joseph playtest):** Jade ~3 crystals (human), one AI domain stuck at **0**, the other
collecting some crystals but nowhere near all-in-70s. FPS stall also reported.

**Root causes fixed in source (not yet re-raced):**

1. **Ruby-at-0 = `IsStationary` forever.** `ArchiveDeployment.BeginEpisode` never calls
   `StartPlayer` / `StartVessel`. When pair-init races ahead of Vessel wiring,
   `Player.StartPlayer` returns early with Vessel null and leaves `VesselStatus.IsStationary`
   true. `TrainingPilot.Update` used to early-return on that flag and `VesselTransformer`
   never `MoveShip`s — the seat sat at 0 crystals for the whole match.
   **Fix (layered):**
   - `TrainingDeploymentService` on `OnMiniGameTurnStarted` runs `EnsureAiPilotsReady`
     (burst retries) then `KeepAiPilotsReady` (1 Hz heartbeat until `OnMiniGameTurnEnd`):
     `StartPlayer`, force-clear `IsStationary`, `InstallOn` missing pilots, `BeginEpisode`.
   - `TrainingPilot.Update` clears `IsStationary` every frame while an episode is active
     (does not early-return). Mid-match re-freeze can no longer strand a seat.
   - `BuildContext` reads `Player.Domain` first so a null-Player Jade default cannot make a
     Ruby seat hunt Jade crystals that `CanBeCollected(Ruby)` refuses.

2. **Boost killed by own trail + archive engage window too tight.** Archive
   `boost.min_clear_distance` (~25) refused `FullSpeedStraight` whenever any prism sat in
   the forward arc — true on every skim-race line. Archive `lock_dot≈0.92` /
   `hold≈0.35` / `cooldown≈2.59` then kept boost brief even when clearance was skipped.
   **Fix:** `BoostManagementPolicy` skips the `NearbyPrisms` path-clearance loop when
   `TargetKind == Crystal`, and Decide overrides crystal chase to `lock_dot≤0.5`,
   `hold≥2.0`, `cooldown≤0.25` (GeneRegistry is first-register-wins, so Decide is the
   reliable path).

Also already landed earlier this session: `TargetSensor` uses `Crystal.Active` +
`CanBeCollected`; `ThrottleControl` skips forward-prism brake on crystal chase;
`InstallAfterFrame` sweeps **all** AI seats (not filtered by `OwnerClientNetId`).

`TrainingControl.DeployArchiveInNormalPlay` remains **on**. Unit coverage:
`TrainingDeployment_EnsuresAiReadyOnTurnStart`,
`BoostManagement_CrystalChase_SkipsPathClearance`,
`BoostManagement_CrystalChase_OpensEngageWindowAndHoldsLonger`,
`TrainingPilot_SourceClearsStationaryDuringEpisode`.

**Crystal farm (scene, 2026-09-29):** `MinigameSkimRace` had
`spawnCrystalWithPlayerDomain: 1` with `PlayerCountPlusExtra` + `extra: 0`, so a
3-seat match minted exactly one domain-locked crystal per `Players[i]` —
Ruby/Gold AIs could not share a free-for-all line (Ruby stuck at 0; Gold
slow-farming its own respawn). Flipped `spawnCrystalWithPlayerDomain: 0` and set
`extraCrystalsToSpawnBeyondPlayerCount: 8` (11 concurrent Blues on I4's 27
anchors).

**Correction (2026-09-30): that flip made collection IMPOSSIBLE for every pilot.**
The claim "Blue = any ship via `Crystal.CanBeCollected`" was wrong about which
rule gates a pickup. `CanBeCollected` is only what `TargetSensor` and the HUD
objective READ; the pickup itself is admitted by the crystal's impactor, and
`crystalPrefab` `BigCrystalVariant` (guid `fced5b15da3ad1d41b4e3b7385609e37`)
carries `TeamCrystalImpactor`, whose `IsDomainMatching` was a strict
`ownDomain == domain`. Blue never equals Jade/Ruby/Gold, so `AcceptImpactee`
refused every vessel while the AI kept steering at crystals it could not take,
and `SkimRaceObjectiveProvider` (which filtered `ownDomain != localDomain`)
never locked the HUD arrow at all. Fix: one rule, `Crystal.IsCollectableBy`
(Blue OR own domain), used by `CanBeCollected`, `TeamCrystalImpactor` and the
objective provider; **Blue on a `TeamCrystalImpactor` now means free-for-all**,
exactly as it does on `Crystal`. Domain-stamped team crystals (the Dolphin's
Claimed Seed) are unchanged. Test: `TeamCrystalImpactorDomainTests`.
**Playtest still required** to confirm pickup works and both AIs clear ≤70 s.

## 0e. Prompt 11 — verification races (FAIL on bar 2; bars 1 and 3 met)

**Result: FAIL.** Three normal arcade races: Skim Race, Squirrel, intensity 4, 4 players, Use
archive in normal play on, Store genome per intensity off. Learn and "Play against trained AI"
were not pressed. An editor script pressed Ready once, and the host seat (Joseph/Jade) sat idle.
The archive is unchanged: `Squirrel_SkimRace_I4`, generation 2, fitness 974.2953. Because of that,
`SKIM_RACE_GARRETT_TEST.md` is unchanged. Each seat was sampled every 10 s of turn time.

| Race | Winner | Score (= finish game s) | Winning seats' crystals | Winner at 70 s | Gold (AI) | Host Jade (idle) | AI seat stalled at 0? |
|---|---|---|---|---|---|---|---|
| V1 | Ruby | 439.81 | 47 + 7 = 54 | 9 + 5 = 14 | 13 | 1 | no |
| V2 | Ruby | 451.20 | 40 + 14 = 54 | 5 + 6 = 11 | 17 | 1 | no |
| V3 | Ruby | 476.30 | 41 + 13 = 54 | 4 + 11 = 15 | 11 | 1 | no |

Final crystal counts come from the end-of-race round stats. They can be one or two higher than
the last 10 s sample.

Bars:
1. **MOVING — met.** Every AI backfill seat had speed > 5 u/s in 98% of samples. The only miss
   is the t = 0 sample. Every AI seat had at least 1 crystal by t = 10–30 s and finished with 7–47.
   Ruby's median speed is 28–29 u/s on the lead seat and 40–44 u/s on the second. Gold's median is
   42–49 u/s, peaking at 91–254 u/s.
2. **FINISH ≤ 70 s — unmet in all three.** The best finish was 439.81 s, 6.3× the bar and 4× Garrett's
   110. At 70 s the winning domain had 11–15 of 54 crystals.
3. **ALL CRYSTALS (winning domain summed ≥ 54) — met.** Every winner summed exactly 54, but only at
   440–476 s. At 70 s the winners had 11–15, so the objective is met only because bar 2 is not.

These races are slower than B0 (322 s) and Prompt 7 (209 s) with the same genome and settings.
That spread, about 270 s across five races, is variance in the deployed pilot, not a regression.
No code changed between those races.

**Root cause (fixed after Prompt 11):** not a `TrainingPilot` collapse. Archive gen-2
`throttle.base` was **0.47435406** with `throttle.ramp_per_second = 0`, so `Decide` emitted
exactly that stick value → Squirrel `XDiff × DefaultThrottleScaler(60) ≈ 28 u/s` (matches the
sampled medians). Ram (`DotForwardObjective ≥ ram_dot`) is the only path that wrote 1.00.
`TrainingPilot` already maps `Throttle` → `XDiff` 1:1.

**Fix:** `ThrottleControlPolicy` floors `throttle.base` at **0.85** (default 0.95); `TrainingGenome.Get`
clamps archived chromosomes on read; Archive / SessionState / export patched to **0.95**. Re-verify
finish time before more Learn — fitness may now score a faster pilot than gen-2's crawl chromosome
was worth.

## 0d. Prompt 10 — approach quality (FAIL, change reverted, Learn NOT re-run)

**Result: FAIL.** One lever was tried, measured in one race and reverted. No policy change is
kept. Learn was not re-run, because flight did not get saner. The archive is still generation 2,
fitness 974.2953. `InputOnlyContractTests` pass 3/3 after the revert.

Both races: arcade Skim Race, intensity 4, 4 players, archive on, per-intensity off. An editor
script pressed Ready, and the host (Jade) sat idle. Every AI seat was sampled every 10 s: crystals,
speed, stick, boost, and range/dot to the target.

| Race | Change | Winner score / finish | Ruby seats | Gold | Host |
|---|---|---|---|---|---|
| B0 | none (baseline) | 322.22 | 40 + 14 = 54 | **17** | 0 |
| B1 | approach brake in `ThrottleControlPolicy` | 375.86 (worse) | 32 + 22 = 54 | 7 | 0 |

- The approach-brake lever came from the prompt's lever 2. It added two genes,
  `throttle.approach_distance` (default 60) and `throttle.approach_dot` (default 0.7), that
  scaled throttle down when the target was inside that range and off the nose. B1 was 54 s slower,
  and Gold fell from 17 crystals to 7, so it was **reverted**. The turn-throttle cap and the
  deadzone coast-miss were not reintroduced.
- In both races every AI seat had speed > 0 all race and at least 1 crystal after the opening
  seconds. Gold was not stuck at 1: it collected 17 in B0. The B0 Gold seat still shows boost
  multiplier spikes of 1.5–2.3 with range 196–283 and dot −0.76 to 0.57, which is the overshoot
  from Prompt 7.
- The B0 baseline (322 s) is slower than Prompt 7's 209 s with the same genome. Single-race
  variance is about 100 s, so one race per change cannot resolve an improvement smaller than that.

Finding for the next prompt: **the throttle reaching the stick is almost binary.** In both races
every AI seat's throttle reads 0.47 or 1.00 in almost every sample. The only exceptions were
0.11 and 0.31 in B1. It barely changes with range or dot. 1.00 is the ram override
(`DotForwardObjective ≥ ram_dot`). The flat 0.47 means whatever `ThrottleControlPolicy`
computes below the ram threshold is replaced by, or blended into, a constant before it reaches
the stick. A throttle-side approach lever, and the throttle genes Learn searches, therefore have
almost no reach. The next step is to trace the `DecisionOutput.Throttle` blend in
`TrainingPilot` down to the stick. Nothing should be tuned until that trace is done. The Learn-side
finding from Prompt 9, that most genomes write 0 steer and 0 throttle, probably shares that path.

## 0c. Prompt 9 — Learn until training finishers exist (STOPPED after 3 generations, bar UNMET)

**Result: STOP.** Learn resumed from the saved session (generation 2, 72 evaluations) and ran
3 full generations: evaluations 73–144, generations 3, 4 and 5. It ran under the Prompt 8 signal
(240 s cap, early exit at CrystalsAtLeast 54, `Scenario_HexRace`, key `Squirrel_SkimRace_I4`) at
simulation time scale 8, with Use archive in normal play on and Store genome per intensity off.
**No evaluation reached 54 crystals, and none ended before the cap.** All 72 ran to
t = 240.0–240.4 s. Per the prompt, no policy, sensor or vessel code was touched, and the sidecar
export was not refreshed.

| Generation | Evaluations | Max crystals | Mean crystals | Evaluations at 0 crystals | Best rollout total |
|---|---|---|---|---|---|
| 3 | 24 | 28 | 1.9 | 18 | 2554.33 (Ruby, 28 crystals, t = 240.4 s) |
| 4 | 24 | 24 | 2.7 | 20 | 2136.20 (Ruby, 24 crystals, t = 240.0 s) |
| 5 | 24 | 22 | 2.3 | 18 | 1912.45 (Ruby, 22 crystals, t = 240.0 s) |

No generation improved on the one before it: the maximum fell from 28 to 24 to 22.

Evidence of why it stops — the pilots do not fly:
- **60 of the 72 rollouts never move the sticks.** Their summary line reads
  `mean|steerX|=0.00 mean|steerY|=0.00 meanThrottle=0.00` for the whole episode, and their first
  stick write at t = 28.9 s is `steer=(0.00,0.00) throttle=0.00`. These seats drift and score
  0 crystals, so selection sees a population that is mostly dead weight.
- The 12 rollouts that do fly (mean throttle 0.71–0.85, steer 0.27–0.58) collect 7–28 crystals
  in 240 s. The winning domain in a Skim Race is a *sum* of seats, and no single seat gets close
  to the 54 needed for the early exit. The same cause was measured in Prompt 7: the fast seats
  circle crystals, and 54 on one pilot inside 70 s needs roughly 2–4× the collection rate seen
  here.
- The Jade seat that does fly keeps steer near 0.03, so it throttles straight and does not track.

What changed on disk: **nothing from the Learn run survived.** In memory, the run ended at
144 evaluations, generation 5, hall of fame 1853.73, and the runner auto-deployed a generation-5
`Squirrel_SkimRace_I4` entry (fitness 1853.7297, not a finisher). The disk was full at the time
(about 170 MB free), and the Editor restarted afterwards. `SessionState.asset` and `Archive.asset`
on disk are still **72 evaluations, generation 2, fitness 974.2953**. Losing the generation-5
entry costs nothing, because it was not a finisher. The table above is from the Editor log and is
the only record of those 72 evaluations.
- `Exports/SkimRace_Squirrel_I4.json` was not re-exported. It still matches the generation-2
  archive entry.
- `TrainingControl.asset`: SimulationTimeScale 8, TargetEpisodes −1 (overnight).

Next single lever, for a later prompt (not done here): the zero-output genomes. Find out why
most of the population writes exactly 0 steer and 0 throttle from the first frame, and whether
that comes from a gate, a zeroed gain gene, or a sensor with no target. More generations under the
same population will keep selecting from 3 or 4 live pilots per generation.

## 0a. Prompt 7 — clear all crystals in ≤ 70 s (partial pass, bar UNMET)

**Result: FAIL** on both bars. The winning score was 209.09 (bar: ≤ 110), and the race
finished at 208.9 s game time (bar: ≤ 70 s). The archive is unchanged (gen 2, fitness
974.2953), so the export and `SKIM_RACE_GARRETT_TEST.md` are unchanged too.

Only one normal arcade race was run in this pass, not the ≥ 3 the prompt asks for.

| Race | Winner | Score | Finish (game s) | Ruby seats | Gold | Jade (idle host) |
|---|---|---|---|---|---|---|
| P7-1 | Ruby (Andrew) | 209.09 | 208.9 | 42 + 12 = 54 | 1 | 0 |

Code changes:
- **Kept:** `TargetSensor` now skips crystals that are still exploding. A collected crystal stays in
  `CellItems` while it explodes, so the pilot kept aiming at a point it had already passed.
  The change is input-only.
- **Tried, then reverted:** a turn-in throttle cap (`throttle.turn_rate_deg`) and a coast-miss
  check on the steering deadzone. In the measured race they did not cut the finish time.

No Learn pass was run in this prompt, because a new Learn pass would still score genomes against the
120 s cap that no episode finishes.

Blocking cause, from the per-seat samples taken every 10 s:
- **The Ruby seats are too slow.** They fly at 30–75 u/s and cover about 180 u per crystal
  (Andrew flew 7,545 u for 42 crystals). A 70 s finish needs about 27 crystals per seat at
  roughly 70 u/s, with no wasted distance. In the samples the seats often held a crystal at
  dot 0.0–0.7 inside 50–150 u, which means they were circling it.
- **Gold overshoots every crystal.** It stays boosted at 180–245 u/s even with the throttle
  stick at 0.24–0.47, so the throttle gene can't slow it, and it collected 1 crystal.

Next single lever: raise `Scenario_HexRace.MaxEpisodeSeconds` to 240 and add a
time-to-target term to `FitnessProfile_HexRace`. Selection can then see finishers and reward
speed. Only after that should Learn be resumed for several generations.

Prompt 8 (section 0b) made that signal change. Learn was not resumed.
Prompt 9 (section 0c) resumed Learn for 3 generations. No finisher appeared.

## 0b. Prompt 8 — unlock finishers in Learn (signal only, Learn NOT run)

**Result: signal updated. Learn was not pressed. No arcade race was run.** The archive is
still generation 2, fitness 974.2953. `TargetSensor`'s exploding-crystal skip is unchanged.
The turn-throttle cap and the steering deadzone stay reverted.

| | Before | After |
|---|---|---|
| `Scenario_HexRace.MaxEpisodeSeconds` | 120 | **240** |
| Early exit | none | **CrystalsAtLeast 54** (intensity-4 target), and only after AssignScores has written a loser sentinel |
| `TrainingControl.WatchdogSeconds` | 120 | **300** |
| Fitness terms | Crystals × 100, GolfScore × 0.1 (negated), TimePenalty × 1 | **same three terms** |

The watchdog is wall-clock and the control asset resolves time scale 0 to 1×. At 1× a 120 s
watchdog force-ends the episode at 120 s of game time, so the 240 s cap would never have been
reached. 300 s of wall clock sits above the new cap. At a later 8× scale the scenario cap is
still the one that binds.

No new `ComponentKind` was added. GolfScore is the winner's finish time once a domain finishes
(`AssignScores` writes that time; a loser is `10000 + crystals remaining`). TimePenalty is
`−EpisodeTime`. Together they are the finish-time term.

Arithmetic, using the live weights:

| Shape | Crystals | Golf raw | Time | Total |
|---|---|---|---|---|
| 54 crystals, finish 70 s | 5400 | 0.1 × (−70) = −7 | −70 | **5323** |
| 11 crystals, sit to 240 s, score still 0 | 1100 | 0 | −240 | **860** |
| 11 crystals, sit to 240 s, score = live clock | 1100 | 0.1 × (−240) = −24 | −240 | **836** |
| Hall of fame (11 crystals, t = 120 s) | | | | **974.2953** |

5323 beats 860, 836, and 974.2953. A 54-crystal finish at 70 s also beats the same 54 crystals
held to 240 s (5400 − 24 − 240 = 5136), so equal crystal counts prefer the faster finish.

The crystal term is this pilot's own count, not the domain sum. A seat that finishes the domain
while personally collecting only a handful of crystals can still lose to a cap-sitter with more
personal crystals (one crystal is worth 100 s of time penalty). The comparison this prompt
requires — 54 at ≤ 70 s against 11 at 240 s — is won by the existing terms.

`CheckEarlyExitConditions` used to return immediately for every golf mode, so an authored
crystal gate never ran. It now waits until some pilot's score is at least the loser sentinel
(10000), which is written in the same pass as the winner's finish time, and then honours
CrystalsAtLeast. A 42 + 12 domain finish never puts 54 on one pilot; that rollout still closes
through `OnMiniGameEnd`, which already runs after AssignScores. The 240 s window is what lets
that close happen at all.

`ApplyCatalogDefaults` stamps 240 s and the crystal gate on the HexRace row only. Other catalog
rows still clear early exits and keep their 120 s cap.

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
camera rendering on. The watchdog was left at 120 then; Prompt 8 raised it to 300
(section 0b) so a 240 s episode is not cut off at 1× time scale.

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
| `Scenario_HexRace.asset` | **24** | **4** | **240** |
| `TrainingModeCatalog` HexRace row | 24 | — | 240 |

Vessel Squirrel (6), mode HexRace (33), intensity 4. The old 3 / 2 / 20
numbers were the reload-loop proof. They live in the YAML copies above.
The live scenario is the catalog contract.

Other scenario fields were left as authored: numeric mutation 0.3 / 0.18,
structural mutation 0.04, novelty weight 0.15, opponent count 3,
`OpponentsUseTrainedGenome` off, `UseResetForReplay` on, minimum episode
5 seconds.

`TrainingControl` after the run: `DeployArchiveInNormalPlay` 1,
`UseStoredGenomeForLowerIntensity` 0, `TargetEpisodes` −1,
`WatchdogSeconds` 300, `AutoStartOnPlay` 0, `HumanPlaysThisLaunch` 0,
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

Best-genome arithmetic from the scored generation: 4 × 100 + 0.1 × (−28.21) + (−120.08) = 277.10.
The hall of fame is a later cap eval (11 crystals, t = 120 s, fitness 974.2953). Prompt 8's
finisher arithmetic is in section 0b: 54 crystals at 70 s scores 5323 on this same recipe.

---

## 3. What is still open

1. **No domain has finished inside a Learn episode yet.** The archive is
   still the 120-second generation. Prompt 8 raised the cap to 240 s and
   added the crystal early-exit, and Learn has not been run on that signal.
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
7. Prompt 8 raised the HexRace episode cap to 240 s, set a 54-crystal
   early exit, and raised the watchdog to 300 s so the cap can be reached
   at 1× time scale. Fitness stayed on the three existing terms. Learn was
   not run.
