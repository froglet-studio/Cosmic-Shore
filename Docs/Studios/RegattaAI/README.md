# Training AI for Regatta — race, watch and train every Regatta AI

(Named "Regatta AI" until 2026-10-15. The plan it prototypes: `Docs/AI_SYSTEM/AI_TRAINING_PLAN.md`.)

**Artifact:** https://claude.ai/artifact/FCC7UWbs2wC6LXYMVW4KA4 (private: share it from its Share menu).
**Its own artifact, separate from the Vessel Studio** (Yash, 2026-10-15; `Docs/AI_SYSTEM/REGATTA_AI_ROADMAP.md`
§2.5 and its decision note). The loop: test an AI here, port it to Unity (`/artifact-to-unity`), watch the real hulls
race in the Third Eye, improve, repeat.

| File | What |
|---|---|
| `index.html` | The page. The card's eight hulls (Rhino, Manta, Dolphin, Urchin, Squirrel, Serpent, Sparrow, Scarab) on their shipped numbers (the keys `Tools/Build/regatta_balance.py` reads), up to 8 players (vessel, domain, difficulty, starting-element style, view), the three cameras, trajectory trails, AI thinking lines, phone play, and the scorecard: race time per hull × Easy / Medium / Hard against the balance model. Hook `window.__regattaAI` (`state`, `set`, `startRace`, `stopRace`, `tick(s)`, `raceHeadless(intensity, hull, level, seed, style, fixMiss)`, `scoreHulls`, `course`, `routesShown`, `train`, `training`, `keepBest`, `trained`, `costOf`, `recordDemo`). |
| `fleet.js` | GENERATED. Every vessel class's flight numbers off its shipped prefab (`export_fleet.py`, with `regatta_balance.py`'s reader; `--check` exits 1 when stale). The 8 card hulls keep their modelled speed verbs; the other 8 fly a labelled generic drive (cruise, plus the prefab's boost held on straights). |
| `course.js` | GENERATED. The circuit as the game builds it: `RegattaCourse.BuildGates` / `BuildRails` per intensity (8 gates, three braided lanes, one point per prism). Never edit by hand. |
| `export_course.sh` + `ExportCourse.cs` | Regenerates `course.js` from the shipped C# (the same compile as `Tools/Build/regatta_course_harness`; needs a dotnet 8 SDK). `--check` exits 1 when `course.js` is stale. Re-run after any edit to `RegattaCourse.cs`, `HeadlongCircuit.cs` or `RaceCourseGeometry.cs`. |
| `build.py` | The publish folder: copies the page, `course.js` and the shared Vessel Studio scripts it loads (`../VesselStudio/studio-theme.js`, `studio-look.js`, `studio-domains.js`, `studio-ide.js`, `ai_race_panel.js`) flat beside it. `--check` also runs `export_course.sh --check`. |

**Publish:** `python3 Docs/Studios/RegattaAI/build.py --check && python3 Docs/Studios/RegattaAI/build.py --out <dir>`,
then publish `<dir>/index.html` with every other file in `<dir>` as `files`, `url` = the artifact above. **Gate:**
`node .claude/skills/vessel-studio/check_studio.cjs Docs/Studios/RegattaAI/index.html --hook __regattaAI --three <local three.min.js>`.

## What it models, and from where

- **Course:** `course.js` (above). Start: a 120 u ring 260 u behind gate 0 (REGATTA.md §5). A ring threads when a step
  crosses its plane inside the mouth, either way (`RaceGateRing.CrossedMouth`). Three laps, 24 rings.
- **Hulls:** each hull's speed verb on its shipped numbers (the page's About panel lists every one). Turn rate =
  scaler + speed × `RotationThrottleScaler`. Rival rail rams halve a Manta's, Dolphin's or Sparrow's throttle for 1 s
  (`VesselChangeSpeedByPrism`); any rail ram resets a Squirrel's boost.
- **Signature moves:** every hull's moves with their SHIPPED limiters (cooldown / ammo / meter), read from the action
  assets on 2026-10-15, in Vessel Config ▸ Signature moves. Only the speed moves change a Regatta race (the rails are
  super-shielded, PvP is petals only), so only those are flown.
- **Steering:** `GateRaceController.ArmRacers` with `MinigameRegatta.unity`'s values (approach lead 480 u capped at 40%
  of the leg, commit 420 u, through 320 u, side latched per ring), full throttle (`AIThrottleFloor`), and
  `AIPilot.UpdateOrbitBreak` (extend and re-attack). Difficulty: `GateRaceHandicap` with `SkimRaceDifficulty.asset`.
- **Each hull's drive** (how it spends its speed verb) is lab-only and named in the page.
- **The studio kit (`/vessel-studio` §1.5), for every AI:** the six settings tabs in a dock on the RIGHT of the stage
  (on any mouse-and-keyboard screen however narrow the viewer, rail capped at 46% of the width; the page injects its
  own `#regatta-dock-right` rule so an older cached `studio-ide.js` cannot stack it; only an upright phone stacks, and a
  phone opens touch play anyway), records below; AI
  Config holds **AI tuning** (pick any of the eight AIs and tune its drive with lab-only sliders, Reset this AI) and the
  shared **Gate steering** as sliders starting at the scene's shipped values; Play Style Config's **Custom** style puts a
  Time-level slider on every hull Time reaches; the Free camera **orbits or flies** (D16); **right-drag (or middle-drag) pans any camera**, double-click resets (the user, 2026-10-15); every player has a **You / AI** toggle in its heading (Game Config ▸ Players): fly any one hull, or none, and switch mid-race without a restart (the hull you leave goes to the AI at its level, the camera follows the one you take); the same toggle as the Unity Third Eye's Pilots panel (`PilotControl`, Yash 2026-10-15); the scorecard's **Hard
  fastest?** column is the levels check (D24). The scorecard always races the current tuning.
- **Lab fix (AI Config, on by default):** commit no earlier than the approach point, and after a missed mouth re-latch
  the side and extend at once. **Ported to Unity for Regatta on `Ys-bleeding-edge` (2026-10-15,
  `GateRaceController.CommitDistanceFor`, `AIPilot.BreakOffNow`), waiting on a playtest.** Off = the game before it.
- **Easier routes (AI Config ▸ Gate steering; lab-only, candidates to port; at their defaults they are the game)** - for
  an AI that swings wide and comes back (Yash, 2026-10-15). *Show each AI's planned route* draws a dashed line from each
  AI through its approach point, the ring and the point past it, so the cause is visible. Then:
  - **Go straight when lined up** (0-90 deg, default 0): when the pilot is already coming at the mouth within this angle
    of the ring's axis, it skips the approach point and flies at the ring's centre, then through.
  - **Exit toward the next ring** (0-1, default 0): the point past the ring bends toward the next ring, growing with the
    square of closeness (none at the commit distance, full at the ring), so the line still crosses near the centre.
  - **Break-off distance** (x0.25-1, default 1): how far a break-off flies before it turns back.
  - *Reset steering* puts all of Gate steering back to the game.

  **Measured (Hard, seed 11, all 8 hulls x I1-I4, lab fix on):** game 30/32 finished, 2635 s for the finishers, 298
  missed mouths, 43 break-offs. *Exit 0.5*: 30/32, 2400 s (-9%), 261 missed, 37 break-offs (Rhino I1 68 -> 29 s, Manta
  I4 116 -> 81 s, Dolphin I3 127 -> 103 s, Scarab I4 DNF -> 89 s; but Dolphin I4 133 s -> DNF). *Straight 25 deg*: 29/32,
  2495 s, 441 missed. *Break-off x0.5*: 28/32, 925 missed (turning back sooner traps it again). *Exit 1* and the
  combinations miss far more. So *Exit 0.5* is the first candidate, and the Dolphin at I4 says it wants a per-hull
  setting.

## Flights: a viewer of Unity flights (2026-10-16)

Training data comes from playing in the Unity editor only (Yash, 2026-10-16), so this page neither records its own races
nor learns. The **Flights** tab (bottom dock) loads a `flight_*.json` that Unity's `HumanFlightRecorder` wrote (**Load a
Unity flight**); the newest six stay in this browser. **Replay** puts your hull back on the course as a white ghost with a
line to the target you were going for, coloured by phase; green, red and amber dots are skims taken, skims skipped and
mistakes. The readout shows the moment's situation (ring distance and bearing, nose off the ring, through the mouth if
straight, turn needed against the tightest, gap to a rail, the cheapest skim, place). **Legs** compares each leg with this
page's Hard AI flown from where you started it (a guide only). The learning runs on the editor flights:
`python3 Tools/Build/situation_features.py --write`. The maths is `situation.js` (also run by Node and twinned in
`Tools/Build/situation_features.py`); the definitions are in `Docs/AI_SYSTEM/situation_features.json`; the plan is
`Docs/AI_SYSTEM/SITUATION_RECORDING_PLAN.md`.

## Training (round 2, 2026-10-15)

The **Training** tab: pick any of the 16 hulls, tick intensities, set population and generations, and the hull's AIs
race each other. It is a cross-entropy search over the AI Config sliders (shared steering + the hull's drive): every
generation races the population headless (Hard, seed 11), the fastest quarter set the next generation, and the champion
races every generation. Cost = race time (a DNF costs 300 s plus 10 s a ring short), plus, with **Learn from me**, the
mean distance from your recorded line / 10. Your line is kept when you fly a hull yourself for at least a lap (one per
hull and intensity). **Keep best** makes it the hull's trained AI (used by AI seats while *Races use the trained AIs* is
on); **Race trained vs untrained** seats both in a live race; **Copy JSON** exports `cosmic-shore.ai-tuning/1` for Unity.

Measured: the Rhino, I1 + I2, 10 AIs x 6 generations: 62.7 s -> 28.5 s a race; live, trained 20.2 s vs untrained 41.5 s
on I1. A generic hull races (the Grizzly finishes I1 Hard in 83.1 s).

## First measurements (2026-10-15, Hard, seed 11, card's elements; seconds for three laps; model in brackets)

| Hull | I1 | I2 | I3 | I4 |
|---|---|---|---|---|
| Rhino | 67.9 (17.8) | 57.5 (21.8) | 103.5 (25.6) | 150.5 (25.9) |
| Manta | 31.4 (23.9) | 53.9 (27.5) | 59.9 (30.4) | 116.3 (30.2) |
| Dolphin | 86.0 (41.9) | 102.3 (48.0) | 127.2 (53.0) | 132.7 (54.1) |
| Urchin | 49.8 (48.3) | 57.1 (54.3) | 60.0 (58.9) | 60.9 (58.7) |
| Squirrel | 66.9 (48.5) | 97.5 (54.6) | 99.1 (59.2) | 83.3 (59.5) |
| Serpent | 63.8 (58.1) | 87.2 (65.4) | 104.8 (70.9) | 107.8 (70.6) |
| Sparrow | 89.1 (73.6) | 112.1 (82.7) | 130.3 (89.6) | 143.5 (89.5) |
| Scarab | 58.7 (44.9) | 73.8 (50.5) | DNF (54.8) | DNF (55.1) |

- **The game's steering finishes 17 of these 32 races; with the lab fix, 30.** On Regatta's short legs the 40% lead
  cap puts the approach point inside the 420 u commit circle, so a pilot commits while still ~200 u off the axis and
  crosses the plane outside the mouth; the per-ring side latch then circles it round the far-side point
  (REGATTA.md §6.1, still open).
- **The card's Time-10 Scarab (324 u/s, ~140 u turning circle) cannot make I3–I4** under the full-throttle floor: the
  corner floors were cut for a 216 u/s Scarab. At rest (216 u/s) it finishes I3 (84.5 s) and still not I4.
- **The Urchin matches the model** (it rides its own lane at 300 u/s: corners are the rail's), and its difficulty does
  nothing while it rides (belief only bites off the rail).
- **The fast hulls are far off the model** (the Rhino 3-6×): the model is a competent pilot on the best line with
  instant braking; the AI coasts down from 840 u/s at 120 u/s² and misses mouths. That is the Rhino drive's work
  (roadmap step 4).
- **A first tuning lead:** the Rhino's "Let go early" at 300 u (default 60) races I1 in 29.0 s instead of 67.9 s:
  it lets go of the ramp far enough out to coast down to the corner speed (it cannot brake), and stops missing mouths.
- **Levels order correctly** for the Serpent and the Manta (Hard < Medium < Easy). The Squirrel's Medium beats its Hard
  on two courses (rail rams reset its boost, which is noise larger than the belief mistakes).
