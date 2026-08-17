# Rhino Shield Swipe — analog trigger swordsmanship

> The blade's **cutting behavior, energy meter, and the energize ritual** — ungated prism
> damage, the ENERGIZED-only super-shield pop (hold the both-triggers chop stance to charge),
> energy banked per kill (blade length + heat), the elemental-crystal 3D burst, and the full
> blade FX pass — live in **`RHINO_ENERGY_SWORD.md`**. This file covers only the
> pose/analog-swipe control model, though the two now share the triggers: the same
> reparameterized sum/difference that poses the blade also feeds the energize stance
> (`FeedSwordStance` → `IRhinoSwordState.SetInStance`). The `ShieldSkimmerScaleDriver`
> "Sword dimensions & scale ownership" section below is driven by that energy meter (no
> tick decay).

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
axis has meaningful travel). Its MOUNT sits at local (0, **−1**, 20.7) — lowered from
9.38, which perched the grip ~3 units above the hull's top, to about the hull's own
vertical centre (the hull box spans y ≈ −8.9…+6.4, centred −1.2). Both numbers are one edit on that instance: y is how high
the sword is *held*, the pitch is how far it is *raised*, and on a 60–240-unit blade
the PITCH is much the stronger lever for "it towers overhead". The executor captures whatever local pose is authored
as its zero point, and pivots rotation **and mount position** about the Fusilage
origin so the blade carves a real arc instead of spinning in place. On top of that
arc it applies the **hilt anchor**: the blade mesh is centred on its transform, so the
pose offsets that centre by the blade's own half-extent along its local +Y, keeping the
HILT at the authored mount and sending every unit of the energy meter's growth out the
tip. Without it a growing blade extends equally in both directions and reads as a
quarterstaff (see `RHINO_ENERGY_SWORD.md` § "The blade is HILT-ANCHORED").

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
  cross-press hands the stance to the still-held side, and **both held = centered full
  chop** (`diff 0, sum 2` — added with the energize ritual so remote peers replay the
  owner's two-trigger stance POSE instead of a one-sided swipe; the energize STANCE
  itself is evaluated from the replicated trigger mirrors, not from these events, so
  every peer reaches the same verdict — see `RHINO_ENERGY_SWORD.md`). Touch and
  keyboard resolve no action for these events (shared mapping has no entry), so they
  are unaffected; a future mobile binding gets the event path for free.

## Files

| Role | File |
|---|---|
| Executor (all runtime state + analog drive) | `Executors/ShieldSwipeActionExecutor.cs` |
| Shared tuning (single source, both directions) | `Data Containers/RhinoShieldSwipeConfigSO.cs` → `_SO_Assets/VesselActions/Rhino/RhinoShieldSwipeConfig.asset` |
| Swing velocity model (tip-vs-hilt impact speed) | `_Scripts/Controller/Vessel/SkimmerSwingKinematics.cs` + `SkimmerSwingKinematicsConfigSO.cs` → `_SO_Assets/VesselActions/Rhino/RhinoSwordSwingKinematicsConfig.asset`; component added on `ForceFieldSkimmer Variant.prefab` |
| Impact composition (vessel velocity + contact-point swing) | `PrismEffectHelper.ContactVelocity` → `SkimmerDamagePrismEffectSO` / `RhinoSkimmerDamagePrismEffectSO` |
| Accurate debris magnitude (velocity passed through as final + per-impact ceiling) | `PrismEffectHelper.DamageProportional` → `Prism.Damage`/`Explode` → `PrismEventData.DebrisSpeedLimit` → `PrismFactory` → `PrismExplosion.TriggerExplosion` |
| Model tests | `_Scripts/Tests/EditMode/SkimmerSwingKinematicsTests.cs` |
| Per-direction event bindings | `Data Containers/RhinoShieldSwipeActionSO.cs` → `RhinoShieldSwipeRight/LeftAction.asset` (direction only) |
| Prefab wiring | `Rhino.prefab`: `ShieldSwipeActionRegistry` GO under ShipActions, registered in `ActionExecutorRegistry._executors`; `_gamepadActionOverrides` events 1/2; skimmer transform rest pose |

Config knobs (`RhinoShieldSwipeConfig.asset`): `swipeYawDegrees` 90, `swipeRollDegrees`
90, `chopPitchDegrees` 65, `analogSmoothingSeconds` 0.04, `swipeOutSeconds` 0.18,
`returnSeconds` 0.3, `swipeCooldownSeconds` 0.35, `swipeEngageThreshold` 0.4,
`stanceSumThreshold` 1.5, `stanceCenterEpsilon` 0.4.

**Swipe recovery.** Each direction owes `swipeCooldownSeconds` after it releases before it
can sweep again (zero while the blade is ENERGIZED), so the sword swings with a rhythm.
It suppresses the DIFFERENCE axis only — the chop and the energize stance ride the sum and
are never blocked, and the blade keeps cutting everything it touches throughout. It is not
the rejected v1 slash cooldown, which gated DAMAGE; see `RHINO_ENERGY_SWORD.md`
§ "Swipe recovery".

## Sword dimensions & scale ownership

The sword's silhouette is the authored local scale on the ForceFieldSkimmer instance
in `Rhino.prefab` — (1.5, 30, 4.8). Resting-length scaling elongates local Y only
(`Skimmer.elongateYOnly`, set on `ForceFieldSkimmer Variant.prefab`; spherical
skimmers on other vessels keep the legacy uniform XYZ path). The one exception is the
transient elemental-crystal **burst** (`RHINO_ENERGY_SWORD.md`), which deliberately
scales all three dimensions for a few seconds before easing back to the authored X/Z.
Because the blade is hilt-anchored, every unit of that scale extends the sword from its
grip rather than through it — and `SkimmerSwingKinematics.lengthScale` is **2** on the
variant, because Unity's capsule spans local ±1 and the model would otherwise describe
only the middle half of the blade you can see.

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
| `v_bladeOrigin/vessel` | `ShieldSwipeActionExecutor` writes `localPosition = sweep * basePos + bladeUp * halfExtent` | the mount arcs about the Fusilage origin; the second term is the hilt anchor, whose growth component `SkimmerSwingKinematics.RemoveGrowthTranslation` strips back out so lengthening is not read as a swing |
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
`SkimmerDamagePrismEffectSO` (the generic effect, wired on every other skimmer) and
`RhinoSkimmerDamagePrismEffectSO` (wired on the sword itself — the energy-sword branch swapped
`RhinoForceFieldSkimmerImpactorDataContainer`'s prism slot from the generic effect to this one,
see `RHINO_ENERGY_SWORD.md`; both assets carry identical swing-model values, so the velocity
model is unchanged by that swap) call it before
`PrismEffectHelper.Damage(..., Vector3 velocity)` → `Prism.Damage` → `Prism.Explode`
(`Velocity = impactVector / volume` on the debris VFX).

A skimmer with **no** `SkimmerSwingKinematics` has no relative motion to add, so
`ContactVelocity` collapses to exactly the previous `Course * Speed` — every other vessel's
spherical skimmer is byte-for-byte unchanged.

| Dial | Where | Default |
|---|---|---|
| `swingVelocityScale` | the damage effect SO, next to `inertia` | 1 (the physical model); 0 restores pre-model behaviour |
| `maxImpactSpeed` | the damage effect SO | 0 = unclamped |
| `proportionalDebris` / `restitution` / `debrisSpeedLimit` | the damage effect SO (skimmer AND hull) | on / 1/3 / 200 (see the retune group below) |
| `restDeadbandSpeed` | `RhinoSwordSwingKinematicsConfig.asset` | 1.5 — below this a parked sword adds exactly nothing |
| `smoothingSeconds`, `maxSampleDeltaSeconds`, `maxAngularSpeedDegrees`, `includeVesselRotation`, `includeElongation` | `RhinoSwordSwingKinematicsConfig.asset` | 0.03 / 0.1 / 3600 / on / **off** |

### Making the magnitude survive to the screen

Computing an accurate impact velocity is only half the job — the debris response has to carry it.

**`Prism.Explode`'s divide is dead code.** It reads

```
debrisVelocity = impactVector / prismProperties.volume
```

but `SetupDestruction` runs first and stands the scale animator **down before reading the
volume**. `PrismScaleAnimator.GetCurrentVolume()` gates on `enabled` and returns 0 once it is
off, so `Mathf.Max(0f, 1f)` pins `prismProperties.volume` to **exactly 1 for every prism,
regardless of size**, at the moment of the divide. The legacy gain is therefore just `inertia` —
not `inertia / volume`.

That matters in two directions:

- With `inertia = 70`, the legacy skimmer path fed `contactSpeed * 70` into a `[30, 100]` clamp,
  so every skim saturated and the magnitude carried no information. (The hull's `Inertia` is 1,
  so hull rams were *not* saturated — they already produced roughly `ramSpeed`.)
- **Do not pre-multiply by volume expecting it to cancel.** It does not, and the leftover is a
  straight volume multiplier. This shipped briefly and was exactly the "Rhino trail prisms feel
  heavier than they should" bug: at `restitution` 1/3 a Rhino trail prism (volume ≈ 0.75) got
  `0.25x` and floored out at 10, while every larger prism sat pinned at the 200 ceiling — the
  Rhino's own trail was the only mass in the game being damped. `Boid` carried the same mistake,
  and worse: its factor was the volume of the *boid's* health prism, not the victim's.

So `PrismEffectHelper.DamageProportional` hands over the debris velocity **as final**, and
`Explode` passes it through untouched — the supplied `DebrisSpeedLimit` is what marks it as a
true velocity. That limit also replaces the mismatched prefab clamp with a ceiling in the same
units. Debris speed is then genuinely volume-independent: 11.7 at cruise and 178 for a tip
strike, whether the prism is a 0.75 trail sliver or a 125-unit environment block.

One more detail, load-bearing: `ClampMagnitude` reports the **pre-clamp** magnitude, so `Speed` —
which drives the shatter rate (`_ExplosionAmount = speed * elapsed`) — has always run at the raw
value while the translation was capped. That quirk is load-bearing tuning on the legacy paths, so
it is left alone there; on the accurate path both channels are put on one number, or raising the
ceiling would finish the shatter inside a single frame while the debris crawled.

`DebrisSpeedLimit` defaults **0** (= use the prefab clamp and the legacy divide), so projectile,
AOE and fauna destruction keep their existing behaviour.

**The hull runs the same model.** `VesselDamagePrismEffectSO` is on `proportionalDebris` too, so
the two paths cannot drift: flying straight at 35 u/s imparts the same debris speed whether the
prism is clipped by the hull or by the parked sword. At `restitution` 1 that was already what the
hull produced (`ramSpeed * Inertia(1) / 1`), so the conversion was behaviour-neutral; it earns
its place by locking the two paths to one formula and one retune.
`VesselDamagePrismEffect.asset` still carries a stale serialized `inertia: 70` — the SO stopped
declaring that field and reads `status.Inertia`; Unity will drop the orphan on next save.

> **Open, out of scope here:** the same animator-disabled-first ordering means the *mass
> accounting* events misreport. `SetupDestruction` raises `OnTrailBlockDestroyed` with
> `Volume = prismProperties.volume` = **1** for every prism, and the creation event raises
> `Volume` read while the prism is still scaled to zero. Anything keying off those channels sees
> a flat 1-per-prism instead of real volume. `Cell.LiveVolume` uses a different path
> (`Prism.CachedVolume` via `PrismSpatialIndex`) and is unaffected, but this is worth a look
> before trusting destroyed-mass stats.

### A parked sword must impart exactly what the hull does

Three things otherwise leave the sword permanently "hotter" than the hull while flying straight,
which is not swordsmanship and reads as a bug:

- **Blade elongation is ambient, not a strike.** `ShieldSkimmerScaleDriver` grows the blade at 30
  and shrinks it at 10 world-units/sec — driven by the energy meter, which rises on every prism
  the sword kills and empties into the crystal burst (`RHINO_ENERGY_SWORD.md`; the old tick-decay
  loop is gone), so the blade is almost never static. At the tip that is a standing **+15 / −5
  u/s** on a ~35 u/s cruise, and the burst's 600 u/s expansion would read far hotter still. The
  term is physically real and stays in the model, but `includeElongation` now defaults **off**;
  turn it on only if a shield extension should genuinely shove.
- **Sampling residue rectifies upward.** Whatever residue survives per-frame differentiation adds
  roughly *perpendicular* to the vessel's velocity, and `|v + n| > |v|` always — residue can only
  bias the magnitude up, never down. `restDeadbandSpeed` (1.5) zeroes sub-threshold relative
  motion so a parked sword contributes exactly nothing. A swipe runs 200–500 u/s, nowhere near it.
- **Slow vessel rotation was being dropped and quantized.** `AngularVelocity` recovers the angle
  from the quaternion's **vector part** via `atan2(|vec|, w)`, not from `acos(w)`.
  `Quaternion.ToAngleAxis` reads 12% high at 0.05°/frame and returns exactly **zero** below
  ~0.01°/frame in float32 — silently dropping a gently-turning vessel, and stepping what it does
  report into noise on a 37-unit lever arm. The `atan2` form measures exact from 0.002°/frame to 5°.

### Measured magnitudes (verify before retuning)

Simulating the authored rig (mount `(0, 9.38, 20.7)`, 20° rest pitch, 90/90/65 sweep over
`swipeOutSeconds` 0.18) against a vessel cruising at 35 u/s:

| blade length | near end | mid | **tip** |
|---|---|---|---|
| rest (scale 30) | 259 | 340 | **534 u/s** (≈15× the ship) |
| full shield (scale 120) | 768 | 340 | **1219 u/s** (≈35× the ship) |

**The near end is not "the ship's speed."** The blade mounts ~23 units out from its swing pivot,
so even the hilt rides a lever arm — and once shield growth pushes the blade past that offset,
the hilt swings on the *far side* of the pivot and moves fast in the opposite direction. That is
why the mid-blade can be the *slowest* point at full growth.

End-to-end debris speed, identical now for every prism size:

| contact speed | legacy (any volume) | proportional | **shipped (x1/3)** |
|---|---|---|---|
| parked sword @ cruise (35) | 100 (ceiling) | 35 — same as the hull | **11.7** |
| hilt, mid-swipe (200) | 100 (ceiling) | 200 | **66.7** |
| mid-blade (340) | 100 (ceiling) | 340 | **113** |
| **tip, mid-swipe (534)** | 100 (ceiling) | 534 | **178** |
| tip, full shield (1219) | 100 (ceiling) | 600 (ceiling) | **200** (ceiling) |
| hull ram @ 35 / 60 / 90 | 30 / 40 / 60 | 35 / 60 / 90 | **11.7 / 20 / 30** |

### The retune is one tuning group

Debris speed ships at **1/3** of the physical read, because full speed looks too hot. `restitution`
alone will not do it — the explosion's `minSpeed` **floor** would swallow the whole low end (at
1/3, every contact under 90 u/s would collapse back onto the floor, exactly the degenerate state
this work removed). Four values move together, all by the same factor:

| value | where | physical | shipped |
|---|---|---|---|
| `restitution` | the three damage effect SOs | 1 | **1/3** |
| `debrisSpeedLimit` | the three damage effect SOs | 600 | **200** |
| `minSpeed` | `PrismExplosion.prefab` | 30 | **10** |
| `maxSpeed` | `PrismExplosion.prefab` | 100 | **33.33** |

`minSpeed`/`maxSpeed` also carry the **legacy** paths (projectiles, AOE, fauna, danger prisms),
which are clamp-bound rather than proportional — scaling the band is what tones *those* down by
the same 1/3, so the retune really is uniform. `inertia` is NOT the lever: the proportional paths
ignore it entirely, and the legacy paths are saturated against the clamp, so lowering it changes
nothing until it drops below saturation.

**`restitution` also drives the shatter rate** (`_ExplosionAmount = speed * elapsed`, on the
accurate path both channels ride one number). So the retune slows the shatter by 3x as well, and
the two stay locked: a gentle graze now crumbles over ~1.8s while a tip strike bursts in ~7
frames. That coupling is deliberate — shatter violence tracks impact force for free — but it does
mean a velocity retune is also a shatter-timing retune.

**Debris SPIN rides the same number too.** `RotateFacesAlongAxis` (the subgraph
`ExplodingBlockGraph` feeds) turns each face by `_ExplosionAmount x _ExplosiveRotation` about
`cross(velocity, normal)` — so the stamped velocity sets the tumble AXIS while its magnitude, via
`_ExplosionAmount`, sets the tumble RATE. `_ExplosiveRotation` (a material constant on
`ExplodingBlockMaterial`, **not** per-instance) is therefore the gain on how much impact velocity
becomes spin, and it is the knob to reach for when debris flies right but tumbles too little or too
much — it moves spin ALONE, unlike `restitution`, which drags speed and shatter timing with it.
Shipped at **0.0169** (raised from the historical 0.01 in two +30% passes). Both debris paths read
it from the one material: `PrismDebris` copies the `PrismExplosion.prefab` renderer's
`sharedMaterial` for its batched entity draw, and the pooled GameObject fallback uses the same
asset, so there is exactly one place to tune.

The ceiling sits at `600 x restitution` = **200**: at the physical read 600 was where the shatter
stopped being perceivable (`_ExplosionAmount` reaches its "fully exploded" ~20.7 at `20.7 / speed`
seconds, so 600 u/s finishes inside ~2 frames), and scaling it with `restitution` keeps that
relationship. Only extreme full-shield tip strikes clip.

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

### Swing velocity model + debris retune (this branch — needs a human at the editor)

None of the below can be checked without play mode; the math is unit-tested, the *feel* is not.

5. **Parked sword == hull.** Fly straight, no trigger. Clip a prism with the hull, then clip an
   identical prism with the parked sword. Debris speed must look the same. (Was: sword visibly
   hotter.)
6. **Tip vs hilt.** Mid-swipe, clip one prism near the blade's base and one near the tip. The tip
   strike must throw debris noticeably harder AND along the swing tangent rather than along your
   course. Select the ForceFieldSkimmer in the hierarchy during play — `OnDrawGizmosSelected`
   draws the blade segment with per-point velocity rays, green→red by speed.
7. **Volume independence** (the fix for "Rhino trail prisms feel heavy"). Clip your own Rhino
   trail (the smallest mass in the game, volume ≈ 0.75) and then a fat environment prism at the
   same speed. Debris speed must match. Any visible difference means the pre-multiply crept back.
8. **Ram speed matters.** Ram prisms at low throttle vs full boost — debris should scale with
   speed instead of looking identical.
9. **The 1/3 retune also slowed the shatter 3x** (`restitution` drives both). Watch a slow graze:
   it now crumbles over ~1.8s where a tip strike bursts in ~7 frames. If the slow end reads as
   sluggish, raise `restitution` on the three damage SOs *and* `debrisSpeedLimit` + the prefab's
   `minSpeed`/`maxSpeed` together — they are one tuning group (see the retune table above).
10. **Other vessels are on the legacy path** and only moved via the clamp band. Spot-check a
    Squirrel/Manta skim and a projectile hit: debris should be ~1/3 its old speed, nothing else.
11. **AstroLeague field reset** (regression for the NaN fix): trigger an arena teardown / field
    reset and confirm those prisms now animate out instead of sitting still until they fade.

## Follow-ups

- **Prism mass-report defect (OPEN, ecology-adjacent)**: `OnTrailBlockDestroyed` raises
  `Volume = 1` for every prism and `OnTrailBlockCreated` reads volume at zero scale — same
  animator-ordering cause as the dead divide. `Cell.LiveVolume` is unaffected (separate
  `CachedVolume` path), but destroyed-mass stats are suspect. Full write-up and fix shape:
  `Docs/ECOSYSTEM.md` §20.2.
- **`VesselDamagePrismEffect.asset` stale field**: still serializes `inertia: 70`; the SO stopped
  declaring it and reads `status.Inertia`. Unity drops the orphan on next save — harmless.
- **Legacy debris paths** (projectiles, AOE, danger, fauna) still ride `impactVector * inertia`
  into the prefab clamp, so their magnitude carries no information. Converting them to
  `DamageProportional` is the same one-line change per effect if that signal is wanted.
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
