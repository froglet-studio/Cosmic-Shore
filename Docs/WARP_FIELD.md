# WARP FIELD — a scalar field that rescales every length a player observes

> A cell can carry a **warp field**: a scalar `s(p)` over space. While it is live, every local
> length a player observes — their vessel's size and speed, their camera's distance and near
> plane, the prisms they lay and the spacing between them — is multiplied by `s` at that vessel's
> position. Flying toward the centre of a radial field, a player shrinks with almost no near-field
> tell (their ship, camera and trail all shrink together), while whatever sits at the centre
> appears to GROW. Other players watch that vessel shrink on its way in. Status: **restored
> 2026-10-08, not yet run in the editor.**

## 0. History — what this restores

The **end-of-2022 "space time warping"** experiment: `Game_TestModeFour.unity`, commit `cdcd58af`
(2022-11-19, "mostly the introduction of scene 4 space time warping") and `8ac98ce0` (2022-12-09,
"tardis tweak"). A `WarpFieldController` read the field at the player every frame and wrote
`player.localScale = s` (and both AI ships'), `CameraManager.SetCameraDistance(s)`,
`throttleScaler = initialThrottleScaler × s` / `defaultThrottle = initialDThrottle × s`, and the
trail (`TrailSpawner` / `Trail` with `warp = true`) laid blocks of size `× s` at a wavelength of
`initialWavelength × s`. The field was `TardisWarp` (per-axis `atan` of distance from the origin).
The SO scaffolding (`WarpFieldSO`, `TardisWarp`, `ZeroWarp`, `WarpFieldData`, `WarpFieldView`)
survived every refactor since; the controller and all its consumers did not.

## 1. Where everything is

| What | Where |
|---|---|
| The field contract (`ScaleAt(offset)`, 1 = no warp; `EaseSeconds`) | `_Scripts/Controller/Environment/WarpField/WarpFieldSO.cs` |
| The radial field — `s = clamp((r/R)^k, min, max)` | `RadialWarp.cs` (same folder) |
| The ONE live field, its centre, its eased weight | `WarpFieldRuntime.cs` |
| Every vessel's root size, on every peer | `WarpFieldVesselScaler.cs` |
| Turning it on | `CellConfigDataSO.WarpField` — the scene's `Cell` activates it in `SpawnVisuals` (never a satellite), releases it in `RetireWorldIntoSuctionRoot` and `OnDisable` |
| Speed + additive velocity (knockback, a hole's pull) | `VesselTransformer.MoveShipScalar` / `MoveShipVector` / `MoveRestricted`, `SingleStickVesselTransformer.MoveShip` |
| Camera distance + near clip | `CustomCameraController.UpdateCamera` (point of use, beside `FollowHeightScale`) |
| Prism size, lane gap, offset, spacing, speed gate, skimmer clearance | `VesselPrismController.SpawnLoopAsync` / `CreateBlock` |
| FOV speed tunnel reads the FELT speed (`Speed / s`) | `Utility/VesselSpeedTunnel.Tick` |
| Gates | `WarpFieldTests` (the smooth law, no-field = exactly 1, ownership); `CrystalWormholeTests` (poles as a product, amplitude 0 = flat, frozen poles) |
| First user | The Crystal Wormhole cell (`Docs/CRYSTAL_WORMHOLE.md`): `Crystal Wormhole Warp Field.asset` |

## 2. The rules that make it hold

- **A pure function of position; nothing replicates.** `s` is read from the authored field at a
  position every peer already has. Vessel scale is not network-synced on any shipped hull (the
  Butterfly syncs scale server-authoritatively, and it receives the same value it would compute),
  so `WarpFieldVesselScaler` sizes every vessel locally. Trail prisms are laid on every peer from
  the replica's position, so each peer stamps the same `s`. `Speed` arrives at replicas already
  warped by the owner — nothing applies `s` to it twice.
- **No field = exactly 1, everywhere.** `WarpFieldRuntime.ScaleAt` returns `1f` with no field and
  every consumer multiplies by it, so a field-free session is byte-for-byte unchanged in what it
  moves and lays. The vessel scaler only exists while a field does, and restores every vessel's
  authored root scale before it goes.
- **Continuity.** A field eases in and out over `EaseSeconds` (default 1.5 s), geometrically
  (`s^w`), so no vessel, camera or trail ever pops. A scene change ends a field outright — the load
  screen is the transition.
- **The pilot's frame is unchanged.** Speed and the velocity channel scale by `s`, the internal
  `speed` (which the turn scalers read) does not — angles are scale-free — and the camera and
  trail scale with the hull. So in the pilot's own lengths: same speed, same turning, same framing,
  same trail. The FOV tunnel divides `s` back out so a shrunken cruise does not read as a crawl.
- **Laid prisms are STATED sizes.** A warped prism is usually far below the trail prism's 0.5
  scale floor, so `CreateBlock` admits it (`Prism.AdmitTargetScale`) exactly as a widened one is.
- **The spawn gate and wavelength are measured in warped lengths** (`Speed > 3·s`,
  `wavelength·s / Speed`), so the lay RATE is unchanged and a shrunken vessel never stops laying.

## 3. Not scaled (stated limits)

Crystal and lifeform sizes, explosion and impact effects, ability ranges authored in world units,
AI planning radii, skim/near-miss distances measured in world units, the far clip plane (world
units are fine there), and anything a vessel does that is not one of the consumers above. Each
is a candidate consumer — add it by multiplying its authored length by
`WarpFieldRuntime.ScaleAt(position)`, nothing more. The 2022 version scaled only the player, two
AI ships, the camera and the trail.

## 4. The radial field, its poles, and the crystal wormhole

`RadialWarp` (smooth since 2026-10-08): `s = √(min² + core²)`, `core = max·y / (1 + y⁴)^¼`,
`y = (r / referenceRadius)^exponent / max` — proportional to distance near the centre, saturating
toward its maximum far out, softly floored, **no crease anywhere** (a hard clamp is an interface).

**Poles.** An environment can register poles (`WarpFieldRuntime.AddPole(transform, amplitude)`). With
any registered, the field is read around them instead of its centre, composed as a PRODUCT, each
raised to its live amplitude: `s = Π s_i^{a_i}` — smooth everywhere, and a pole at amplitude 0 is flat
space. A pole keeps its last position and amplitude once its transform is gone, so it eases out with
the field (no pop); a new field starts with none. The crystal wormhole (`Docs/CRYSTAL_WORMHOLE.md`)
registers its attractor and repulsor, whose amplitudes beat against each other as the pair forms and
annihilates; its cell ships `referenceRadius 350`, floor 0.01, both reaches clear of the toys.

Because the vessel's world speed is proportional to `r` near a pole, a pilot holding a constant felt
speed approaches EXPONENTIALLY: what is ahead swells at a steady rate for as long as they fly at it.
The poles' pull and push on vessels is a felt law in each hull's own cruise speed and frame
(`Docs/CRYSTAL_WORMHOLE.md` §3).

## 5. Verify in the editor (owed)

Menu_Main → freestyle → Cell Selector → **Crystal Wormhole**. Fly at the centre: your hull, camera and
trail should stay the same on screen while the hole grows to fill the view; nothing should pop
when the world comes in or when you select another world (1.5 s ease). In a session with a second
player or AI, watch them shrink as they fly in. Check that a 1%-scale trail is still laid (tiny
prisms right behind you) and that the near plane does not clip your own hull. FrogletTools ▸
Toolbox ▸ Logging ▸ `Ecology` shows `[WarpField]` on/off lines.
