# Grizzly Time — the Grizzly-only circuit race

> `GameModes.GrizzlyTime = 63`. A closed loop of switch rings is cut through the cell and every
> pilot flies **two laps** of it in order; the first **domain** whose **lead runner** threads the
> last gate of the last lap wins, on finish **time** (golf). The race the Grizzly was asked for,
> alongside its existing arena mode **Grizzly Charge** (`62`).

## 1. What the mode is asking

The Grizzly is the fleet's slowest, tightest hull: it cruises at **50 u/s** and turns at
`50 × 0.1 + 90` = **95 °/s** — a **30 u** circle. Everything past cruise is **riding its own
blasts** (`R_VesselActions/GRIZZLY_TRIGGER_BOMBS.md`): LT and RT each fire a bomb, a second pull
freezes it, the release detonates it, and a Grizzly inside its own blast is **thrown away from
the bomb** — so the race is fire, freeze, fly past, blow it behind you. A full squeeze's blast
(scale 200) hands over `200 / 1.2 s × 1.5` = 250 u/s at the bomb, eased 1.5 → 0.5 over a second
and clamped by the vessel's **100 u/s** velocity-modifier ceiling — so a full launch ridden close
sits on the ceiling for its whole second and carries the hull **~100 u**. Launching flat out:
**150 u/s**, three times cruise.

That push is a **world-space** velocity (`VesselTransformer.velocityShift`). It keeps going the
way it was thrown when the bomb went off, and the turn rate never sees it. So a corner is one
question:

> **How much launch is this corner worth?**

| launch held | speed | circle it holds | reads as |
|---|---|---|---|
| full | **150 u/s** | **90 u** | riding a full-squeeze blast |
| ~half | ~100 | ~60 | a half-squeeze blast, or a full one bleeding off |
| none | 50 | **30 u** | coasting — a pivot, once the last launch has carried its ~100 u |

A launch taken into a corner throws you wide of it; a launch saved for after the corner costs the
time it takes to fire, freeze and ride the next bomb. **Ammo, not the ceiling, sets the sustained
pace:** a full launch costs 0.35 of a pool that refills at 0.15/s, so a pilot who launches as
often as the pool allows averages ~90 u/s (cruise plus ~41 u/s of launch carry) and bursts to 150
— the opening full pool is worth three launches. That is the Manta's Soar trade in Redline, at a
third of the scale, with the boost meter replaced by a magazine.

**Intensity is how many corners a lap asks that question at.** Measured over 400 seeds, the
median lap (`GrizzlyTimeCourseTests`):

| intensity | corners that cost launch | hardest corner | 2nd | 3rd | mouth radius | lap |
|---|---|---|---|---|---|---|
| 1 | **0** of 14 | 253 u · 100% | 283 u · 100% | 302 u · 100% | 64 | ~3.8 k u |
| 2 | **1** of 14 | 69 u · **76%** | 181 u · 100% | 201 u · 100% | 52 | ~4.0 k u |
| 3 | **2** of 14 | 54 u · **59%** | 69 u · 76% | 101 u · 100% | 42 | ~4.5 k u |
| 4 | **3** of 14 | 43 u · **47%** | 57 u · 63% | 78 u · 87% | 34 | ~4.7 k u |

Percentages are of the 150 u/s top speed via `FastestSpeedForCorner`. A two-lap race is ~8 k u:
~55 s if every metre were flown on the ceiling, ~90 s at the ammo-limited sustained pace, three
minutes coasting. The ladder was measured when the speed came from a bomb pump (2026-10-06); the
pump and the launch share the same ceiling and the same turn rate, so the ladder carried over
unchanged — `GrizzlyTimeCourseTests` re-ran it against the launch model.

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
- The Grizzly's shipped kit for the racing: the trigger bombs are the throttle, **Rush** (B) is a burst
  onto a straight, **Dig In** (A) is a hard stop, and a bomb blown across a rival's trail breaks it
  (the blast spares only your own domain). None of that is scored; the race is.

## 3. The cut — why fourteen gates on a small circle

A corner's radius on this metric is `(shorter leg / 2) / tan(turn / 2)`. Redline's eight-gate
circle at 820 leaves 466–868 u legs, which no Grizzly corner could ever fall inside 90 u on. Three
things moved, each measured with the shipped solver compiled out of editor:

- **Fourteen gates a lap** on a **560 u** base circle (just outside the nucleus shell at 480):
  legs of 250–420 u, two and a half to four and a half full launches' carry each. Ten gates at 620 left 375–570 u
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

## 4. The AI bomb-jumps — a vessel change, not a mode one

`AIPilot` writes the stick and the throttle and nothing else, so an AI Grizzly can never press a
trigger. In a race cut around the full-launch circle that would make every bot a 50 u/s obstacle —
and an all-AI domain that cannot play is a defect. `GrizzlyTriggerBombExecutor` carries an
**autopilot drive** (Redline's autopilot Soar, REDLINE.md §5, on the Grizzly's kit): while
`AIPilot.AutoPilotEnabled` and the stick is straight (inside `aiFireStickBand` 0.35), it fires a
full bomb, freezes it 18 u ahead, flies past it and detonates it 10 u behind — thrown forward, away
from it — through the REPLICATED press/release, so every peer runs the same bomb. Gated on the pilot being an autopilot,
so the menu's lava-lamp Grizzly and a released companion bomb-jump too. `0` disables the drive.

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
| autopilot bomb-jump | `GrizzlyTriggerBombConfig.asset` → `aiFireStickBand` / `aiFreezeDistance` | 0.35 / 18 |
| launch strength | `VesselImpulseByExplosionEffect.asset` → `selfLaunchMultiplier`; trigger-bomb `maxBlastScale` | 1.5; 120 |
| ammo (the sustained pace) | `Grizzly.prefab` Ammo `resourceGainRate`; `maxAmmoCost` | 0.15/s; 0.35 |
| comeback rate | `ArcadeGameGrizzlyTime.asset` | **0.25** (7 gates behind buys 1.75 levels) |
| course shell | scene → `courseOuterRadius` / `courseInnerRadiusFallback` | 1080 / 480 |

Every vessel number the cut depends on is restated as a constant on `GrizzlyTimeCourse` and
**read back off `Grizzly.prefab`, `GrizzlyTriggerBombConfig.asset`,
`VesselImpulseByExplosionEffect.asset` and `AOEGrizzlyExplosion.prefab`** by
`Course_constants_match_the_shipped_Grizzly_and_its_launch` — retune the hull, the bombs or the
launch and the test names the constant that moved.

## 6. Assets — all authored by `Tools/Build/author_grizzly_time_assets.py`

The scene is cloned once from `MinigameRedline` (controller script, field block, fresh project-
unique `GlobalObjectIdHash`es) and then adopted as committed (`arcade_mode_lib.committed_scene`).
The generator authors the card, the scoring rule, the preview (`ModePreview_GrizzlyTime`,
registered in `Resources/ModePreviewLibrary`), the build-scene row, the always-unlocked row, both
rosters (`OrganicRematchGames` + `ArcadeGames`) and the end-condition target, and asserts the
comeback rate, the lap arithmetic, the constants it shares with the C#, the AI prefab state, the
autopilot bomb-jump (on, and freezing inside its own blast) and the toybox roster. `--check` diffs every file.

## 7. In-editor verification

1. **Arcade card.** Menu → Arcade: **Grizzly Time** and **Grizzly Charge** both appear and are
   clickable on a fresh account; Grizzly Time's launch panel pins the vessel to **Grizzly**.
2. **Launch at intensity 1, 2 players.** Fourteen rings bloom in a closed loop; your next gate is
   lit and the objective arrow points at it.
3. **Bomb-jump a lap.** Fire, freeze just ahead, ride the blast; alternate LT/RT. Each launch
   should throw you to ~150 for about a second. At intensity 1 every corner can be taken with a
   launch running.
4. **Lap wrap.** After gate 14 the lit ring returns to gate 1 and the goal row reads 14/28.
5. **Intensity 4.** A median lap has two near-hairpins that punish launching into them (you
   swing wide of the next mouth) and a third you can just hold on a half-squeeze launch.
6. **AI.** Add AI Grizzlies: they must **steer** (the `AIPilot` enable), **fire, freeze and ride
   bombs on the straights** (the autopilot bomb-jump), and complete more than one lap.
7. **MPPM two clients.** Both see the same circuit; a client's gate reports are credited once;
   each peer sees the other's bombs at matching sizes.
8. **Regressions.** Redline and Headlong unchanged; the menu lava-lamp Grizzly now bomb-jumps —
   if that reads too busy, the dial is `aiFireStickBand` (0 disables).

## 8. Known limitations / follow-ups

- **Not editor-verified.** Authored headless: the course and its 14-test suite were compiled and
  run out of editor (Roslyn, against the card-art harness's Unity shim) over 400 seeds × 4
  intensities, and the suite was watched failing under a mutated course. Nobody has flown it.
- **The launch curve is the steady-state circle.** A launch keeps the direction it was thrown in
  when the bomb went off, so a turn taken mid-launch slides wide of 90 u; every costing corner sits well
  inside it (≤ 78 u at the median), so the slide moves no corner across the line — but a play-test
  may want the level-2 corner tighter.
- **Ammo sets the sustained pace (~90 u/s), and it is a first-pass number.** If races feel like
  coasting between launches, the dial is the Ammo `resourceGainRate` on `Grizzly.prefab` (0.15/s)
  or `maxAmmoCost` (0.35); neither moves the corner ladder, which is cut against the ceiling.
- **Card art** is the `/cardart` COURSE-tier render (`CardBackgrounds/GrizzlyTime.png`): the
  shipped `GrizzlyTimeCourse` at intensity 2 through the card-art harness, the `GrizzlyTime` branch
  of Redline's recipe in `render_card_backgrounds.py`. Judged at card size; not yet seen in the grid.
- **No toasts of its own** — the gate-race platform's stat toasts only, as Redline.
- **The AI never uses Rush or the charged cannon**; its edge is the trigger-bomb launch alone, always full-size.
