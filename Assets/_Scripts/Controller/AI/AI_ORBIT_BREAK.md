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

### The objective is not a point — it has a capture radius

Shipping the test above with `c = 0` produced a second, opposite complaint: **the AI peeled away
just before collecting a crystal.** That is the test being *correct* about the wrong question. It
asks whether the vessel can fly onto an infinitely small point, and a pursuer does not need to —
it needs to pass within the objective's collect radius.

With `c = 0`, the bearing error that trips a break-off is brutal at short range:

| range | breaks off above |
|---|---|
| 10 u | 6.9° |
| 20 u | 13.9° |
| 30 u | 21.1° |
| 50 u | 36.9° |

So an AI 20 units from a crystal, 14° off the nose — a collect it would have made — decides it
cannot reach the point and leaves.

The generalisation is exact and moves one term. The objective is truly unreachable only when its
whole capture sphere sits inside the turning circle, `|d − C| < R − c`:

```
|d|² + 2Rc − c²  <  2R·|d⊥|                    (reduces to |d|² < 2R·|d⊥| at c = 0)
```

and the guaranteed separation moves with it, to **`2R − c`**. The same quadratic's *other* root is
`|d| ≤ c` — "already inside the capture sphere" — so **the do-not-peel-away-on-final-approach case
is not a special case at all**: it is the second half of the same solution. At `c ≥ R` nothing is
ever unreachable, which is correct and self-consistent: a slow vessel with a generous capture radius
can always clip its objective.

Measured over 400 randomized pursuits (Dolphin at 80 u/s):

| capture radius | closest range it ever broke off at | break-offs |
|---|---|---|
| 0 u | *any* range | 2,787 |
| 8 u | 13.0 u | 61 |
| **18 u (shipped)** | **23.8 u** | **28** |
| 25 u | 32.1 u | 15 |

`Crystal.prefab` is a sphere of radius 1.2 at root scale 10, so ~12 units in the world; 18 adds hull
and errs generous **on purpose**. Erring large is the safe direction here and the reason is
structural: too small peels away on approach with nothing to catch it, while too large just means
the pilot orbits a little longer before `OrbitDetector` — which is watching regardless — notices.

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

**Exit** once the pilot has its **runway** — the separation it wants before turning back:

```
runway = max( 2R − c ,  speed × approachRunSeconds )
```

…subject to a `orbitBreakMinSeconds` floor and a `orbitBreakMaxSeconds` safety stop. The two terms
answer different questions and the larger wins:

- **`2R − c` is the geometric floor.** Below it the objective can still be inside the turning
  circle, so exiting there can leave the pilot trapped exactly as it was. Not optional.
- **`speed × approachRunSeconds` is the tactical floor.** A purely geometric break-off turns around
  the instant it is *allowed* to, which is right for arriving and useless for a vessel that has to
  DO something on the way in.

**Expressing the second as a time is what makes it portable.** The separation that matters scales
with speed, and `2R/v = 2/ω` is a constant for a given turn rate — so a runway measured in seconds
buys the same run at every speed, and it is simultaneously how long the pilot spends leaving and how
long the return leg lasts:

| speed | `R` | runway at 2.5 s | run time |
|---|---|---|---|
| 60 u/s | 31 u | 150 u | 2.50 s |
| 150 u/s | 78 u | 375 u | 2.50 s |
| 357 u/s | 186 u | 892 u | 2.50 s |

The cost is monotonic and small — over 400 randomized pursuits, all of these reach 400/400:

| `approachRunSeconds` | run | mean time to objective | worst |
|---|---|---|---|
| 0 (geometry only) | 0.82 s | 2.04 s | 5.07 s |
| 1.0 | 1.00 s | 2.05 s | 5.07 s |
| **1.5 (fleet default)** | **1.50 s** | **2.10 s** | **5.57 s** |
| 2.0 | 2.00 s | 2.16 s | 6.48 s |
| **2.5 (Dolphin)** | **2.50 s** | **2.23 s** | **7.43 s** |
| 3.5 | 3.50 s | 2.37 s | 9.37 s |

**The Dolphin is authored at 2.5 s** (`approachRunSeconds` on `Dolphin.prefab`) because it is the
one vessel that aims on the way in: it locks its course on the crystal and then swings its nose onto
a rival before the blast lands. 180° at its authored 110°/s is 1.64 s, so 2.5 s of straight run
leaves real margin. Any future vessel that needs to line something up during its approach raises the
same dial.

Two rejected alternatives, both measured rather than reasoned about:

- An **entry dwell** — requiring the condition to hold for 0.3 s before committing — cut needless
  break-offs only 104 → 97 out of 400, cost mean time, and badly hurt moving targets (a fleeing
  target went 2.6 s → 9.9 s with three break-offs instead of one).
- A **hysteresis exit** ("leave once the objective is clear of a smaller circle") was the original
  rule and is now subsumed. It made break-offs end as early as the geometry allowed, which is
  precisely the short approach run this section exists to lengthen.

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
| objective at the turning-circle centre | **never reached** (40 s, 12.2 laps) | reached in **4.4 s** |
| 400 randomized objectives | 373/400 (93.3%) | **400/400** |
| mean time to reach (successes) | 1.92 s | 2.10 s |
| worst time to reach | 4.02 s | 5.57 s |
| closest range it ever broke off at | — | 23.8 u (capture radius 18) |

+0.18 s of mean time buys the last 6.7% of "reaches the objective at all". The worst case grows
because the break-off deliberately flies further out than it strictly has to — that is the approach
run being bought, not a regression. The tests carry a trimmed version of all of this, so a
regression fails CI rather than a playtest.

## Tuning

Everything is serialized on `AIPilot` under **Orbit break (extend and re-attack)**, per-prefab.
`breakOrbits` off returns that pilot to plain pure pursuit — for isolating a problem, not for
shipping.

The two that matter:

| field | default | what it is for |
|---|---|---|
| `approachRunSeconds` | 1.5 (Dolphin 2.5) | how much straight run at the objective the break-off buys |
| `objectiveCaptureRadius` | 18 | how close counts as arrived; too small and the pilot peels off on final approach |

`objectiveCaptureRadius` is the one to revisit per mode rather than per vessel — a crystal, a ball
and an opposing hull are not the same size — but it is on the vessel because that is where `AIPilot`
lives. Err generous.

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
1. **It must never break off on final approach.** Watch a pilot inside ~25 units of a crystal: it
   should fly through, not peel. If it peels, `objectiveCaptureRadius` is too small for that
   objective.
2. **Rampage or The Bends** specifically: a boosted AI Dolphin has a ~370 unit unreachable bubble,
   so this is where the old behaviour was most visible and the change should be most obvious.
3. Set `breakOrbits` off on one AI's prefab and watch the two side by side — the old behaviour
   should be reproducible on demand.
4. Confirm a break-off never lights the aim telegraph (§ the Dolphin's Echo Sight): a cone
   pointing away from the objective means the suppression regressed.
5. Joust / Dog Fight (`seekPlayers`): confirm the AI still closes on a manoeuvring opponent and
   does not break off constantly — a moving target legitimately enters and leaves the turning
   circle, and the hysteresis is what keeps that from chattering.
