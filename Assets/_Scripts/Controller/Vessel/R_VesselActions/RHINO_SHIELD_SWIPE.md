# Rhino Shield Swipe — analog trigger swordsmanship

The Rhino's ForceFieldSkimmer capsule (the only CapsuleCollider on the vessel — its
"sword") is puppeteered by the analog triggers. The vessel plays like a swordsman:
the triggers are reparameterized Manta-style into a difference axis and a sum axis,
each mapped to an orientation axis of the sword.

## Control model

| Input | Range | Sword response |
|---|---|---|
| **Difference** (RT − LT) | −1 .. +1 | Lateral swipe: yaw + roll, up to ±90°/90° at full single pulls. Right pull = rightward yaw + counterclockwise roll (from the pilot's seat); left mirrors both axes. |
| **Sum** (RT + LT) | 0 .. 2 | Downward chop: pitch about the parent's right axis, up to 65° at both-full. Both triggers = difference 0, so the sword stays centered but chops straight down. A single full pull carries sum 1 → half chop. |

The raised rest pose is authored on the ForceFieldSkimmer instance transform in
`Rhino.prefab` (currently ~20° pitch; it was 41.8° pre-feature — raised so the chop
axis has meaningful travel). The executor captures whatever local pose is authored
as its zero point, and pivots rotation **and mount position** about the Fusilage
origin so the blade carves a real arc instead of spinning in place.

Sign conventions (Unity, verified): positive about up = yaw right; positive about
+forward = **counterclockwise** roll from the pilot's seat (`AngleAxis(+90, forward)`
maps right→up). Note `BarrelRollController`'s header comment states this backwards —
do not copy it (follow-up below).

## Input paths

- **Analog (local pilot, Gamepad/DualMouse)**: `ShieldSwipeActionExecutor.Update`
  reads `Gamepad.current` trigger axes **directly off the hardware** each frame
  (deadzone 0.05, renormalized so travel is continuous from the deadzone edge) and
  position-tracks them through a small jitter filter (`analogSmoothingSeconds`).
  The `InputStatus` trigger properties are NOT used for the local pose — they are
  NetworkVariable mirrors meant for replication. DualMouse falls back to the mirror
  (its "triggers" are binary RMBs by design).
- **Events (everything else)**: the gamepad strategy's trigger edges raise
  `RightStickAction(1)` / `LeftStickAction(2)`, bound in the Rhino's
  `_gamepadActionOverrides` to `RhinoShieldSwipeRight/LeftAction.asset`. On the local
  analog pilot these events are inert (the analog drive owns the pose) but they
  replicate through `R_VesselActionHandler`'s RPC chain, so **remote peers animate an
  event-driven approximation**: press = full stance + half chop (rate-limited by
  `swipeOutSeconds` so it reads as a swing), release = return (`returnSeconds`),
  cross-press hands the stance to the still-held side. Touch and keyboard resolve no
  action for these events (shared mapping has no entry), so they are unaffected; a
  future mobile binding gets the event path for free.

## Files

| Role | File |
|---|---|
| Executor (all runtime state + analog drive) | `Executors/ShieldSwipeActionExecutor.cs` |
| Shared tuning (single source, both directions) | `Data Containers/RhinoShieldSwipeConfigSO.cs` → `_SO_Assets/VesselActions/Rhino/RhinoShieldSwipeConfig.asset` |
| Swing velocity model (tip-vs-hilt impact speed) | `_Scripts/Controller/Vessel/SkimmerSwingKinematics.cs` + `SkimmerSwingKinematicsConfigSO.cs` → `_SO_Assets/VesselActions/Rhino/RhinoSwordSwingKinematicsConfig.asset`; component added on `ForceFieldSkimmer Variant.prefab` |
| Impact composition (vessel velocity + contact-point swing) | `PrismEffectHelper.ContactVelocity` → `SkimmerDamagePrismEffectSO` / `RhinoSkimmerDamagePrismEffectSO` |
| Model tests | `_Scripts/Tests/EditMode/SkimmerSwingKinematicsTests.cs` |
| Per-direction event bindings | `Data Containers/RhinoShieldSwipeActionSO.cs` → `RhinoShieldSwipeRight/LeftAction.asset` (direction only) |
| Prefab wiring | `Rhino.prefab`: `ShieldSwipeActionRegistry` GO under ShipActions, registered in `ActionExecutorRegistry._executors`; `_gamepadActionOverrides` events 1/2; skimmer transform rest pose |

Config knobs (`RhinoShieldSwipeConfig.asset`): `swipeYawDegrees` 90, `swipeRollDegrees`
90, `chopPitchDegrees` 65, `analogSmoothingSeconds` 0.04, `swipeOutSeconds` 0.18,
`returnSeconds` 0.3.

## Sword dimensions & scale ownership

The sword's silhouette is the authored local scale on the ForceFieldSkimmer instance
in `Rhino.prefab` — (1.5, 30, 4.8) — and X/Z are **never** scaled at runtime. All
runtime scaling elongates local Y only (`Skimmer.elongateYOnly`, set on
`ForceFieldSkimmer Variant.prefab`; spherical skimmers on other vessels keep the
legacy uniform XYZ path).

Exactly one component writes the sword's scale at runtime:

- **`ShieldSkimmerScaleDriver`** (on `ScaleSkimmerObject`) owns the transform — it
  sets `Skimmer.HasExternalScaleDriver` in `OnEnable`, which stands the Skimmer's own
  elemental scale write down. Each frame it tweens world Y between its resting base
  and `ShieldSkimmerScaleConfig.asset` `maxScale` (120) from the Shield resource
  (index 1), preserving the authored X/Z via `Skimmer.AuthoredShape`.
- **SPACE element sets the resting base**: the driver reads
  `Skimmer.LiveElementalScale` — the Skimmer's `Scale` ElementalFloat, authored on
  the variant prefab as Space 30 → 50 — as its live `BaseScale`, so Space levels
  lengthen the sword's resting length and shield growth composes on top. A Space
  deficit (negative levels) shortens it below 30 via the usual unclamped lerp.
- `GrowSkimmerActionRegistry` in `Rhino.prefab` is **inactive** — superseded by the
  driver. Its `GrowSkimmerActionExecutor` still writes uniform XYZ; do not re-enable
  it on the sword without porting the Y-only path.

`Skimmer.AuthoredShape` is captured in `Awake` (before any writer runs); Unity
guarantees all Awakes complete before the driver's first `Update` write.

## Self-impact exclusion (shipped with this feature)

The Rhino's sword capsule permanently overlaps its own hull, and the impact pipeline
had no self-exclusion — the Rhino's own `VesselDamageBySkimmerEffect`
(`inputToMute: RightStickAction`, 5s) ran with victim = attacker = the pilot on every
re-enter, permanently muting the pilot's own right trigger (invisible until this
feature bound an action to that event). `SkimmerImpactor` and `VesselImpactor` now
carry mirrored guards: **a vessel and its own skimmer never impact each other.**
Enemy-Rhino skims still mute the victim's right trigger — that debuff now visibly
disarms an active swipe, which is the designed interaction.

## Swing velocity model — the tip hits harder than the hilt

The sword is a **rigid segment swinging on a lever arm**, so no single number describes
"how fast the sword is moving." `SkimmerSwingKinematics`
(`_Scripts/Controller/Vessel/SkimmerSwingKinematics.cs`, on the ForceFieldSkimmer) models
the velocity of *any point* on the blade, and impact effects feed a destroyed prism the
velocity of the point that actually touched it.

### The model

```
v(P) = v_vessel  +  omega_vessel x (P - vesselOrigin)  +  R_vessel * v_rel(P)
v_rel(P) = v_bladeOrigin/vessel  +  omega_blade/vessel x (P - bladeOrigin)  +  (dL/dt)*f*axis
```

| Term | Source | Meaning |
|---|---|---|
| `v_vessel` | `Speed * Course + VesselTransformer.VelocityShift` | the hull's own translation — the canonical value the transformer integrates each frame |
| `omega_vessel x r` | vessel rotation, differentiated | a hard turn genuinely sweeps a 35-unit sword. Optional (`includeVesselRotation`) |
| `v_bladeOrigin/vessel` | `ShieldSwipeActionExecutor` writes `localPosition = sweep * basePos` | the mount arcs about the Fusilage origin |
| `omega_blade/vessel x r` | `localRotation = sweep * baseRot`, differentiated | the blade's own spin — the dominant term at the tip |
| `(dL/dt)*f*axis` | `ShieldSkimmerScaleDriver` growing local Y | a lengthening blade drives its points outward. Optional (`includeElongation`) |

Every rate is differentiated **in the vessel's frame**, so vessel translation, teleports,
respawns and pooling can never leak into the swing. Sampling is in `LateUpdate` — after the
swipe executor and scale driver have written the pose, which is also the pose the next
`FixedUpdate` evaluates triggers against, so an impact reads the rates that produced its
own contact. `OnEnable` drops the previous sample so a vessel swap can't differentiate
across the discontinuity.

### Which part of the sword hit

The sword's collider is a **SphereCollider (radius 0.5, centred on the blade)** scaled by
the transform's largest axis — so the trigger volume is a ball of radius = the blade's
half-length, and the trigger alone cannot say *where* on the blade something is.
`ClosestBladePoint(worldPoint)` recovers it by projecting onto the blade's centreline
segment and clamping; `NormalizedAlongBlade` reports 0 at the hilt, 1 at the tip. Hilt vs.
tip is derived from geometry (**the end farther from the pivot is the tip**), never
authored, so re-posing or re-parenting the sword cannot invert them.

### Impact wiring

`PrismEffectHelper.ContactVelocity` composes it, and both
`SkimmerDamagePrismEffectSO` (the generic effect that the Rhino sword's
`RhinoForceFieldSkimmerImpactorDataContainer` actually wires — `RhinoSkimmerDamagePrismEffect`
exists but is **not** in that container) and `RhinoSkimmerDamagePrismEffectSO` call it before
`PrismEffectHelper.Damage(..., Vector3 velocity)` → `Prism.Damage` → `Prism.Explode`
(`Velocity = impactVector / volume` on the debris VFX).

A skimmer with **no** `SkimmerSwingKinematics` has no relative motion to add, so
`ContactVelocity` collapses to exactly the previous `Course * Speed` — every other vessel's
spherical skimmer is byte-for-byte unchanged.

| Dial | Where | Default |
|---|---|---|
| `swingVelocityScale` | the damage effect SO, next to `inertia` | 1 (the physical model); 0 restores pre-model behaviour |
| `maxImpactSpeed` | the damage effect SO | 0 = unclamped |
| `smoothingSeconds`, `maxSampleDeltaSeconds`, `maxAngularSpeedDegrees`, `includeVesselRotation`, `includeElongation` | `RhinoSwordSwingKinematicsConfig.asset` | 0.03 / 0.1 / 3600 / on / on |

### Measured magnitudes (verify before retuning)

Simulating the authored rig (mount `(0, 9.38, 20.7)`, 20° rest pitch, 90/90/65 sweep over
`swipeOutSeconds` 0.18) against a vessel cruising at 35 u/s:

| blade length | near end | mid | **tip** |
|---|---|---|---|
| rest (scale 30) | 259 | 340 | **534 u/s** (≈15× the ship) |
| full shield (scale 120) | 768 | 340 | **1219 u/s** (≈35× the ship) |

Two consequences worth knowing before tuning:

- **The near end is not "the ship's speed."** The blade mounts ~23 units out from its swing
  pivot, so even the hilt rides a lever arm — and once shield growth pushes the blade past
  that offset, the hilt swings on the *far side* of the pivot and moves fast in the
  opposite direction. That is why the mid-blade can be the *slowest* point at full growth.
- **Debris magnitude saturates downstream.** `PrismExplosion` clamps the debris speed to
  `[30, 250]`, and the fed velocity reaches it at `250 * volume / inertia` ≈ 57 u/s (at the
  nominal volume 16) — which the ship's own cruise speed already approaches. So during a
  swipe the readable difference between a hilt graze and a tip strike is mostly
  **direction** (course vs. swing tangent) rather than magnitude. Lower `swingVelocityScale`
  or raise `PrismExplosion.maxSpeed` if a magnitude gradient is wanted.

### Verification

`Assets/_Scripts/Tests/EditMode/SkimmerSwingKinematicsTests.cs` rebuilds the authored rig
and checks `RelativeVelocity` against an analytically differentiated material point across
swipe / vessel-turn / blade-growth / all-at-once trajectories (1% relative tolerance), plus
the idle-sword no-op, tip-faster-than-midpoint, per-term isolation, and the
`AngularVelocity` shortest-arc and clamp behaviours.

Independently, the composition was checked against a continuum-limit ground truth: error
≤0.06% (numerical noise) as `dt -> 0`, and the residual at real frame rates halves exactly
with `dt` — the O(dt) chord-vs-arc signature every per-frame differentiator has, not a
model error.

## Known issue — analog stepping (OPEN)

The designer reports the sword still steps between boundary states instead of
holding intermediate analog poses, on their setup, even after all known software
quantizers were removed. Investigation trail (all shipped):

1. Rate-limited `MoveTowards` drive replaced with position tracking (the rate limit
   was at/below human pull speed, so the pose slewed to endpoints).
2. Deadzone renormalized (no step at the 0.05 boundary).
3. `InputStatus` NetworkVariable trigger mirrors bypassed — the pose reads
   `Gamepad.current.leftTrigger/rightTrigger.ReadValue()` directly.

With the pose now a pure function of the raw axis values, remaining suspects are
upstream of the code: pads that report digital triggers (Switch-style), or **Steam
Input** rewriting an analog pad's triggers as digital buttons. Diagnostic: watch the
trigger axis in `Window > Analysis > Input Debugger` while feathering — smooth 0→1
there but a stepped sword falsifies this analysis; 0/1 there confirms the driver.

## In-editor verification

1. Launch any Rhino-playable mode with a gamepad (or Menu_Main freestyle, swap to
   Rhino). Feather RT partially → sword should hold a partial rightward arc; LT
   mirrors; both triggers → centered straight-down chop scaling with pull.
2. Verify the right trigger works repeatedly (regression check for the self-mute
   fix), including immediately after skimming prisms and after left swipes.
3. Second client or MPPM: remote Rhino should show full swings on the owner's
   trigger presses (binary approximation, no analog pose).
4. Turn end / vessel swap mid-pull: sword snaps back to its authored rest pose.

## Follow-ups

- **Analog stepping**: run the Input Debugger diagnostic above; if raw axes are
  smooth, reopen the investigation (next suspect: none identified — instrument
  `ShieldSwipeActionExecutor` targets).
- **Fix `BarrelRollController` header comment** (says positive-about-forward = CW
  from the pilot's seat; it is CCW). Comment-only, other feature's file.
- **Mobile binding**: decide the touch gesture (the event path already handles
  binary inputs; bind `_touchActionOverrides` when designed).
- **Analog replication**: remote peers see binary swings only. If the analog pose
  should replicate, add owner-write diff/sum NetworkVariables to the executor
  (cheap: 2 floats) instead of widening the event vocabulary.
- **`GrowSkimmerActionExecutor` uniform stomp**: still writes `Vector3.one * localZ`
  (flattens any non-uniform skimmer). Inactive on the Rhino; port the
  `Skimmer.ElongateYOnly` path before reusing it on a sword-shaped skimmer.
- **Trail wait-time retune check**: `VesselPrismController` `waitTillOutsideSkimmer`
  reads the sword's `localScale.z`, which the scale fix restored from the inflated
  uniform 30–120 to the authored 4.8 — fresh Rhino trail prisms arm sooner. Verify
  the Rhino can't clip its own just-laid trail; if it can, tune the prefab's
  `waitTime` rather than re-inflating z.
