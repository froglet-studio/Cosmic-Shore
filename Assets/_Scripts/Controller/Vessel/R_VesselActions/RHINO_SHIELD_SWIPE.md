# Rhino Shield Swipe — analog trigger swordsmanship

> The **energy economy** layered on top of this rig — energy gained per prism destroyed, the
> crystal 3D burst + explosion, the energize stance (white blade + tracers + super-shield popping
> + 0 slash cooldown), and the 1 s slash cooldown — lives in **`RHINO_ENERGY_SWORD.md`**. This
> file covers only the pose/analog-swipe control model. The `ShieldSkimmerScaleDriver` "Sword
> dimensions & scale ownership" section below is now driven by that energy meter.

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
