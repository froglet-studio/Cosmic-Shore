# Waystation (`GameModes.Waystation = 58`)

The **Butterfly-only migration race**. The rings come in **clusters** — coils you weave — laid a
**FOLD** apart. Inside a cluster you fly; between clusters you teleport. Every pilot flies the
same course in ORDER, and the first **DOMAIN** whose **LEAD RUNNER** threads the last ring wins.

| | |
|---|---|
| Scene | `Assets/_Scenes/Multiplayer Scenes/MinigameWaystation.unity` |
| Controller | `WaystationController : GateRaceController` |
| Course | `WaystationCourse` + `WaystationCourseSettings` |
| Metric | `ScoringMetric.SwitchesThreaded` (9) — **reused**, not added |
| Domain fold | `ScoringMetrics.BestByDomain` (the lead runner, via `GateRaceScoringRuleSO`) |
| Rule asset | `WaystationScoringRule.asset` — a **second asset** on `GateRaceScoringRuleSO` |
| Turn monitor | `RaceGateTurnMonitor` — the platform's, which asks the controller |
| Target | `EndConditionOverridesSO.waystationRingTarget` (24) |
| Comeback | `ScoreDifferenceSource.SwitchesThreaded`, rate **0.4** |
| Cell | `Barren` — the same shell-only cell Switchback runs in |
| Offline authority | `Tools/Build/waystation_course.py` (`--check`, `--self-test`, `--compile`) |
| Compile harness | `Tools/Build/waystation_harness/` — compiles and RUNS the shipped course C# |
| Generator | `Tools/Build/author_waystation_assets.py` (`--check`) |

## The loop, in the Butterfly's own terms

The Butterfly is the fleet's slowest hull by design — 67 u/s flat out, 45°/s of turn — and its
**Time** ability is the **Fold**: hold LT, the vessel stops, a ghost appears, and releasing
teleports you to it. Since the Fold collapsed to **one reach along the heading**
(`R_VesselActions/BUTTERFLY_FOLD.md`, "One reach, everywhere") it has exactly one degree of
freedom: *the direction you were already flying*.

That single degree of freedom is the mode.

1. **Weave the coil.** A cluster's first `n-1` rings sit on a coil about the cluster's centre,
   threaded along the direction you arrived from. Each ring faces the coil's own tangent, so it
   faces the way you are travelling when you reach it.
2. **Line up on the exit gate.** The last ring of every cluster is a separate **exit gate**,
   laid ahead of the coil and **FACING the next cluster**, offset by the intensity's own cone.
3. **Fold the gap.** You leave along the gate's line, so threading it well makes the jump free.
   Thread it badly and you pay for the turn on a 45°/s hull before you can commit.

**A teleport threads nothing.** A fold crosses hundreds of units along its own heading and the
next cluster's rings are on that heading *by construction*, so without a rule a pilot would be
paid for every ring their jump passed through. The rule is the **platform's**, not this mode's:
`VesselTransformer.TeleportCount` states the fact and `GateRaceController` declines any step that
contains one. It is a **counter** rather than a distance because the step guard already there
fails the other way round — `maxPlausibleSpeed` rejects a LONG jump by accident and credits a
SHORT one, so a distance test can never be the answer.

## What the mode does NOT add

No metric, no turn monitor, no scoring class, no cell, no weapon, no vessel change. Two C# files
and their assets. The gate-race platform supplies the course broadcast, the rings, crossing
detection, the owner-detects/server-records round trip, AI steering and the final scores.

## The course, and why every number in it is measured

`Tools/Build/waystation_course.py` **mirrors** `WaystationCourse.cs` — the same xorshift, the
same draw order, the same arithmetic — reads the tables and the hull's constants out of the
repo, sweeps **200 seeds × 4 intensities** and **fails the build** on any of six checks. The
tables were *derived* there, not chosen.

| check | what it means | measured |
|---|---|---|
| `corner` | every corner clears the Butterfly's own minimum turning circle by 20% | **1.21×–1.70×** of 85.3 u |
| `mouth` | every ring's whole MOUTH stays inside the cell's shell | `[500, 1056]` inside `[412, 1060]` |
| `fold` | every fold gap is inside the fold's **RESTING** reach | up to **1332** of 1800 |
| `worth` | every fold gap is LONGER than the cluster it leaves | 424 vs 316 … 626 vs 383 |
| `gap` | no two consecutive clusters interpenetrate | 415–526 u vs two mouths |
| `cone` | the exit gate's facing is within the authored cone **exactly** | 40.0 / 55.0 / 72.0 / 90.0 |

The hull's numbers are **read off `Butterfly.prefab`** (`DefaultMinimumSpeed` 12 +
`DefaultThrottleScaler` 55 = 67 u/s; `Pitch`/`YawScaler` 45; `RotationThrottleScaler` 0 → a
minimum circle of **85.3 u**) and the fold's reach off `ButterflyFoldAction.asset`. Nothing is
typed into the model: *a constant copied out of an asset is true only on the day it is copied*,
and a vessel retune must move the course or fail it.

**The resting reach is the ceiling on purpose.** `reachRange` is 1800 at rest and 3600 at
Time 5, and the top rung's longest fold is 1332 — a mode whose hardest jump needed the upgrade
would play differently depending on whether a pilot found a crystal, and the comeback system
hands that upgrade to whoever is *losing*.

## The intensity ladder

**Intensity is how much coil there is between folds, how tight it is wound, and how hard the
exit line is.**

| | i1 | i2 | i3 | i4 |
|---|---|---|---|---|
| rings per cluster | 3 | 4 | 5 | 6 |
| — of which coil | 2 | 3 | 4 | 5 |
| coil radius | 160 | 150 | 135 | 120 |
| coil step (deg/ring) | 70 | 75 | 80 | 85 |
| coil pitch | 70 | 70 | 60 | 55 |
| ring mouth radius | 56 | 50 | 44 | 40 |
| hop (cluster to cluster) | 700–950 | 760–1000 | 820–1080 | 880–1150 |
| exit cone (deg) | 40 | 55 | 72 | 90 |
| clusters at target 24 | 8 | 6 | 5 | 4 |
| rings laid | 24 | 24 | 25 | 24 |

**Three numbers are deliberately NOT tables, and that is the point.** The exit lead (120), the
exit approach cap (45°) and the wander (80°) are one value each because each is pinned by
something that does not vary with intensity — the lead by how far the structure may reach before
it leaves the cell, the approach cap by the hull's turning circle, the wander by *a fold is never
behind you*. A table there would be four copies of one constraint.

## Findings — four of them outlive the mode

### 1. A positional clamp is not a containment strategy for a walk

The first course was a capped-turn WALK whose steps were clamped back into the cell's shell when
they left it. Measured, that produced corners at **38 u** against a hull that needs 85: pulling a
step's endpoint back **shortens the leg**, and a leg is exactly what the corner radius is measured
on. The clamp that kept rings in the cell was silently making the course unflyable, and no
per-corner tuning could have found it because the clamp only fires at the wall.

Choose the **heading** and the **length** so the step lands inside; never move the point
afterwards.

### 2. A single capped re-aim cannot contain anything

The second course steered its heading away from the wall inside the same turn cap instead of
clamping. Corners were then fine and the chain reached **3,576 units** from the cell centre inside
a 1,200 membrane: a bounded turn cannot undo an unbounded walk, because each hop adds a thousand
units and the turn can only bend by 80°.

**Containment is now a property of the construction.** Every cluster centre lies *exactly* on one
sphere and a hop is a **geodesic step** on it, so the chain cannot leave however long it runs.
There is no clamp, no rejection, no retry and no failure path — which is also why
`WaystationController.BuildCourse` never backs off and always returns the ring count it promised.

### 3. A coil ring cannot be an aiming device

The exit gate was first the *last coil ring*, with the coil's **roll solved** so that ring's
tangent pointed at the next cluster — one `atan2`, and it looked elegant. It cannot work: a coil
ring faces its own tangent, whose tilt off the coil plane is fixed by
`pitch / (CoilRadius · step)`, so it can be rolled in **azimuth** and never in **elevation**.
Measured at up to **167°** off the fold against an authored 72° cone.

The exit gate is its own gate, and it **faces the next cluster from ITS OWN POSITION** — a fold
starts where the pilot is, so facing it from the cluster's centre would have been the wrong
question. That is what makes the offset *exactly* the authored cone rather than approximately it.

### 4. A corner is measured on CHORDS, so clamp the chord

The exit gate's approach turn was clamped against the last ring's **tangent**. A corner is the
angle between two **chords**, and the helix tangent at a ring is not the chord to it — so the cap
bounded the wrong angle and the real corner ran **45% under** it. Clamp against what the check
measures.

## The offline authority's own negative controls

`--self-test` reproduces all three design defects and asserts each fires the check that caught it:

```
a positional clamp instead of a geodesic hop        -> gap, mouth, worth
a positional clamp on the exit gate (tight shell)   -> corner, mouth
a coil ring as the aiming device                    -> cone
```

The exit-gate control runs in a **deliberately tightened shell**, because on the shipped course
the clamp never engages at all — which is itself evidence for the design (containment has margin
to spare) and makes the control **vacuous** without it. *A control that cannot engage proves
nothing.*

## AI

An AI **folds**, and it had to be given the verb explicitly. `AIPilot` writes a stick and a
throttle and nothing else, so every held ability in the fleet is inert under autopilot — and here
the held ability is the whole mode: a bot that only flew would cross each ~900-unit gap at 67 u/s
against a human who crosses it instantly, which is not a competitor.

The drive lives in the **controller** rather than on the vessel (the Broadside / Tollway / Hijack
shape) because the decision is mode knowledge — how far the next ring is — while the **numbers stay
in the ability's own asset**, asked through the new `R_VesselActionHandler.TryGetBoundAction<T>` so
a retune of the Fold moves the bot with it. It presses through
`PerformShipControllerActionsReplicated`, never a local `StartAction`: an AI is simulated
server-only, so a local press would teleport the vessel on one machine and play the wither and the
bloom for nobody. The hold IS the distance (`distance / ReachSpeed`), because the executor reaches
`hold × ReachSpeed` along the heading.

**It fails safe.** Three gates — far enough to be worth folding (`aiFoldMinDistance` 420), inside
the fold's resolved reach, and lined up within `aiFoldAimDegrees` (12°) — each refuse by simply not
pressing, and a bot that never presses flies the course exactly as it did before the drive existed.
That is deliberate: pitch and yaw are dead for the fold's duration, so a bot that presses off-line
lands off-line with no way to correct, and no amount of folding is worth one wrong jump.

### `OnServerTick` — a seam added so the drive could not break the platform

`GateRaceController.Update` is a **Unity message, not a virtual**. A subclass that declared its own
`void Update()` would HIDE it, Unity would call only the derived slot, and **crossing detection —
the whole race — would silently stop working with nothing in the console**. No gate-race subclass
had ever declared one, so the hazard was untested rather than absent. `OnServerTick` is the seam:
called on the server every frame, unconditionally (a driver usually needs the turn-running test,
and a driver *tearing down* what it started needs the frames after it).

**General rule: a Unity message on a base class is a slot a subclass can take without being told,
so a base that expects to be subclassed should expose a named hook rather than a message.**

## Toasts

**Two idle hints and nothing else**, and the absence is a decision: the gate-race platform has no
gate-threaded hook, so a milestone or lead-change situation here would have **no poster** — and an
enum member nothing raises reads exactly like a feature. An idle hint needs no poster (the toast
system fires it off `idleSeconds`), which is why Regatta authored only hints too. They are the
mode's two verbs, because a pilot who never finds the Fold simply orbits the first cluster forever.

## Assets

Every one is authored by `Tools/Build/author_waystation_assets.py`; none is hand-edited.

- `_SO_Assets/Scoring Rules/WaystationScoringRule.asset`
- `_SO_Assets/Games/ArcadeGameWaystation.asset`
- `_SO_Assets/Game Toasts/GameToastConfig_Waystation.asset`
- `_SO_Assets/Mode Previews/ModePreview_Waystation.asset`
- `_Scenes/Multiplayer Scenes/MinigameWaystation.unity` — **cloned from Switchback**, the
  platform's other OPEN chain. Four swaps and nothing else: the controller script, the scoring
  rule, the four AI hulls (Dolphin → Butterfly), and the **removal** of Switchback's own
  `firstGateDistance`, which Waystation deliberately does not declare.
- registrations: the master roster, the Arcade grid, `alwaysUnlockedModes`, Build Settings (after
  Skein), `waystationRingTarget`, the toast library, the preview library.

## Verification status

**Authored headless; nothing has been opened in the editor.** What is proved:

- The course model against six checks over **200 seeds × 4 intensities**, with three negative
  controls that each fire the check they were written for.
- **The shipped C# IS the model.** `--compile` builds `Tools/Build/waystation_harness`, which
  compiles `WaystationCourse.cs` and `RaceCourseGeometry.cs` **verbatim out of `Assets/`** against
  a `Vector3`/`Mathf` shim, runs them, and compares **gate for gate**: 3,880 gates agree to
  **0.0059 u** of position and **0.0005°** of axis — float32 against float64 on a course spanning
  a thousand units. So the six checks above are statements about the game rather than about a
  transcription of it.
- The generator's `--check`, watched **failing** on a mutated asset and passing when restored.
- The nine standing out-of-editor gates.

What is not proved: that it plays. Also not proved by any of the above — the controller, the AI
fold drive and the scene are **not** in the compile harness (they reach Netcode and the whole
gameplay monolith), so they have had a syntax pass and an API-surface read and no type check.

## Known limitations

- **An AI's fold is UNTUNED.** The drive is written and fails safe, but `aiFoldMinDistance`,
  `aiFoldAimDegrees` and `aiFoldRetrySeconds` are reasoned numbers rather than measured ones: how
  often a bot's heading actually falls inside a 12-degree cone on its next ring is a property of
  `AIPilot`'s steering, which no offline model here covers. If bots fold rarely, that cone is the
  dial — and widening it is safe in the direction that matters, because the reach gate still stops
  a fold that would overshoot.
- **The preview is shell-only.** The course is generated at match start and broadcast as geometry,
  so there are no rings to stand in a preview arena. A `StructurePrefab` of one standing cluster
  would fix it and is the recorded gap — the same gap Switchback, Regatta and Tollway have.
- **`maxPlausibleSpeed` is inherited at 400** against a hull that tops out at 67. It is the step
  guard for respawns and ejects, and the teleport counter is what declines a fold, so the
  generosity costs nothing — but it is inherited rather than derived, and a tighter value would
  describe this hull.
- **No milestone toasts** (above).
