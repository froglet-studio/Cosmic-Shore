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
| `Main Menu Follow Target` position | `(0, 0, -350)` | `lavaLampOrbitRadius` 350, `lavaLampStartDirection` (0,0,-1) |
| `RotateAroundOrigin` speed / direction | 2, `(0, 1, -1)` | `lavaLampOrbitAxis` (0,1,-1), `lavaLampDegreesPerSecond` 2.83 (= 2·√2) |
| `CinemachineFollow.FollowOffset` (WorldSpace) | `(0, 30, 0)` | `lavaLampHeightOffset` 30 |
| `CinemachineFollow.PositionDamping` | 1 | `positionSmoothTime` 0.3 |
| `CinemachineRotationComposer.Damping` | 10 | `rotationSharpness` 0.45 (≈ 4.605 / 10) |
| `CameraManager.LookAtCrystal` → `cellData.CrystalTransform` | — | `lavaLampAimAtCrystal` (resolved via `TryGetLocalCrystal`) |
| Lens FOV | 60 | `fieldOfView` 0 (= match the player's gameplay FOV) |

Two properties are worth knowing before retuning it:

- **Why it reads as calm.** A lap takes ~127 s and the aim damping is ~2 s, so essentially all
  on-screen motion belongs to the vessels, trails and crystals rather than the camera. At FOV 60,
  radius 350 frames a vertical half-extent of 202 units at the cell centre — the nucleus (radius
  200) sits almost exactly edge-to-edge, which is what put "the whole cell" on display. The camera
  is well inside the membrane (radius 1200), so nothing was ever removed to make this shot work.
- **The orbit crosses the pole.** With the axis tilted 45°, the camera passes directly over the
  cell centre once per lap (verified: `|dot(viewDir, up)|` reaches 1.0, and camera *y* sweeps
  30 → 380). A from-scratch `LookRotation` would roll-flip there, so
  `MenuCameraConfigSO.ComputeLookUpHint` eases the up-hint toward the orbit axis across a wide band
  (27% of the lap), which the view direction holds a constant angle to and therefore never
  parallels. Setting `lavaLampOrbitAxis` to `(0, 1, 0)` gives a flat equatorial orbit that avoids
  the pole — and its roll — entirely.

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
