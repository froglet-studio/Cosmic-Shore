# Grizzly Time — the Grizzly-only circuit race

> `GameModes.GrizzlyTime = 63`. A closed loop of switch rings is cut through the cell and every
> pilot flies **three laps** of it in order; the first **domain** whose **lead runner** threads the
> last gate of the last lap wins, on finish **time** (golf). The race the Grizzly was asked for,
> alongside its existing arena mode **Grizzly Charge** (`62`).

## 1. What the mode is asking

The Grizzly is the fleet's slowest, tightest hull: it cruises at **50 u/s** and turns at
`50 × 0.1 + 90` = **95 °/s** — a **30 u** circle. Everything past cruise is **riding its own
blasts** (`R_VesselActions/GRIZZLY_TRIGGER_BOMBS.md`): LT and RT each fire a bomb, a second pull
freezes it, the release detonates it, and a Grizzly inside its own blast is **thrown away from
the bomb** — so the race is fire, freeze, fly past, blow it behind you. A full squeeze's blast
(scale 200) hands over `200 / 1.2 s × 4.5` = 750 u/s at the bomb, eased 1.5 → 0.5 over a second
and clamped by the launch's own **300 u/s** ceiling (`selfLaunchCeiling` — three times the 100 u/s
every other shove shares; the launch was tripled on 2026-10-08) — so a full launch ridden close
sits on that ceiling for its whole second and carries the hull **~300 u**. Launching flat out:
**350 u/s**, seven times cruise.

That push is a **world-space** velocity (`VesselTransformer.velocityShift`). It keeps going the
way it was thrown when the bomb went off, and the turn rate never sees it. So a corner is one
question:

> **How much launch is this corner worth?**

| launch held | speed | circle it holds | reads as |
|---|---|---|---|
| full | **350 u/s** | **211 u** | riding a full-squeeze blast |
| ~half | ~200 | ~120 | a launch bleeding off |
| none | 50 | **30 u** | coasting — a pivot, once the last launch has carried its ~300 u |

A launch taken into a corner throws you wide of it; a launch saved for after the corner costs the
time it takes to fire, freeze and ride the next bomb. **Ammo, not the ceiling, sets the sustained
pace:** a full launch costs 0.35 of a pool that refills at 0.15/s, so a pilot who launches as
often as the pool allows averages ~180 u/s (cruise plus ~130 u/s of launch carry) and bursts to 350
— the opening full pool is worth three launches. That is the Manta's Soar trade in Redline, with
the boost meter replaced by a magazine.

**Intensity is how many corners a lap asks that question at.** Measured over 400 seeds, the
median lap (`GrizzlyTimeCourseTests`):

| intensity | corners that cost launch | hardest corner | 2nd | 3rd | mouth radius | lap |
|---|---|---|---|---|---|---|
| 1 | **0** of 8 | 321 u · 100% | 387 u · 100% | 487 u · 100% | 96 | ~4.7 k u |
| 2 | **1** of 8 | 161 u · **76%** | 299 u · 100% | 353 u · 100% | 72 | ~4.9 k u |
| 3 | **2** of 8 | 136 u · **64%** | 149 u · 71% | 306 u · 100% | 58 | ~5.1 k u |
| 4 | **3** of 8 | 98 u · **47%** | 115 u · 54% | 190 u · 90% | 46 | ~5.2 k u |

Percentages are of the 350 u/s top speed via `FastestSpeedForCorner`. A three-lap race is
~15 k u: ~45 s if every metre were flown on the ceiling, ~85 s at the ammo-limited sustained pace,
five minutes coasting. The first cut (2026-10-06) was fourteen gates on a 560 u ring against a
150 u/s top speed; tripling the launch (2026-10-08) made every one of those legs shorter than a
single launch's carry, so the circuit was re-cut (§3).

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

## 3. The cut — Headlong's octagon, against the 211 u circle

A corner's radius on this metric is `(shorter leg / 2) / tan(turn / 2)`. With the launch tripled,
the Grizzly's full-launch circle is **211 u** and a full launch carries **~300 u** — the Rhino's
territory, so the cut is Headlong's, re-measured with the shipped solver compiled out of editor:

- **Eight gates a lap** on Headlong's **800 u** base circle, three laps a race (Headlong's and
  Redline's 24). Legs of 475–610 u at the median: one launch's carry and half again on every leg
  (`Legs_are_long_enough_to_launch_down`). The old fourteen gates on 560 u left 250–420 u legs —
  shorter than one launch now carries — and no shorter lap fits a hairpin with long enough legs
  inside the 480–1080 shell.
- **The reach dial is `AngularSpread`** — REDLINE.md §4's finding, again. Profiles were swept over
  four spreads per level and picked so each level's hardest corner costs ≥ 8 points more than the
  last: level 2 asks one 110° corner and needs spread 4 to make it cost anything; level 3 asks
  150° + 135° half a lap apart, which pull the ring into a lens at spread 2 on their own; level 4 is
  140° + 135° + 70° at spread 4 — a triangle with three braking zones.
- **`RadialSwing` 0.42**, Headlong's.

The safety floor is stated in absolute Grizzly units (`CornerFloorRadius` 169 / 106 / 95 / 89 u),
never below the 30 u cruise pivot. Presentation caps (50 / 70 / 72 / 76°) each sit over the
level's measured worst half-turn (44 / 68 / 68 / 73°). Mouths are Headlong's (96 / 72 / 58 / 46):
the Grizzly now arrives at a Rhino's pace, on a push it cannot steer.

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

AI approach numbers are Redline's ratios on the Grizzly's 211 u circle: commit **380** / lead
**420** / through **285** (Redline: 420 / 480 / 320 against 237 u).

## 5. Numbers, and where they are authored

| Knob | Where | Shipped |
|---|---|---|
| race length (laps × rings) | `Resources/EndConditionOverrides` → `grizzlyTimeGateTarget` | **24** |
| laps | `MinigameGrizzlyTime.unity` → `GrizzlyTimeController.laps` | **3** |
| rings per lap / base circle | `GrizzlyTimeCourse.GatesPerLap` / `.BaseRadius` | **8** / **800** |
| corner profile, spread, floor, mouth, presentation | `GrizzlyTimeCourse.ForIntensity` | §1, §3 |
| AI commit / lead / through | scene → `GateRaceController` | 380 / 420 / 285 |
| detection clamp | scene → `maxPlausibleSpeed` | **1400** (4× the 350 top speed) |
| autopilot bomb-jump | `GrizzlyTriggerBombConfig.asset` → `aiFireStickBand` / `aiFreezeDistance` | 0.35 / 18 |
| launch strength / ceiling | `GrizzlyTriggerBombConfig.asset` → `selfLaunchMultiplier` / `selfLaunchCeiling`; `maxBlastScale` | 4.5 / 300; 200 |
| ammo (the sustained pace) | `Grizzly.prefab` Ammo `resourceGainRate`; `maxAmmoCost` | 0.15/s; 0.35 |
| comeback rate | `ArcadeGameGrizzlyTime.asset` | **0.3** (6 gates behind buys 1.8 levels) |
| course shell | scene → `courseOuterRadius` / `courseInnerRadiusFallback` | 1080 / 480 |

Every vessel number the cut depends on is restated as a constant on `GrizzlyTimeCourse` and
**read back off `Grizzly.prefab`, `GrizzlyTriggerBombConfig.asset` and
`AOEGrizzlyExplosion.prefab`** by
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
2. **Launch at intensity 1, 2 players.** Eight rings bloom in a closed loop; your next gate is
   lit and the objective arrow points at it.
3. **Bomb-jump a lap.** Fire, freeze just ahead, ride the blast; alternate LT/RT. Each launch
   should throw you to ~350 for about a second and carry you ~300 u. At intensity 1 every corner
   can be taken with a launch running.
4. **Lap wrap.** After gate 8 the lit ring returns to gate 1 and the goal row reads 8/24.
5. **Intensity 4.** A median lap is a triangle: two near-hairpins that punish launching into
   them (you swing wide of the next mouth) and a third you can hold with most of a launch.
6. **AI.** Add AI Grizzlies: they must **steer** (the `AIPilot` enable), **fire, freeze and ride
   bombs on the straights** (the autopilot bomb-jump), and complete more than one lap.
7. **MPPM two clients.** Both see the same circuit; a client's gate reports are credited once;
   each peer sees the other's bombs at matching sizes.
8. **Regressions.** Redline and Headlong unchanged; the menu lava-lamp Grizzly now bomb-jumps —
   if that reads too busy, the dial is `aiFireStickBand` (0 disables).

## 8. Known limitations / follow-ups

- **Not editor-verified.** Authored headless: the course and its test suite were compiled and
  run out of editor (Roslyn, against the card-art harness's Unity shim) over 400 seeds × 4
  intensities, and the suite was watched failing under a mutated course. Nobody has flown it.
- **The launch curve is the steady-state circle.** A launch keeps the direction it was thrown in
  when the bomb went off, so a turn taken mid-launch slides wide of 211 u; the level-4 corners sit
  well inside it (≤ 115 u at the median), but level 2's single corner (161 u) is closer to the
  line, so a play-test may want it tighter.
- **The launch cannot be steered.** At 300 u/s of world-space push, a launch aimed badly carries
  the hull ~300 u the wrong way. The mouths are Headlong's for that reason; if threading still
  reads as luck, the dials are `RingRadius` or `selfLaunchSeconds` (shorter launch, same peak).
- **Ammo sets the sustained pace (~180 u/s), and it is a first-pass number.** If races feel like
  coasting between launches, the dial is the Ammo `resourceGainRate` on `Grizzly.prefab` (0.15/s)
  or `maxAmmoCost` (0.35); neither moves the corner ladder, which is cut against the ceiling.
- **Card art** is the `/cardart` COURSE-tier render (`CardBackgrounds/GrizzlyTime.png`): the
  shipped `GrizzlyTimeCourse` at intensity 2 through the card-art harness, the `GrizzlyTime` branch
  of Redline's recipe in `render_card_backgrounds.py`. Judged at card size; not yet seen in the grid.
- **No toasts of its own** — the gate-race platform's stat toasts only, as Redline.
- **The AI never uses Rush or the charged cannon**; its edge is the trigger-bomb launch alone, always full-size.
