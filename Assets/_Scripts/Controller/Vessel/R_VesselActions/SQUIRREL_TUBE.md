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
  grazes them gets **10× boost energy** (`SkimmerBoostPrismEffect.dangerEnergyMultiplier`). A level-0
  Space Skimmer reaches the ring from the centre; the vessel body flies clear of it. Tune `radius`
  so that holds for the live skimmer size. Because the prisms come from the **Boost pool** (below)
  their colliders are live the instant they spawn — a skimmer can boost off them immediately, even
  though a vessel flying the centre usually never touches them.

## Pooling — the dedicated "Boost" prism pool (best practice, no Instantiate/Destroy)

The wall's prisms are **pooled**, not instantiated, and they come from a pool built for exactly this
job: **fast-growing prisms whose collider turns on immediately** — something to boost off that
*usually doesn't collide at all*.

- **`PrismType.Boost`** routes through the shared `PrismEventChannelWithReturnSO`
  (`EventOnSpawnPrismAndReturn.asset`) → `PrismFactory.SpawnBoostPrism` → a **dedicated**
  `InteractivePrismPoolManager` (`BoostPrismPool`, a child of `PrismManagers.prefab`).
- `SpawnBoostPrism` sets, on every `Get`:
  - `prism.waitTime = 0` — the collider comes on the frame after `Initialize` instead of after the
    normal **0.6 s spawn window**, so a skimmer can boost off it right away.
  - `prism.SetGrowthRate(boostPrismGrowthRate)` — a fast bloom-in (the shared prism prefab caches its
    slow `0.01` GrowthRate onto the scale animator at `Awake`, so `SetGrowthRate` pushes the fast
    value through to the animator too).
- **Why a separate pool:** the pool's `OnGet`/`OnRelease` don't reset `waitTime`/`GrowthRate`, so
  applying those overrides to a *shared* pool would leak an immediate-collider / fast-grow prism into
  the next plain trail block that pool hands out. A dedicated pool means every prism in it is meant to
  be fast + collider-immediate, so nothing leaks.
- **Joust danger blocks use the same pool.** `AOEDangerHemisphereBlocks` (the danger-block formation
  a skimmer overtake throws up) also spawns `PrismType.Boost` — same purpose, a boost-off surface
  with a live collider — so the two share one pool and one behaviour.
- Each tube prism is still configured like a trail block: `ChangeTeam(domain)`, `IsDangerous = true`
  **before** `Initialize` (so `Initialize`'s `MakeDangerous` repaints it to the team's dangerous
  material), then `Initialize(playerName)` to bloom it in.
- The spawn is **batched** a few per frame (`spawnPerFrame`) to avoid a single-frame spike.
- On teardown (turn end via `OnMiniGameTurnEnd`, or vessel despawn via `OnDisable`) the tracked prisms
  are **returned to the pool** — `PrismKinds.Clear` first, then `Prism.ReturnToPool` (which
  self-unsubscribes so an already-recycled prism is a safe no-op). Scene reloads release the whole
  pool anyway.

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
| Boost prism pool | `PrismFactory.SpawnBoostPrism` (`PrismType.Boost`), `BoostPrismPool` in `PrismManagers.prefab`, `Prism.SetGrowthRate` |
| Shared with joust | `AOEDangerHemisphereBlocks` (danger-block formation also spawns `PrismType.Boost`) |

## Tuning (all on the SO)

`prismType` (**Boost** — the fast/immediate-collider pool), `danger`, `radius`, `segments`, `rings`,
`ringSpacing`, `prismScale`, `leadSeconds`, `forwardOffset` (min floor), `spawnPerFrame`, `cooldown`.
Grow-in speed and collider timing are pool-level, on `PrismFactory` (`boostPrismGrowthRate`, default
0.2) + `SpawnBoostPrism` (`waitTime = 0`). **`radius` needs in-editor tuning against the live level-0
skimmer size.**
