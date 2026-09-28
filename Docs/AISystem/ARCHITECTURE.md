# AI pilot system — target architecture

**Status: PROPOSED (2026-09-28).** Nothing below is implemented unless it says *exists*. This is the
design the Squirrel slice ([`SQUIRREL_SKIM.md`](SQUIRREL_SKIM.md)) is built as the first instance
of, and the frame every later vessel slots into ([`ROADMAP.md`](ROADMAP.md)). The shipped system it
replaces is audited in [`CURRENT_STATE.md`](CURRENT_STATE.md); defect numbers `G#` refer there.

| Doc | Read it for |
|---|---|
| [`CURRENT_STATE.md`](CURRENT_STATE.md) | what ships today, one frame of a bot, what to keep, defects G1–G13 |
| **ARCHITECTURE.md** (this) | the layers, their contracts, the rules, difficulty, networking, budget, tooling |
| [`SQUIRREL_SKIM.md`](SQUIRREL_SKIM.md) | the first vertical slice: a Squirrel that skims Skim Race like a person |
| [`ROADMAP.md`](ROADMAP.md) | phases, vessel order, mode migration, risks, open decisions |

---

## 0. Goals and non-goals

**Goals.**
1. One framework that flies **every** vessel class, current and future, with its **whole kit** —
   not just "point at the objective".
2. **Human-like.** A bot plays by a human's rules through a human's controls; a harder bot is a
   *better pilot*, never a pilot with better numbers.
3. **Vessel knowledge lives with the vessel, mode knowledge with the mode.** Pairing a new vessel
   with an old mode — or an old vessel with a new mode — needs no edit to either.
4. **Measured.** Every behaviour has a metric, an offline harness runs the shipped decision code,
   and tuning is data.
5. **Cheap and quiet.** A fixed per-bot budget, server-only, no garbage per frame.

**Non-goals (for now).** ML-trained agents (a training pipeline, a new package, and a behaviour
nobody can tune or explain — revisit only for a narrow skill if the hand-built one plateaus);
stat-cheating rubber-banding; client-side AI; replacing the orbit-break math, which is right.

## 1. The rules the design is built on

These are the part to keep even if every class name below changes.

- **R1 — Same controls as a human.** A bot's only outputs are the `InputStatus` stick/throttle
  surface and ability presses through `R_VesselActionHandler`. It never writes `Speed`, `Course`,
  `BoostMultiplier` or the transform. (Retires G6's `Course` write.) This is CLAUDE.md's *AI parity
  is free if you don't fork* read as a law: every ability, every flight-model quirk and every damage
  channel then applies to bots with nothing extra to wire.
- **R2 — Same senses as a human.** A bot perceives what a pilot could see: geometry, visible
  objects, other vessels' positions and motion. Not hidden state (another pilot's element levels,
  a rival's cooldown). Reading a track's ORDERED prism list is fine — it is the visible ribbon, just
  indexed.
- **R3 — The vessel owns HOW, the mode owns WHAT.** A mode publishes objectives; a vessel's driver
  decides how its hull and kit achieve them.
- **R4 — Resolve abilities by capability, never by control.** `TryGetBoundAction<T>` answers per
  hull and per device map (G4 is what hardcoding a control costs).
- **R5 — Anything another peer must see is pressed through the replicated path**
  (`PerformShipControllerActionsReplicated`): placed mass, projectiles, telegraphs, VFX. A local
  press is only for motion-only abilities, whose result already replicates through the transform.
- **R6 — Predict with the game's own math.** Turning: `VesselTransformer.MaxTurnRateDegreesPerSecond`,
  `MinTurnRadius`, `PursuitReachability`. Contact: `ShieldShellMath` (box / octahedron / stella —
  the functions the contact tier itself resolves with). Speed response: the transformer's own
  `LERP_AMOUNT`. Never a second approximation of a rule the game already states; the moment it
  drifts, the bot's predictions and the world disagree and nothing reports it.
- **R7 — Difficulty is imperfection.** Reaction delay, precision, planning horizon, decision
  quality, risk tolerance, lapses. Never speed, damage or energy multipliers. If losing players need
  a hand, the `ElementalComebackSystem` already gives it to everyone, bots included.
- **R8 — Private randomness.** Bot noise comes from its own seeded `System.Random` (or
  `Unity.Mathematics.Random`), never `UnityEngine.Random`, whose global state seeded generators rely
  on (`SegmentSpawner` re-seeds it to lay the Skim Race track).
- **R9 — Pure core, thin shell.** Planning and control are pure static functions over structs,
  unit-testable in edit mode and runnable in an offline Roslyn harness (`PursuitReachability` is the
  existing example). `MonoBehaviour`s only gather inputs and write outputs.
- **R10 — Fail safe.** A layer that cannot decide hands back the platform default. A bot that does
  not press is better than a bot that presses wrong.

## 2. The layers

```
          ┌──────────────── mode: WHAT ─────────────────┐
          │ IAIObjectiveSource → AIObjective list        │
          │ (legacy SetExternalTargetProvider adapter)   │
          └──────────────────────┬──────────────────────┘
                                 │ objectives
┌── perception ─────┐  view  ┌───▼──────────────────────┐  intent  ┌── vessel: HOW ─────────────┐
│ AISensors          │──────▶│ AIBrain                  │─────────▶│ AIVesselDriver (per hull)   │
│ self · prism field │ (with │ utility pick of a        │          │ flight model · behaviours · │
│ ribbons · rivals · │ react │ behaviour + a safety veto│          │ ability policies            │
│ objectives         │ delay)└──────────────────────────┘          └─────────────┬──────────────┘
└────────────────────┘                                                          │ FlightCommand
      AIDifficultyProfileSO (+ per-bot personality) is read by every layer ┌──────▼─────────────┐
                                                                           │ AIFlightController  │
                                                                           │ (the pilot's hands) │
                                                                           └──────┬─────────────┘
                                                        InputStatus sticks/throttle + handler presses
```

`AIPilot` stays the component (it is in `VesselStatus`'s `[RequireComponent]` set and has dozens of
callers) and keeps its public API — `ConfigureForGameMode`, the two provider setters,
`TakeModeHooksFrom`, `RetargetCell`, `Start/StopAIPilot`, `AutoPilotEnabled`, `IsBreakingOrbit`.
It becomes the host that owns the layers and ticks them.

### 2.1 Perception — `AISensors` → `AIWorldView`

A plain struct snapshot, rebuilt on cadences:

| channel | source | cadence |
|---|---|---|
| **Self** — position, velocity (`Course × Speed`), nose, **commanded** orientation, boost, drift, own element levels, ability readiness (from executors), live turn rate and `MinTurnRadius`, skimmer radius (`SkimmerImpactor.SphereWorldRadius`), hull extent | `IVesselStatus`, `VesselTransformer`, executors; hull extent MEASURED once at `Initialize` from the hull colliders' world bounds (not a mesh's bind-pose bounds — the root-bone trap `VESSEL_CONSTRUCTION.md` records) | every frame |
| **Prism field** — prisms ahead: position, rotation, scale, shield tier, domain, danger | `PrismSpatialIndex.QueryCone` (apex = self, axis = course, length = speed × horizon) plus a small `QuerySphere` around the hull; contact envelope per prism from `ShieldShellMath` | 10–20 Hz, staggered per bot |
| **Ribbons** — ordered prism structures (a track, a wake, a rail) | a prism's `Trail` → a `RibbonCursor` (index, lerp, direction) walked each frame; needs a non-allocating sibling of `Trail.LookAhead`, which allocates a list per call | every frame (amortised O(1)) |
| **Objectives** | the mode's `IAIObjectiveSource` (§2.2) | on event + 2 Hz |
| **Rivals** — position, velocity, domain, class | `GameDataSO.Players` | 5–10 Hz |

**Reaction delay is modelled honestly:** the brain reads a snapshot from `reactionTime` ago (a
small ring buffer). A slow bot sees late; it is not told to "wait before acting", which reads as
hesitation rather than as human.

### 2.2 Objectives — the mode's side (`IAIObjectiveSource`)

A mode says what it wants, as data:

```csharp
public enum AIObjectiveKind { SteerTo, Collect, ThreadGate, Strike, Engage, Follow, Hold, AimAt }

public struct AIObjective
{
    public AIObjectiveKind Kind;
    public Vector3 Position;        // refreshed by the source each poll
    public Vector3 Velocity;        // for intercepts (a ball, a rival)
    public Vector3 Axis;            // gates: which way through
    public float   CaptureRadius;   // what counts as arrived (AI_ORBIT_BREAK.md: err generous)
    public float   Value;           // how much the mode wants it, relative to its siblings
    public Object  Subject;         // the crystal / rival / ball, for identity and commitment
}

public interface IAIObjectiveSource
{
    void CollectObjectives(AIPilot pilot, List<AIObjective> into);
    bool TryGetRoute(AIPilot pilot, out Trail route, out int direction);   // optional course hint
}
```

- The **platform default source** is today's behaviour, unchanged: crystals through
  `AIObjectiveScoring` (commitment hysteresis included), opponents under `seekPlayers`.
- **Objectives compose.** A gate race lists its next ring AND the crystals on the way; the brain
  weighs them. That is what dissolves G8 — `GateRaceController`'s hand-written detour becomes
  platform behaviour every mode gets.
- **The legacy adapter keeps all eleven hooked controllers working on day one:**
  `SetExternalTargetProvider(f)` becomes a single `SteerTo` objective sampled from `f` with the
  pilot's own capture radius, and `SetDriftLookTargetProvider(f)` becomes an `AimAt` hint. Modes
  then migrate one at a time (`ROADMAP.md` §3), each migration deleting mode code.

### 2.3 Decision — `AIBrain`

**A small utility selector**, not a behaviour-tree framework and not GOAP. The behaviour set per
bot is small, the context is continuous (distances, speeds, energy), and difficulty maps naturally
onto a utility selector (noise and temperature on the scores, reaction delay on the view) — and it
adds no package (CLAUDE.md: new dependencies are flagged, not slipped in).

- A **behaviour** is a small class: `float Score(in AIWorldView, objectives, profile)` in [0, 1] and
  `AIIntent Plan(...)`. Platform behaviours: `SeekObjective`, `FollowRoute`, `PursueRival`,
  `BreakOrbit` (the shipped math, moved not rewritten), `Recover`, `Loiter`. Vessel behaviours come
  from the driver (`Skim` on the Squirrel, `CommitDrift` on the Dolphin, `Ride` on the Urchin, …).
- **Selection** is argmax with hysteresis — a commitment bonus on the running behaviour and a
  minimum dwell — so a bot does not flicker between two near-equal plans.
- **Safety is a veto, not a competitor.** Hull-contact avoidance runs after selection on every
  plan, at every difficulty (§2.5). A behaviour that must never lose to "go faster" should not be
  scored against it.
- Output, **`AIIntent`**: a goal (point, or route + side + offset), a speed policy, an aim target,
  and ability requests *by capability*.

### 2.4 Vessel knowledge — `AIVesselDriver`

An optional `MonoBehaviour` on the vessel prefab (absent ⇒ `GenericDriver`, i.e. today's
behaviour). It owns everything that is true of the hull and not of the mode:

- **The flight-model descriptor** — which transformer family the hull runs (two-stick scissor
  throttle; one-thumb fixed throttle; the Urchin's signed axis; the Scarab's integrator), where its
  turn rate comes from, its rotation lag, what its drift does.
- **Its behaviours** (`Skim`, `CommitDrift`, `Ride`, …) — added to the brain's candidate set.
- **Its ability policies** — one per capability: `ShouldPress(view, intent, profile)` /
  `ShouldRelease(...)`, resolved through `TryGetBoundAction<T>` (R4), pressed replicated where R5
  says so. A policy reads the ability's own SO and executor for its numbers (cooldown, reach,
  `TubeReady`) rather than copying them — the reason `TryGetBoundAction` hands back the SO.

What moves where, once the seam exists:

| today (where the knowledge lives) | becomes |
|---|---|
| `AIPilot` — Dolphin commit drift + Echo Sight telegraph | `DolphinDriver` |
| `MantaAnalogTurnBoostExecutor` — the autopilot Soar drive | `MantaDriver` policy (the executor keeps the mechanics) |
| Broadside / Undertow / Wrecking Ball — `TryAutopilotDash` calls | `ScarabDriver` "engage" policy |
| Broadside / Hijack — Urchin spike + slip presses | `UrchinDriver` |
| Tollway — Scarab switch presses | `ScarabDriver` "place anchor" policy |
| Waystation — Butterfly Fold presses | `ButterflyDriver` |
| `AIPilot.abilities` blind cycler | per-ability policies; the cycler survives only as `GenericDriver`'s fallback for a hull nobody has written a driver for yet |

A mode then asks for *engage rival X* or *thread this gate* and never learns a control.

### 2.5 Control — `AIFlightController` (the pilot's hands)

Input: a `FlightCommand` — a reference point with its tangent and curvature (or a bare direction),
a speed or throttle policy, a drift request, a roll preference. Output: one write of the
`InputStatus` sticks and throttle per frame. Pure math, one thin adapter.

**The plant it has to fly**, as the transformer actually builds it:

1. The stick integrates into a **commanded** orientation at the live turn rate
   (`Pitch/Yaw/Roll` accumulate into `accumulatedRotation`).
2. The real orientation **follows with a first-order lag**, τ = 1/`LERP_AMOUNT` ≈ **0.67 s**
   (`RotateShip`'s slerp).
3. Outside a drift the velocity follows the nose (vector model: grip; scalar model: `Course`);
   inside one it slides at the drift's grip.
4. Speed approaches the throttle target exponentially at `LERP_AMOUNT`, with the terminal brake
   (`MinimumThrottleBrake`) on a zero target.

**The law.** Measure heading error against the **commanded** orientation (expose a read-only
`VesselTransformer.CommandedRotation`; the bot owns that integrator, so steering it directly
removes the 0.67 s lag from the feedback loop), use a PD term plus **curvature feed-forward**
(`ω_ff = v·κ`), and clamp the stick to `ω / ω_max`. Pick the look-ahead at no less than `v·τ` plus
margin so the lag is pre-compensated. Plan speed off the curvature ahead: a corner of curvature κ
is flyable at `v ≤ ω_max/κ`, so brake before it — or drift, which multiplies `ω_max`.

**Per family:** two-stick hulls write `XSum/YSum/YDiff/XDiff`; one-thumb hulls write
`EasedLeftJoystickPosition` (their throttle is fixed by the transformer); the Urchin's axis is
signed about the stick's rest; drift is pressed through the driver's drift capability. Replacing
the shipped law (G1) is the single most valuable change for every vessel at once, and it is
measurable against `AI_ORBIT_BREAK.md`'s numbers (400/400 must survive).

### 2.6 Difficulty — `AIDifficultyProfileSO`

One asset per tier; every layer reads it. Illustrative starting values — the harness sets the real
ones (`ROADMAP.md` Phase 4):

| knob | Rookie | Easy | Normal | Hard | Expert | layer |
|---|---|---|---|---|---|---|
| reaction time (s) | 0.40 | 0.30 | 0.20 | 0.12 | 0.06 | perception |
| planning horizon (s) | 0.5 | 0.7 | 1.0 | 1.3 | 1.6 | perception / driver |
| steering wander (°, low-frequency noise) | 6 | 4 | 2.5 | 1.2 | 0.4 | control |
| decision temperature (softmax over scores) | 0.25 | 0.15 | 0.08 | 0.04 | 0.01 | brain |
| ability use (fraction of the optimal policy) | 0.2 | 0.45 | 0.7 | 0.9 | 1.0 | driver |
| safety margin (× hull clearance) | 2.0 | 1.6 | 1.3 | 1.15 | 1.05 | veto |
| lapses (per minute) | 3 | 2 | 1 | 0.4 | 0.1 | all |
| awareness (field of view °) | 120 | 150 | 180 | 240 | 360 | perception |

- Each bot gets a **personality**: the tier's values jittered by a seed derived from its name, so
  three "Normal" bots are three pilots rather than three clones — and a Maelstrom seat (dealt once,
  replayed every round) keeps its personality all tournament.
- Vessel drivers add their own knobs in their own section of the profile (the Squirrel's are in
  `SQUIRREL_SKIM.md` §5.10).
- **Where the tier comes from is an open decision** (§4 D1). Until it is made, the default maps
  from intensity exactly as today (`skill = intensity × 0.25`), so nothing changes by accident.

### 2.7 Networking

- AI runs on the server only (*exists*, `Player.StartPlayer`).
- Sticks and throttle replicate through `InputStatus`'s owner-write variables (*exists*).
- Presses follow R5. The blind cycler's local presses (G5) are retired with it.
- The arena pilot swap hands a hull between a bot and a human mid-flight: a driver must release
  everything it holds on `StopAIPilot`, as the shipped commit-drift and cycler paths now do.

### 2.8 Performance

- **Budget: ≤ 0.1 ms per bot per frame on average (≤ 1.2 ms for a twelve-seat lobby), zero
  allocations per frame.** Profiler markers per layer (`AI.Perception`, `AI.Brain`, `AI.Driver`,
  `AI.Flight`).
- Spatial queries are staggered (a bot queries on frames where `hash(id) % N == frame % N`), capped
  in length, and reuse their result lists. Nothing is sampled while stationary.
- A central tick (one manager iterating bots) instead of per-bot `Update` is the natural next step
  once there is more than one layer to stagger (`ROADMAP.md` §4 D4).

### 2.9 Tooling and verification

- **Edit-mode tests** for every pure function: the controller, the skim-shell solver, the excursion
  planner, utility scoring — the `PursuitReachabilityTests` style.
- **An offline flight harness** (`Tools/Build/ai_flight_harness/`, the Roslyn compile-and-run
  pattern `urchin_reverse_harness` already uses): the SHIPPED pure AI code against a transcription
  of the flight model, on the real Skim Race tracks parsed from the scene. Per tier: lap time, skim
  duty, time to take-off, hull contacts, crystals per lap. It asserts the tier ladder is monotone
  and that Expert never touches. Transcribing the flight model is the one thing it cannot prove —
  pin the transcription against `VesselTransformer` in the same way the existing harnesses pin
  theirs.
- **`Tools/Build/squirrel_skim_model.py`** (*exists, this branch*) — the skim economy, the Boost
  Ring, the Squirrel's wake, the track geometry, how far each crystal pulls a pilot off the ribbon,
  and what today's straight-at-the-crystal line actually skims. Everything is read from the shipped
  assets, and the Monte Carlo parts are seeded, so a re-run is a diff.
- **In-editor:** an AI overlay (reference path, skim shell, candidate plans, the chosen one, energy
  and duty readouts) on a new `CSLogChannel.AI` verbose channel, and a
  **FrogletTools ▸ AI ▸ AI Pilot Inspector** reader window listing each bot's live layers.

## 3. Migration — no big bang

1. **Extract `AIFlightController`** and route today's targets through it. Behaviour parity for every
   mode; re-measure the orbit-break numbers.
2. **Add the `AIVesselDriver` seam** with `GenericDriver` = today, and move the Dolphin's commit loop
   into `DolphinDriver` — the proof the seam carries a real vessel.
3. **`SquirrelDriver`** — the first new capability (`SQUIRREL_SKIM.md`).
4. **Objective sources** with the legacy adapter; migrate mode-held vessel knowledge into drivers,
   mode by mode.
5. **Difficulty** profiles and their selector.

Each step ships on its own, behind the previous step's measurements. `ROADMAP.md` §1 lays these steps
out as Phases 0–6, with exit criteria.

## 4. Open decisions

- **D1 — Where does a match's difficulty come from?** A selector on the launch panel (recommended:
  intensity is an ARENA setting, and coupling the two means a player who wants a hard track cannot
  have a gentle opponent), or derived from intensity as today. Either way the default reproduces
  today's mapping.
- **D2 — Adaptive difficulty?** Recommended: no stat changes ever (R7). If wanted, let a bot move
  within its tier's imperfection knobs against the gap to the best human, off by default.
- **D3 — Perception fairness.** Proposed: geometry and visible motion yes; other pilots' element
  levels and cooldowns no.
- **D4 — Central AI tick** (one manager, staggering, one profiler marker) versus per-bot `Update`.
  Recommended once the driver seam lands.
