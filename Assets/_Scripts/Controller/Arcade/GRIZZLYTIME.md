# Grizzly Time — the Grizzly-only circuit race

> `GameModes.GrizzlyTime = 63`. A closed loop of switch rings is cut through the cell and every
> pilot flies **two laps** of it in order; the first **domain** whose **lead runner** threads the
> last gate of the last lap wins, on finish **time** (golf). The race the Grizzly was asked for,
> alongside its existing arena mode **Grizzly Charge** (`62`).

## 1. What the mode is asking

The Grizzly is the fleet's slowest, tightest hull: it cruises at **50 u/s** and turns at
`50 × 0.1 + 90` = **95 °/s** — a **30 u** circle. Everything past cruise is the **bomb pump**
(`R_VesselActions/GRIZZLY_BOMB_PUMP.md`): each trigger release blows a bomb that adds up to
40 u/s along the nose for 0.8 s, each trigger on its own 0.45 s clock, so alternating LT → RT lands
a kick every 0.225 s and the stacked kicks sit on the vessel's **100 u/s** velocity-modifier
ceiling. Pumped flat out: **150 u/s**, three times cruise.

Those kicks are a **world-space** velocity (`VesselTransformer.velocityShift`). They keep going
the way the nose pointed when they were blown, and the turn rate never sees them. So a corner is
one question:

> **How much pump is this corner worth?**

| pump held | speed | circle it holds | reads as |
|---|---|---|---|
| full | **150 u/s** | **90 u** | both triggers alternating, full squeezes |
| ~half | ~120 | ~72 | feathering the bombs |
| none | 50 | **30 u** | triggers released — a pivot, after a ~50 u slide on the old heading |

Lifting is not free twice over: the live kicks carry the hull up to **~53 u** along its old
heading before the pivot bites (`GrizzlyTimeCourse.SlideAfterLift`), so a lift has to come a beat
**before** the gate — and afterwards the pump takes three bombs (~0.7 s) to wind back to the
ceiling. That is the same shape as the Manta's Soar trade in Redline, at a third of the scale.

**Intensity is how many corners a lap asks that question at.** Measured over 400 seeds, the
median lap (`GrizzlyTimeCourseTests`):

| intensity | corners that cost pump | hardest corner | 2nd | 3rd | mouth radius | lap |
|---|---|---|---|---|---|---|
| 1 | **0** of 14 | 253 u · 100% | 283 u · 100% | 302 u · 100% | 64 | ~3.8 k u |
| 2 | **1** of 14 | 69 u · **76%** | 181 u · 100% | 201 u · 100% | 52 | ~4.0 k u |
| 3 | **2** of 14 | 54 u · **59%** | 69 u · 76% | 101 u · 100% | 42 | ~4.5 k u |
| 4 | **3** of 14 | 43 u · **47%** | 57 u · 63% | 78 u · 87% | 34 | ~4.7 k u |

Percentages are of the 150 u/s top speed via `FastestSpeedForCorner`. A clean two-lap race is
~55–65 s pumped flat out; the same race at cruise is three times that.

## 2. What the mode does NOT add

No new weapon, no new ability, no cell of its own, no new scoring metric, and **no circuit
generator**. It reuses:

- `ScoringMetric.SwitchesThreaded` (9), the `BestByDomain` fold, the goal-stack row, the objective
  icon — all keyed on the metric.
- `GateRaceScoringRuleSO` — one class, one asset per mode (`GrizzlyTimeScoringRule.asset`).
- `GateRaceController` and the whole gate-race platform (course broadcast, rings, crossing
  detection, owner-detects/server-records, AI steering, final scores).
- **`HeadlongCircuit`**, the shared closed-circuit solver. `GrizzlyTimeCourse` supplies the
  Grizzly's settings exactly as `RedlineCourse` supplies the Manta's; the solver is untouched
  (Redline's 14-test suite still passes against it).
- The Grizzly's shipped kit for the racing: the bomb pump is the throttle, **Rush** (B) is a burst
  onto a straight, **Dig In** (A) is a hard stop, and a bomb blown across a rival's trail breaks it
  (the blast spares only your own domain). None of that is scored; the race is.

## 3. The cut — why fourteen gates on a small circle

A corner's radius on this metric is `(shorter leg / 2) / tan(turn / 2)`. Redline's eight-gate
circle at 820 leaves 466–868 u legs, which no Grizzly corner could ever fall inside 90 u on. Three
things moved, each measured with the shipped solver compiled out of editor:

- **Fourteen gates a lap** on a **560 u** base circle (just outside the nucleus shell at 480):
  legs of 250–420 u, one and a half to three seconds of pump each. Ten gates at 620 left 375–570 u
  legs and the ladder did not separate (levels 2 and 3 both produced zero-or-one costing corner).
- **The reach dial is `AngularSpread`, not the profile** — REDLINE.md §4's finding, reproduced.
  Levels 2–4 ask for 140°, 150°+135° and 160°+150°+50° and only produce them at spreads of
  3 / 6 / 10. At 14 gates the profile asking three ~110–130° corners (the obvious level-4 row)
  produced one costing corner; two near-hairpins plus a 50° kink produce three.
- **`RadialSwing` is inert on this cut** (the flat ring already sits against the nucleus shell);
  kept at 0.42 so the three cuts read alike.

The safety floor is stated in absolute Grizzly units (`CornerFloorRadius` 72 / 45 / 41 / 38 u),
never below the 30 u cruise pivot. Presentation caps (50 / 82 / 84 / 86°) each sit over the
level's measured worst half-turn (44 / 79 / 77 / 80°).

## 4. The AI pumps — a vessel change, not a mode one

`AIPilot` writes the stick and the throttle and nothing else, so before this an AI Grizzly could
never bomb: the pump read trigger pressure from `InputStatus`, which an autopilot never writes. In
a race cut around the full-pump circle that made every bot a 50 u/s obstacle — and an all-AI domain
that cannot play is a defect. `GrizzlyBombPumpExecutor` now carries an **autopilot drive**
(Redline's autopilot Soar, REDLINE.md §5, on the Grizzly's kit): while `AIPilot.AutoPilotEnabled`
it alternates LT/RT on the triggers' own cooldowns, with a bomb size that is **how straight the
stick is** — full inside `GrizzlyBombPumpConfig.aiPumpStickBand` (0.35), easing to the smallest
bomb at a full deflection. Gated on the pilot being an autopilot, so the menu's lava-lamp Grizzly
and a released companion now pump too. `0` disables the drive.

Two prefab defects had to go for any of that to matter (both on `Grizzly.prefab`, both from the
pre-restoration prefab):

- **`AIPilot` was serialized disabled** (`m_Enabled: 0`). `StartAIPilot` does not enable the
  component and steering runs in `Update`, so an AI Grizzly never steered — in Grizzly Charge too.
  Every other hull ships it enabled. Now enabled; the generator asserts it.
- **AI throttle was pinned at 0.6** (a 30 u/s cruise). Now 0.7 → 1.0 across skill (skill =
  intensity × 0.25), so a level-4 bot cruises at the full 50.

AI approach numbers are Redline's scaled to the Grizzly's 90 u circle: commit **160** / lead
**180** / through **120** (Redline: 420 / 480 / 320 against 237 u).

## 5. Numbers, and where they are authored

| Knob | Where | Shipped |
|---|---|---|
| race length (laps × rings) | `Resources/EndConditionOverrides` → `grizzlyTimeGateTarget` | **28** |
| laps | `MinigameGrizzlyTime.unity` → `GrizzlyTimeController.laps` | **2** |
| rings per lap / base circle | `GrizzlyTimeCourse.GatesPerLap` / `.BaseRadius` | **14** / **560** |
| corner profile, spread, floor, mouth, presentation | `GrizzlyTimeCourse.ForIntensity` | §1, §3 |
| AI commit / lead / through | scene → `GateRaceController` | 160 / 180 / 120 |
| detection clamp | scene → `maxPlausibleSpeed` | **600** |
| autopilot pump band | `GrizzlyBombPumpConfig.asset` → `aiPumpStickBand` | 0.35 |
| comeback rate | `ArcadeGameGrizzlyTime.asset` | **0.25** (7 gates behind buys 1.75 levels) |
| course shell | scene → `courseOuterRadius` / `courseInnerRadiusFallback` | 1080 / 480 |

Every vessel number the cut depends on is restated as a constant on `GrizzlyTimeCourse` and
**read back off `Grizzly.prefab` and `GrizzlyBombPumpConfig.asset`** by
`Course_constants_match_the_shipped_Grizzly_and_pump` — retune the hull or the pump and the test
names the constant that moved.

## 6. Assets — all authored by `Tools/Build/author_grizzly_time_assets.py`

The scene is cloned once from `MinigameRedline` (controller script, field block, fresh project-
unique `GlobalObjectIdHash`es) and then adopted as committed (`arcade_mode_lib.committed_scene`).
The generator authors the card, the scoring rule, the preview (`ModePreview_GrizzlyTime`,
registered in `Resources/ModePreviewLibrary`), the build-scene row, the always-unlocked row, both
rosters (`OrganicRematchGames` + `ArcadeGames`) and the end-condition target, and asserts the
comeback rate, the lap arithmetic, the constants it shares with the C#, the AI prefab state, the
autopilot pump band and the toybox roster. `--check` diffs every file.

## 7. In-editor verification

1. **Arcade card.** Menu → Arcade: **Grizzly Time** and **Grizzly Charge** both appear and are
   clickable on a fresh account; Grizzly Time's launch panel pins the vessel to **Grizzly**.
2. **Launch at intensity 1, 2 players.** Fourteen rings bloom in a closed loop; your next gate is
   lit and the objective arrow points at it.
3. **Pump a lap.** Alternate LT/RT with full squeezes: speed should sit near 150. At intensity 1
   nothing should make you lift.
4. **Lap wrap.** After gate 14 the lit ring returns to gate 1 and the goal row reads 14/28.
5. **Intensity 4.** A median lap has two near-hairpins that punish pumping through them (you
   swing wide of the next mouth) and a third corner you can just hold by feathering.
6. **AI.** Add AI Grizzlies: they must **steer** (the `AIPilot` enable), **bomb rhythmically on the
   straights and soften in the corners** (the autopilot pump), and complete more than one lap.
7. **MPPM two clients.** Both see the same circuit; a client's gate reports are credited once;
   each peer sees the other's bombs at matching sizes.
8. **Regressions.** Redline and Headlong unchanged; the menu lava-lamp Grizzly now pumps — if
   that reads too busy, the dial is `aiPumpStickBand` (0 disables).

## 8. Known limitations / follow-ups

- **Not editor-verified.** Authored headless: the course and its 14-test suite were compiled and
  run out of editor (Roslyn, against the card-art harness's Unity shim) over 400 seeds × 4
  intensities, and the suite was watched failing under a mutated course. Nobody has flown it.
- **The pump curve is the steady-state circle.** In a sustained turn the live kicks point along
  the last 0.8 s of nose headings, so the real pumped path lags a little wide of 90 u; every
  costing corner sits well inside it (≤ 78 u at the median), so the lag moves no corner across the
  line — but a play-test may want the level-2 corner tighter.
- **Card art** is the `/cardart` COURSE-tier render (`CardBackgrounds/GrizzlyTime.png`): the
  shipped `GrizzlyTimeCourse` at intensity 2 through the card-art harness, the `GrizzlyTime` branch
  of Redline's recipe in `render_card_backgrounds.py`. Judged at card size; not yet seen in the grid.
- **No toasts of its own** — the gate-race platform's stat toasts only, as Redline.
- **The AI never uses Rush or the cannon self-launch**; its edge is the pump alone.
