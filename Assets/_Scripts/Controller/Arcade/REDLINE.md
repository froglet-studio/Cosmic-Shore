# Redline — the Manta-only circuit race

> `GameModes.Redline = 53`. A closed loop of switch rings is cut through the cell and every
> pilot flies **laps** of it in order; the first **domain** whose **lead runner** threads the
> last gate of the last lap wins. The race the Manta was asked for: **crazy fast**.

## 1. What the mode is asking

The Manta has two held inputs and they share two triggers. **Soar** is the OVERLAP of the
triggers (`min(LT, RT)`, the analog boost) and **Yastri** is their DIFFERENCE (`RT − LT`, a
flat yaw of up to 60°/s on top of the stick). So a pilot holding one trigger flat and easing the
other off is trading boost for yaw on one linear scale — boost `b` buys `180 × (1 + 3b)` u/s and
costs `60 × (1 − b)` deg/s of trigger yaw — and every corner is one question:

> **How much Soar is this corner worth?**

Speed follows the answer with a 1.5/s exponential lag (`VesselTransformer.LERP_AMOUNT`), so a
boost given up costs about **two seconds** to win back. Composing the Manta's authored numbers
gives the curve the course is cut against (`RedlineCourse.CornerRadiusAtBoost`):

| boost held | speed | circle it holds | reads as |
|---|---|---|---|
| 1.00 | **720 u/s** | 237 u | flat out, both triggers buried |
| 0.75 | 585 | 207 | a lift |
| 0.50 | 450 | 172 | a real corner |
| 0.25 | 315 | 131 | slow in, hard out |
| 0.00 | 180 | 82 | one trigger released, the other flat — a pivot |

(Stick-only, at full boost: pitch 213 u, yaw 237 u — a pilot who **rolls** a corner into pitch
beats the curve, which is the skill layer rather than the design. A Time-10 Manta at 936 u/s
holds 247 u; the course is cut for the resting vessel, so an element level buys a real edge.)

**Intensity is what mix of corners a lap asks for.** Measured over 400 seeds, the median lap:

| intensity | corners that cost Soar | its hardest corner | mouth ⌀ | reads as |
|---|---|---|---|---|
| 1 | **0** of 8 | 362 u — 100% of top | 220 | flat out end to end; learn the gates |
| 2 | **1** of 8 | 153 u — 53% | 176 | one corner to get right |
| 3 | **2** of 8 | 121 u — 40%, then 57% | 144 | a lap with a rhythm |
| 4 | **3** of 8 | 99 u — **31%**, then 56%, then a **knife-edge** at 98% | 116 | two hairpins and a sweeper you hold flat out only by being exact |

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
- The Manta's shipped kit for the racing. Its **Yastri turn trails** — laid across the line
  through every corner, flared, shielded at Mass 5 — are the interference: a rival on the same
  line rams them and slows (`VesselChangeSpeedByPrismEffectSO` is wired on the Manta). A
  crystal on the course fires its **Kabloom** the ordinary way, and a rival's trail is
  skimmable — arm a bomb drafting behind someone, graze them to plant it, cash it on the next
  crystal. None of that is scored; the race is.

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
  exact, on a mouth of 58 u.

Mouths are a step wider than Headlong's at every level (110/88/72/58 vs 96/72/58/46): a Manta
at 720 crosses one in a sixth of a second with a quarter of the Rhino's lateral authority.
Presentation caps (50/72/76/80) each sit just over the level's measured worst half-turn
(44/67/72/71) — a gate faces its corner's bisector, so a cap under the half-turn zeroes the
jitter budget exactly where the gates most need to face you.

### Measured

`RedlineCourseTests` sweeps **400 seeds × 4 intensities** — closure, shell, mouth separation,
the absolute floor, the presentation cap, gate 0's pole placement, determinism, **the corner
demand**, the knife-edge, the speed-cost ordering (each level's hardest corner costs at least
5 more points of top speed than the last) and the runway. The shipped C# was compiled against
real `Vector3`/`Quaternion`/`Mathf` semantics and the suite **run** out of editor (14/14):

| intensity | corners costing Soar (median lap) | #1 | #2 | #3 | legs | max turn |
|---|---|---|---|---|---|---|
| 1 | 0 / 8 | 362 u · 100% | 483 u · 100% | 517 u · 100% | 559–656 | 77° |
| 2 | 1 / 8 | 153 u · 53% | 289 u · 100% | 430 u · 100% | 435–751 | 116° |
| 3 | 2 / 8 | 121 u · 40% | 160 u · 57% | 277 u · 100% | 475–813 | 132° |
| 4 | 3 / 8 | **99 u · 31%** | 157 u · 56% | 234 u · 98% | 466–868 | 135° |

Percentages are of the Manta's 720 u/s top speed via `FastestSpeedForCorner`; "b" in the
generator's own sweep output is the boost fraction that corner allows.

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
lava-lamp Manta and a released companion fly the same kit a human does. No Yastri for the bot —
no net trigger — so its steering is the stick's 30 + 0.2v yaw, which is exactly the curve the
course is cut to. The AI approach numbers in the scene (commit 420 / lead 480 / through 320) are
sized to the 237 u circle rather than inherited from Switchback's Dolphin.

## 6. Numbers, and where they are authored

| Knob | Where | Shipped |
|---|---|---|
| race length (laps × rings) | `Resources/EndConditionOverrides` → `redlineGateTarget` | **24** |
| laps | `MinigameRedline.unity` → `RedlineController.laps` | **3** |
| rings per lap | `RedlineCourse.ForIntensity` → `GateCount` | **8** |
| circuit base radius | same | **820** (~5 k per lap) |
| corner profile (turn angles) | same, per intensity | see §1 — with `AngularSpread`, **this is the design** |
| absolute safety floor / mouth / present cap | same, per intensity | 178–90 u; 110–58; 50–80 |
| AI commit / lead / through | scene → `GateRaceController` | 420 / 480 / 320 |
| detection clamp | scene → `maxPlausibleSpeed` | **1400** (a Time-10 Manta's 30 fps step is 31 u; the inherited 400 rejected it within a unit) |
| autopilot boost band | `Manta.prefab` → `MantaAnalogTurnBoostExecutor.aiBoostStickBand` | 0.35 |
| comeback rate | `ArcadeGameRedline.asset` | **0.35** (six gates behind buys 2.1 levels — and a Time level IS speed on this hull) |
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
   the speed readout — it is the mode's only feedback on what a corner cost you.
4. **Lap wrap.** After the eighth gate the lit ring returns to gate 1 and the goal row reads
   8/24 — **not** "finished".
5. **Intensity 4.** A median lap must contain two corners that force a trigger off (one taking
   you near cruise) and a third you can *just* hold flat out on a clean line — feeding in more
   stick than it needs should visibly not be enough, and easing a trigger should save it.
6. **AI.** Add an AI Manta and watch it **Soar on the straights and lift in the corners** — the
   drive of §5 — and complete more than one lap.
7. **MPPM two clients.** Both peers see the same circuit; a client's gate reports are credited
   and neither peer is credited twice for one crossing. Confirm a fast Manta's crossings are
   never dropped as "implausible" (the 400 → 1400 clamp).
8. **Regression — Headlong unchanged.** Launch Headlong: same circuit feel, Rhino only, its
   400-seed suite still green (the solver gained a field it leaves at 0).

## 9. Known limitations / follow-ups

- **Not editor-verified.** Asserted by a real out-of-editor compile of the course + tests
  against Unity-semantics stubs and a 1,600-circuit run of the shipped solver; nobody has
  flown it. See `Docs/UNITY_VERIFICATION_CHECKLIST.md`.
- **The autopilot drive is new to the whole fleet's AI Manta**, not just this mode — a
  lava-lamp Manta now Soars on straights. If the menu reads too fast, the dial is
  `aiBoostStickBand` on `Manta.prefab` (0 disables the drive).
- **Level 4's third corner is a knife-edge by design** (§4). If play-test wants three
  unambiguous hairpins, the lever is a NINTH gate per lap (27-gate race), not the profile.
- **The AI never uses Yastri**, so it cannot fly the 82 u pivot; its tightest is the stick's
  156 u at cruise. A level-4 hairpin (99 u) it will overshoot and re-attack via the orbit
  break — slower, but never stuck.
