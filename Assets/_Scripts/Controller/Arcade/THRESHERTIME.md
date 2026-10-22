# Thresher Time — the Thresher-only circuit race

> `GameModes.ThresherTime = 66`. A closed loop of switch rings is cut through the cell and every
> pilot flies **three laps** of it in order; the first **domain** whose **lead runner** threads the
> last gate of the last lap wins, on finish **time** (golf). Grizzly Time's shape (itself Redline's
> on Headlong's solver), cut against the Thresher's sling. The hull: `R_VesselActions/THRESHER.md`.
> **A gate is threaded by the hull OR the ball** (Garrett, 2026-10-19; `IRingThreadProbe`, THRESHER.md
> §5b): the same mouth test on the ball's segment, so a chain swung through the lit ring counts.

## 1. What the mode is asking

The Thresher cruises at **125 u/s** at full throttle (`DefaultMinimumSpeed` 15 + `DefaultThrottleScaler`
110 on `Thresher.prefab`) and turns at a flat **120 °/s** — `RotationThrottleScaler` is 0, so the
rate never grows with speed and the circle grows in proportion to it: **60 u** at cruise.

Everything past cruise is **slinging** (THRESHER.md §4–§4a, §5a). Pull LT all the way and the ball
stops and plants; the ship hooks onto an orbit round it and the orbit spins up; RT is the chain's
rigidity, and pulling it winds the orbit in, which spins it up further by angular momentum
(`ThresherChainSolver.StepElasticRadius`: the orbit's speed scales by old radius / new radius). Let
go and the hull leaves the circle at the orbit's speed. The orbit is clamped by the BALL's ceiling,
`maxBall × gameCruise / sandboxCruise` = 2600 × 70 / 380 = **479 u/s** (`ThresherConfig.asset`), so
that is the sling's top speed — nearly four times cruise.

Measured with the **shipped solver** at `ThresherConfig.asset`'s analog dials (the scratch harness
of §7, 60 Hz, planting from a 125 u/s tow; "carried" is the distance the release covers above
cruise while it bleeds back down):

| how the sling was wound | release speed | its circle | carried above cruise |
|---|---|---|---|
| rigid plant (RT held), let go at 0.5 s | 136 u/s | 65 u | ~0 |
| rigid plant, let go at 1 s | ~230 | 110 u | 58 u |
| slack orbit 1 s, then RT wound in for 0.6 s | 270 | 129 u | 88 u |
| slack orbit 2 s + wind-in | 362 | 173 u | 143 u |
| slack orbit 3 s + wind-in | ~451 | 215 u | 192 u |
| the ceiling (`SlingCeiling`) | **479** | **229 u** | ~200 (236 analytic, without the ball's tug) |

**A sling is the hull's OWN momentum, not a push.** The Thresher flies the vector flight model,
whose grip snaps momentum onto the nose every frame outside a drift, so — unlike the Grizzly's
world-space blast launch, which the turn rate never sees — the hull turns ALL of a sling at its 120
°/s, on a circle that grows with the speed. And the throttle's step pulls the speed back to cruise at
`LERP_AMOUNT` **1.5/s** (`VesselTransformer.cs`; the towed ball's tug makes it a little faster — a 452
u/s release is at ~192 u/s a second later). So a corner is one question:

> **How much sling is this corner worth?**

A corner wider than the full-sling circle (229 u) keeps a ceiling sling; a tighter one throws it
wide, and the pilot either lets the sling bleed before the turn-in or **turns the corner by planting
at it** — a plant is itself a corner (a rigid plant had swung the hull 155° half a second after the
pull) and the next sling. The orbit is time not spent going round the track: a three-second wound
sling buys ~190 u of excess, so **a sling pays as a CORNER, not on a straight** — the skill is
planting where the course turns anyway and letting go on the next leg's line.

**Intensity is how many corners a lap asks that question at.** Measured over 400 seeds, the median
lap (`ThresherTimeCourseTests`; percentages are of the 479 u/s top speed via
`FastestSpeedForCorner`):

| intensity | corners that cost sling | hardest corner | 2nd | 3rd | mouth radius | lap |
|---|---|---|---|---|---|---|
| 1 | **0** of 8 | 321 u · 100% | 387 u · 100% | 487 u · 100% | 96 | ~4.7 k u |
| 2 | **1** of 8 | 161 u · **71%** | 314 u · 100% | 363 u · 100% | 72 | ~4.9 k u |
| 3 | **2** of 8 | 136 u · **59%** | 149 u · 65% | 306 u · 100% | 58 | ~5.1 k u |
| 4 | **3** of 8 | 101 u · **44%** | 118 u · 52% | 201 u · 88% | 46 | ~5.2 k u |

A three-lap race is ~15 k u: two minutes at cruise. How much under that a good slinger gets is the
open play-test question (§8).

## 2. What the mode does NOT add

No new ability, no cell of its own, no new scoring metric, and **no circuit generator**. It reuses:

- `ScoringMetric.SwitchesThreaded` (9), the `BestByDomain` fold, the goal-stack row, the objective
  icon — all keyed on the metric. `ModeGenre` needs no row (a race metric is the Time petal).
- `GateRaceScoringRuleSO` — one class, one asset per mode (`ThresherTimeScoringRule.asset`).
- `GateRaceController` and the whole gate-race platform (course broadcast, rings, crossing
  detection, owner-detects/server-records, AI steering, final scores, the shared race toasts).
- **`HeadlongCircuit`**, the shared closed-circuit solver. `ThresherTimeCourse` supplies the
  Thresher's settings exactly as `GrizzlyTimeCourse` supplies the Grizzly's; the solver is untouched.
- `RaceGateObjectiveProvider` (registered in `MiniGameHUD.CreateObjectiveProviderForGameMode`).
- The Thresher's shipped kit, untouched — **no vessel file was edited for this mode**. The Plant is the
  sling, the Winch is the wind-in, the ball smashes whatever a rival lays across the line, and the
  aimed launch's boost (×4.8, `launchBoost`, every crosshair launch since 2026-10-13) is above the ladder (§3).

## 3. The cut — Grizzly Time's octagon, against the 229 u circle

A corner's radius on this metric is `(shorter leg / 2) / tan(turn / 2)`. The Thresher's full-sling
circle (**229 u**) is within 8% of the Grizzly's full-launch circle (211 u), so the cut is Grizzly
Time's, re-measured against the Thresher's curve with the shipped solver compiled out of editor:

- **Eight gates a lap** on Headlong's **800 u** base circle, three laps a race (24 gate threadings).
- **Grizzly Time's corner profiles and `AngularSpread`s** (REDLINE.md §4: spread is the reach dial).
  Measured against 229 u they give the same 0 / 1 / 2 / 3 ladder; each level's hardest corner costs
  ≥ 8 points of top speed more than the last (100 / 71 / 59 / 44%).
- **Floors in the Thresher's own units**: `CornerFloorRadius` = 0.80 / 0.50 / 0.45 / 0.42 × 229 =
  **183 / 114 / 103 / 96 u** — every one at least **1.6× the 60 u cruise circle**, so a pilot who
  never slings (and every AI, §4) holds every corner at full throttle. The test asserts > 1.5×.
- **Legs long enough to spend a sling on**: a ceiling sling bleeds 90% of its excess over **~404 u**
  (`BleedDistance`), and the shortest leg of a median lap is 474–535 u at every level
  (`Legs_are_long_enough_to_spend_a_sling_on`), so a release is spent on its leg rather than thrown
  into the next corner.
- **Mouths are Headlong's** (96 / 72 / 58 / 46): a sling arrives at up to 479 u/s — a Rhino's pace — on
  a hull that corrects at only 120 °/s, so its lateral authority on the way in is a Rhino's too.
- **Presentation caps** (50 / 70 / 72 / 76°) each sit over the level's measured worst half-turn
  (43.5 / 67.2 / 68.2 / 73.3°). `RadialSwing` 0.42 and the lateral swing are Headlong's.

**Above the ladder — and now outrunning it (2026-10-13).** Every aimed launch (out of the crosshair
view, THRESHER.md §5a) leaves at `launchBoost` **×4.8** the orbit's speed, whatever its timing, so a
ceiling sling leaves at **2 299 u/s** (`BoostedSlingSpeed`) on a ~1 100 u circle — wider than the
1 080 u course shell, so a boosted launch is thrown wide of every corner and spent on the leg, while a
spin-cap launch (154 u/s orbit) leaves at 739 u/s on a ~350 u circle. Until that date the boost was
Time L5's ×1.6 "Perfect Release" on a 0.25 s timing window (766 u/s ceiling, 366 u circle), which the
ladder could absorb. **The ladder was not re-cut for the change** — whether the race keeps the boost,
caps it, or is re-cut is a design call (THRESHER.md §9); only the detection clamp moved. The skim boost (a rival's trail, ×5 on the 110 u/s
throttle span, 565 u/s) is the other channel past cruise; it depends on rivals, so nothing is cut
against it. Time (`spinMultiplier` ×1 → ×1.75) raises the spin-up's own cap (154 → 270 u/s) and
rate, so it reaches the ceiling sooner, not higher.

## 4. AI — cruise only, honestly

`AIPilot` writes the stick and the throttle and nothing else, and the Thresher's sling is the
**triggers** (LT plants, RT winds). **An AI Thresher never slings.** It races at cruise on the
gate-race platform's approach steering — throttle `lerp(0.2, 0.9, skill)` with skill = intensity ×
0.25, so 56 / 76 / 95 / **114 u/s** at levels 1–4 (the towed ball, its chain slack because no RT is
pulled, costs 1–10% of that in a hard turn) — on a ≤ 60 u circle. That is why every floor sits above
1.5× the cruise circle: **every corner of every level is flyable at an AI's speed**, so an all-AI
domain completes the race (the Tollway rule) — it is a cruise-pace field, not a slinging one, and a
human who slings will beat it.

- AI approach numbers are sized to that cruise circle, not the 229 u full-sling one: Slingshot's
  ratios (2.2 / 2.5 / 1.7×) on 60 u — commit **135** / lead **150** / through **100**. The generator
  asserts they sit outside the cruise circle (AIPilot has no arrive-and-stop).
- `Thresher.prefab`'s `AIPilot` is enabled (asserted). Its `boostPolicy` is the Squirrel's
  `SkimRingAIPolicySO`, inherited with the hull clone; it binds only a `SquirrelTubeActionSO`, which the
  Thresher does not carry, so its driver is null and inert.
- **Follow-up, not done here:** an autopilot sling (Grizzly Time's autopilot bomb-jump and Slingshot's
  `AutopilotSling` are the models — plant through the REPLICATED press when the next gate is ≥ ~70° off
  the nose, release when the orbit's tangent faces it). It belongs in `ThresherExecutor`, which another
  branch was editing while this mode was authored (the hull/animation/ball pass), so it was left out
  rather than merged blind. Until then the AI is honest about being slower.

## 5. Numbers, and where they are authored

| Knob | Where | Shipped |
|---|---|---|
| race length (laps × rings) | `Resources/EndConditionOverrides` → `thresherTimeGateTarget` | **24** |
| laps | `MinigameThresherTime.unity` → `ThresherTimeController.laps` | **3** |
| rings per lap / base circle | `ThresherTimeCourse.GatesPerLap` / `.BaseRadius` | **8** / **800** |
| corner profile, spread, floor, mouth, presentation | `ThresherTimeCourse.ForIntensity` | §3 |
| AI commit / lead / through | scene → `GateRaceController` | 135 / 150 / 100 |
| detection clamp | scene → `maxPlausibleSpeed` | **4600** (2× the 2 299 u/s boosted sling; 1600 before 2026-10-13) |
| sling ceiling | `ThresherConfig.asset` → `dials.maxBall` × `gameCruise` / `sandboxCruise` | 2600 × 70 / 380 = 479 u/s |
| spin-up cap / aimed-launch boost | `ThresherConfig.asset` → `dials.lockMax`, `launchBoost` | 2.2 (× 70 = 154 u/s); 4.8 |
| cruise / turn | `Thresher.prefab` → `DefaultMinimumSpeed` + `DefaultThrottleScaler`; `Pitch/YawScaler`, `RotationThrottleScaler` | 15 + 110; 120, 0 |
| sling bleed rate | `VesselTransformer.LERP_AMOUNT` (C# const) | 1.5/s |
| comeback rate | `ArcadeGameThresherTime.asset` | **0.3** (6 gates behind buys 1.8 levels) |
| course shell | scene → `courseOuterRadius` / `courseInnerRadiusFallback` | 1080 / 480 |

Every vessel number the cut depends on is restated as a constant on `ThresherTimeCourse` and **read
back off `Thresher.prefab`, `ThresherConfig.asset` and `VesselTransformer.cs`** by
`Course_constants_match_the_shipped_Thresher_and_its_sling` — retune the hull, the chain or the
throttle and the test names the constant that moved. (`Thresher.prefab` keys present in the prefab
are live; the cut reads none the prefab leaves to its C# initializer.)

## 6. Assets — all authored by `Tools/Build/author_thresher_time_assets.py`

The scene is cloned once from `MinigameGrizzlyTime` (controller script, field block, fresh
project-unique `GlobalObjectIdHash`es) and then adopted as committed (`arcade_mode_lib.committed_scene`).
The generator authors the card (`ArcadeGameThresherTime`, Vessels = `SO_Class_Thresher`), the scoring
rule, the toast config (`GameToastConfig_ThresherTime`, Redline's race beats, registered in
`GameToastLibrary`), the preview (`ModePreview_ThresherTime`, registered in `Resources/ModePreviewLibrary`), the
build-scene row, the always-unlocked row, both rosters (`OrganicRematchGames` + `ArcadeGames`) and the
end-condition target, and asserts the comeback rate, the lap arithmetic, the constants it shares with
the C#, the HUD provider row, the detection clamp against the boosted sling, the AI approach against
the cruise circle, the AI prefab state, the sling ceiling's dials and the toybox roster. `--check` diffs
every file. `arcade_mode_lib.VESSELS` gained the Thresher.

**Card art** is the `/cardart` COURSE-tier render (`CardBackgrounds/ThresherTime.png`): the shipped
`ThresherTimeCourse` at intensity 2 through the card-art harness, a `ThresherTime` branch of the gate
circuits' recipe in `render_card_backgrounds.py`.

## 7. Verification status

**Authored headless, not yet run in the editor** (no Unity in the session that built it).

- The course and its test file were compiled and **run** out of editor (Roslyn, against the card-art
  harness's Unity shim and a scratch NUnit shim): **14/14** over 400 seeds × 4 intensities, and the
  suite was watched failing under three mutations (turn scaler 120 → 90: three tests; level-4 profile
  swapped for level 1's: the corner count; level-4 floor 0.42 → 0.20: the cruise-circle floor test).
- The sling table of §1 was measured by compiling the shipped `ThresherChainSolver.cs` beside the
  Thresher chain harness's stubs and flying `ThresherAnalogTriggerTests`'s stand-in pilot (the same
  `Flight` model) at full-throttle cruise.

### In-editor verification

1. **Arcade card.** Menu → Arcade: **Thresher Time** appears, clickable on a fresh account; its launch
   panel pins the vessel to **Thresher**.
2. **Launch at intensity 1, 2 players.** Eight rings bloom in a closed loop; your next gate is lit and
   the objective arrow points at it. The goal row reads 0/24.
3. **Sling a lap.** Plant (LT full), wind in (RT), let go on the next leg's line. A long slack orbit
   then a wind-in should leave at well over 400 u/s and bleed to cruise inside two seconds. At
   intensity 1 every corner can be taken with a sling running.
4. **Lap wrap.** After gate 8 the lit ring returns to gate 1 and the goal row reads 8/24.
5. **Intensity 4.** A median lap is a triangle: two near-hairpins (~100–120 u) that throw a sling wide
   and a third (~200 u) that holds most of one. Planting at the hairpins should be the fast line.
6. **AI.** Add AI Threshers: they steer, thread rings at cruise, never plant, and finish the race.
7. **MPPM two clients.** Both see the same circuit; a client's gate reports are credited once; a
   sling at the ceiling is not read as a respawn (`maxPlausibleSpeed` 1600).
8. **Regressions.** Grizzly Time, Redline and Headlong unchanged.
9. **The ball threads (2026-10-19).** Fly past the lit ring just outside its mouth and swing the ball
   through it: the ring counts once and the next one lights. A respawn beside a ring threads nothing.

## 8. Known limitations / follow-ups

- **Not editor-verified.** Nobody has flown it.
- **AI never slings** (§4). An autopilot sling in `ThresherExecutor` is the follow-up.
- **Whether slinging WINS is the open question.** The orbit is time off the track; the table in §1
  says a sling pays as a corner, not as a straight. If a cruise pilot who never plants keeps up with a
  slinger, the dials are the chain's (`lockSpin`, `chainRigidRate`, `maxBall`) or the course (tighter
  corners make the plant the only good line) — the ladder's reference circle moves with `maxBall`.
- **The curve is the steady-state circle at the speed held**, and a sling's speed is not held: a corner
  taken a second after the release is taken on a smaller circle than the release speed's.
- **Analog depths do not replicate** (THRESHER.md §7): a remote peer sees the sling's ball and chain
  differently during a partial pull. Gate credit is owner-detected, so scores agree.
- **No toasts of its own beyond the race beats**: `GameToastConfig_ThresherTime` is Redline's set word for word
  (halfway / lead change / home stretch / final lap / comeback). A sling-specific idle hint would need its own
  `GameToastSituation` block.
