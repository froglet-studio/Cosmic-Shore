# Redline — the Manta-only circuit race

> `GameModes.Redline = 53`. A closed loop of switch rings is cut through the cell and every
> pilot flies **laps** of it in order; the first **domain** whose **lead runner** threads the
> last gate of the last lap wins. The race the Manta was asked for: **crazy fast**.

## 1. What the mode is asking

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

**Nothing else is needed.** The trigger DIFFERENCE (`RT − LT`) is Yastri, the Manta's Mass ability
(one trigger flat, the other released: an 82 u pivot, `RedlineCourse.CornerRadiusAtBoost`). Before
2026-11-04 the I3 and I4 corners (121 u, 99 u) were cut for it. Now no corner of any seed is tighter
than 156 u, so Soar alone makes every one (`RedlineCourseTests.Every_corner_fits_its_absolute_safety_floor`).
The cell's omni crystal is off, so Mass, Charge and Space are never fed, and the only pickups are
Time crystals (§4a). The Manta's kit is unchanged (CLAUDE.md, "A game mode never changes the
vessel"): Yastri still turns, and the Sting and Kabloom still act, but the race never asks for them.

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
  never asked for: a Yastri turn trail is laid only by a pilot who holds one trigger, and the
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

**The bot pivots, too.** Past `aiYastriStickBand` (0.75) of YAW stick the drive adds a net
trigger on the turn's side: the inner trigger stays at the boost intent and the outer one rises
by the excess deflection, rescaled over the band's remainder, so a full yaw stick is one trigger
flat and the other released — the 82 u pivot of §1. The overlap (the boost) is the same at every
stick as it was before the pivot, and below the band the two triggers stay equal, so a straight
or an ordinary corner flies exactly as it did; the pivot only adds yaw where the bot is already
asking for more turn than the stick's 30 + 0.2v gives. It reads the yaw axis alone, so a pitch
haul never pivots, and the bot lays the Yastri turn trail a human would while it does. Still no
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
| race length (laps × rings) | `Resources/EndConditionOverrides` → `redlineGateTarget` | **40** (5 × 8, since the 2026-10-26 ~90 s target pass; it was 24) |
| laps | `MinigameRedline.unity` → `RedlineController.laps` (`RedlineCourseSource.DefaultLaps`) | **5** |
| rings per lap | `RedlineCourse.ForIntensity` → `GateCount` | **8** |
| circuit base radius | same | **820** (~5 k per lap) |
| corner profile (turn angles) | same, per intensity | see §1 — with `AngularSpread` and the floor, **this is the design** |
| corner floor / mouth / present cap | same, per intensity | 178–205–175–160 u (never under Soar alone's 156); 110–88–72–**44**; 50–80 |
| Time crystals a lap | `RedlineTimeCrystals.PerLapByIntensity` | 0 / 1 / 2 / 3 (on the line at I2-I3, inside the apexes at I4) |
| Time crystal regrow | `RedlineController.timeCrystalRegrowSeconds` (code default; not in the scene) | 5 s |
| AI steers for Time crystals | `RedlineController.aiTimeCrystalSlack` (code default) | **0** (off, §4a) |
| the cell's omni crystal | scene → `CrystalManager.crystalCountMode` / `fixedCrystalCount` (authored by the generator) | FixedCount / **0** |
| AI commit / lead / through | scene → `GateRaceController` | 420 / 480 / 320 |
| AI steering | `RedlineController.AICommitFromApproachPoint` (code) | the Regatta steering (§5.1) |
| detection clamp | scene → `maxPlausibleSpeed` | **1400** (a Time-10 Manta's 30 fps step is 31 u; the inherited 400 rejected it within a unit) |
| autopilot boost band | `MantaAnalogTurnBoostExecutor.aiBoostStickBand` (code default; not stored on `Manta.prefab` until it is next saved) | 0.35 |
| autopilot Yastri band | `MantaAnalogTurnBoostExecutor.aiYastriStickBand` (code default; the prefab has no entry until it is next saved) | 0.75 |
| comeback rate | `ArcadeGameRedline.asset` | **0.21** (ten gates behind, a quarter of the race, buys 2.1 levels; the comeback raises all four elements equally, the platform's law, and on this hull only the Time share changes what a pilot does) |
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
   together and the stick hard over**: holding one trigger alone (the Yastri pivot) should never
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

- **Not editor-verified.** Asserted by a real out-of-editor compile of the course + tests
  against Unity-semantics stubs and a 1,600-circuit run of the shipped solver; nobody has
  flown it, and there is no Redline entry in `Docs/UNITY_VERIFICATION_CHECKLIST.md` yet, so the
  first-flight check is still unrecorded.
- **The autopilot drive is new to the whole fleet's AI Manta**, not just this mode — a
  lava-lamp Manta now Soars on straights. If the menu reads too fast, the dial is
  `aiBoostStickBand` on the Manta's `MantaAnalogTurnBoostExecutor` (0 disables the drive), and the pivot's is
  `aiYastriStickBand` (1 disables it).
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
- **The AI's Yastri pivot is unflown.** It is keyed on a saturated yaw stick, and AIPilot's
  stick (a clamp of heading-angle-in-degrees × cross product) can saturate well short of a
  hairpin, so if play-test shows the bot pivoting on corners it could Soar through (or weaving
  on a straight), raise `aiYastriStickBand` toward 1
  (1 disables the pivot and restores the band-only drive exactly). If it overshoots a tight
  corner, lower it toward `aiBoostStickBand`. Either way it is a vessel dial, so it
  reaches every AI Manta, not just Redline's — the same fleet-wide scope as the drive itself.
