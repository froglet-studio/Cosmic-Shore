# Headlong — the Rhino-only circuit race

> `GameModes.Headlong = 49`. A closed loop of switch rings is cut through the cell and every
> pilot flies **laps** of it in order; the first **domain** whose **lead runner** threads the
> last gate of the last lap wins.

## 1. What the mode is asking

The Rhino's ramp boost pays **full** power only while the pilot holds full throttle and
near-zero stick (`StraightLineGesture`, deviation < 0.30), and past that it does not switch off —
it **grades down**, lerping the boost multiplier toward plain cruise as the stick goes over
(`RHINO_RAMP_BOOST.md`). So the mode is one question, asked once per corner:

> **How much of your boost is this corner worth?**

Turn rate is linear in stick and the max turn rate grows with speed, so composing those two with
the ramp's one lerp gives a continuous curve — the tightest circle the Rhino can hold at each
sustained speed:

| stick | speed | radius it holds | reads as |
|---|---|---|---|
| 0.30 | **1210 u/s** | 332 u | flat out, nothing given up |
| 0.40 | 1046 | 244 | a fast sweeper |
| 0.50 | 881 | 190 | a real corner |
| 0.70 | 553 | 124 | slow in, hard out |
| 1.00 | 60 | 29 | pivot in place |

**A corner is therefore an optimisation, not a classification**: find the largest speed whose
radius fits, and trade it against how long the following straight is (a tighter line is quicker
*through* a corner and costs seconds of ramp coming out). That is what a pilot practises, and it
is different on every corner of every seed.

**Intensity is what mix of corners a lap asks for.** Measured over 600 seeds, the median lap:

| intensity | corners that cost speed | its hardest corner | mouth ⌀ | reads as |
|---|---|---|---|---|
| 1 | **1** of 8 | 321 u — 99% of top | 192 | one gentle lift; learn the gates |
| 2 | **1** of 8 | 224 u — 82% | 144 | one corner to get right |
| 3 | **2** of 8 | 166 u — 65%, then 96% | 116 | a lap with a rhythm |
| 4 | **3** of 8 | 107 u — **37%**, then 64%, 91% | 92 | two hairpins and three straights |

Lift and you pay up to 5.2 s to wind back up — against a lap of roughly 5 k units, which a pilot
who held the boost covers in about four seconds. **The gap between a clean lap and a scruffy one
is most of a lap.**

### The first cut of this ladder did not deliver it, and the failure is worth carrying

Intensity originally authored only a corner **floor** — a lower bound the relaxation enforced —
and trusted a rising random perturbation to "genuinely PRODUCE sharper corners". Measured over
600 seeds × 8 corners against the shipped generator, it did not. A symmetric perturbation makes
as many corners wider as narrower and the floor then deletes exactly the courses that got
interesting, so the median corner sat near the base octagon's own radius at every level and
**93% of intensity-4 corners were takeable at full speed with no lift at all**. The mode's whole
premise — *intensity is how many corners you can take without lifting* — was false at every
level, and every test in `HeadlongCircuitTests` passed throughout.

> **A floor is a permission, not a demand.** If a generated property is the design, generate
> TOWARD it and assert the median, not the extreme.

`CornerProfile` is that fix: the ladder now authors the turn angles a lap is *built* to, and
`Every_intensity_demands_the_corners_it_claims` asserts how many corners of a median lap actually
cost speed.

## 2. Why the Rhino, and only the Rhino

Because its turn radius **converges** with speed, which is unique in the fleet. With
`RotationThrottleScaler` at 0.5, `R(v) = 180v / (π(0.5v + 90))` approaches **115 u** and never
exceeds it: 100 u at 1210 u/s, 114 u at 12 100 u/s. Every other hull's turning circle grows
without bound, so a circuit whose corners are cut at a fixed radius is a course only this vessel
gets *better* at as it accelerates.

Derivation, and the tuning pass that produced those numbers:
`_Scripts/Controller/Vessel/R_VesselActions/RHINO_RAMP_BOOST.md`.

## 3. What the mode does NOT add

No new weapon, no new ability, no cell of its own, no new scoring metric, and **nothing bespoke
about laps**. It reuses:

- `ScoringMetric.SwitchesThreaded` (9) and `IRoundStats.SwitchesThreaded` — a lapped circuit's
  gates are threaded in order exactly as an open chain's are, so the metric, the `BestByDomain`
  fold, the goal-stack row and the objective icon all come free. CLAUDE.md's rule: the goal row is
  keyed on the **metric**, never on the game mode.
- `GateRaceScoringRuleSO` — one class, one asset per mode. Nothing in it is mode-specific.
- `GateRaceController` and the whole gate-race platform (below).
- The Rhino's shipped kit for the racing; its **sword** is the interference.

A lap costs exactly one method: `GateRaceController.RingIndexFor` wraps a threaded count onto a
ring, and `RaceLength` is `rings × laps`. `SwitchThreadScoring.Credit` needed no change at all —
its validation is a bare equality against the pilot's own count with **no upper bound**, which is
precisely what a lapped course needs. A cap there would have made laps a special case in the one
place that has to stay a single equality test.

## 4. The circuit generator

`HeadlongCircuit` (+ `HeadlongCircuitSettings`), pure and deterministic — see
`RaceCourseGeometry` for the shared RNG and geometry.

**It builds TO a corner profile.** Each intensity authors one target **turn angle** per gate; the
generator deals them around the lap, then solves a per-vertex "sharpness" by bisection (three
Gauss-Seidel sweeps, since a vertex's sharpness moves its two gate gaps and therefore its
neighbours' corners) until each vertex turns through what it was asked for.

Four things about that are load-bearing:

- **Turn angle, not corner radius.** A closed loop turns through 360° in total, so a profile
  stated in angles is satisfiable by construction as long as it sums to about that. A profile
  stated in radii can quietly ask for eight corners each tighter than the ring can give — at
  which point every vertex saturates the solver together, the contrast vanishes, and the
  generator hands back the neutral ring. The first attempt did exactly that and produced 100%
  free corners at every level while looking correct.
- **Both shape terms are CONTRAST**, measured against the ring's own mean. Driving radius
  absolutely meant that when the profile over-asked, every vertex went out together and the whole
  ring inflated to the shell — a *larger* regular octagon, i.e. gentler corners, in the name of
  tightening them. Only the differences between vertices can mean anything.
- **Radius alone cannot cut a tight corner.** On a circle the corner radius is exactly
  `BaseRadius × cos(gap/2)`, so a narrow gap shortens the leg and softens the turn in the same
  proportion and the two cancel. A hairpin needs a vertex driven **out** between two driven
  **in**, with its two gaps pulled angularly **together**.
- **Demanding corners are dealt APART**, not shuffled freely. Two hairpins landing adjacent is
  not merely a worse rhythm, it is geometrically self-defeating — both vertices ask to be the
  spike, the contrast cancels, and the solver settles for two medium corners (measured: adjacent
  135° and 100° targets both came out at ~92°). The sorted profile is dealt into even then odd
  slots, so the two biggest turns sit half a lap apart by construction; the rotation and a
  direction flip are what is left for the seed.

**It still cannot fail.** The profile solve is followed by the same legality/relaxation loop as
before — shell, mouth separation and a **safety** corner floor — relaxing the whole shape toward
the neutral ring, which is always legal. `CornerRadiusFactor` is now that safety floor rather
than the design.

**Gate 0 sits on the spawn formation's pole**, and that is a fairness rule rather than a layout
preference: pilots spawn on an equatorial ring, so only a point on that ring's axis is
equidistant from all of them. It is applied as a **rigid rotation of the finished circuit**,
which is why it is free — every property the solve just established (corner radii, turn angles,
leg lengths, distance from the cell centre) is rotation-invariant.

Each gate faces the **flow bisector** of its corner, and the jitter that makes it "randomly
oriented" is spent from what is *left* of the presentation cap after the corner has taken its
half. **So the cap must COVER half the level's hardest turn**: a 149° hairpin presents its mouth
74.4° off the line you arrive on however the jitter is spent, and no authoring can improve on
that. Get it wrong and `cap − halfTurn` clamps to zero at every real corner, so the gates that
most need to face you are the ones that stop being oriented at all. The caps
(50 / 64 / 74 / 78) each sit just above their level's measured worst half-turn.

### Measured

`HeadlongCircuitTests` sweeps **400 seeds × 4 intensities** and asserts the safety floor, the
shell, the mouth separation, the presentation cap, gate 0's pole placement, determinism, **and
the corner demand**. The shipped C# was additionally compiled against real
`Vector3`/`Quaternion`/`Mathf` and **run** over 600 seeds:

| intensity | corners costing speed (median lap) | #1 | #2 | #3 | legs | max turn |
|---|---|---|---|---|---|---|
| 1 | 1 / 8 | 321 u · 99% | 388 u · 100% | 486 u · 100% | 533–665 | 87° |
| 2 | 1 / 8 | 224 u · 82% | 434 u · 100% | 481 u · 100% | 397–762 | 121° |
| 3 | 2 / 8 | 166 u · 65% | 305 u · 96% | 369 u · 100% | 332–820 | 142° |
| 4 | 3 / 8 | **107 u · 37%** | 165 u · 64% | 268 u · 91% | 489–924 | 149° |

Percentages are of the Rhino's 1210 u/s top speed, via `FastestSpeedForCorner`. Level 4 spends
its whole 360° budget on three corners and is therefore a **triangle with gates down its sides**:
three real braking zones and three long straights to wind the ramp back up, which is the most
demanding shape eight gates can make.

Two tests encode the ladder rather than the geometry, because the geometry was never what broke:
`Every_intensity_demands_the_corners_it_claims` (how many corners of a median lap cost speed) and
`The_hardest_corner_of_each_level_costs_more_speed_than_the_last` (each level's hardest corner
must cost at least 5 more points of top speed than the level below).

## 5. The gate-race platform

Headlong is the second mode on it, and the reason it exists. `SwitchbackController` was 780
lines of gate-race plumbing with about twenty of Switchback in it; the machinery moved to a
shared base rather than being forked:

| File | What it owns |
|---|---|
| `Racing/GateRaceController.cs` | course broadcast, ring construction, crossing detection, the optimistic report/reconcile round trip, AI steering, final scores |
| `Racing/RaceGateRing.cs` | one ring (runtime-created only) |
| `Racing/RaceGateTurnMonitor.cs` | the target — asked of the **controller**, never of an overrides key |
| `Racing/RaceGateObjectiveProvider.cs` | the per-viewer "which ring is mine next" arrow |
| `Racing/RaceCourseGeometry.cs` | `RaceGate`, the xorshift RNG, deflection, turn clamping, corner radius, min-turn-radius |
| `Racing/SwitchThreadScoring.cs` | the one place a threaded gate is credited |
| `Scoring/GateRaceScoringRuleSO.cs` | golf timing + the `BestByDomain` fold |

A subclass supplies **three** things: `ModeName`, `BuildCourse`, and `LapsPerRace`.

**One real bug was found in the extraction**: the AI target provider tested
`index >= _course.Count` and indexed `_course[index]` directly, so on a lapped circuit every AI
would have finished after one lap and loitered for the rest of the race. `lockedIndex` stays
**raw** on purpose — arriving at the same ring on the next lap *is* a new leg.

**And one bug the extraction ITSELF shipped, worth recording for the next scripted refactor.**
The whole "what a subclass supplies" block above — `ModeName`, `LapsPerRace`, `RaceLength`,
`RingIndexFor`, `BuildCourse`, `ResolveShell` — never landed in `GateRaceController`, so the
branch did not compile (5 × CS0115, three on `HeadlongController` and two on
`SwitchbackController`). The script that performed the split made `_course`/`_rings`
`protected` in one `str.replace` and *then* tried to insert the hooks against an anchor that
still contained the old `readonly` text. **A Python `str.replace` with no match is a silent
no-op**: it returns the string unchanged, raises nothing, and the resulting diff showed only
the `protected` edit — which is exactly what a correct run would also show for that hunk. Two
rules come out of it, and the second is the one that actually costs you:

1. **A scripted edit must assert its own anchor** (`assert old in t`) for every replacement.
   An edit that did not happen is indistinguishable from an edit that was not needed.
2. **A diff review cannot see an edit that did not happen.** The review here was a deliberate
   `diff -u` rather than a compile, on the reasoning that stubbing Netcode + SOAP for these
   controllers was too expensive. It was not: the whole package boundary (UnityEngine, Netcode,
   Collections, SOAP, UniTask, Reflex) is ~400 lines of stubs, after which the three
   controllers, the four extracted `Racing/` files, `GateRaceScoringRuleSO` and the **real**
   `MiniGameControllerBase` → `MultiplayerMiniGameControllerBase` →
   `MultiplayerDomainGamesController` chain compile against the **real** `GameDataSO`,
   `ScoringRuleSO`, `ScoringMetrics`, `TurnMonitor`, `EndConditionOverridesSO` and `GameModes`.
   Build it *before* the refactor, not after the errors arrive — and prove it is a gate by
   deleting the block again and watching the same five errors come back.

## 6. Numbers, and where they are authored

| Knob | Where | Shipped |
|---|---|---|
| race length (laps × rings) | `Resources/EndConditionOverrides` → `headlongGateTarget` | **24** |
| laps | `MinigameHeadlong.unity` → `HeadlongController.laps` | **3** |
| rings per lap | `HeadlongCircuitSettings.ForIntensity` → `GateCount` | **8** |
| circuit base radius | same | **800** (~5 k per lap) |
| corner profile (turn angles) | same, per intensity | see §4 — **this is the design** |
| safety corner floor / mouth / present cap | same, per intensity | 0.62–0.20 × flat-out; see §4 |
| comeback rate | `ArcadeGameHeadlong.asset` | **0.35** |
| course shell | scene → `courseOuterRadius` / `courseInnerRadiusFallback` | 1080 / 480 |

**The race length is ONE number.** The controller divides it by `laps` to size the circuit and
the turn monitor asks the controller for the target, so the finish line and the course cannot be
different lengths. `Tools/Build/author_headlong_assets.py --check` asserts that, that the target
is a whole number of laps, and that the comeback rate still buys a whole element level at a
quarter-of-target deficit — the trap `DOGFIGHT.md`, `BENDS.md`, `WILDLIFE_LIBERATION.md` and
`SWITCHBACK.md` have each recorded independently.

## 7. The arena is a REFERENCE, not a fork

The scene is cloned from `MinigameSwitchback`, and almost nothing is changed — which is the
point. Switchback already flies a gate race in the barren Skim Race cell with an **equatorial**
spawn ring, which is exactly the fairness rule this mode needs (gate 0 on that ring's pole). The
clone swaps the controller, its scoring rule asset and `firstGateDistance → laps`, and the
generator **asserts** that the donor still provides the four things it is inheriting rather than
leaving them as an absence.

## 8. In-editor verification

1. **Arcade card.** Menu → Arcade: a **Headlong** card appears, is clickable on a fresh account,
   and its launch panel pins the vessel to **Rhino** with no other hull selectable.
2. **Launch at intensity 1, 2 players.** The connecting panel holds until the circuit arrives,
   then eight rings bloom in a closed loop. Your next gate is lit lime and the objective arrow
   points at it.
3. **Fly a lap holding full throttle.** Speed should climb linearly toward 1210 over ~5.2 s. At
   intensity 1 and 2 there should be exactly ONE corner that makes you give some of it back;
   everything else goes flat out with quiet hands. Watch the speed readout — it is the mode's
   only feedback on how much stick a corner cost you.
4. **Lap wrap.** After the eighth gate the lit ring should return to gate 1 and the goal row
   should read 8/24 — **not** "finished". This is the lap path, and it is the one thing the
   extraction could plausibly have broken.
5. **Intensity 4 — the pass this branch exists for.** A median lap must contain **three**
   corners that cost speed, one of them a hairpin taking you to roughly a third of top speed, and
   a couple of long straights to rebuild on. Confirm the trade is real and readable: feeding in a
   LITTLE more stick than the corner needs should visibly cost you speed on the exit straight,
   and a smooth minimum-stick line should beat a brake-and-pivot one everywhere except the
   hairpins.
5b. **The graded ramp is the new thing to judge**, and it is a vessel change — verify it in
   freestyle too, per `RHINO_RAMP_BOOST.md` step 3a. If the slope feels too forgiving, the dial
   is `straightnessGraceBand` (down toward 0.3); too punishing, up past 1.0.
6. **AI.** Add an AI and watch it complete **more than one lap** — the extraction bug above.
7. **MPPM two clients.** Both peers see the same circuit; a client's gate reports are credited
   (its goal row advances) and neither peer can be credited twice for one crossing.
8. **Regression — Switchback unchanged.** Launch Switchback: 20 gates in an open chain, Dolphin
   only, same feel as before. Its scene, its card and its 400-seed test suite are untouched.

## 9. Known limitations / follow-ups

- **Not editor-verified.** Everything above §8 is asserted by static analysis, a real
  out-of-editor **compile** of the whole racing set AND the vessel-side change against stubbed
  packages, a 2400-circuit offline run of the shipped generator, and a 21-test edit-mode suite
  compiled and run under a stub harness. Nobody has flown it. See
  `Docs/UNITY_VERIFICATION_CHECKLIST.md`.
- **The AI has never been tuned for a circuit.** It inherits Switchback's approach/commit
  distances (260/300/220), which were sized for a Dolphin at 347 u/s. A Rhino at 1210 arrives
  3.5× faster and those numbers are very likely too short. The graded ramp helps here for free —
  an AI that steers now sheds speed rather than carrying full ramp into a corner it cannot
  make — but it is not a substitute for tuning the distances.
- **Intensity 4's hairpins present their mouths ~74° off the arrival line**, because a gate faces
  its corner's bisector and that is half of a 149° turn. The mouth was widened (40 → 46) to
  compensate; whether that is enough is a play-test question, and widening it further is the
  lever. Aiming the gate at the inbound line instead was considered and rejected: it only moves
  the problem to the exit, since no single axis can be within 50° of both legs of a hairpin.
- **The mode does not read the boost state anywhere.** The whole design rests on the pilot
  choosing to hold it, and nothing on the HUD says whether they still have it beyond the speed
  itself. A "boost held" streak readout is the obvious next thing and is deliberately not here.
- **`Tools/Build/author_switchback_assets.py --check` fails on two files** (`ArcadeGameSwitchback.
  asset`, `MinigameSwitchback.unity`) for reasons that **predate this branch** — the shipped
  scene was re-saved by Unity (fileID renumbering, `GlobalObjectIdHash`) and the card gained
  `CallToActionTargetType` from `fe92c813`. This branch repaired that script's script PATHS after
  the refactor but deliberately did not re-author those two files.
