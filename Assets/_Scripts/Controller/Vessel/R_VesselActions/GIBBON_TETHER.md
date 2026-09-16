# GIBBON — two-thumb flight on two tether beams

`VesselClassType.Gibbon = 13`. A dual-stick flyer whose two triggers fire lateral beams. A beam
cuts everything hostile on its path, plants an **anchor prism of your own domain** at its endpoint,
and becomes a **swing line** that reels you in and carries you faster than the vessel can fly.

This is the **second** design against this brief. The first is
`claude/spider-momentum-physics-8jlit2` (player-facing "Gibbon", zero-G brachiation on existing
prisms). It is superseded, not extended — but it was not wasted, and §6 records what was taken
from it. Id 13 and the name are deliberately the ones that branch allocated, so the two can never
collide on a merge.

## 1. The loop

**One squeeze per arm is the whole rhythm.** A press does two jobs, which is what removes the
"remember to let go" problem entirely:

| Input | Free arm | Arm holding a line |
|---|---|---|
| Trigger **press** | begin charging | **drop the line** *and* begin charging the next |
| Trigger **held** | analog depth **is** the beam's length, drawn ahead of you as a preview | — |
| Trigger **release** | **fire**: cut the path, plant the anchor, go taut | — |
| Left trigger | fires along the hull's **−right** axis | |
| Right trigger | fires along the hull's **+right** axis | |

**Roll is the aiming control.** The beams are locked to the hull's own lateral axis — never a
reticle, never a world axis — so banking 90° turns the right trigger into an "up" beam. Two sticks
fly the ship and the ship aims the beams; a full 3D aiming system costs no extra input, and there
is no cursor to read. That is the whole case for this being a *two-thumb flyer* rather than the
previous build's cursor game.

## 2. Where the speed comes from

A pure rope cannot accelerate you — it redirects. The accelerator is the **winch**: a live line's
rest length contracts on its own, and because a radial force exerts no torque about the anchor,
**angular momentum is conserved for free** and tangential speed rises as the radius falls. There is
no pump formula anywhere; the physics does it.

The pilot's decision is therefore **when to let go** — and reaching for the next line *is* letting
go, so the decision is made with the same press that starts the next shot.

Measured, from cruise (70 u/s), four alternating grabs released at 90° of sweep:

| | speed | time on the line |
|---|---|---|
| start | 70.0 | |
| grab 1 | **108.2** | 2.22 s |
| grab 2 | **135.3** | 1.80 s |
| grab 3 | **152.6** | 1.60 s |
| grab 4 | **161.5** | 1.52 s |

Each grab is *shorter* than the last. That accelerating rhythm is the feel the mechanic is for, and
it is emergent — nothing rewards tempo explicitly.

### The real ceiling is not the cap

`SpeedCap` (260) is a backstop. The **binding** limit in most swings is `MaxSwingRate`: angular
momentum `L` is fixed once you are on the line, the reel stops at `r = v/ω`, so a swing tops out
near **`sqrt(L·ω)`**. `L` is set by the beam length you fired at the speed you fired it — so

> **a longer beam fired fast banks more angular momentum and therefore pays out more speed.**

That makes the analog charge a genuine strategic choice rather than a range dial, and it was not
designed in — it fell out of conserving angular momentum. Measured: a 140 u beam from 70 u/s tops
out near 133 u/s; a 900 u beam from 230 u/s reaches the 260 cap instead.

## 3. Physics — a FORCE, not a constraint

The line is a **rope**: slack does nothing at all. Past the rest length it applies

- **spring** `−k·min(stretch, maxStretch)·d̂` — explicit, but stable because the stretch fed to it
  is clamped, so the acceleration it can ever produce is bounded by `Stiffness × MaxStretch`, a
  number that can be *stated*. Past `BreakStretch` the line **snaps** and releases itself, which is
  both the game rule and the bound that keeps the solver near equilibrium.
- **damper** on the **radial** component only, applied as the exact solution of `dv/dt = −c·v`
  (`v·e^(−c·dt)`), not `v − c·v·dt`. Unconditionally stable across a hitch frame, and frame-rate
  identical — measured 0.27 % speed and 0.26° heading between 30 Hz and 120 Hz.
- **the tangential component is never touched.** This is the design. A rope does no tangential
  work, so swing speed is preserved and angular momentum is conserved. Damping the whole velocity
  is the single easiest way to make this vessel feel like mud.

**Two lines are `Vector3` addition and nothing else.** That is the main reason this build is ~450
lines where the previous one was 1,726: a rigid constraint has to answer *"is this configuration
feasible?"* — hence that build's exact ray/sphere snap, exact circle stepping, and closed-form
intersection-circle solver. A force never asks. There is no two-tether special case in this code.

## 4. How it reaches the flight model — three seams, not a fifth transformer

The fleet already has four transformers each carrying its own `MoveShip`, which is why a fix
written into one reaches only the vessels running that class. The previous build added a fifth,
1,726 lines, and with it a fifth copy of grip, publishing, the modifier channels and integration.

`GibbonVesselTransformer` is **three overrides and no move step**, so the danger-prism slow,
knockback, the speed tunnel, external `Course` writes and every future fleet-wide change reach it
for free. It needed three small seams on `VesselTransformer`, **each defaulting to today's exact
behaviour** — `Vector3.zero` added to a float vector is an identity, so every existing vessel is
bit-identical:

| Seam | Default | Why it did not exist |
|---|---|---|
| `ComputeExternalAcceleration(velocity, dt)` | `Vector3.zero` | The vector model had **no door for a lateral force**: grip only rotates momentum toward the nose, thrust only pushes along it, `ShapeSpeed` can only change magnitude. Anything bending the path from outside had to fork `MoveShip`. |
| `NoseConvergence(dt)` | the drift expression, unchanged | Outside a drift the base snaps momentum onto the nose every frame — which would erase the tether force as fast as it was applied. |
| `AnalogTriggerDrift` | `true` | Both triggers are spent on the arms, so this vessel structurally cannot drift. Stated in **code**, not a prefab bool an inspector click could flip back on. |

`NoseConvergence` is deliberately **not zero** while taut: a small convergence keeps the two sticks
connected mid-arc, so you can lean into or out of a swing by pointing. That is the difference
between riding the tether and watching it.

## 5. The anchor is conserved mass, and it stays

The planted prism is ordinary conserved mass in the pilot's domain, laid through the same pooled
path as the Squirrel's boost ring (`PrismType.Boost` — "collider-live-on-spawn", which an anchor
must be). It is grazeable by the food web, steal-able, rideable, and it scores in every
prisms-destroyed mode.

**It is not removed when the line drops.** Withering it on release would be passive mass removal
wearing a cleanup's costume. So this vessel's trail is a **constellation**: the arena it leaves
behind is the record of where it swung, and you can re-anchor to your own old work.

The live line **also cuts** (your call): a taut line slices hostile prisms it sweeps, so movement
and destruction are the same act. Own-domain mass is skipped, which is what protects your own
constellation — the rule is "hostile", not a per-object exception. Super-shielded mass is
untouchable by contract, so the line simply does not cut it.

## 6. What was taken from the superseded build

Its best artifact was not code, it was the **method**: prove the physics offline by driving the
shipped C# through Unity-shaped stubs, because a swing that explodes or gains free energy is
invisible in review. `Tools/Build/gibbon_tether_harness` does exactly that — it compiles
`TetherSolver.cs` **from its real path**, so it cannot drift from what ships.

It caught a defect on its first run. `T9`: the rest-length floor was paying a line back *out* —
the exact chatter bug the previous branch had documented and fixed, reproduced independently by me
writing the obvious `rest = Max(floor, rest − step)`. That is why `TetherSolver.ApplyReel` is a
named function with the rule in its doc comment rather than a one-liner at the call site: fixing it
there fixes it once, fixing it at the call site fixes it until the next caller.

Also taken: the rest-length floor bounding **orbital rate** rather than radius (so the end of a
reel is readable instead of a blur), and the soft-efficiency speed cap that stops the winch doing
work rather than braking a pilot who earned their speed.

## 7. Files

| File | Role |
|---|---|
| `TetherSolver.cs` | All tether physics as **pure functions** — no Unity objects, no `Time.deltaTime`. Testable, and what the harness compiles. |
| `GibbonVesselTransformer.cs` | Three overrides; wires the seams. No move step. |
| `R_VesselActions/Executors/GibbonTetherExecutor.cs` | Both arms: charge, fire, anchor, reel, cut, drop. |
| `R_VesselActions/Data Containers/GibbonArmActionSO.cs` | One arm's trigger; press = drop+charge, release = fire. |
| `R_VesselActions/Data Containers/GibbonTetherConfigSO.cs` | Every number, one asset, shared by both arms. |
| `VesselTransformer.cs` | The three seams (default-identical). |
| `Tools/Build/gibbon_tether_harness/` | 12 assertions + the feel ladder. Exits non-zero on failure. |
| `_Prefabs/Spacevessels/Gibbon.prefab` | The hull. A **clone of `Squirrel.prefab`** (the only dual-stick hull already on the vector flight model) with the transformer swapped, the tether executor added to its `ShipActions` object, both triggers rebound to the arms, and `vesselType: 13`. |
| `_SO_Assets/VesselActions/Gibbon/` | `GibbonTetherConfig` + the two arm actions. |

## 8. Tuning knobs

| Knob | Ships | What it does |
|---|---|---|
| `MinBeamLength` / `MaxBeamLength` | 45 / 320 | The analog charge's range — and therefore the angular momentum you can bank |
| `Stiffness` | 9 | Steel cable vs bungee. Max force is `Stiffness × MaxStretch` |
| `RadialDamping` | 3.2 | Smoothness. Radial only — never touch the tangential term |
| `MaxStretch` / `BreakStretch` | 26 / 70 | Force ceiling / the snap |
| `ReelRate` | 34 | **The speed dial** |
| `MaxSwingRate` | 3.2 rad/s | The *real* speed ceiling in most swings (`sqrt(L·ω)`) |
| `SpeedCap` / `OverspeedDrag` | 260 / 26 | Terminal velocity backstop |
| `TetheredNoseConvergence` | 1.4 /s | How connected the sticks feel mid-swing |
| `LiveCutInterval` | 0.05 s | Live-line cut sampling rate |

## 9. How to test it

It is in the freestyle **Toy Box → Vessel Changer**. `ToyVesselRoster.Default` carries it and
`Toy_VesselChanger.asset` authors no list of its own, so it appears with no further wiring; the
prefab is registered in `Vessel Prefab Container.asset` so the swap resolves.

1. Menu_Main → freestyle → fly the **Vessel Changer** toy → pick the Gibbon.
2. **Squeeze and hold a trigger.** A thin cyan aiming line draws out to the side, and its length
   tracks the trigger. **Release** — the beam fires, a prism appears at the endpoint, anything
   hostile on the path dies, and the line goes taut and thickens as it loads.
3. **Bank.** The beams follow the hull's lateral axis, so rolling 90° fires the right trigger
   straight up. This is the whole aiming system.
4. **Hold the swing** and watch the speed climb; **press the same trigger again** to drop the line
   and start winding the next shot in one motion.
5. **Fire both.** Two lines is where the handling gets strange in the intended way.

Cruise is 70 u/s at neutral sticks (125 flat out), and a good swing should clear 150.

On a **keyboard** the charge is a timed wind-up instead (Left/Right Shift, ~0.9 s to full reach),
because only a gamepad reports real trigger travel. Touch is unbound.

## 10. Not done / open

- **No HUD, no audio, no real VFX.** The lines are runtime `LineRenderer`s built by the executor —
  enough to read the mechanic, explicitly not art. Nothing here has been opened in Unity.
- **It wears the Squirrel's hull, HUD and telemetry**, because it is a clone. Its two inert
  Squirrel executors (`DriftTrailActionExecutor`, `SquirrelTubeActionExecutor`) are still on the
  prefab but are unbound and dropped from the registry — they only subscribe to events, so they do
  nothing. Both are cleanup, not blockers.
- **Space and Time are explicit open design slots** in `Resources/ElementalAbilityMaps/Gibbon.asset`
  (`Input: 0`, empty `UpgradeLabel`), per the design gate — so the ability-row auditor reports them
  LOCKED rather than green. Proposals recorded in the asset: Space → reach, Time → the swing's rate.
  **Not authored, pending sign-off.**
- **The two arms have no L5 upgrades**, and cannot honestly have separate ones while they are
  mechanically identical. That is a real design question, not an oversight.
- **Charge depth is one tick stale on peers.** Press/release round-trip through
  `R_VesselActionHandler` and the trigger analogs are already owner-written NetworkVariables, so no
  new networking was needed — but a peer can receive the release up to a tick before the owner's
  final depth and plant its anchor slightly short. Bounded by one tick of trigger travel; it cannot
  desync the hull, whose motion replicates. The exact fix is to send the length in the RPC.
- **The live line's cut is a SAMPLE, not a test.** The line is long and moving; `LiveCutInterval`
  samples the current segment rather than the swept quad, so fast mass can slip through. Same class
  as the platform's "a fixed-timestep trigger is a sample" rule.
- **Non-analog devices lose the charge.** Keyboard and both mouse schemes write a binary 0/1 into
  the trigger analogs, so on those devices every shot is a maximum-length shot. Recorded rather
  than faked.
- **AI does not fly it.** `GibbonTetherExecutor.AutopilotDepth` exists as the hook; nothing drives
  the arms under autopilot yet, so an AI Gibbon flies as a plain two-stick vessel.
