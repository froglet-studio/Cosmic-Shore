# Squirrel "Oak Trunk" Tube Ability

A Squirrel vessel ability: **hold to preview, release to form** a long tube of thick **danger
prisms** along the vessel's flight axis — a giant trunk you rocket through the hollow centre while it
obstructs everyone else. Modeled on the Dolphin's deploy-crystal shape, but the committed object is
an *arrangement of prisms* instead of a single crystal.

## Controls (drift simplification)

The Squirrel's drift was collapsed from two triggers onto **one analog trigger**, freeing the other
trigger for this ability.

| Input | Gamepad | Touch |
|---|---|---|
| Drift (analog: no-drift → single → sharp) | Left trigger | keep-left-finger (`OnlyLeftStickAction`) |
| Tube (press = preview, release = form) | Right trigger | keep-right-finger (`OnlyRightStickAction`) |

`VesselTransformer.singleTriggerDrift` (on the Squirrel only) remaps the left trigger's 0-1 travel
across the full 0-2 drift range so a single trigger spans the light→sharp range the Manta still gets
from summing both triggers. See `DriftActionSO.playDriftSfx` (off on the sharp tier) so the shared
drift SFX isn't doubled when both tiers stack on one trigger.

## How it composes with the fundamentals

- **Prisms / Mass** — the tube is real conserved mass laid through the pooled prism path. It **blooms
  in** (continuity law), registers with `PrismSpatialIndex`, and is removed only by an active force
  (skim/ability/fauna) — **no TTL, no decay, no idle culler**.
- **Domain** — the wall carries the caster's domain, so it feeds the containing cell's per-domain
  volume (phase / fauna targeting). This is deliberate emergence, not a cosmetic overlay. Tune
  `radius`/`rings`/`prismScale` with awareness that a deploy is a real mass injection.
- **Elementals** — the wall is **danger** prisms: they slam any vessel body that touches them
  (friendly fire included — locked design, danger effects never gate on domain), and a skimmer that
  grazes them gets **10× boost energy** (`SkimmerBoostPrismEffect.dangerEnergyMultiplier`). A level-0
  Space Skimmer reaches the ring from the centre; the vessel body flies clear of it. Tune `radius`
  so that holds for the live skimmer size.

## Orientation — projects from the vessel MODEL, not the camera

The tube's axis and the preview both key off the **visual ship model** transform, resolved by
`SquirrelTubeActionExecutor.ResolveModelTransform`:

1. `orientationSource` — an **explicit serialized override** on the executor (wire this to the
   transform that actually banks/rotates for the vessel), else
2. `IVesselStatus.ShipGeometries[0]` (the customization-collected visual mesh), else
3. `IVesselStatus.OrientationHandle`, else
4. the vessel root (last resort).

The origin is the vessel centre; the axis is `model.rotation * forward`. **Do not** use
`Vessel.Transform` (the root) alone — the camera follows the root (`CameraFollowTarget`), so a
root-aligned tube reads as camera-locked.

> **Caveat — the Squirrel puppeteers via an `Animator` (bone deformation), not a rigid transform.**
> The Squirrel's `MantaAnimationContoller` feeds Pitch/Yaw/Roll blend params to a skinned mesh, so
> `ShipGeometries[0]` (a `SkinnedMeshRenderer`) may **not** rigidly rotate — the visible bank lives
> in the skeleton. So the tube also adds an **input-derived lean** on top of the resolved
> orientation so it banks/swings with flight and drift regardless.

### Input-derived lean (SO fields)

On top of the resolved model orientation, the tube leans with the same steering signals the vessel
animation uses (`pitch = InputStatus.YSum`, `yaw = XSum`, `roll = YDiff`):

- `puppetRollDegrees` (default 20) — banks the ring with roll input. Roll leaves the **axis**
  untouched, so fly-through is unaffected. Negate to flip the bank side.
- `puppetPitchYawDegrees` (default 6) — swings the axis with turns/drift. Kept small so the axis
  barely tilts, and it settles to aligned as the input returns to centre (straighten out to release,
  and the tube forms along your heading so you still thread the hollow centre).

Set both to 0 to project purely from the resolved orientation (no lean). This is the tunable knob
for "how much does the tube visibly respond to flight/drift."

## Pooling (best practice — no Instantiate/Destroy)

The wall's prisms are **pooled**, not instantiated:

- Spawned through the shared `PrismEventChannelWithReturnSO` (`EventOnSpawnPrismAndReturn.asset`) →
  `PrismFactory` → the per-vessel `InteractivePrismPoolManager` — the exact path the vessel trail
  (`VesselPrismController`) and AOE danger blocks (`AOEDangerHemisphereBlocks`) use.
- Each prism is configured like a trail block: `ChangeTeam(domain)`, `IsDangerous = true` **before**
  `Initialize` (so `Initialize`'s `MakeDangerous` repaints it to the team's dangerous material),
  then `Initialize(playerName)` to bloom it in.
- The spawn is **batched** a few per frame (`spawnPerFrame`) to avoid a single-frame spike.
- On teardown (turn end via `OnMiniGameTurnEnd`/reset, or vessel despawn via `OnDisable`) the tracked
  prisms are **returned to the pool** (`Prism.ReturnToPool`, which self-unsubscribes so an
  already-recycled prism is a safe no-op). Scene reloads release the whole pool anyway.

The **preview** is a single combined ghost mesh (one prism-sized box per ring position, one draw
call), cached per SO-geometry signature and rebuilt only when the geometry changes — it is not 352
live GameObjects.

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

`prismType` (pooled prism kind), `danger`, `radius`, `segments`, `rings`, `ringSpacing`, `prismScale`,
`forwardOffset`, `previewMaterial`/`previewColor`/`previewFadeInSeconds`, `spawnPerFrame`, `cooldown`.
Defaults: radius 8, 8 segments × 44 rings, scale 6, forwardOffset 24, cooldown 20s. **`radius` needs
in-editor tuning against the live level-0 skimmer size.**
