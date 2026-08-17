# Camera Migration Review

This document tracks the camera system's migrations. Updated March 2026 to reflect current file paths after the `_Scripts/Game/` → `_Scripts/Controller/` reorganization; updated August 2026 for the Menu_Main move OFF Cinemachine.

## Architecture

Gameplay cameras are plain-`Camera` rigs driven by `CustomCameraController`, with per-vessel `CameraSettingsSO` ScriptableObject assets. Vessels apply their settings through `VesselCameraCustomizer` via the `ICameraConfigurator` interface. Runtime cameras implement `ICameraController` to consume settings directly. `CameraManager` (DI singleton) manages the overall camera lifecycle and provides utility methods like `SnapPlayerCameraToTarget()`.

**Menu_Main no longer uses Cinemachine.** `MainMenuCameraController` drives the scene's main camera transform directly through a set of `MenuCameraConfigSO` configurations (orbit / cinematic trail / tight chase / top-down pan / lava lamp). A config carries framing, smoothing, lens, and blend duration only — there is still no target field, so a menu camera cannot be authored to point at an arbitrary object. **What it frames follows from its `MenuCameraRigKind`**, resolved by the controller each frame: the first four frame the LOCAL VESSEL, and `LavaLamp` frames the CELL (see below). Transitions to/from the gameplay camera blend between two live, vessel-anchored endpoints (the menu rig pose and the exact pose `CustomCameraController.SnapToTarget` computes), so the blend rides the moving AI vessel instead of chasing it through world space. The `CinemachineBrain` was removed from Menu_Main's scene camera; the legacy `CM Main Menu` vCam in `CameraManager.prefab` is kept permanently inactive (`CameraManager.SetMainMenuCameraActive` now deactivates it) pending a future prefab cleanup.

## The Lava Lamp rig (`MenuCameraRigKind.LavaLamp`)

Restored August 2026 as `MenuCam_LavaLamp1.asset`, reconstructing the ambience shot Menu_Main used
before it moved to close vessel-following framings: a very slow orbit of the **cell centre**, aimed
at the **crystal**, with the vessel merely one of the things drifting through the frame. It is the
only rig kind that does not frame the vessel, and therefore the only one that needs no vessel — it
runs from scene load rather than waiting on the spawn chain.

The defaults are measured from the legacy Cinemachine rig, which still exists (inactive) as
`CM Main Menu` in `Bootstrap.unity` and `CameraManager.prefab`:

| Legacy Cinemachine | Value | `MenuCameraConfigSO` |
|---|---|---|
| `Main Menu Follow Target` position | `(0, 0, -350)` | `lavaLampStartDirection` (0,0,-1); `lavaLampOrbitRadius` **920**, see below |
| `RotateAroundOrigin` speed / direction | 2, `(0, 1, -1)` | `lavaLampOrbitAxis` (0,1,-1), `lavaLampDegreesPerSecond` 2.83 (= 2·√2) |
| `CinemachineFollow.FollowOffset` (WorldSpace) | `(0, 30, 0)` | `lavaLampHeightOffset` 30 |
| `CinemachineFollow.PositionDamping` | 1 | `positionSmoothTime` 0.3 |
| `CinemachineRotationComposer.Damping` | 10 | `rotationSharpness` 0.45 (≈ 4.605 / 10) |
| `CameraManager.LookAtCrystal` → `cellData.CrystalTransform` | — | `lavaLampAimAtCrystal` (resolved via `TryGetLocalCrystal`) |
| Lens FOV | 60 | `fieldOfView` 0 (= match the player's gameplay FOV) |

Three properties are worth knowing before retuning it:

- **Why it reads as calm.** A lap takes ~127 s and the aim damping is ~2 s, so essentially all
  on-screen motion belongs to the vessels, trails and crystals rather than the camera. The camera
  is well inside the membrane (radius 1200), so nothing was ever removed to make this shot work.

- **The radius is 686, not the legacy 350, because the NUCLEUS GREW.** Every other legacy value
  transferred directly; this one could not. The nucleus is `Node2.fbx` (mesh half-extent 0.9798)
  at `Nucleus.prefab` scale 400 — **world radius ≈ 392**, roughly double the ~200 it had in the
  lava-lamp era. The legacy 350 was a *curated* value, tuned so the nucleus filled the frame; 686
  is that same framing re-derived against the bigger nucleus. At FOV 60 the vertical half-extent
  framed at the cell centre is `R·tan30`:

  | R | half-extent at centre | nucleus as % of it | note |
  |---|---|---|---|
  | 350 (legacy) | 202 | **194%** | camera is INSIDE the nucleus sphere; it overflows the frame ~2× |
  | **686 (shipped)** | 396 | 99% | reproduces the legacy *framing* — nucleus edge-to-edge |
  | 920 | 531 | 74% | whole nucleus plus a wide cytoplasm margin |

  The hard ceiling is the **toys**: `ToyboxController` rings them at
  `MembraneRadius × membraneFraction` = `1200 × 0.82` = **984** on the y=0 galactic plane, with a
  42-unit trigger, so the nearest trigger surface reaches 942 toward the origin. Any `R < 942`
  keeps the orbit clear of every toy at every orbit angle (686 leaves 256 units of margin).
  **Re-derive that ceiling if the membrane radius, `membraneFraction`, or the trigger radius
  changes.**

- **Roll is set by `lavaLampPoleBlendStart`, not by the geometry.** With world-up as the hint,
  `LookRotation` produces exactly zero roll — the camera's right vector stays horizontal — so
  *every* degree of roll the lava lamp shows comes from `ComputeLookUpHint`'s blend sliding the
  hint toward the orbit axis. Crystals spawn anywhere in a ball of the nucleus radius
  (`CrystalManager.anchorlessSpawnRadius` 0 → 392), and the adversarial case is the camera at peak
  latitude aiming at a crystal at the far bottom of that ball, at the camera's own longitude.

  The field's default of **0.99 is chosen against the measured geometry, and yields zero roll**:

  | R (45° incl.) | true worst-case verticality | `cross(up, dir)` there | roll at threshold 0.99 |
  |---|---|---|---|
  | 686 | 0.9859 | 0.168 | **none — the blend never engages** |
  | 920 | 0.9449 | 0.327 | none |

  The threshold is **not** a numerical-safety limit, which is what made the original 0.85 a
  mistake here: `LookRotation` only degrades when `cross(up, dir)` approaches ~0.01, i.e. above
  ~0.9999. At 0.85 the blend fired on 43% of crystal spawns for a median 5.3° / worst 13.5° horizon
  tilt lasting ~8 s of each lap. Raising it to 0.99 removes all of that with an order of magnitude
  of numerical margin still in hand.

  Two follow-on rules. An orbit that genuinely **crosses the pole** (the legacy `(0,1,-1)` cone,
  where verticality reaches 1.0) must *lower* this value — there the roll is unavoidable and a
  narrow band would snap it; ~0.85 spreads it gently. And if the nucleus grows again, re-check the
  worst case: the crystal ball scales with it, so it is the one input that can push verticality
  back through the threshold. Inclination is the secondary lever
  (`lavaLampOrbitAxis = (tan i, 1, 0)`), and lowering it reduces the worst case further.

## Key Files

| File | Location | Purpose |
|---|---|---|
| `CustomCameraController.cs` | `Assets/_Scripts/Controller/Camera/` | Runtime gameplay camera controller (follow/zoom/shake) — the player cam |
| `MainMenuCameraController.cs` | `Assets/_Scripts/Controller/Camera/` | Menu_Main camera rig + freestyle transition blends (no Cinemachine) |
| `MenuCameraConfigSO.cs` | `Assets/_Scripts/Controller/Camera/` | Menu camera configuration asset (rig kind, framing, smoothing, blend duration); instances in `Assets/_SO_Assets/Camera/MenuCam_*.asset` |
| `VesselCameraCustomizer.cs` | `Assets/_Scripts/Controller/Vessel/` | Per-vessel camera setting application (formerly `ShipCameraCustomizer`) |
| `CameraSettingsSO.cs` | `Assets/_Scripts/Controller/Camera/` | ScriptableObject with per-vessel camera values (follow distance, FOV, damping, etc.) |
| `ICameraController.cs` | `Assets/_Scripts/Controller/Camera/` | Interface implemented by camera controllers |
| `ICameraConfigurator.cs` | `Assets/_Scripts/Controller/Camera/` | Interface for applying `CameraSettingsSO` |
| `CameraManager.cs` | `Assets/_Scripts/Controller/Managers/` | DI singleton — camera lifecycle, snap-to-target, end-camera follow |

## Files Removed (Migration Complete)

- `Assets/_Scripts/Game/Camera/LegacyCameraController.cs` — replaced by `CustomCameraController`
- `CameraRigAnchor.cs` — no longer exists; functionality absorbed into `CustomCameraController` and Cinemachine follow targets

## Per-Vessel Camera Assets

Each vessel class has its own `CameraSettingsSO` asset instance, allowing designers to tune follow distance, FOV, damping, and offsets independently per vessel.

## Multiplayer Camera Behavior

In multiplayer (including Menu_Main with party members), each client has its own independent camera following its own vessel. No camera state is synced across the network — each client controls their own camera independently. `MenuCrystalClickHandler` raises the freestyle transition SOAP events; `MainMenuCameraController` reacts by blending the scene camera between the active `MenuCameraConfigSO` framing and the gameplay camera pose, then hands off to / takes over from `CameraManager`'s player cam.

## Integration Notes

- The camera system integrates with the **Input Strategy Pattern** (`IInputStrategy`) for platform-agnostic zoom/orbit controls.
- `CameraSettingsSO` follows the project's ScriptableObject config separation pattern — tunable values live in the SO asset, not in MonoBehaviours.
- `CameraManager` is registered as a DI singleton via `AppManager.InstallBindings()` and is accessed via `[Inject]` throughout the codebase.
- Camera state can be observed by other systems via SOAP `ScriptableVariable` if needed.
