# Squirrel "Oak Trunk" Tube Ability

A Squirrel vessel ability: **press to place** a long tube of thick **danger prisms** straight out in
front of the vessel — a giant trunk you rocket through the hollow centre while it obstructs everyone
else. No preview; it just places the prisms.

## Controls (drift simplification)

The Squirrel's drift was collapsed from two triggers onto **one analog trigger**, freeing the other
trigger for this ability.

| Input | Gamepad | Touch |
|---|---|---|
| Drift (analog: no-drift → single → sharp) | Left trigger | keep-left-finger (`OnlyLeftStickAction`) |
| Tube (press to place) | Right trigger | keep-right-finger (`OnlyRightStickAction`) |

`VesselTransformer.singleTriggerDrift` (on the Squirrel only) remaps the left trigger's 0-1 travel
across the full 0-2 drift range so a single trigger spans the light→sharp range the Manta still gets
from summing both triggers. See `DriftActionSO.playDriftSfx` (off on the sharp tier) so the shared
drift SFX isn't doubled when both tiers stack on one trigger.

The ability fires on the trigger **press** (`Begin`); release (`Commit`) does nothing.

## Placement — straight out the front, led by speed

The tube forms ahead of the vessel along its **nose / flight direction** (`Vessel.Transform.forward`),
at an offset that scales with speed so it appears a fixed travel-time ahead:

```
offset = max(forwardOffset, Speed * leadSeconds)
origin = vessel.position + vessel.forward * offset
rotation = vessel.rotation
```

- `leadSeconds` (default 1) — the tube appears ~1 second of travel ahead, so a faster Squirrel gets
  more room to line up.
- `forwardOffset` (default 24) — a **minimum** floor so it never forms on top of the vessel when
  slow/stopped.

The axis is the flight direction, so flying straight carries the vessel through the hollow centre.
There is no preview and no orientation lean — earlier iterations added a puppet-lean that tilted the
axis and made the tube read as exiting the top; that was removed.

## How it composes with the fundamentals

- **Prisms / Mass** — the tube is real conserved mass laid through the pooled prism path. It **blooms
  in** (continuity law), registers with `PrismSpatialIndex`, and is removed only by an active force
  (skim/ability/fauna) — **no TTL, no decay, no idle culler**.
- **Domain** — the wall carries the caster's domain, so it feeds the containing cell's per-domain
  volume (phase / fauna targeting). Deliberate emergence, not a cosmetic overlay.
- **Elementals** — the wall is **danger** prisms: they slam any vessel body that touches them
  (friendly fire included — locked design, danger effects never gate on domain), and a skimmer that
  grazes them gets **10× boost energy** (`SkimmerBoostPrismEffect.dangerEnergyMultiplier`) — the 10×
  bonus is gated behind the vessel's **Charge level-5 upgrade ("Live Wire")**; below it danger skims
  pay base energy. **Time** shortens the deploy cooldown (`cooldownMultiplierAtFullTime`, ×0.5 at
  level 10) and at **Time level 5 ("Twin Rings")** each deploy lays an extra ring (baseline is now
  ONE ring; `upgradeExtraRings`). A level-0
  Space Skimmer reaches the ring from the centre; the vessel body flies clear of it. Tune `radius`
  so that holds for the live skimmer size. Because the prisms come from the **Boost pool** (below)
  their colliders are live the instant they spawn — a skimmer can boost off them immediately, even
  though a vessel flying the centre usually never touches them.

## BoostRingBuilder — one ring primitive for omnicrystal, joust, and the tube

Three features throw a fly-through **ring of 8 boost prisms** around the flight path, and all three
had the same inconsistency: the player's speed could outrun the prisms' colliders (0.6 s spawn window
+ collider scaling up from zero with the bloom), so the intended skim sometimes never registered.
They now share ONE primitive — **`BoostRingBuilder`** (`Controller/Environment/Spawning/`, next to
`PrismTrailBuilder`) — fix ring behaviour there once and all three follow:

| Case | Path |
|---|---|
| Squirrel omnicrystal hit | `SquirrelVesselExplosionByCrystalEffect` → `AOEShieldedRingSpawner` → `AOEBlockSpawner` + `SpawnableRings` (shielded) |
| Joust (overtake) | `VesselExplosionBySkimmerEffect` → `AOEDangerRingSpawner` → same `AOEBlockSpawner` + `SpawnableRings` (danger) |
| Tube ability | `SquirrelTubeActionExecutor` — one `LayRing` per ring along the axis |

The two guarantees that make the skim deterministic:

1. **Full-size collider from frame 0.** Every ring prism comes from the dedicated **Boost pool**
   (`PrismType.Boost` → `PrismFactory.SpawnBoostPrism` → `BoostPrismPool` in `PrismManagers.prefab`;
   `waitTime = 0`, fast bloom via `Prism.SetGrowthRate`). Under the clock-material law the
   transform is FINAL at stamp, so the authored BoxCollider is already a full-size world
   footprint while the GPU blooms the visual — no per-frame collider compensation.
2. **Speed-independent open geometry.** Prisms lie long-side ALONG the ring axis, "up" outward
   radially — the wide-open arrangement the old ring only reached when fast. The old
   `SpawnableRings` tilted each prism toward the ring centre by `speed * 0.3°` (speed arrived as the
   `intensity` argument from `AOEBlockSpawner`), so a slow vessel got a closed spoke-wheel it
   couldn't fly through. The tilt and the speed pass are gone.

Kind handling: **all kinds** (Danger, Shielded, SuperShielded) apply immediately in
`BoostRingBuilder.LayOne` — with transform-at-final from stamp, shield shells are full-size at
frame 0 and no longer need a deferred `onGrown` callback.

Why the Boost pool is separate: `SpawnBoostPrism`'s `waitTime`/`GrowthRate` overrides aren't reset by
the pool's `OnGet`/`OnRelease`, so on a *shared* pool they would leak into the next plain trail block.
(`AOEDangerHemisphereBlocks` — the Rhino skimmer formation, not the joust ring — also draws from the
Boost pool.)

Tube-specific flow: rings are laid a few per frame (`spawnPerFrame / segments` rings each frame) to
avoid a spike, and on teardown (turn end via `OnMiniGameTurnEnd`, or vessel despawn via `OnDisable`)
the tracked prisms are **returned to the pool** — `PrismKinds.Clear` first, then `Prism.ReturnToPool`
(self-unsubscribing, safe on an already-recycled prism). Scene reloads release the whole pool anyway.

## Cooldown HUD

`SquirrelVesselHUDController` polls `SquirrelTubeActionExecutor.CooldownRemaining01` each frame and
drives a radial fill in the freed HUD slot (empties on deploy, refills as it recharges). That slot
was freed by **merging the joust and crystal icons into one impact icon** — a single icon now flashes
on either a vessel hit (joust) or a crystal hit.

## Files

| Role | File |
|---|---|
| Ability config (SO) | `R_VesselActions/Data Containers/SquirrelTubeActionSO.cs` |
| Runtime executor | `R_VesselActions/Executors/SquirrelTubeActionExecutor.cs` |
| SO asset | `_SO_Assets/VesselActions/Squirrel/SquirrelTubeAction.asset` |
| HUD view / controller | `UI/View/SquirrelVesselHUDView.cs`, `R_VesselActions/Data Containers/SquirrelVesselHUDController.cs` |
| Drift analog toggle | `VesselTransformer.singleTriggerDrift`, `DriftActionSO.playDriftSfx` |
| Input mapping | `Squirrel.prefab` R_VesselActionHandler (`OnlyLeft/OnlyRight` + gamepad `LeftStick/RightStick` overrides) |
| Shared ring primitive | `BoostRingBuilder` (`Controller/Environment/Spawning/`) |
| Boost prism pool | `PrismFactory.SpawnBoostPrism` (`PrismType.Boost`), `BoostPrismPool` in `PrismManagers.prefab`, `Prism.SetGrowthRate` |
| Omnicrystal + joust rings | `SpawnableRings` + `AOEBlockSpawner` on `AOERingSpawner.prefab` (variants `AOEShieldedRingSpawner`, `AOEDangerRingSpawner`) |

## Tuning (all on the SO)

`danger`, `radius`, `segments`, `rings`, `ringSpacing`, `prismScale`, `leadSeconds`, `forwardOffset`
(min floor), `spawnPerFrame`, `cooldown`, `cooldownMultiplierAtFullTime` (Time → cooldown, ×0.5 at
level 10), `minCooldownMultiplier`, `upgradeExtraRings` (Time-5 Twin Rings). Grow-in speed and
collider timing are pool-level, on
`PrismFactory` (`boostPrismGrowthRate`, default 8 — `PrismScaleManager` clamps
`growthRate * deltaTime` into [0.05, 0.1] lerp/frame, so ≥6 pins the max bloom speed) +
`SpawnBoostPrism` (`waitTime = 0`); the collider never waits on either (full-size from frame 0 —
transform final at stamp under the clock law). **`radius` needs in-editor tuning against the live level-0 skimmer
size.**
