# Regatta AI — where it stands (report for Garrett)

**The same report as a page to share:** [Regatta AI Report](https://claude.ai/artifact/RmxWBLGfSsAXsUeubFXkUm) (shared from its Share menu).

**From:** Yash's sessions, 2026-10-15. **Branch:** `Ys-bleeding-edge`, for testing. Nothing here is on `bleeding-edge`
yet, and nothing goes there until Yash has tested it. **Status:** built and checked offline. **Not yet played in the
Unity editor.** That playtest is the next step (section 5).

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
- **The lab is now "Training AI for Regatta".** Every vessel races in it. AIs train by racing each other, learn from a
  player's line, and export what they learn for Unity.
  - The Rhino went from **62.7 s to 28.5 s** a race in 6 rounds.
  - In a live race the trained Rhino beat the untrained one, **20.2 s to 41.5 s**.

## 2. Try it

**The artifact (any browser, phone or PC):** [Training AI for Regatta](https://claude.ai/artifact/FCC7UWbs2wC6LXYMVW4KA4).
It is private until Yash shares it from its Share menu.

- **Race and watch.** Press *Start* for the five-hull test grid. *C* changes camera, *V* changes pilot, and the trails
  and dashed planned-route lines show each AI's line.
- **Train.** Open the *Training* tab at the bottom, pick a hull, press *Train*, then *Race trained vs untrained*.
- **Teach it your line.** Untick *Is AI* on player 1, fly a hull for a lap, then train with *Learn from me* above 0.
- **Easier routes.** AI Config ▸ Gate steering has three sliders for AIs that swing wide: *Go straight when lined
  up*, *Exit toward the next ring* and *Break-off distance*. At their defaults they are the game's steering.

**Unity:**
1. Pull `Ys-bleeding-edge`, open the project and wait for the compile.
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

**The audit fixes, hull by hull** (`REGATTA.md` §6.1):

| Hull | Before | Now |
|---|---|---|
| All | AIs raced at 30–90% throttle | Full throttle in Regatta |
| All | Difficulty did nothing in Regatta | Easy / Medium / Hard offered |
| Manta and the fast hulls | On short legs the lined-up point sat behind the previous ring, so the AI broke off at every ring | That point is capped at 40% of the leg (every gate race) |
| Rhino | The AI never kept its stick steady enough to build the ramp | A ramp policy: steady on straights, lets go before turns |
| Dolphin | Its boost fired at the cell centre, wide of the ring | It aims the boost down its line to the ring |
| Urchin | It found its own colour's rail one time in three; riding, it dropped to 0.6 throttle | It heads for its own rail, and no break-off while riding |
| Squirrel | A pilot swap gave it the old ring-blind autopilot | It gets the Skim Race pilot |

**Your gate races are untouched.** `GateRaceController` is shared, but the steering fix is off for Headlong, Switchback,
Breakwater, Skein and Redline: each has an inspector switch, `aiCommitFromApproachPoint`, left off. The 40% lead cap
does apply to every gate race.

## 4. The browser lab: Training AI for Regatta

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
- The checklist entries in `Docs/UNITY_VERIFICATION_CHECKLIST.md` (Regatta, 2026-10-14 and 2026-10-15).

**Still open in the AIs** (`REGATTA.md` §6.1):
- The Manta's Soar has no corner look-ahead.
- An Urchin can flip its ride direction at I3–I4.
- Squirrel lane heights stack across all AI seats.
- The Rhino may overshoot I3–I4 corners.
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
_Generated 2026-10-15 from git (last 4 days), who_works_where.py and Docs/TEAM_TASKS.md: `python3 Tools/Build/team_report.py area regatta-ai --days 4`._

### Recent changes (21)
- 2026-10-15 `c2ef91660` feat(regatta-ai): Training AI for Regatta - every hull, AIs that train each other, learn from a player's line — on Ys-bleeding-edge
- 2026-10-15 `b702e1692` feat(regatta-ai): easier routes - planned-route line and three steering knobs — on Ys-bleeding-edge
- 2026-10-15 `71d2d175b` feat(regatta-ai): right-click press and hold pans the camera — on Ys-bleeding-edge
- 2026-10-15 `bd9fafb90` fix(regatta-ai): commit at the approach point, re-attack after a missed mouth — on Ys-bleeding-edge
- 2026-10-15 `60db8af10` fix(regatta-ai): settings dock stays right of the stage in any viewer — on Ys-bleeding-edge
- 2026-10-15 `12bbfbaca` feat(regatta-ai): tabs on the right of the stage, and the studio kit for every AI — on Ys-bleeding-edge
- 2026-10-15 `2d4a9bf44` fix(regatta-ai): heading buttons sit on the right, beside the fold button — on Ys-bleeding-edge
- 2026-10-15 `5b7eec061` feat(regatta-ai): the Regatta AI race studio, its own artifact; HyperSea taken out of the Vessel Studio — on Ys-bleeding-edge
- 2026-10-15 `e51c1ce96` docs(ai): Regatta AIs are tested in their own Regatta artifact, not the Vessel Studio — on Ys-bleeding-edge
- 2026-10-15 `db88a4af4` feat(third-eye): trajectory trails and what every racing AI is doing — on Ys-bleeding-edge
- 2026-10-15 `484582299` fix(regatta-ai): five racing AIs fixed from a code audit - Rhino ramps, Urchin finds its lane, Dolphin aims its boost — on Ys-bleeding-edge
- 2026-10-14 `d52877904` docs(regatta): the host's grid picker and the five-hull test race — on Ys-bleeding-edge
- 2026-10-14 `a68859e8b` fix(tooling): restore seven characters the menu-reorg script dropped — on Ys-bleeding-edge, bleeding-edge
- 2026-10-14 `34d902cac` refactor(tooling): reorganise the FrogletTools menu and retire 24 dead tools — on Ys-bleeding-edge, bleeding-edge
- 2026-10-14 `1f4eede6a` feat(skimrace): ring halves by intensity 4; omni crystal 20% larger — on Ys-bleeding-edge, bleeding-edge
- 2026-10-14 `5b9e1e057` docs(ai): Regatta AI roadmap - six racing AIs, one drive pattern, Amoebius as the harness — on Ys-bleeding-edge, bleeding-edge
- 2026-10-14 `53363759f` feat(brittlestar): new one-touch vessel - modes, five turrets, ring detonation — on Ys-bleeding-edge, bleeding-edge
- 2026-10-13 `3b005f012` fix(ai): AI Serpent cloaks replicate; AIs stop tracking cloaked pilots — on another branch
- 2026-10-13 `bcba23705` feat(skimrace): omni crystal inside each race ring; ring shrinks with intensity — on Ys-bleeding-edge, bleeding-edge
- 2026-10-13 `16b41b045` feat(stoat): port the Stoat, Slingshot and Warpline from claude/peaceful-rubin-hhw49n — on Ys-bleeding-edge, bleeding-edge
- 2026-10-12 `fb2bff7ac` feat(skimrace): replace omni crystals with domain race rings — on Ys-bleeding-edge, bleeding-edge

### Also in flight on other branches
- `cece/vibrant-archimedes-n3xn3x` (unattributed, 2026-10-13): merge: bleeding-edge into cece/vibrant-archimedes-n3xn3x; Serpent Charge 66 -> 67

### Open tasks (Docs/TEAM_TASKS.md)
- T-20261015-18e7 [taken by Yash] test: Playtest Regatta in Unity on Ys-bleeding-edge: five hulls (Squirrel, Manta, Rhino, Urchin, Dolphin) on AI seats, Hard, I1 then I3, watched in the Third Eye with Trails on. (who: Yash (testing it), anyone with the Unity Editor)
- T-20261015-0064 [open] decision: Decide the AI-training direction: extend the genetic trainer (Assets/_Scripts/Utility/AITraining) for Regatta rather than build a second one; where consented player flights are stored and the privacy wording; whether a neural-network AI may ever ship. (who: Garrett and the core four; studyholic and Shombith for the trainer)
- T-20261015-cb8f [open] decision: Decide whether the gate-race steering fix (commit at the approach point, re-attack after a missed ring; on in Regatta only) is turned on for Headlong, Switchback, Breakwater, Skein and Redline (inspector switch aiCommitFromApproachPoint) after a playtest. (who: Garrett (gate races owner))
- T-20261015-2082 [open] dev: Build T1 of the AI training plan: the AI Lab (Test Range R1-R3) in MinigameRegatta - any hull on any seat (trained or untrained), 0.25x-8x time, the Third Eye's trails and readout in a development build, speed graph and phase timeline, a scorecard JSON per run. (who: Yash (offered), with Garrett for the trainer hand-off)
<!-- team_report:end -->
