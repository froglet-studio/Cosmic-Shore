# Regatta AI — where it stands (report for Garrett)

**The same report as a page to share:** [Regatta AI Report](https://claude.ai/artifact/RmxWBLGfSsAXsUeubFXkUm). The page
was last updated 2026-10-15; this file is current.

**From:** Yash's sessions, 2026-10-15 to 2026-10-24. **Branch:** everything below is on `bleeding-edge`: sections 1-4
up to the learned Squirrel by PRs #1150, #1154 and #1165, the rest by the `Ys-bleeding-edge` merges up to #1523
(2026-10-25). **Status:** built and checked offline (compiles, gates, self-tests, and offline models of the Rhino,
Dolphin and Manta drives). The Manta, Rhino and Dolphin now carry numbers taken from Yash's own flights. **None of the
AI changes has been played in the Unity editor.** That is the next step (section 5).

## 1. In one minute

- **Regatta now races five different hulls at once.** The host picks the hull of every AI seat: Squirrel, Manta, Rhino,
  Urchin, Dolphin.
- **All five AIs were audited, and the race-breakers were fixed in Unity.** The Rhino ramps, the Urchin finds its own
  lane, the Dolphin aims its boost at the ring, and every AI races at full throttle.
- **You can watch every AI's path in the Third Eye** (FrogletTools ▸ AI ▸ Third Eye). Trails show where each pilot flew,
  and each AI's readout says what it is doing.
- **A browser lab found a steering bug, and the fix is in Unity.** AIs turned in too early on Regatta's short legs,
  missed the ring, then circled it. With the fix, the lab's AIs finish **30 of 32** hard races instead of **17**. In
  Unity it is on for Regatta only.
- **The browser lab is gone (2026-10-16).** Yash: "delete the artifact, it's not needed; only tests will happen in the
  Unity editor itself." AIs are tested in the editor (the Third Eye, with a You / AI toggle on every pilot), and
  training data comes only from editor play (`Docs/AI_SYSTEM/SITUATION_RECORDING_PLAN.md`). What the lab measured is
  kept in `Docs/AI_SYSTEM/REGATTA_AI_ROADMAP.md`.

- **2026-10-17, not yet played:**
  - **The Stoat is on the Regatta card** (48.5 s at I1 in the balance model, at Time 10). Its autopilot learns from
    editor flights like the others.
  - **Regatta's AIs now aim through the ring's centre.** A drifting hull locks its course on its "through" point. That
    point used to sit on the ring's axis, which is off the pilot's line when it commits off-axis, so the locked line
    passed beside the mouth. In the offline models this stalled the Dolphin at I2-I4 and the old Rhino drive at I3-I4;
    on the centre line every race finished. This changes every Regatta AI.
  - **The Rhino brakes on its ramp** to a corner speed sized by the next turn, and ramps only where the leg pays back.
    Retuned for the centre steering, it roughly ties the old drive in the model (213 s against 208 s over I1-I4); the
    brake is kept for hairpins, and its corner speed will be learned from Yash's Rhino flights.
  - **The Dolphin drifts again at the top of each discharge.** The drift freezes speed, so drifting at the peak keeps
    the most speed. Model: 202 to 299 u/s average on a straight; three laps 54.0 / 64.6 / 73.1 / 71.9 s at I1-I4
    (was 56.9 / 66.0 / 80.8 / 74.0).
  - **The Manta holds Soar deeper into turns in Regatta** (bands 0.8 / 0.85, was 0.35 / 0.75): 1.5-2 s faster per race
    in the model. Dropping Soar before a corner was also modelled and was slower everywhere, so it was not shipped.
  - These are models (no break-off, no rotation lag). The editor is the measurement.
- **2026-10-17 to 2026-10-24: every hull races its own line, and three hulls learned from Yash's flights.**
  - **Every hull flies its own line along the side of the braid** (`b3ff6623e1`, `REGATTA.md` §6.2), not through the
    ring's centre, where it is 22 u from all three super-shielded rails at once. The line runs in the gap between two
    rails and never crosses one. Each hull has its own distance out, room from the rails and careful-to-bold risk
    (`Resources/RaceLines/<Hull>_Regatta`, `author_race_lines.py`); each AI seat draws its own risk, so one grid has
    careful and bold pilots of the same hull. The Squirrel and the Urchin keep their own steering.
  - **The speed drives judge the ring, not the line's steering point** (`fb907b1d72`). On the line the steering point is
    only 0.35-0.8 s ahead, so the Rhino's and Dolphin's "is there runway?" checks never passed.
  - **Learned or set from Yash's flights, Regatta only** (the other modes keep the shipped numbers):

    | Hull | Flights | What changed | How |
    |---|---|---|---|
    | Manta | 13 races, 9 finished | Soar bands 0.8 / 0.85 → **0.975 / 0.975**; line 0.70 → **0.809** | learned (`f9da9618b8`) |
    | Rhino | 6 races, 2 finished (I1, I3) | corner speed 600 → **333 u/s**; line 0.80 → **0.676** | set by hand from all six (`336cf1ace9`) |
    | Dolphin | 1 race, unfinished (I1) | line 0.60 → **0.785** | set by hand (`a2436ca284`) |

    The learner writes only from three finished races of a hull; "set by hand" was Yash's call, and the learner replaces
    those numbers at the third finished race. People fly wider than the starting lines guessed on all three hulls.
  - **What the flights showed.** The Rhino: Yash never passes about 590 u/s (the ramp's ceiling is 840), threads rings
    at a median 333 u/s, ramps on straights as short as 0.6 s (the AI waits for 1.5 s) and steers past the plateau while
    ramping. The offline model is slower at the new corner speed (I1 50.2 against 44.9 s), but it has no rails to
    scrape; Yash's finished I1 was 36.5 s, faster than the model at either value. The Dolphin: Yash drifts 83% of the
    race and drifts again at each discharge's peak (19 of 21 times), which is the AI's rule since 2026-10-17, and spends
    the charge to 0.96 (the AI 0.95).
  - **The line learner was fixed** (`336cf1ace9`): each crossing is measured against its own ring's mouth. Mixing I1 and
    I3 crossings had read the Rhino's line as 0.815.
  - **The Stoat is on hold** (`REGATTA_AI_ROADMAP.md` §3.1): Garrett is still configuring it.
- **The AIs now learn from people's editor flights.** Every race played by hand in the editor is recorded automatically
  (`HumanFlightRecorder`) and sent automatically when Play stops (no question since 2026-10-16). 46 Squirrel races were
  stored by the evening of 2026-10-16 (38 at I1, 8 at I3); on 2026-10-17 the branches held 49 Squirrel and 5 Urchin
  Regatta flights. By 2026-10-24 they also hold 13 Manta, 6 Rhino and 1 Dolphin (none of the Stoat).
- **The Squirrel AI's Regatta numbers were learned from those flights** (`learn_from_flights.py --write`, 2026-10-17).
  The main change: it now skims the side of the rail the rings are on, the side people skim 94% of the time. It used to
  cross the rail to reach every ring.
- **A racing line is averaged from the clean, fast legs** (`racing_line.py`, `Docs/AI_SYSTEM/RACING_LINE.md`). Legs with
  a mistake are left out, and each leg's style (rail riding, drift, abilities, boost) is read from the recording.
  - Ideal lap: 19.8 s at I1. A person's median lap is 33.0 s.
  - Rail riding is a little slower but far safer on several legs.
  - The line itself is not yet flown by the AI.

## 2. Try it

**The browser lab (Training AI for Regatta) was deleted on 2026-10-16**; everything is tried in the Unity editor:
Regatta, solo, five hulls seated (six with the Stoat), FrogletTools ▸ AI ▸ Third Eye with Trails on, and its Pilots panel to fly any hull or
hand yours to the AI.

**Unity:**
1. Pull `bleeding-edge` (or `Ys-bleeding-edge`), open the project and wait for the compile.
2. Start Arcade ▸ Regatta, solo, Hard. Tap the AI chips to seat the five hulls, then launch.
3. Open FrogletTools ▸ AI ▸ Third Eye and turn *Trails* on. Near a ring, each AI should read *Approach* then *Through*.
   After a missed ring it should read *break-off* and come straight back, not loop round the far side.
4. Every hull should finish three laps at I1. Repeat at I3, and note any hull that does not, and at which ring.

## 3. What changed in the game (Unity)

| What | Commit | Files |
|---|---|---|
| The host picks every AI seat's hull, so five hulls race on one grid | `9dfa9ff19` | `AIHullSeating`, `ArcadeGameConfigureModal`, `ServerPlayerVesselInitializerWithAI` |
| Five racing AIs fixed from a code audit (table below) | `484582299` | `AIPilot`, `GateRaceController`, `RegattaController`, `RampBoostAIPolicySO` (new), `PilotSwap`, `AIDifficulty` |
| Third Eye: a trail per pilot (its last seconds of flight) and what each racing AI is doing | `db88a4af4` | `Editor/AI/ThirdEye*` |
| Steering fix: commit at the approach point, and re-attack after a missed ring (Regatta only) | `bd9fafb90` | `GateRaceController`, `RaceGateRing.PlaneCrossing`, `AIPilot.BreakOffNow`, `GateRaceSteeringTests` |
| Third Eye: a You / AI toggle on every pilot (hand your hull to the AI, or fly any AI's hull) | `2744411fb` | `PilotControl`, `IAIPilotArming`, `ThirdEyeWindow`, `ThirdEyePilots` |
| The AI training recorder, automatic in every mode (flight/3: situation, camera, rivals, rails, rings, state bits; the last ring counted, the hull named at the start, button presses) | `b797be06c`, `d72ac8c14`, `6d7241a92` | `HumanFlightRecorder` |
| The Squirrel's Regatta numbers learned from editor flights (skims the ring face of the rail) | `e9a2a42fe` | `SkimRaceAIConfig_Regatta.asset` via `author_skimrace_ai_config.py` |
| Offline readers of the flights (no game change): situations and choices, the Skim Race pilot's numbers, the racing line | | `Tools/Build/situation_features.py`, `learn_from_flights.py`, `racing_line.py` |
| The other drives learn from flights, Regatta only (per-mode variants `Resources/AIBoostPolicies/<asset>_Regatta`) | `6c03de73c` | `learn_drives_from_flights.py`, `AIBoostPolicySO.ForMode`, `AIModeTuning`, `MantaSoarAITuningSO`, `RegattaDriveTuningTests` |
| the Stoat on the Regatta card | `67aaa050a` | `author_regatta_assets.py`, `regatta_balance.py` |
| the Rhino brakes on the ramp; ramps only where the leg pays back | `658fda119` | `RampBoostAIPolicySO`, `AIPilot.SetNextLegProvider`, `RhinoRampDriveTests`, `rhino_drive_sim.py` |
| the Dolphin drifts again at the discharge's peak; Regatta aims through the ring's centre; Rhino retuned for it (`cornerReleaseDegrees` 30, `minSpeedGain` 50) | `9b064721b` | `ChargeBoostAIPolicySO`, `GateRaceController.ArmRacer`, `DolphinChargeDriveTests`, `dolphin_drive_sim.py` |
| the Manta's Regatta Soar bands 0.8 / 0.85 | `f4e35748c` | `MantaSoarAITuning_Regatta.asset`, `manta_drive_sim.py` |
| Every hull races its own line along the side of the braid | `b3ff6623e1` | `RegattaRaceLine`, `RaceLineStyleSO`, `RegattaController.TryRaceLine`, `author_race_lines.py`, `RegattaRaceLineTests` |
| The speed drives judge the ring, not the line's steering point; the Stoat's autopilot gauge likewise | `fb907b1d72` | `AIPilot.ObjectivePosition`, `StoatDipoleMath.AutopilotGauge` |
| The Manta learned from Yash's flights: Soar bands 0.975 / 0.975, line 0.809 | `f9da9618b8` | `MantaSoarAITuning_Regatta.asset`, `race_lines_learned.json` |
| The Rhino's corner speed 333 and line 0.676 from Yash's six races; the line learner scales each crossing by its own ring | `336cf1ace9` | `RhinoAIBoostPolicy_Regatta.asset` (new), `Rhino_Regatta.asset`, `learn_drives_from_flights.py` |
| The Dolphin's line 0.785 from Yash's race | `a2436ca284` | `Dolphin_Regatta.asset` |

**The audit fixes, hull by hull** (`REGATTA.md` §6.1):

| Hull | Before | Now |
|---|---|---|
| All | AIs raced at 30–90% throttle | Full throttle in Regatta |
| All | Difficulty did nothing in Regatta | Easy / Medium / Hard offered |
| Manta and the fast hulls | On short legs the lined-up point sat behind the previous ring, so the AI broke off at every ring | That point is capped at 40% of the leg (every gate race) |
| Rhino | The AI never pressed the ramp at all (only a human input raises the press) | A ramp policy: steady on straights, lets go before turns (rebuilt 2026-10-17: brakes on the ramp) |
| Dolphin | Its boost fired at the cell centre, wide of the ring | It aims the boost down its line to the ring |
| Urchin | It found its own colour's rail one time in three; riding, it dropped to 0.6 throttle | It heads for its own rail, and no break-off while riding |
| Squirrel | A pilot swap gave it the old ring-blind autopilot | It gets the Skim Race pilot |

**Your gate races are untouched.** `GateRaceController` is shared, but the steering fix and the 2026-10-17 centre-line
"through" point are off for Headlong, Switchback, Breakwater, Skein and Redline: both sit behind the inspector switch
`aiCommitFromApproachPoint`, left off there. The 40% lead cap does apply to every gate race.

## 4. The browser lab: Training AI for Regatta (deleted 2026-10-16; kept here as a record)

| | |
|---|---|
| Course | The game's own `RegattaCourse` output, generated from the C# |
| Hulls | All 16. The 8 card hulls fly their real speed abilities; the other 8 fly a generic drive, labelled as such. Flight numbers are generated from the prefabs |
| AIs | `GateRaceController`'s steering with the scene's values, one drive per hull, and `GateRaceHandicap` for Easy and Medium |
| Scorecard | Race time per hull × difficulty, against `regatta_balance.py`'s skilled-pilot model |
| Training | A population of one hull's AIs race each other; the fastest breed the next round, and the champion defends every round |
| Learn from me | A player's recorded line is added to the score |
| Hand-off | *Copy JSON* exports `cosmic-shore.ai-tuning/1` for Unity |

**What it measured:**
- The shipped steering finished **17 of 32** Hard races (8 hulls × 4 intensities). With the fix, **30**.
- *Exit toward the next ring 0.5* was 9% faster with fewer missed rings, but it cost the Dolphin its I4 finish.
- The card's Time-10 Scarab cannot finish I3–I4 at full throttle.
- The Rhino's "let go early" setting at 300 u takes I1 from 67.9 s to 29.0 s.

## 5. Open items, and what is asked of you

**To test (the task board, `Docs/TEAM_TASKS.md`):**
- The Unity playtest in section 2. Yash has taken it.
- The Rhino drive in the editor: `RhinoRampDriveTests`, then an AI Rhino at I1-I4 (T-20261016-01ec).
- The Dolphin drive and the centre steering: `DolphinChargeDriveTests`, then an AI Dolphin at I1-I4 (T-20261016-b719).
- The Manta's Regatta bands: an AI Manta at I1-I4, `RegattaDriveTuningTests` (T-20261016-d177).
- The race lines: every styled hull between two rails, never crossing one, `RegattaRaceLineTests` (T-20261017-8cc0).
- The Rhino at corner speed 333 and line 0.676: I1 and I3 times against before (T-20261024-2979).
- The Dolphin at line 0.785: still threads every ring after a drift (T-20261024-c9ba).
- The learned Squirrel AI in Regatta: does it hit the rail less? (T-20261016-39e6).
- The recorder checks: 24/24 on a finish, button presses, the hull named (checklist, 2026-10-16).
- The You / AI toggle (T-20261015-e266).
- The checklist entries in `Docs/UNITY_VERIFICATION_CHECKLIST.md` (Regatta, 2026-10-14 to 2026-10-16).

**Flights of the other hulls** (Yash, T-20261016-89ed): the Manta has learned. The Rhino needs **one more finished
race** and the Dolphin **three finished races** for the learner to set their ramp and discharge timing (and replace the
hand-set lines); flipping Third Eye ▸ Pilots ▸ AI on your hull for a lap also records the AI's numbers beside yours.
The Stoat waits for Garrett's configuration (T-20261016-b525).

**More Squirrel flights wanted** (T-20261016-6695, Yash). Just race; nothing to type.
- About 100 races in all.
- Mix in rail riding, drifting and abilities, some Hard rivals, and I2-I4.
- None so far use abilities or Hard rivals, and drift has only 3-4 clean runs per leg.

**Next build step:** port the racing line and the per-leg style choice into the Squirrel's Skim Race pilot
(T-20261015-7fc3, Garrett with Yash). Before it ships, race it against today's AI in the editor.

**Still open in the AIs** (`REGATTA.md` §6.1):
- The Manta's Soar has no corner look-ahead: modelled 2026-10-17 and slower on every course, so not shipped (its turn
  radius barely shrinks when it slows).
- An Urchin can flip its ride direction at I3–I4.
- Squirrel lane heights stack across all AI seats.
- The Rhino may overshoot I3–I4 corners: addressed by the brake (2026-10-17) and the corner speed from Yash's flights
  (2026-10-24), unmeasured in the editor.
- The Scarab cannot finish I3–I4 at full throttle.

**Decisions for you** (`Docs/AI_SYSTEM/AI_TRAINING_PLAN.md` §5). The plan grows the training into Unity: an AI Lab
scene, Regatta in your genetic trainer, a consented flight recorder ("Would you like to train the AIs with your own
moves?") and, later, AIs that copy players.
1. Extend your genetic trainer (`Assets/_Scripts/Utility/AITraining`, with studyholic and Shombith) rather than build a
   second one. Recommended.
2. Where player flights are stored, and the privacy wording. The core four decide; Shombith owns UGS.
3. Whether a neural-network AI may ever ship, or whether learning only moves the numbers of hand-written AIs.
4. Turn the steering fix on for your other gate races after a playtest, or leave them as they are.

## 6. Latest activity (generated)

This block is refreshed by `python3 Tools/Build/team_report.py area regatta-ai --write`. Any Claude session asked for
this report runs it first (`Docs/TEAM.md` rule 10).

<!-- team_report:begin (generated by Tools/Build/team_report.py; do not edit) -->
_Generated 2026-10-25 from git (last 30 days), who_works_where.py and the team-task issues: `python3 Tools/Build/team_report.py area regatta-ai --days 30`._

### Recent changes (25+)
- 2026-10-25 `0ba946a8c` fix(ai-training): the recorder's "ended" says how the turn ended, not whether you reached the target — on bleeding-edge
- 2026-10-24 `a2436ca28` tune(regatta-ai): Dolphin race line 0.785 from Yash's Dolphin race — on bleeding-edge
- 2026-10-24 `336cf1ace` tune(regatta-ai): Rhino corner speed 333 and line 0.676 from Yash's six Rhino races — on bleeding-edge
- 2026-10-24 `f9da9618b` feat(regatta-ai): the Manta AI learns from Yash's flights - Soar bands 0.975, line 0.809 — on bleeding-edge
- 2026-10-23 `8071a3110` docs(crew): ship pass - multiplayer start-here row, T-20261021-aa19 progress, the mobile report, the pose-relay trap — on bleeding-edge
- 2026-10-23 `1c749076d` fix(crew): a Wrasse rider's kite threads race rings and crystal rings exactly as it flew — on bleeding-edge
- 2026-10-20 `ca8674b00` fix(environment): Regatta, Skein, Hopf, torus knot and Helicoid lay flush prisms — on bleeding-edge
- 2026-10-20 `47c0d54ca` feat(crew): AI Wrasses are two AIs; the flail trips toys, not the kite; one Sling for every input — on bleeding-edge
- 2026-10-20 `b6293d5a3` feat(crew): the Wrasse's Sling, switches, and Wrasse Run (GameModes 68) — on bleeding-edge
- 2026-10-19 `8369666ca` feat(thresher): the ball threads race rings; the tail hangs along the cord instead of beating — on bleeding-edge
- 2026-10-18 `36e47e09c` feat(diagnostics): one switch makes a development build play like release — on bleeding-edge
- 2026-10-18 `159bfa245` docs(rhino): describe the merged ramp AI, stale class names, ship trap, team tasks — on bleeding-edge
- 2026-10-17 `fb907b1d7` fix(regatta-ai): the speed drives judge the ring, not the race line's steering point; the Stoat documented as on hold — on bleeding-edge
- 2026-10-17 `4ad0e76b2` refactor(ai): one home for hull AI - Butterfly driver and Scarab planner move under Controller/AI/<Hull>/ — on bleeding-edge
- 2026-10-17 `7de0e6645` refactor(ai): delete AIPilot's "#region Unused Methods" and the field only it used — on bleeding-edge
- 2026-10-17 `e4fc4d266` fix(stoat-ai): on a race line, judge range by the ring, not the steering point — on bleeding-edge
- 2026-10-17 `b3ff6623e` feat(regatta-ai): every hull races its own line along the sides of the braid, learned from people per hull — on bleeding-edge
- 2026-10-17 `65c838521` refactor(enums): give every implicitly-valued enum member its current value — on bleeding-edge
- 2026-10-17 `bf8bea0d7` feat(team): tell anyone doing UGS work how the AI training data should be stored — on bleeding-edge
- 2026-10-17 `17b330fe4` feat(ai-docs): vessel x game-mode chart - AIs, abilities, stats, cards, and what has flown where — on bleeding-edge
- 2026-10-16 `eec618a6d` fix(regatta-ai): no AI targets a crystal in Regatta - only the course's rings — on bleeding-edge
- 2026-10-16 `919e7727d` docs(ai): bring the AI docs in line with the code; document the Rhino, Dolphin and Manta drives — on bleeding-edge
- 2026-10-16 `9b064721b` feat(ai/dolphin): drift again at the discharge's peak; Regatta AIs aim through the ring's centre — on bleeding-edge
- 2026-10-16 `658fda119` feat(ai/rhino): brake on the ramp to a corner speed, ramp only where the leg pays back — on bleeding-edge
- 2026-10-16 `67aaa050a` feat(regatta): the Stoat races Regatta, and its autopilot learns from editor flights — on bleeding-edge

### Also in flight on other branches
- `cece/vibrant-archimedes-n3xn3x` (unattributed, 2026-10-16): feat(serpent-charge): a sniper duel - only snipes on rival pilots score
- `claude/admiring-euler-86o7hv` (unattributed, 2026-10-06): fix(ai): resolve AIPilot's commit drift against the active input device

### Open tickets (Froglet Tasks)
- T-20261024-c9ba [open] test: Race an AI Dolphin in Regatta I1 and I3 (Third Eye, trails on) with the line 0.785 set from your Dolphin race: it should fly about three quarters of the way out, never cross a rail, and still thread every ring after a drift; note its times (who: anyone)
- T-20261024-2979 [open] test: Race an AI Rhino in Regatta I1 and I3 (Third Eye on it) with the corner speed 333 and line 0.676 set from your six Rhino races: it should brake to about 330 u/s before sharp rings, fly about two thirds of the way out, and scrape rails less; note its times (before: about 45 s I1, 50 s I3; the offline model predicts slower) (who: anyone)
- T-20261017-8cc0 [open] test: Watch the AI hulls race their own lines in Regatta (Third Eye, trails on): Rhino, Manta, Dolphin, Stoat should fly between two rails outside the braid's core, never crossing a rail, the Rhino widest; run RegattaRaceLineTests; then fly each hull yourself a few races so learn_drives_from_flights.py can set its line from you (who: anyone)
- T-20261017-e2c3 [open] decision: Before storing AI training flights in UGS, read Docs/AI_SYSTEM/AI_DATA_IN_UGS.md and talk to Yash or Garrett: per-race summaries in Cloud Save, full flights unchanged in a file store, lossless compression only (who: anyone)
- T-20261016-630e [open] test: Check that no Regatta AI targets a crystal: run RegattaNoCrystalTargetTests (edit mode), then race Regatta with a Rhino and other AI seats in the Third Eye; no readout says 'Crystal on the way' and no AI bends toward an omni crystal (who: anyone)
- T-20261016-d177 [open] test: Watch an AI Manta race Regatta I1-I4 in the Third Eye: it should hold Soar (~700 u/s) through most of each leg, pivot only in hard turns, and thread rings without circling; run RegattaDriveTuningTests (who: anyone)
- T-20261016-b719 [open] test: Run DolphinChargeDriveTests and watch an AI Dolphin race Regatta I1-I4 in the Third Eye: after each discharge it should drift again near its top speed (Third Eye: 'drifts again at the peak'), and every Regatta AI should thread rings through their centre without circling (who: anyone)
- T-20261016-01ec [open] test: Run RhinoRampDriveTests and watch an AI Rhino race Regatta I1-I4 in the Third Eye: it should ramp on straights, brake ON the ramp before each ring, and only let go round sharp corners (who: anyone)
- T-20261016-b525 [open] test: Fly Regatta I1 as the Stoat, 5+ finished races, storing every one: Mass mode's dipole on the straights, Time mode (B) slings into the hard corners; in one race flip Third Eye > Pilots > AI on your Stoat for a lap and back. Then ask a session to run learn_drives_from_flights.py --write (writes StoatModeAction / StoatDipoleConfig / StoatSlingConfig _Regatta) (who: anyone)
- T-20261016-89ed [open] test: Fly Regatta I1 as the Rhino, the Dolphin, the Manta and the Urchin, 5+ finished races each, storing every one; in one race per hull flip Third Eye > Pilots > AI on your hull for a lap and back (gives the AI's numbers beside yours). Then ask a session to run learn_drives_from_flights.py --write (who: anyone)
- T-20261016-39e6 [open] test: Race the Regatta Squirrel AI after its ring-face change (SkimHeight -6.5, learned from your flights): watch it in the Third Eye skim the side of the rail the rings are on; count its rail hits and its rings when you finish (old median 14/24); revert the one asset to compare with the old AI (who: anyone)
- T-20261016-6695 [open] test: Race the Squirrel in Regatta in the Unity editor, about 100 races (Docs/AI_SYSTEM/RACING_LINE.md §4). Nothing to type: each leg's style (rail riding, drift, abilities, boost) and the rival difficulty are read from the recording. Mix it up: ride your rail, drift, press abilities, pick Hard rivals in the lobby for some races, play I2-I4 too. Store every race, the bad ones too (who: anyone)
- T-20261016-f695 [open] decision: Once 3+ hand-flown Regatta editor flights with the camera exist, compare the measured reaction lag (situation_features.py report / learned_situations.json reactionLag) with SkimRaceDifficultySO's Easy/Medium/Hard ReactionSeconds (0.5 / 0.25 / ...) and decide whether any should change (who: anyone)
- T-20261015-0576 [open] test: After your next hand-flown Regatta race in the editor, open the stored flight_Regatta_*.json and check the new situation data: 44 columns per sample, rails (one per lane) with ownRail, ringList, railEvents after a rail hit, camMode not -1; toggle You/AI for a few seconds and those rows have held 1 (who: anyone)
- T-20261015-aa0d [open] test: Playtest the Squirrel AI skimming Regatta's rails: Squirrels on AI seats at I1 and I4, the Third Eye's skim readout on one (lift 7.5, direct off, shell gap 1.5-6), and tune Resources/SkimRaceAIConfig_Regatta.asset live if it still strikes or drifts off its rail (who: anyone)
- T-20261015-2082 [open] dev: Build T1 of the AI training plan: the AI Lab (Test Range R1-R3) in MinigameRegatta - any hull on any seat (trained or untrained), 0.25x-8x time, the Third Eye's trails and readout in a development build, speed graph and phase timeline, a scorecard JSON per run. (who: anyone)
- T-20261015-cb8f [open] decision: Decide whether the gate-race steering fix (commit at the approach point, re-attack after a missed ring; on in Regatta only) is turned on for Headlong, Switchback, Breakwater, Skein and Redline (inspector switch aiCommitFromApproachPoint) after a playtest. (who: anyone)
- T-20261015-0064 [open] decision: Decide the AI-training direction: extend the genetic trainer (Assets/_Scripts/Utility/AITraining) for Regatta rather than build a second one; where consented player flights are stored and the privacy wording; whether a neural-network AI may ever ship. (who: anyone)
- T-20261015-18e7 [taken by Yash] test: Playtest Regatta in Unity on Ys-bleeding-edge: five hulls (Squirrel, Manta, Rhino, Urchin, Dolphin) on AI seats, Hard, I1 then I3, watched in the Third Eye with Trails on. (who: anyone)
<!-- team_report:end -->
