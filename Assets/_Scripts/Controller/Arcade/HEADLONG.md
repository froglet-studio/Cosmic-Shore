# Headlong — the Rhino-only circuit race

> `GameModes.Headlong = 48`. A closed loop of switch rings is cut through the cell and every
> pilot flies **laps** of it in order; the first **domain** whose **lead runner** threads the
> last gate of the last lap wins.

## 1. What the mode is asking

The Rhino's ramp boost engages only while the pilot holds full throttle and **near-zero stick**
(`(1 − XDiff) + |YDiff| + |YSum| + |XSum| < 0.3`, in every `IInputStrategy`'s
`PerformSpeedAndDirectionalEffects`), and it takes **6.1 s** to wind up to **910 u/s**. So the
mode is one question, asked once per corner:

> **Can you take this corner without letting go?**

Turn rate is linear in stick, so a pilot holding the boost may turn at only `0.28 × ω(v)` — and
the tightest circle they can fly without dropping it is

    R_flat-out(v) = R_min(v) / 0.28  ≈  114.8 / 0.28  =  **410 u**  at top speed

That number is the mode. Every corner on the circuit is cut against it, and **intensity is how
many corners clear it**:

| intensity | corner floor | max turn | mouth ⌀ | reads as |
|---|---|---|---|---|
| 1 | **1.50 ×** flat-out | 56° | 192 | every corner takeable flat out, with room |
| 2 | **1.15 ×** | 72° | 144 | every corner takeable flat out, barely |
| 3 | **0.90 ×** | 92° | 108 | some corners demand a lift |
| 4 | **0.70 ×** | 107° | 80 | several do, and the mouths are tight too |

Lift and you pay 6.1 s to wind back up — against a lap of roughly 4.9 k units, which a pilot who
held the boost covers in about five seconds. **The gap between a clean lap and a scruffy one is
most of a lap.**

## 2. Why the Rhino, and only the Rhino

Because its turn radius **converges** with speed, which is unique in the fleet. With
`RotationThrottleScaler` at 0.4, `R(v) = 180v / (π(0.4v + 90))` approaches **143 u** and never
exceeds it: 115 u at 910 u/s, 140 u at 9 100 u/s. Every other hull's turning circle grows without
bound, so a circuit whose corners are cut at a fixed radius is a course only this vessel gets
*better* at as it accelerates.

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

**It cannot fail, by construction.** It does not walk-and-backtrack the way `SwitchbackCourse`
does (whose constraint is *reachability*: can a Dolphin get from this gate to the next one). It
starts from a **regular octagon** — always legal, corners at 1.8× the flat-out radius — perturbs
every vertex, and **relaxes** the perturbation toward that base case until the corner floor and
the cell shell both hold. Scaling the whole perturbation set rather than re-rolling is what makes
the relaxation monotone and therefore terminating; measured, it converges in **≤ 12 of 48 steps**.

**Gate 0 sits on the spawn formation's pole**, and that is a fairness rule rather than a layout
preference: pilots spawn on an equatorial ring, so only a point on that ring's axis is
equidistant from all of them. It is applied as a **rigid rotation of the finished circuit**,
which is why it is free — every property the relaxation just established (corner radii, turn
angles, leg lengths, distance from the cell centre) is rotation-invariant.

Each gate faces the **flow bisector** of its corner, and the jitter that makes it "randomly
oriented" is spent from what is *left* of the presentation cap after the corner has taken its
half. Switchback's rule; on a circuit it applies at **every** vertex, because every vertex is a
corner.

### Measured

`HeadlongCircuitTests` sweeps **400 seeds × 4 intensities** and asserts the corner floor, the
shell, the mouth separation, the presentation cap, gate 0's pole placement and determinism. The
shipped C# was additionally compiled against real `Vector3`/`Quaternion`/`Mathf` and **run**:

| intensity | worst corner (× flat-out) | max turn | min leg | presentation | pole error |
|---|---|---|---|---|---|
| 1 | 1.50 | 56.2° | 563 | 44.8 ≤ 45 | 0.000° |
| 2 | 1.15 | 72.2° | 501 | 50.0 ≤ 50 | 0.000° |
| 3 | 0.90 | 92.1° | 453 | 55.0 ≤ 55 | 0.000° |
| 4 | 0.70 | 106.9° | 420 | 59.9 ≤ 60 | 0.000° |

1600 circuits, zero violations. Note the worst corner lands **exactly** on each floor: the
relaxation is *binding*, which is what makes the ladder real rather than incidental —
`Ladder_is_binding_and_ordered` asserts that in both directions, because a relaxation that
silently collapsed every course to the base octagon would pass a floor-only check while
destroying the intensity ladder.

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

## 6. Numbers, and where they are authored

| Knob | Where | Shipped |
|---|---|---|
| race length (laps × rings) | `Resources/EndConditionOverrides` → `headlongGateTarget` | **24** |
| laps | `MinigameHeadlong.unity` → `HeadlongController.laps` | **3** |
| rings per lap | `HeadlongCircuitSettings.ForIntensity` → `GateCount` | **8** |
| circuit base radius | same | **800** (612 u legs, ~4.9 k per lap) |
| corner floor / perturbation / mouth | same, per intensity | see §1 |
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
3. **Fly a lap holding full throttle.** Speed should climb linearly toward 910 over ~6 s. At
   intensity 1 and 2 you should be able to thread **every** corner without the boost dropping,
   with quiet hands. Watch the speed readout: if it falls, you spent too much stick.
4. **Lap wrap.** After the eighth gate the lit ring should return to gate 1 and the goal row
   should read 8/24 — **not** "finished". This is the lap path, and it is the one thing the
   extraction could plausibly have broken.
5. **Intensity 4.** At least one corner should be untakeable at speed. Confirm the choice is real:
   lifting, turning and re-winding should be *slower on that corner and faster overall* than
   ploughing into the wall of the shell.
6. **AI.** Add an AI and watch it complete **more than one lap** — the extraction bug above.
7. **MPPM two clients.** Both peers see the same circuit; a client's gate reports are credited
   (its goal row advances) and neither peer can be credited twice for one crossing.
8. **Regression — Switchback unchanged.** Launch Switchback: 20 gates in an open chain, Dolphin
   only, same feel as before. Its scene, its card and its 400-seed test suite are untouched.

## 9. Known limitations / follow-ups

- **Not editor-verified.** Everything above §8 is asserted by static analysis, a 1600-circuit
  offline run of the shipped generator, and a 10-test edit-mode suite run under a stub harness.
  Nobody has flown it. See `Docs/UNITY_VERIFICATION_CHECKLIST.md`.
- **The AI has never been tuned for a circuit.** It inherits Switchback's approach/commit
  distances (260/300/220), which were sized for a Dolphin at 347 u/s. A Rhino at 910 arrives
  2.6× faster and those numbers are very likely too short.
- **The mode does not read the boost state anywhere.** The whole design rests on the pilot
  choosing to hold it, and nothing on the HUD says whether they still have it beyond the speed
  itself. A "boost held" streak readout is the obvious next thing and is deliberately not here.
- **`Tools/Build/author_switchback_assets.py --check` fails on two files** (`ArcadeGameSwitchback.
  asset`, `MinigameSwitchback.unity`) for reasons that **predate this branch** — the shipped
  scene was re-saved by Unity (fileID renumbering, `GlobalObjectIdHash`) and the card gained
  `CallToActionTargetType` from `fe92c813`. This branch repaired that script's script PATHS after
  the refactor but deliberately did not re-author those two files.
