# Arcade match length: every end-game target aimed at ~90 seconds

Garrett, 2026-10-26: "estimate new score requirements etc. for arcade games such that experienced players are
finishing at around the 90 seconds mark", then "apply all". This file is the record of what was measured, what
changed, and what is still an estimate. The targets themselves live where they always do
(`Assets/Resources/EndConditionOverrides.asset`, edited through FrogletTools > Game Modes > End Game Conditions,
`/EndGameConditions` skill); this is the reasoning behind the numbers.

## 1. Where the times come from

- **Measured:** the 206 editor flights `HumanFlightRecorder` sent to `origin/bugledger/*`
  (`BugLedger/shared/reports/<user>/flights/flight_<Mode>_*.json`, read with `Tools/Build/learn_from_flights.py`'s
  `flights_on_branches()`). Each carries `raceSeconds`, `finished`, `raceTarget`, the end-of-match `result` table
  (who won, at what time, what everyone else had left) and a 10 Hz `rings` column, so a gate race gives a time to
  the first gate and a seconds-per-gate pace even when the pilot did not finish. Ledger names: `yskha` = Yash,
  `gradi` = Garrett, `belac` = Caleb, `shombith` = Shombith; `owner` and `mrglo` are not in `Docs/TEAM.json`.
- **Modelled:** modes with no finished flight were estimated from their own rules and numbers (arena supply, spawn
  rates, hull speeds, weapon rates), by reading the mode code, cards, generators and budget scripts. Those rows say
  "estimate" and carry low confidence: **they want one timed playtest each**.
- "Experienced" = the team's faster runs, which are mostly intensity 1. One target serves every intensity except
  where a per-intensity list exists (Cleave, now Rampage, and SkimRace's laps), so higher intensities still run
  longer: Yash took 2.6-3.6 s a gate in Switchback at I1 and 12-14 s at I4.

## 2. What changed

| Mode | What it counts | Before | After | Experienced time before -> after | Basis |
|---|---|---|---|---|---|
| Switchback | gates (1 lap; more gates = longer course) | 20 | **26** | I1 58-75 s -> ~85 s | measured, 15 finishes |
| Thresher Time | laps x 8 gates | 3 laps = 24 | **2 laps = 16** | 127-180 s -> ~75-125 s | measured, 2 finishes |
| Waystation | rings (dealt in clusters; more = longer) | 24 | **15** | 205 s -> ~90-130 s | measured, 1 pilot |
| Grizzly Time | laps x 8 gates | 3 laps = 24 | **2 laps = 16** | unfinishable today (4 gates in 268 s) | see §4 |
| Headlong | laps x 8 gates | 3 laps = 24 | **5 laps = 40** | ~40-55 s -> ~70-90 s | estimate (Regatta transfer) |
| Redline | laps x 8 gates | 3 laps = 24 | **5 laps = 40** | ~40-75 s -> ~70-120 s | estimate (Regatta transfer) |
| SkimRace | crystals = laps x crystals a lap (`lapsPerIntensity`) | [3,3,2,2] -> 24/30/56/52 | **[3,3,1,1] -> 24/30/28/26** | I3 ~200 s, I4 ~145 s -> ~75-100 s | AI benchmark + one I3 race (AI won at 199 s) |
| Scurry (Crystal Capture) | crystals | 20 | **14** | ~100-140 s -> ~90 s | estimate; 9 runs left unfinished at 28-97 s |
| Joust | jousts | 3 | **2** | not reached by 144 s | estimate |
| Dog Fight | points | 90 | **40** | not reached in 63-180 s | estimate |
| Grizzly Charge | points (Dog Fight's monitor and price list) | 90 (Dog Fight's key) | **90, its own key** | median 93 s over 7 matches (unchanged) | measured |
| Dustup | dustings | 10 | **4** | not reached by 211 s | estimate |
| Scarab Scramble | goals | 10 | **3** | not reached by 148 s, or by 748 s | estimate |
| Tollway | tolls | 4 | **3** | ~120-240 s | estimate |
| Wrecking Ball | hostile prisms | 1500 | **650** | not reached by 178 s solo | estimate |
| Hijack | steals | 750 | **500** | not reached by 167 s (human + 2 AI) | estimate |
| Sirocco | hostile prisms | 600 | **400** | ~120-180 s | estimate |
| Cleave | hostile prisms, I1/I2/I3/I4 | 1200/1200/1500/1500 | **950/950/1000/1000** | I4 152.7 s -> ~100 s | measured at I4 (one solo win) |
| Rampage | hostile prisms, now per intensity | 2000 | **2000/1700/1300/1000** | I4 ~3-4 min -> ~90 s | estimate (forest is ~5x smaller at I4) |
| Bloomrush | timed round | 120 s | **90 s** | 120 -> 90 | by definition |
| Tapestry | timed round | 150 s | **90 s** | 150 -> 90 | by definition |

**Kept**, because they already land near 90 s or no evidence says otherwise: Breakwater (29 crossings, 84-89 s at
I1), Warpline (10 gates, median 90 s over 11 runs), Wrasse Run (16, 94 s and AI winners at 91-108 s), Slingshot (16,
~90-110 s by transfer from Warpline), Bends (3), Undertow (12), Salvo (700, possibly already under 90 s; time it
first), Wildlife Liberation (30), Maelstrom (6: it is a 3-6 game series, not one match).

## 3. How each change was made

- **Through the generator, never the asset alone.** Every mode with an `author_<mode>_assets.py` writes its target
  back into the asset when it runs, so each change moved the generator constant, `EndConditionOverridesSO`'s
  `Default*` constant and its field initialiser, the asset's Live AND `*Build` values, and was re-run so the cards,
  scenes and derived numbers (Tollway's phase thresholds, Tapestry's comeback rate) follow. All 46
  `author_*_assets.py --check` pass, as do `cleave_budget.py`, `waystation_course.py --target 15`,
  `hijack_budget.py`, `skein_budget.py`, `broadside_balance.py`, `arcade_mode_lib.py --self-test`,
  `author_skimrace_relativity_track.py --check`, `check_console_logging.py` and
  `check_conditional_compilation.py`.
- **Circuits change LAPS, not ring count.** Headlong, Redline, Thresher Time and Grizzly Time lay
  `ceil(target / laps)` rings on a fixed-radius loop, so a lower target with the same laps only thins the rings.
  Each of those changed its generator `LAPS`, `<Mode>CourseSource.DefaultLaps`, the scene's controller `laps:` and
  the SO default together (`target = laps x 8`).
- **The comeback is rescaled with the target.** `bonusLevels = deficit x rate`, and every generator records the
  levels a quarter-of-target deficit buys. Each changed mode keeps that number: Dog Fight 0.12 -> 0.27, Dustup
  0.6 -> 1.5, Hijack 0.016 -> 0.024, Sirocco 0.01 -> 0.015, Switchback 0.5 -> 0.385, Tollway 1.5 -> 2.0, Waystation
  0.4 -> 0.64, Wrecking Ball 0.006 -> 0.014, Headlong and Redline 0.35 -> 0.21, Thresher Time and Grizzly Time
  0.3 -> 0.45, Cleave 0.0125 -> 0.0158, Scarab Scramble (card only, no generator) 0.5 -> 1.67, Bloomrush
  0.00036 -> 0.00048; Tapestry's rate derives itself from the round.
- **Switchback at 26 gates fits.** The course walk (`SwitchbackCourse.Generate`) was ported to Python (same
  xorshift32 and helpers) and swept 400 seeds x 4 intensities: 1,600 of 1,600 fit at 26, as at 20 (the control, mean
  course 9.7k u, matching the measured ~10k). `SwitchbackCourseTests.Gates` is 26, so the editor sweep re-proves it.
- **Grizzly Charge got its own key** (`grizzlyChargePointTarget`, 90) because it runs `DogFightPointTurnMonitor`:
  lowering Dog Fight would have cut Grizzly Charge, already at ~93 s, to ~40 s. The monitor picks the key by
  `gameData.GameMode`, the same read the crystal monitor uses to tell SkimRace from Scurry.
- **Rampage got a per-intensity list** (`rampagePrismTargetByIntensity`), Cleave's pattern line for line: SO field,
  getter with the scalar fallback, Build twin in all three sync helpers, the window's rows, and the monitor reading
  `SelectedIntensity` on the server.
- **Weekly Challenge objectives stay reachable**: every pooled objective is at or under its mode's new target
  (`WeeklyChallengeTests.ShippedCatalog_ObjectivesAreReachableInsideANormalMatch`); Scarab Scramble's 3-goal
  objective now equals its target.

## 4. Not done, and why

- **SkimRace I1 stays at 3 laps** (the estimate proposed 4). Its tuned AI policy (`SkimRaceAIConfig_I1`) matches the
  track fingerprint, which counts laps; a fourth lap would put the game on the general policy until
  `Tools/Build/skimrace_retune.py 1` runs, and that needs a .NET SDK this session did not have. I1 is ~60-75 s at AI
  pace, already close. I3 has no tuned policy and I4's was already stale ("RETUNE NEEDED" before this change), so
  moving those two cost nothing. Yash's benchmark limits (`SkimRaceRaceRecorder.DefaultLimitSeconds`, 70/80/70/70)
  are untouched; the shorter I3/I4 races only make them easier to meet. Run `skimrace_retune.py 4` (and 3, if a
  tuned I3 is wanted) on a machine with dotnet.
- **Skein is unchanged.** Its ring count only spaces rings on a fixed cable (laps per intensity 1/2/4/6 set the
  length), and the count is load-bearing geometry proven only at 24 by `skein_budget.py`. Shortening it means fewer
  cable laps per intensity and a re-measured budget, which is a design pass of its own.
- **Grizzly Time is unfinishable for a reason the target cannot fix.** Yash flew 20.3k u (more than the whole race)
  and threaded 4 of 24 gates in 268 s; the AI Grizzlies reached 8 and 15. The bomb launch throws the pilot off line,
  and gate 0 sits at the shell edge. Two laps is the length once the steering works; the steering is the real fix.
- **Per-intensity targets for Switchback and Breakwater** (I4 runs 2-3x the I1 time) would need the same
  per-intensity list Rampage now has; not done here.

## 5. To verify

Nothing here was run in the Unity editor (no editor or `unity` CLI in this session; see the PR's verification
status). The editor checks: the End Game Conditions window shows the new rows and values; `SwitchbackCourseTests`,
`WeeklyChallengeTests`, `RaceCourseSourceTests` pass; one match each of Grizzly Charge (still ends at 90 points) and
Dog Fight (ends at 40), Rampage at I1 and I4 (the target reads 2000 and 1000); and a timed playtest of every
"estimate" row above, recording the time, so this table can move from estimate to measured.
