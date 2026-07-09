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
  so that holds for the live skimmer size.

## Pooling (best practice — no Instantiate/Destroy)

The wall's prisms are **pooled**, not instantiated:

- Spawned through the shared `PrismEventChannelWithReturnSO` (`EventOnSpawnPrismAndReturn.asset`) →
  `PrismFactory` → the per-vessel `InteractivePrismPoolManager` — the exact path the vessel trail
  (`VesselPrismController`) and AOE danger blocks (`AOEDangerHemisphereBlocks`) use.
- Each prism is configured like a trail block: `ChangeTeam(domain)`, `IsDangerous = true` **before**
  `Initialize` (so `Initialize`'s `MakeDangerous` repaints it to the team's dangerous material),
  then `Initialize(playerName)` to bloom it in.
- The spawn is **batched** a few per frame (`spawnPerFrame`) to avoid a single-frame spike.
- On teardown (turn end via `OnMiniGameTurnEnd`, or vessel despawn via `OnDisable`) the tracked prisms
  are **returned to the pool** — `PrismKinds.Clear` first (so a recycled prism can't carry its danger
  flag into a later plain trail block), then `Prism.ReturnToPool` (which self-unsubscribes so an
  already-recycled prism is a safe no-op). Scene reloads release the whole pool anyway.

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

## Tuning (all on the SO)

`prismType`, `danger`, `radius`, `segments`, `rings`, `ringSpacing`, `prismScale`, `leadSeconds`,
`forwardOffset` (min floor), `spawnPerFrame`, `cooldown`. Defaults: radius 8, 8 segments × 44 rings,
scale 6, leadSeconds 1, forwardOffset 24, cooldown 20s. **`radius` needs in-editor tuning against the
live level-0 skimmer size.**
