# The AI orbit — why every pilot circled its objective, and the fix

**Files:** `PursuitReachability.cs` (the math), `AIPilot.UpdateOrbitBreak` (the state machine),
`VesselTransformer.MinTurnRadius` (the live radius), `Tests/Editor/PursuitReachabilityTests.cs`.

---

## The symptom

Every AI, on every vessel, in every mode, would sometimes settle into a stable circle around the
thing it was chasing — a crystal, a ball, another pilot — and stay there. It looked like a tuning
problem and it survived every tuning pass, because turning *harder* is precisely the wrong response.

## The cause is geometry

A vessel flying at speed `v` with a maximum turn rate `ω` cannot fly a circle tighter than

```
R = v / ω
```

Pure pursuit — "steer at the objective, as hard as you can" — therefore **cannot reach anything
inside one of the two circles of radius `R` tangent to its own velocity**. Every frame it turns as
hard as it can; every frame the objective stays inside; the result is a stable orbit. This is the
**Dubins vehicle** reachability condition (Dubins, 1957), and the remedy is the one pilots use:
*extend and re-attack* — leave, come around, come back in on an arc you can actually fly.

For a Dolphin (110°/s authored on the prefab), `R` is a function of how fast it happens to be going:

| speed | `R` | unreachable inside |
|---|---|---|
| 40 u/s | 21 u | 42 u |
| 80 u/s | 42 u | 83 u |
| 150 u/s | 78 u | 156 u |
| 300 u/s | 156 u | 313 u |
| 357 u/s (boosted, Rampage) | 186 u | 372 u |

That last row is the one worth staring at. A boosted Dolphin cannot turn onto anything within
**372 units** of itself — a large fraction of the arena — and a crystal that lands there is one it
will circle indefinitely. This is also why the radius cannot be an authored constant: it is not a
property of the vessel, it is a property of the vessel *at this speed*.

## The test is one line

With `d` the vector to the objective, `f` the unit direction of travel, and `d⊥` the part of `d`
across `f`, the turning circle on the objective's side is centred at `C = R·(d⊥/|d⊥|)`, and the
objective is inside it when `|d − C| < R`. Expand, and the `R²` cancels:

```
|d|² − 2R·|d⊥| + R² < R²    ⟺    |d|² < 2R·|d⊥|    ⟺    |d| < 2R·sin θ
```

No trig, no square root beyond the two lengths, and it is **exact** rather than a heuristic —
`PursuitReachabilityTests.TurningCircleTest_IsExactlyTheCircleItClaimsToBe` proves the cancellation
against the long-hand circle definition over 20,000 random configurations, 0 disagreements.

**And the exit condition falls out of the same line.** `sin θ ≤ 1`, so an objective more than `2R`
away is reachable from *any* heading. "Get `2R` of separation" is a guarantee, not a guess — and it
is exactly the fly-out-and-come-back the manoeuvre is named for.

## Two triggers, because there are two kinds of orbit

| | catches | when it fires |
|---|---|---|
| **Turning-circle test** | the orbit that bounded turn rate causes | immediately, before a single lap |
| **`OrbitDetector`** | every other cause — a target that keeps moving, an impulse fighting the pursuit | after 540° swept with no progress |

The geometric test is strictly better where it applies: it is exact, it is predictive, and it costs
one cross product. The detector exists because it cannot possibly cover orbits it does not describe,
and those look identical from outside. It measures the symptom instead: **angle swept around the
objective, with no progress made.**

Swept angle is accumulated *unsigned* — a signed sweep needs a reference axis and in 3D there is no
natural one. The "no progress" gate is what separates a genuine spiralling approach (sweeps angle
**and** closes) from an orbit (only sweeps).

> **The gate's reference range is the range at the START of the accumulation window — not a running
> minimum.** A running minimum tracks a steady approach downward, so the current range is always
> within a hair of it, the progress test can never fire, and the detector silently degrades into
> something that only recognises an orbit at *exactly* constant range. It shipped that way for about
> ten minutes; the test that caught it (a closing spiral reported as an orbit) is now
> `OrbitDetector_FiresOnAStalledOrbitAndStaysQuietWhileClosing`.

## The break-off

**Enter** when the objective is inside the turning circle, or the detector fires.

**Steer** along the current heading, biased away from the objective by `orbitBreakAwayBias`. The
cheapest escape is the one that costs the least *turning*: a vessel already committed to a hard turn
gains separation fastest by rolling out and flying the tangent, not by hauling around 180° to point
away first — which keeps it near the objective for the whole reversal.

> `orbitBreakAwayBias` is a **look** dial, not a performance one. Measured over 400 randomized
> pursuits, biases of 0, 0.35, 0.6, 1.0 and 1.5 all reached 400/400 with mean times inside 0.06 s of
> each other. It ships at 0.35 so the manoeuvre reads as a deliberate break rather than as drifting
> past.

**Exit** on any of:
- separation past `2R × orbitBreakExitMargin` — the guarantee;
- the objective clear of a deliberately *smaller* circle (`× orbitBreakExitHysteresis`), but never
  before `orbitBreakMinSeconds`;
- `orbitBreakMaxSeconds`, a safety stop.

Both halves of that exit are load-bearing:

- The **hysteresis** is a Schmitt trigger. Exiting the instant the test clears means re-entering on
  the next frame, forever.
- The **minimum duration** is what makes a *detector-triggered* break-off work at all. That orbit
  has no turning-circle condition to clear, so without a floor it would end on its first frame and
  the AI would never actually break off.

An **entry dwell** was tried and rejected: requiring the condition to hold for 0.3 s before
committing cut needless break-offs only 104 → 97 out of 400, cost mean time, and badly hurt moving
targets (a fleeing target went 2.6 s → 9.9 s with three break-offs instead of one). Measured, not
assumed.

## Not while drifting

A drift deliberately locks `Course` and stops the vessel turning at all, so `R` is effectively
infinite and *everything* would read as an unbreakable orbit. A drift is a committed manoeuvre with
its own exit; when it ends badly the pilot re-seeks, and the break-off is available again the moment
it does. `UpdateOrbitBreak` returns early on `IsDrifting`.

Two related suppressions, for the same reason — a break-off is not a commitment:

- the AI does not **drift** while extending (`LookingAtCrystal` is forced false), and
- it does not light its **aim telegraph** (announcing an aim at the escape point would be announcing
  an aim at nothing).

## The heading is the COURSE, not the nose

`HeadingDirection()` reads `VesselStatus.Course`. Outside a drift the two are the same; inside one
they are not, and the turn radius applies to the direction of *travel*. Reasoning about reachability
from the nose would be wrong exactly when the vessel is doing something interesting.

## Measured

Against the shipped vessel model (Dolphin at 80 u/s, 110°/s), flying the shipped predicates:

| | pure pursuit | with the break-off |
|---|---|---|
| objective at the turning-circle centre | **never reached** (40 s, 12.2 laps) | reached in **3.6 s** |
| 400 randomized objectives | 343/400 (85.8%) | **400/400** |
| mean time to reach (successes) | 2.18 s | 2.45 s |
| worst time to reach | 4.03 s | 4.03 s |

+0.27 s of mean time buys +14 percentage points of "reaches the objective at all", and the worst
case does not move. The tests carry a trimmed version of this so a regression fails CI rather than a
playtest.

## Tuning

Everything is serialized on `AIPilot` under **Orbit break (extend and re-attack)**, per-prefab.
`breakOrbits` off returns that pilot to plain pure pursuit — for isolating a problem, not for
shipping.

## What this does NOT do

- It does not make the AI better at *choosing* objectives, only at reaching the one it chose.
- It does not help when the objective is simply faster than the pursuer (a target crossing at
  80 u/s against an 80 u/s vessel is unreachable for reasons no manoeuvre fixes).
- It is not path planning. A full Dubins solver would compute the optimal CSC path; this reaches the
  same place with one predicate and one timer, because the AI re-evaluates every frame anyway and
  an optimal path recomputed 60 times a second is wasted work.

## In-editor verification

1. Any mode with AI. Watch a pilot that has just overshot its objective — it should fly out,
   come around, and come back in, rather than settling into a circle.
2. **Rampage or The Bends** specifically: a boosted AI Dolphin has a ~370 unit unreachable bubble,
   so this is where the old behaviour was most visible and the change should be most obvious.
3. Set `breakOrbits` off on one AI's prefab and watch the two side by side — the old behaviour
   should be reproducible on demand.
4. Confirm a break-off never lights the aim telegraph (§ the Dolphin's Echo Sight): a cone
   pointing away from the objective means the suppression regressed.
5. Joust / Dog Fight (`seekPlayers`): confirm the AI still closes on a manoeuvring opponent and
   does not break off constantly — a moving target legitimately enters and leaves the turning
   circle, and the hysteresis is what keeps that from chattering.
