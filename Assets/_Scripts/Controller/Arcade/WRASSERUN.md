# Wrasse Run — the Wrasse and Grouper's circuit race

> `GameModes.WrasseRun = 68` (67 is `SerpentCharge`'s on an in-flight branch). A closed loop of
> switch rings is cut through the cell and every crew flies **two laps** of it in order; the first
> **domain** whose **lead runner** threads the last ring of the last lap wins, on finish **time** (golf).
> Thresher Time's shape (`THRESHERTIME.md`) on the shared `HeadlongCircuit` solver. The hull:
> `Assets/_Scripts/Controller/Vessel/Crew/KITE_NAVIGATOR.md`.

Garrett, 2026-10-20: *"make a wrasse and grouper racing game based around the wrasse collecting rings
and trying to favor time crystals (among others) to give the pilot the boost needed to get to the next
ring collector and align the wrasse to collect them."*

## 1. The loop

- **The Wrasse collects the rings.** A ring counts only when the navigator's **kite** threads it:
  `KiteNavigator` is the hull's `IRingThreadProbe`, and `WrasseRunController` answers
  `HullThreadsWhileProbeLive => false`. The hull flying through on its own does nothing.
- **The Grouper aligns the Wrasse.** The kite flies the whole kite view (round 7): a plane through the hull,
  ~216 u deep and that times the screen's aspect wide, centred on the hull. It turns toward the hull's heading
  over ~220 u flown, so a yaw in place does not swing it. The pilot does not have to thread the mouth: the pilot
  has to put the lit ring IN the kite's plane (its centre within its radius of the plane, its axis not along the
  board's up) and ON the screen. Rings are lifted out of the circuit's plane (`LateralPerturbation` 60 / 100 / 140 / 180),
  so each one asks the Grouper to roll and pitch the board onto it.
- **Time is the fuel.** The Grouper cruises at **60 u/s**. Every boost is the Wrasse's: a Time loop of
  n motes saves `round(2^(n−1))` boosts of 1.5 s, spent on the pilot's right trigger (both thumbs lifted
  on a touch screen) at the prefab's ×5 throttle, **260 u/s**. While a race has a next ring for the hull:
  - Time motes come more often, among the others (`KiteNavigatorConfig.raceTimeMoteBonus` 2, added to
    their weight of 1 + 1 per petal);
  - the AI squad mate values a Time loop 3× a loop of another element the same size
    (`raceTimeLoopPreference`).
- **The kite is slung** (the Sling, `KiteNavigatorCore.Sling`, from the Wrasse Lasso Lab) by every input:
  drag a thumb and the kite trails it on a 42 u rope (drawn for its player while held), small circles whirl it
  round, lift to let it fly; the mouse and a thumbstick are hands too (the stick places the hand on the screen,
  as the mouse does), and holding a mouse button or a trigger lets go; and the AI squad mate holds the same
  rope. Whirl it through the ring, or let it fly through.

## 2. What the mode does NOT add

No new ability, no cell of its own, no new metric and no circuit generator. It reuses
`ScoringMetric.SwitchesThreaded` (9), `GateRaceScoringRuleSO` (one asset, `WrasseRunScoringRule`),
`GateRaceController` and the whole gate-race platform, `HeadlongCircuit`, `RaceGateObjectiveProvider`,
the shared race toasts, and the Wrasse's own kit (no vessel ability was added; the kite's Time loops and
saved boosts are round 4d's).

**Platform changes it made**, each general:
- `GateRaceController.HullThreadsWhileProbeLive` (virtual, true everywhere else): a mode can make the
  second body the only collector. A hull whose probe is not live (`RingThreadProbeSample.Live`) always
  threads, so an all-AI domain can still finish (the Tollway rule).
- `GateRaceController.TryGetNextRing(vessel)`: the ring a hull must thread next in the gate race running on
  this machine, for any pilot this machine simulates, so a second body knows where to go. The Wrasse's
  navigator (human crew or AI crew) flies through it in EVERY gate race.
- `ISwitchThreadProbe` (opt-in, on `IRingThreadProbe`): a probe that also trips the Toybox's switches
  (`Toy.Update` samples it and fires the toy when its segment passes through the trigger sphere; the toy
  re-arms once the hull AND the probe are clear). The Thresher's ball (the flail) opts in; the Wrasse's kite
  does not (Garrett, 2026-10-20: "lets flip things so the wrasse does not activate toys, but the thresher
  flail does").

## 3. The cut

`WrasseRunCourse.ForIntensity`: Thresher Time's octagon and corner profiles (0 / 1 / 2 / 3 corners that
cost speed), floors in the Grouper's units.

| | cruise | boosted |
|---|---|---|
| speed | 60 u/s (10 + 50) | 260 u/s (10 + 50 × 5) |
| turn | 106 °/s | 126 °/s |
| circle | **32 u** | **118 u** |

Measured over 200 seeds per level with the shipped solver compiled out of editor:

| intensity | floor (× 118 u) | hardest corner, median | lap, median | mouth | rings lifted |
|---|---|---|---|---|---|
| 1 | 95 u (0.80) | 148 u | 4.6 k u | 60 | 60 |
| 2 | 71 u (0.60) | 192 u | 3.9 k u | 50 | 100 |
| 3 | 59 u (0.50) | 103 u | 4.7 k u | 42 | 140 |
| 4 | 53 u (0.45) | 98 u | 4.6 k u | 36 | 180 |

Every floor is ≥ 1.6× the cruise circle, so a crew that never boosts (every AI crew) holds every corner
at full throttle. Two laps of ~4.6 k u are ~150 s at cruise; every boost buys ~300 u.

## 4. AI

- **An AI crew is two AIs** (Garrett, 2026-10-20: "AI vessels should be controlled by two AI, one for the
  wrasse, and one for the grouper"; `KiteNavigatorConfig.aiCrewNavigates`). On the machine that simulates
  it (the host), an AI Wrasse seats its kite too (`KiteNavigator.ShouldRun`: not the local pilot's hull,
  on autopilot, network-owned here), with no view, swap or human input:
  - **the Grouper's AI** is the hull's `AIPilot` on the gate-race approach steering, sized to the 32 u
    cruise circle: commit **72** / lead **80** / through **55** (Slingshot's ratios). Flying the hull
    through the ring puts the ring in the kite's plane, on its board. It spends a saved boost while the next
    ring is at least `aiBoostRingDistance` (180 u) away and roughly ahead (`AIPilotWantsBoost`).
    `Wrasse.prefab`'s `AIPilot` is enabled (asserted).
  - **the Wrasse's AI** is `KiteNavigatorAI` on the Sling: it drags the kite through the next ring
    (`TryGetNextRing`) whenever the board plane cuts its mouth, and otherwise whirls loops, preferring Time.
  - With `aiCrewNavigates` off, an AI hull seats no kite, its probe is never live, and its HULL threads.
- The hull's `AIPilot` names **no boost policy** (2026-10-26). The Sparrow's `SparrowAIBoostPolicy`, inherited with the
  clone, pressed a `BoostActionSO` the Wrasse does not bind, so it never did anything; the boost is the crew's
  (`AIPilotWantsBoost` above). `author_wrasse_assets.py` clears it in the clone and checks it.
- **A human's AI squad mate** (the human pilots, the AI navigates) is the same navigator AI on the same
  rope: through the ring first, aiming a point just past the mouth along the ring's axis, else loops.
- **A human navigator with an AI pilot** (the human took the kite with the swap): the hull's `AIPilot`
  steers toward the next ring on the platform's approach, so the ring comes to the board; the human
  slings the kite through it.

## 5. Numbers, and where they are authored

| Knob | Where | Shipped |
|---|---|---|
| race length (laps × rings) | `Resources/EndConditionOverrides` → `wrasseRunGateTarget` | **16** |
| laps | `MinigameWrasseRun.unity` → `WrasseRunController.laps` | **2** |
| rings per lap / base circle | `WrasseRunCourse.GatesPerLap` / `.BaseRadius` | **8** / **640** |
| corner profile, spread, floor, mouth, lift, presentation | `WrasseRunCourse.ForIntensity` | §3 |
| AI commit / lead / through | scene → `GateRaceController` | 72 / 80 / 55 |
| detection clamp (hull and kite) | scene → `maxPlausibleSpeed` | **5200** (2× the kite's ~2570 u/s worst world speed: 260 + 364 + the board turning under a kite at its far corner, ~890 u out: a 2.4:1 screen plus the 240 u it may fly past any edge) |
| Time mote bonus / AI Time preference | `KiteNavigatorConfig.asset` → `raceTimeMoteBonus`, `raceTimeLoopPreference` | 2 / 3 |
| the sling | `KiteNavigatorConfig.asset` → `sling*` | rope 42, stiffness 260, damping 5, drag 0.35 / 0.9, top 364 (round 7: 70% of 520); tail 4.8 board radii |
| comeback rate | `ArcadeGameWrasseRun.asset` | **0.5** (4 rings behind buys 2 levels) |

## 6. Assets — all authored by `Tools/Build/author_wrasse_run_assets.py`

The scene is cloned once from `MinigameThresherTime` (controller script, field block, fresh
`GlobalObjectIdHash`es) and then adopted as committed (`arcade_mode_lib.committed_scene`). The generator
authors the card (`ArcadeGameWrasseRun`, Vessels = `SO_Class_Wrasse`), the scoring rule, the toasts
(`GameToastConfig_WrasseRun`), the preview (`ModePreview_WrasseRun`), the build-scene row, the
always-unlocked row, both rosters and the end-condition target, and asserts the comeback rate, the lap
arithmetic, the constants it shares with the C#, the HUD provider row, the clamp against the kite, the AI
approach against the cruise circle, the AI prefab state, the hull's numbers and the toybox roster.
`--check` diffs every file. `arcade_mode_lib.VESSELS` gained the Wrasse.

**Card art:** `CardBackgrounds/WrasseRun.png`, rendered offline by `/cardart` (the COURSE tier: the
intensity-2 circuit from the shipped `HeadlongCircuit`).

## 7. Verification status

**Authored headless, not yet run in the editor** (no Unity in the session that built it).

- The course was compiled and run out of editor with the shipped `HeadlongCircuit` (200 seeds × 4
  levels: 8 gates, inside the shell, no corner under its floor). `WrasseRunCourseTests` holds the same
  in the editor.
- The Sling is held to the Wrasse Lasso Lab page frame by frame by
  `Tools/Build/kite_navigator_lab/sling_parity.cjs` (shipped C# compiled from its real path).

### In-editor verification

1. **Arcade card.** Menu → Arcade: **Wrasse Run** appears; its launch panel pins the vessel to the Wrasse
   (the card reads "Wrasse and Grouper").
2. **Launch at intensity 1, 2 players.** Eight rings bloom in a closed loop; your next ring is lit lime
   and the arrow points at it. The goal row reads 0/16.
3. **The hull alone does nothing.** Fly the Grouper straight through the lit ring: no credit.
4. **The Wrasse collects.** With the AI navigating, fly the ring into the kite view's board: the AI flies
   the kite through it and the ring counts. Swap to the kite (2 / D-pad right / double-tap the small
   view), let the AI pilot bring the ring to you, sling the kite through it: it counts once.
5. **Time is the fuel.** In the race, more of the board's motes are Time diamonds; a Time loop banks
   boosts, and the right trigger (both thumbs lifted on a phone) spends one at ~260 u/s.
6. **AI crews.** Add AI Wrasses: each one's Wrasse (simulated on the host, its body seen by everyone) slings through its rings and
   loops Time; its hull boosts on the straights; they finish. A pad's stick places the rope's hand like
   a mouse.
7. **Two clients.** A client's ring reports are credited once.
8. **Regressions.** Thresher Time's ball still threads; Headlong, Redline, Grizzly Time unchanged.

## 8. Known limitations / follow-ups

- **Not editor-verified.** Nobody has flown it.
- **The kite runs locally; its body is seen everywhere.** Like everything on the seat, the kite runs on the
  machine that flies the hull (its human's, or the host for an AI crew). Ring credit is owner-detected and
  server-recorded, so scores agree. Since round 7 every peer sees the Wrasse's body where the kite flies
  (`WrasseCrewBodies`); the board, motes and effects (blasts, Mass clusters) stay on the flying machine.
- **Card art** (`/cardart`) and an idle hint toast are follow-ups.
