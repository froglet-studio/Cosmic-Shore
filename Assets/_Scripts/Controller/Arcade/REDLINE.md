# Redline — the Manta-only circuit race

> `GameModes.Redline = 53`. A closed loop of switch rings is cut through the cell and every
> pilot flies **laps** of it in order; the first **domain** whose **lead runner** threads the
> last gate of the last lap wins. The race the Manta was asked for: **crazy fast**.

## 0. Soar from the gun, the heat, and rivals that race (2026-11-11, round 5) - NOT editor-verified

Round 5 of the Time games (`Docs/AI_SYSTEM/TIME_GAME_FUN_SCORES.md`, "the ten most-played vessels") found the race
broken before it was dull: **since 2026-11-07 Soar is the Manta's Time LEVEL-5 upgrade** (`96952eb939`:
`MantaAnalogTurnBoostExecutor.soarUnlockedBy` = Time, the ability map's `UnlockLevel` 5 / `RelockBelowLevel` 4), and
**Time raises the cruise too** (`c4052cb70e`: `ThrottleScalerMultiplier` 1 -> 1.5 over Time 0-10). This card seeded no
Time, so every Manta, human and AI, raced the "crazy fast" circuit at 180 u/s with Soar dark - at I1 for the whole race
(no crystals), at I2-I4 until a fifth Time crystal lit it. Every speed sections 1-5 quote is the hull before that change.

What changed (the vessel's kit is untouched; the course, every seed of it, is the same course):

| | before | now | where |
|---|---|---|---|
| start | Time 0, Soar dark | **Time 6 at I1-I4**: Soar lit from the gun, two petals over its relock | `RedlineCourse.StartingTimeLevelByIntensity`, written to the card's `StartingElements` by the generator |
| speed | 180 u/s (720 if Soar were lit) | **1104 u/s** flat out at Time 6, 1404 at Time 10; full-Soar circle 252 / 259 u | `RedlineCourse.SoarSpeed`, `SoarTop`, `FullSoarRadius` (the flown curve) |
| race | 5 laps, 40 gates | **8 laps, 64 gates** (a lap is ~5 s at I1, ~11 s at I4 for the model's ideal pilot) | `redlineGateTarget` 64, `RedlineCourseSource.DefaultLaps` 8 |
| AI line | the gate way (approach point on the axis, then a through point) | **a race line through the mouths' centres** (`TryRaceLine`); a missed mouth is still re-attacked the gate way | `RedlineController.aiRaceLine` |
| AI throttle | the skill throttle (0.375 at I1: a third of the hull) | **a floor by difficulty**: Easy 0.7, Medium 0.85, Hard and Legendary 1 | `aiThrottleFloorByDifficulty`, through the new `GateRaceController.AIThrottleFloorFor(pilot)` |
| AI nose | 2 / 3 / 6 / 9 per s at every intensity | the same at I1-I2; **Easy 2.5 at I3; 4 / 5 / 6 / 9 at I4** | `aiNoseFollowByIntensity` |
| feedback | the shared race beats | **the heat**: every clean thread throws a shock ring off its gate (redhot: a bigger one), streaks called at 3 / 8 / 16 / every 16, lap splits, best lap, a clean lap chases shock rings round the circuit, the redline (1362 u/s, Time 10 only) flares every ring once | `RedlineHeat`, `RedlineController.Heat`, toasts 360-366, nine empty FMOD slots |

**The heat** (`RedlineHeat`): a thread is CLEAN through the heart of the mouth (half its radius) and REDHOT when it is
clean at 80% of full Soar for the pilot's Time level. Anything else - a thread off the heart, a missed mouth - ends the
streak. Graded on the local pilot's own machine from its own hull crossing the gate it is flying at (the race's own plane
test), so it is unscored and unnetworked: it is how the race tells a pilot they flew it well. The shock ring is a copy
of the gate's mouth that grows to 1.6x (2.6x redhot) and vanishes in 0.45 s - in the world, a shape, never a colour.
Toasts (all authored on `GameToastConfig_Redline`, which the generator now owns, its guid kept): 360 the hint, 361 the
streak, 362 the lap split, 363 a best lap, 364 a clean lap, 365 Soar dark (Time fell below 4), 366 the redline.
367-369 are free.

### 0.1 Measured offline (`Tools/Build/manta_redline_sim.py --reading`, 6 courses an intensity, 8 laps)

The model now flies the flown hull (`TIME_MODEL` "flown": the unlock, the cruise multiplier, the card's start) and
PEOPLE as well as the AI: no nose lag, the throttle buried, straight at each mouth's centre, with their own triggers -
`bury` (both always flat), two habits (`ease` half at every corner; `band` easing only as the stick goes over) and the
`reader` (each corner flown in its head first: the most Soar at which a hull turning flat out from the gate still
threads the next mouth, lifted for at the turn-in and the Yawstery pull out of a knife).

| median race (s) | I1 | I2 | I3 | I4 |
|---|---:|---:|---:|---:|
| person, bury | 37.5 | 43.7 | 71.3 | 89.2 |
| person, the best habit | 37.5 | 43.7 | 64.8 (band) | 89.2 (bury) |
| person, the reader | 37.5 | 43.7 | 71.9 | 88.9 |
| **reading the course saves** | **0%** | **0%** | **-11%** | **+0.3%** |
| AI Easy (6 of 6 at every level) | 139.0 | 142.5 | 152.9 | 149.0 |
| AI Medium | 102.9 | 108.5 | 133.9 | 136.8 |
| AI Hard | 68.4 | 70.7 | 95.6 | 120.9 |
| AI Legendary | 60.1 | 63.6 | 71.4 | 91.0 |
| before: the AI on the gate way, Soar lit, 5 laps, Legendary / Easy | 98 / 151 | 90 / 158 | 96 / 230 (3 of 4) | 104 / none of 4 |

**What it says.** (1) With Soar lit the race is what it was named for, and 8 laps put an ideal pilot at 38-89 s (a
person ~50-115). (2) The AIs now race: every difficulty finishes every level, ordered, and Legendary is within 2-60% of
the model's ideal person (it trails most at I1, where an ideal person never misses). (3) **On this course, reading the
corners does not pay at Soar speed**: a flat-out Manta that misses a mouth loops back in ~1.4 s (its 252 u circle at
1100 u/s), which costs about what a lift does, and the course's own corner radius (the circle tangent to both legs at
half-leg, what the course is CUT on) overstates every corner for a pilot at the mouths' centres: a pilot who lifts BY
it loses 20-45% at I2-I4. The ladder is real in misses (0 / 0 / 51 / 73 for bury over six races) and lap time
(4.7 -> 11 s), not in lift decisions. **The next round re-cuts the course for the flown hull** (section 9). Prism posts
marking the lift points were designed and measured, and not built: they would mark a decision the course does not ask.
(4) Clean threads are 93-100% for every person model: the model aims at the centre perfectly, so the heat's real
spread is a person's, and only the editor shows it.

`--self-test` checks 13-17 hold the flown hull to `Manta.prefab`, `Manta.asset` and the card, the Time-0 start as the
negative control, the shipped AI finishing I1 and I4 at Easy and Legendary, and the race line beating the gate way.
Checks 9-12 (the 2026-11-07 nose-lead record, run on the old hull) were already red on `bleeding-edge` before this work
(the same 13 lines, measured on HEAD's own copy); they are left as the record and not re-measured here.

### 0.2 On bleeding-edge, and the step-1 fun pass (2026-11-13)

Round 5 lived only on `Ys-bleeding-edge`, so on `bleeding-edge` Soar stayed dark in Redline until it was cherry-picked
for the elemental loop's Manta Time step 1 (`Docs/ElementalLoop/PLAYBOOK.md`; Caleb's Manta polish, `Docs/Manta/POLISH.md`,
touched the trail, haptics and camera, not the race). Re-measured there, nothing re-tuned:

- **The AI's Soar bands are not the floor.** A 5 x 4 grid of `aiBoostStickBand` x `aiYawsteryStickBand` at Legendary
  (nose 9/s, floor 1, 6 courses an intensity) puts the shipped 0.35 / 0.75 within 3% of the best at I1-I3 and 16% at I4
  (best 0.2 / 0.5: 61 / 67 / 74 / 88 s against 61 / 64 / 71 / 105 s). The bands are a vessel dial every AI Manta
  flies, so a change waits for step 3 (`time-arcade`), searched on the signed-off ladder.
- **The rivals at the fun rung (I2):** the model's ideal person 44 s; Legendary 64 s, Hard 71 s, Medium (the default
  difficulty) 109 s, Easy 145 s. A first fly test against **Hard** rivals is the one that reads whether they race.
- **The open question is the verb at speed** (section 0.1): at Time 6 the model's perfect-aim person never gains by
  lifting at I1-I2. A real pilot misses mouths a perfect aim does not, so whether a lift pays is the fun card's
  question, and the re-cut of section 9 waits on that flight.

## 1. What the mode is asking

> **Sections 1-5 are the 2026-11-04 design, on the hull before Soar's unlock; section 0 is how the race is flown now.**

**Redline teaches one ability: Soar, the Manta's Time ability** (Yash, 2026-11-04: "introduce the
new player to the time ability of Manta in intensity 1, and in intensity 4 it will get intense; no
other elemental abilities needed, only time"). Soar is the OVERLAP of the triggers (`min(LT, RT)`,
the analog boost): both held at `s` buy `180 × (1 + 3s)` u/s, and easing BOTH together turns the
Manta tighter, because its stick's turn rate (`0.2 v + 30` deg/s) grows slower than its speed. So
every corner is one question:

> **How much Soar is this corner worth?**

Speed follows the answer with a 1.5/s exponential lag (`VesselTransformer.LERP_AMOUNT`), so Soar
given up costs about **two seconds** to win back. Composing the Manta's authored numbers gives the
curve the course is cut against (`RedlineCourse.SoarRadius`):

| Soar held (both triggers) | speed | circle it holds | reads as |
|---|---|---|---|
| 1.00 | **720 u/s** | 237 u | flat out, both triggers buried |
| 0.75 | 585 | 228 | a lift |
| 0.50 | 450 | 215 | a real corner |
| 0.25 | 315 | 194 | slow in, hard out |
| 0.00 | 180 | **156** | both released, the stick alone - the tightest corner Redline ever deals |

**Nothing else is needed.** The trigger DIFFERENCE (`RT − LT`) is Yawstery, the Manta's Mass ability
(one trigger flat, the other released: an 82 u pivot, `RedlineCourse.CornerRadiusAtBoost`). Before
2026-11-04 the I3 and I4 corners (121 u, 99 u) were cut for it. Now no corner of any seed is tighter
than 156 u, so Soar alone makes every one (`RedlineCourseTests.Every_corner_fits_its_absolute_safety_floor`).
The cell's omni crystal is off, so Mass, Charge and Space are never fed, and the only pickups are
Time crystals (§4a). The Manta's kit is unchanged (CLAUDE.md, "A game mode never changes the
vessel"): Yawstery still turns, and the Sting and Kabloom still act, but the race never asks for them.

(A Time-10 Manta at 936 u/s holds 247 u; the course is cut for the resting vessel, so a Time
crystal buys speed and asks for more lift in the corners.)

**Intensity is what mix of corners a lap asks for: one new corner a level.** Measured over 400
seeds, the median lap:

| intensity | corners that cost Soar | its corners (Soar they allow) | mouth ⌀ | Time crystals a lap | reads as |
|---|---|---|---|---|---|
| 1 | **0** of 8 | 362 u (100%) | 220 | none | bury both triggers, learn the gates |
| 2 | **1** of 8 | 217 u (53%) | 176 | 1, on the line | one corner: ease both together, then wind it back |
| 3 | **2** of 8 | 184 u (17%), 228 u (76%) | 144 | 2, on the line | a rhythm: lift late, bury early |
| 4 | **3** of 8 | 171 u (8%), 199 u (30%), a **knife-edge** 233 u (88%) | 88 | 3, inside the apexes | the triggers nearly off, a third, and a sweeper held flat only by being exact, through the tightest mouth, with greed against the line |

Lift for a corner and you pay ~2 s to wind the Soar back up — against a lap of ~5 k units a
pilot who held it covers in seven seconds. **The gap between a clean lap and a scruffy one is
most of a lap**, which is Headlong's proposition on a hull that is fast rather than one that
converges.

## 2. Why the Manta, and only the Manta

It is the fleet's fast hull — cruise 180, ×4 on Soar to 720, ×1.3 again at Time 10 — and its
turning circle is **bounded**: with `RotationThrottleScaler` 0.2 the radius converges on
`180/(π × 0.2)` = 286 u and never exceeds it. A course whose corners are cut around that radius
is a course this vessel flies at the speed the mode is named for, on long swooping legs it can
**pitch** through (50°/s pitch against 30°/s yaw — which is why the course is cut further out of
its plane than Headlong's at every level).

Every number is read off `Manta.prefab` (`DefaultThrottleScaler` 180, `boostMultiplier` 4,
`RotationThrottleScaler` 0.2, `PitchScaler` 50 / `YawScaler` 30) and
`MantaAnalogTurnBoostExecutor` (`maxYawDegPerSec` 60), restated as constants on `RedlineCourse`
and pinned by `RedlineCourseTests.Full_boost_radius_matches_the_shipped_Manta` — if the
vessel is retuned, the test names every corner that moved with it.

## 3. What the mode does NOT add

No new weapon, no new ability, no cell of its own, no new scoring metric, **and no generator of
its own**. It reuses:

- `ScoringMetric.SwitchesThreaded` (9), `IRoundStats.SwitchesThreaded`, the `BestByDomain`
  fold, the goal-stack row and the objective icon — all keyed on the metric, so all free.
- `GateRaceScoringRuleSO` — one class, one asset per mode (`RedlineScoringRule.asset`).
- `GateRaceController` and the whole gate-race platform (course broadcast, rings, crossing
  detection, the owner-detects/server-records round trip, AI steering, final scores).
- **`HeadlongCircuit`, the closed-circuit solver.** `RedlineCourse` supplies the Manta's
  settings to `HeadlongCircuit.Generate`; the only change to the solver is
  `HeadlongCircuitSettings.CornerFloorRadius`, an ABSOLUTE safety floor a course cut for
  another vessel can state in its own units (Headlong leaves it 0 and is bit-for-bit what it
  was). A second copy of the solver would have been the semantic duplicate the ship protocol
  scans for.
- The Manta's shipped kit for the racing, Soar first. Its other abilities are untouched and
  never asked for: a Yawstery turn trail is laid only by a pilot who holds one trigger, and the
  Sting and Kabloom need a rival's trail to arm on. None of that is scored; the race is.
- **The elemental set's Time crystal** (`ElementalCrystalSetSO`, one size, one petal) for the
  course's pickups, laid on every peer from the broadcast course the way Serpent Gulag lays its
  field (§4a), instead of the cell's omni crystal.

## 4. The cut

`RedlineCourse.ForIntensity` builds `HeadlongCircuitSettings` for the Manta. Two things the
tuning pass found are worth carrying:

- **The profile is not the only dial.** The solver builds TOWARD a turn-angle profile
  (HEADLONG.md §4), but its reach at a given profile is bounded by `AngularSpread` — how far two
  gates may be pulled together. Raising level 2's asked-for corner from 118° to 130° changed
  nothing (max turn stayed ~76°); raising the spread from 2.0 to 3.0 produced the corner. And a
  SMALLER base circle made every corner WIDER (shorter legs at the same turn are bigger circles
  — the cancellation `FlatRing` documents). **Tune the reach, then the profile.**
- **Eight gates a lap, not six.** Six was measured and rejected: longer legs at the same turn
  are bigger circles, and even level 4 lost its third corner. The Manta's runway is the legs the
  solver leaves long (466–868 u at level 4, a second or more flat out), not a smaller gate
  count.
- **Level 4's third corner is a knife-edge, and the design says so.** With eight gates and 360°
  to spend, every variant of base radius, profile and spread left the third-tightest corner
  between 230 and 250 u — on the full-boost radius, not inside it. Rather than force a number
  the geometry will not give, `Level_four_has_a_knife_edge_third_corner` asserts what it does
  give: a corner within 3% of the full-boost radius, holdable flat out only by a pilot who is
  exact, on a mouth of 44 u.
- **The Soar-only ladder (2026-11-04): the FLOOR sets the hardest corner, and level 4 spreads its
  turn over three even corners.** The floors are 178 / 205 / 175 / 160 u: at I2-I4 the profile asks
  for more than the floor allows and the relaxation stops at it, so the floor is each level's
  lesson (half Soar, a sixth, triggers off). Level 4's 150/124/86 profile broke a 160 u floor and
  relaxed the whole lap down to ONE costing corner; spreading the turn over four or five corners
  made them all WIDER (longer legs, the same turn). Three even corners, 125/120/115, keep three
  costing corners and the knife-edge. Measured on the 400-seed sweep in `course_port.py` first,
  then in the shipped C# (`Tools/Build/redline_course_harness/run.sh --sweep`); the two agree.

Mouths are a step wider than Headlong's at levels 1-3 (110/88/72 vs 96/72/58): a Manta at 720
crosses one in a sixth of a second with a quarter of the Rhino's lateral authority. **Level 4
is deliberately not** — at **44** it is tighter than Headlong's hardest mouth (46) and the
ladder's biggest single step (72 -> 44, against 110 -> 88 -> 72). It was 58 and read as
generous in play: level 4 already carries the knife-edge corner, and a mouth a Manta can miss
is what stops flat-out being the default answer to every gate. The floor a mouth actually has
is far below it — Switchback's level 4 is 4.29 (a Dolphin hull x 1.5) and Breakwater's
tightest port is 42 — so this is a demanding gate, not a trick one. Shrinking it only relaxes
`IsLegal`'s mouth-separation term (`RingRadius x 2`), so the solver is strictly freer; all 14
`RedlineCourseTests` still pass over the same 400-seed sweep, the corner ladder included.
Presentation caps (50/72/76/80) each sit over the level's measured worst half-turn
(44/53/59/66 on the 2026-11-04 ladder) — a gate faces its corner's bisector, so a cap under the
half-turn zeroes the jitter budget exactly where the gates most need to face you.

### Measured

`RedlineCourseTests` sweeps **400 seeds × 4 intensities** — closure, shell, mouth separation,
the absolute floor (never under Soar alone's 156 u), the presentation cap, gate 0's pole
placement, determinism, **the corner demand** (0 / 1 / 2 / 3), the knife-edge, the speed-cost
ordering (each level's hardest corner costs at least 5 more points of top speed than the last, on
the Soar-only curve) and the runway; `RedlineTimeCrystalsTests` sweeps the crystals (§4a).
`Tools/Build/redline_course_harness/run.sh` compiles the shipped C# against
`race_course_source_harness`'s Unity math stub and runs both suites out of the editor in about
15 s (20/20 on 2026-11-04; `--self-test` adds two negative controls that must fail: an I4 floor
of 90 u and crystals set outside their corners):

| intensity | corners costing Soar (median lap) | #1 | #2 | #3 | tightest of 400 seeds |
|---|---|---|---|---|---|
| 1 | 0 / 8 | 362 u · 100% | 483 u · 100% | 517 u · 100% | 321 u |
| 2 | 1 / 8 | 217 u · 53% | 399 u · 100% | 600 u · 100% | 205 u |
| 3 | 2 / 8 | 184 u · 17% | 228 u · 76% | 490 u · 100% | 175 u |
| 4 | 3 / 8 | **171 u · 8%** | 199 u · 30% | 233 u · 88% | 160 u |

Percentages are the Soar each corner allows on the Soar-only curve (`FastestSoarForCorner`).

### 4a. The Time crystals

`RedlineTimeCrystals.Place` is a pure function of the broadcast course: a lap's tightest corners
(the costing ones) carry its crystals, none at I1, then one, two and three a lap. At I2-I3 a
crystal sits halfway down the leg INTO its corner, on the line; at I4 it sits a quarter of the way
out, 70 u toward the inside of the turn, so taking it is a tighter exit (less Soar). Each is the
elemental set's Time crystal: one Time level, which on the Manta is Soar's top speed (+3% a
level, ×1.3 at level 10). `RedlineController` lays them on every peer when the course is raised
(Serpent Gulag's recipe: collected per peer, regrown in place `timeCrystalRegrowSeconds` = 5 s
later), and the scene's `CrystalManager` spawns none (`author_redline_assets.py` authors its count
to 0), so nothing else is ever fed. The Manta takes one within about 13.5 u (its 10 u near-field
skimmer plus the crystal's 3.55 u).

**The AI does not steer for them as shipped** (`RedlineController.aiTimeCrystalSlack` 0). The
platform's crystal detour reads managed crystals only, so the mode has its own hook
(`TryOverrideAim`: the live crystal ahead that costs least on the way to the next ring, within the
slack). But with the fleet's AI nose lag a faster Manta misses more mouths: at a 150 u slack the
offline AI took ten levels at I4 and finished 1 of 6 races, against 6 of 6 without (§5.1). It
waits on the AI nose rate (`Docs/AI_SYSTEM/AI_PLAN.md` decision 7).

## 5. The AI Soars — a vessel change, not a mode one

`AIPilot` writes the stick and the throttle and nothing else, so before this an AI Manta could
never boost: `MantaAnalogTurnBoostExecutor` read the triggers behind a gamepad/keyboard device
gate, and an autopilot has neither. In a race cut around the Manta's full-boost radius that made
every bot a 180 u/s obstacle. The executor now carries an **autopilot drive**: while
`AIPilot.AutoPilotEnabled`, both triggers are held at a boost intent that is *how straight the
stick is* — full inside `aiBoostStickBand` (0.35), fading linearly to nothing at a full
deflection. That mirrors the human trade rather than inventing a policy (a pilot buries both
triggers on a straight and eases off to turn), it needs no knowledge of the course, and it is
gated on the pilot being an AUTOPILOT rather than on the player being an AI, so the menu's
lava-lamp Manta and a released companion fly the same kit a human does.

**The bot pivots, too.** Past `aiYawsteryStickBand` (0.75) of YAW stick the drive adds a net
trigger on the turn's side: the inner trigger stays at the boost intent and the outer one rises
by the excess deflection, rescaled over the band's remainder, so a full yaw stick is one trigger
flat and the other released — the 82 u pivot of §1. The overlap (the boost) is the same at every
stick as it was before the pivot, and below the band the two triggers stay equal, so a straight
or an ordinary corner flies exactly as it did; the pivot only adds yaw where the bot is already
asking for more turn than the stick's 30 + 0.2v gives. It reads the yaw axis alone, so a pitch
haul never pivots, and the bot lays the Yawstery turn trail a human would while it does. Still no
knowledge of the course, so a level-4 hairpin (99 u) is now within the bot's reach rather than
one it must overshoot (unflown — see §9). (Redline's own corners no longer need it: the Soar-only
ladder of 2026-11-04 never deals a corner under 156 u, §1. The pivot is the vessel's, for every
mode.) The pure shape is
`MantaAnalogTurnBoostExecutor.AutopilotTriggers`, asserted by
`Tests/Editor/MantaAutopilotDriveTests.cs`. The AI approach numbers in the scene (commit 420 /
lead 480 / through 320) are sized to the 237 u circle rather than inherited from Switchback's Dolphin.

### 5.1 Measured offline: the steering, the ladder, the crystals (2026-11-03 / 04)

Redline is the Manta's own Time game, where its AI trains (`Docs/AI_SYSTEM/MINIGAME_TRAINING_PLAN.md` section 13).
`Tools/Build/manta_redline_sim.py` races the AI Manta the way this scene flies it, ported line for line: the
course (`course_port.py`, held to the shipped C# by `redline_course_harness/redline_parity.jsonl`), the Time crystals
(`RedlineTimeCrystals`, held the same way), the gate aim, `AIPilot`'s stick, skill throttle and orbit break-off, the
Soar triggers of §5, and the hull's turn law with the AI nose lag (1.5/s); it reads the race's laps from
`RedlineCourseSource.DefaultLaps`. On Regatta's own I1 course it laps in 28 s; the editor's Hard AI Mantas lapped it in
23-28 s. The older `manta_drive_sim.py` (Regatta's model: full throttle, no nose lag) is about 3x faster than either.

| 5 laps, 6 courses an intensity, shipped bands 0.35 / 0.75 | I1 | I2 | I3 | I4 | finished |
|---|---:|---:|---:|---:|---:|
| the old ladder, the old steering (before 2026-11-03) | 136 s | 657 s (3 of 6) | 850 s (1 of 6) | - | 10 / 24 |
| the old ladder, the Regatta steering (2026-11-03) | 183 s | 186 s | 206 s | 541 s (5 of 6) | 23 / 24 |
| **the Soar-only ladder with Time crystals, as shipped (2026-11-04)** | **183 s** | **181 s** | **193 s** | **538 s** | **24 / 24** |
| the same, the AI steering for the crystals (`aiTimeCrystalSlack` 150) | 183 s | 186 s | 274 s | 881 s (1 of 6) | 19 / 24 |
| the same, full throttle (`AIThrottleFloor` 1) | 165 s | 195 s | 254 s | 511 s | 24 / 24 |
| the same with no AI nose lag (a what-if: the nose IS the command) | 111 s | 102 s | 106 s | 95 s | 24 / 24 |

On the old steering the AI lined up on a point 320 u past the mouth on its axis and circled after every miss, and no
pair of Soar bands got it through I3 or I4 (the 44 u mouth). So Redline flies the Regatta steering, as ten other gate
races do (`RedlineAISteeringTests`); at I1 it costs the AI time (183 s against 136 s), because it lines up on every
ring. Its throttle stays the skill throttle (`intensity x 0.25` lerps the Manta's 0.2-0.9, so 37.5% at I1): a floor of 1
is faster at I1 and I4 and slower at I2 and I3, so it buys nothing the ladder needs. The AI does not steer for the
Time crystals: the speed they buy made it miss more mouths (§4a). **The nose lag is the biggest lever left**: with none the AI is 1.7x faster at I1 and 5.7x at I4. Whether an
AI's nose rate is part of its genome or a difficulty setting is `AI_PLAN.md` decision 7 (Yash). Until then the AI
rivals are a new player's to beat at every level, and I4 is not yet intense against them. The Soar bands are searched
by the compute job `redline-manta-time`.

## 6. Numbers, and where they are authored

| Knob | Where | Shipped |
|---|---|---|
| race length (laps × rings) | `Resources/EndConditionOverrides` → `redlineGateTarget` | **64** (8 × 8, since 2026-11-11, section 0; 40 from the 2026-10-26 ~90 s pass; it was 24) |
| laps | `MinigameRedline.unity` → `RedlineController.laps` (`RedlineCourseSource.DefaultLaps`) | **8** |
| starting Time level | `RedlineCourse.StartingTimeLevelByIntensity` → the card's `StartingElements` (generator) | **6** at I1-I4 (Soar lit) |
| AI race line | `RedlineController.aiRaceLine` (code default) | **on**: straight at each mouth's centre (section 0) |
| AI throttle floor by difficulty | `RedlineController.aiThrottleFloorByDifficulty` (code default) | Easy / Medium / Hard / Legendary **0.7 / 0.85 / 1 / 1** |
| heat | `RedlineController` → `heatOn`, `cleanFlarePunch` 1.6, `redhotFlarePunch` 2.6, `flareSeconds` 0.45, `chaseStagger` 0.06; `RedlineHeat` (clean 0.5 of the mouth, redhot 0.8 of full Soar, redline 0.97 of Time 10's) | on |
| heat toasts | `GameToastConfig_Redline` (generator), `GameToastSituation` 360-366 | section 0 |
| heat sounds | `RedlineController` → nine `EventReference` slots | **empty** (FMOD convention) |
| rings per lap | `RedlineCourse.ForIntensity` → `GateCount` | **8** |
| circuit base radius | same | **820** (~5 k per lap) |
| corner profile (turn angles) | same, per intensity | see §1 — with `AngularSpread` and the floor, **this is the design** |
| corner floor / mouth / present cap | same, per intensity | 178–205–175–160 u (never under Soar alone's 156); 110–88–72–**44**; 50–80 |
| Time crystals a lap | `RedlineTimeCrystals.PerLapByIntensity` | 0 / 1 / 2 / 3 (on the line at I2-I3, inside the apexes at I4) |
| Time crystal regrow | `RedlineController.timeCrystalRegrowSeconds` (code default; not in the scene) | 5 s |
| AI steers for Time crystals | `RedlineController.aiTimeCrystalSlack` (code default) | **0** (off, §4a) |
| AI nose follow rate by difficulty | `RedlineController.aiNoseFollowByIntensity` (code default; Switchback's shape) | Easy / Medium / Hard / Legendary **2 / 3 / 6 / 9** per second at I1-I2; **2.5** / 3 / 6 / 9 at I3; **4 / 5** / 6 / 9 at I4 (2026-11-11, section 0; fleet lag 1.5; `Docs/Reports/REDLINE_MANTA_NOSE.md` §4; not yet run in Unity) |
| the cell's omni crystal | scene → `CrystalManager.crystalCountMode` / `fixedCrystalCount` (authored by the generator) | FixedCount / **0** |
| AI commit / lead / through | scene → `GateRaceController` | 420 / 480 / 320 |
| AI steering | `RedlineController.AICommitFromApproachPoint` (code) | the Regatta steering (§5.1) |
| detection clamp | scene → `maxPlausibleSpeed` | **1400** (the clamp is twice this a frame: a Time-10 Manta at full Soar is 1404 u/s and an overcharged one ~1827, both inside it; the inherited 400 rejected even the old 936) |
| autopilot boost band | `MantaAnalogTurnBoostExecutor.aiBoostStickBand` (code default; not stored on `Manta.prefab` until it is next saved) | 0.35 |
| autopilot Yawstery band | `MantaAnalogTurnBoostExecutor.aiYawsteryStickBand` (code default; the prefab has no entry until it is next saved) | 0.75 |
| comeback rate | `ArcadeGameRedline.asset` | **0.21** (ten gates behind, a lap and a quarter of the 8-lap race, buys 2.1 levels; the comeback raises all four elements equally, the platform's law, and on this hull only the Time share changes what a pilot does: +5% cruise and +3% Soar a level) |
| course shell | scene → `courseOuterRadius` / `courseInnerRadiusFallback` | 1080 / 480 |

**The race length is ONE number.** The controller divides it by `laps` to size the circuit and
the turn monitor asks the controller for the target. `Tools/Build/author_redline_assets.py
--check` asserts that, that the target is a whole number of laps, that the comeback rate still
buys a whole element level at a quarter-of-target deficit, and that the speed clamp clears a
Time-10 Manta.

## 7. The arena is a REFERENCE, not a fork

The scene is cloned from `MinigameHeadlong` and four things are changed: the controller, its
scoring-rule asset, the AI approach numbers + speed clamp, and (on the card) the vessel. The
generator asserts the donor still provides the four things it is inheriting — the barren race
cell, the equatorial spawn ring, the `SwitchesThreaded` comeback source and golf direction —
rather than leaving them as an absence. The AI roster's `vesselClass` in the scene is Headlong's
and irrelevant: the card's `Vessels` list clamps every AI to the Manta
(`GameDataSO.ClampVesselToGame`), the same authority that clamps the human pick.

## 8. In-editor verification

1. **Arcade card.** Menu → Arcade: a **Redline** card appears, is clickable on a fresh account,
   and its launch panel pins the vessel to **Manta** with no other hull selectable.
2. **Launch at intensity 1, 2 players.** The connecting panel holds until the circuit arrives,
   then eight rings bloom in a closed loop. Your next gate is lit lime and the objective arrow
   points at it.
3. **Fly a lap with both triggers buried.** Speed should settle at 720 within ~2 s. At intensity
   1 nothing should make you lift; every corner goes at full Soar with the stick alone. Watch
   the speed readout — it is the mode's only feedback on what a corner cost you. No crystal
   stands anywhere on the course at intensity 1.
4. **Lap wrap.** After the eighth gate the lit ring returns to gate 1 and the goal row reads
   8/40 — **not** "finished".
5. **Intensity 2-4, Soar only.** I2 has one corner that needs both triggers eased to about half;
   I3 two; I4 three — one with both triggers nearly off, one at a third, and a third you can
   *just* hold flat out on a clean line. **Every corner can be made with both triggers eased
   together and the stick hard over**: holding one trigger alone (the Yawstery pivot) should never
   be needed.
6. **Time crystals.** I2 shows one crystal a lap halfway down the leg into its corner, I3 two,
   I4 three just inside the corner exits. Taking one raises your Time level by one (the Time
   petal) and your Soar top speed by about 3%; it is back about 5 s after you take it. No omni
   crystal appears, and your Mass, Charge and Space petals never fill.
7. **AI.** Add an AI Manta and watch it **Soar on the straights and lift in the corners** — the
   drive of §5 — and finish every intensity. A miss at an I4 44 u mouth should be followed by a
   turn back and a re-attack within a few seconds. It flies past the Time crystals without
   steering for them (§4a). A human Manta on gamepad or keyboard must feel exactly as before.
8. **MPPM two clients.** Both peers see the same circuit and the same crystals; a client's gate
   reports are credited and neither peer is credited twice for one crossing. Confirm a fast
   Manta's crossings are never dropped as "implausible" (the 400 → 1400 clamp).
9. **Regression — Headlong unchanged.** Launch Headlong: same circuit feel, Rhino only, its
   400-seed suite still green (the solver gained a field it leaves at 0).

## 9. Known limitations / follow-ups

- **Section 0 is not editor-verified** (2026-11-11): the Time-6 start, 8 laps, the AI race line and floors, the heat. The
  out-of-editor compile covers `RedlineCourse`, `RedlineHeat` and their tests (`redline_course_harness`, 29 of 29, four
  negative controls); `RedlineController` and its `Heat` partial have not been compiled. The checklist entry is in
  `Docs/UNITY_VERIFICATION_CHECKLIST.md`.
- **Reading the course does not pay at Soar speed** (section 0.1: 0 / 0 / -11 / +0.3%). The next round is a re-cut for
  the flown hull, a MODE change: either a ladder cut on the Time-6 curve with corners a flat-out Manta cannot loop back
  from cheaply, or overshoot fences (Headlong's hedges, Breach's walls) where a full-Soar line misses a mouth, so a miss
  costs more than a lift. Measure it in `manta_redline_sim.py --reading` first; the card art re-renders with the course.
- **Whether Redline's Time crystals leave the Manta's Mass decoys** (every elemental crystal it takes leaves one, and a
  rival who takes a decoy gets a danger field that could relock its Soar) is unverified for mode-minted crystals.

- **Not editor-verified.** Asserted by a real out-of-editor compile of the course + tests
  against Unity-semantics stubs and a 1,600-circuit run of the shipped solver; nobody has
  flown it, and there is no Redline entry in `Docs/UNITY_VERIFICATION_CHECKLIST.md` yet, so the
  first-flight check is still unrecorded.
- **The autopilot drive is new to the whole fleet's AI Manta**, not just this mode — a
  lava-lamp Manta now Soars on straights. If the menu reads too fast, the dial is
  `aiBoostStickBand` on the Manta's `MantaAnalogTurnBoostExecutor` (0 disables the drive), and the pivot's is
  `aiYawsteryStickBand` (1 disables it).
- **Level 4's third corner is a knife-edge by design** (§4). If play-test wants three
  unambiguous corners and a fourth, the lever is a NINTH gate per lap (a 45-gate race), not the profile.
- **The Soar-only ladder, the Time crystals and the Regatta steering are measured offline only**
  (§4, §4a, §5.1). The editor checks are in `Docs/UNITY_VERIFICATION_CHECKLIST.md`.
- **I4 is not yet intense against the AI.** The AI Mantas finish every level but run 183-538 s
  where a person flying at full throttle with no nose lag should take a fraction of that; the
  lever is the AI nose rate (`AI_PLAN.md` decision 7), and the AI steering for Time crystals waits
  on it too (§4a).
- **The card art was re-rendered for the new I2 course** (2026-11-04, `render_card_backgrounds.py
  Redline`, `--check` OK). It was rendered on Windows through a local copy of `card_art_harness/run.sh`
  that hands the compiler native paths (the shipped one passes Git Bash paths and fails there); that
  copy reproduced the committed Lantern Run and Serpent Gulag cards byte for byte first. The card
  draws the course, not the Time crystals (they are laid at run time).
- **The Mentor tips** (`Tips_Redline.asset`, authored once by `author_drill_assets.py` and then
  human-owned) teach Soar alone: hold it, ease both triggers for a corner, wind it back early, and
  what a Time crystal does.
- **The AI's Yawstery pivot is unflown.** It is keyed on a saturated yaw stick, and AIPilot's
  stick (a clamp of heading-angle-in-degrees × cross product) can saturate well short of a
  hairpin, so if play-test shows the bot pivoting on corners it could Soar through (or weaving
  on a straight), raise `aiYawsteryStickBand` toward 1
  (1 disables the pivot and restores the band-only drive exactly). If it overshoots a tight
  corner, lower it toward `aiBoostStickBand`. Either way it is a vessel dial, so it
  reaches every AI Manta, not just Redline's — the same fleet-wide scope as the drive itself.
