# Camera Migration Review

This document tracks the camera system's migrations. Updated March 2026 to reflect current file paths after the `_Scripts/Game/` → `_Scripts/Controller/` reorganization; updated August 2026 for the Menu_Main move OFF Cinemachine.

## Architecture

Gameplay cameras are plain-`Camera` rigs driven by `CustomCameraController`, with per-vessel `CameraSettingsSO` ScriptableObject assets. Vessels apply their settings through `VesselCameraCustomizer` via the `ICameraConfigurator` interface. Runtime cameras implement `ICameraController` to consume settings directly. `CameraManager` (DI singleton) manages the overall camera lifecycle and provides utility methods like `SnapPlayerCameraToTarget()`.

**Menu_Main no longer uses Cinemachine.** `MainMenuCameraController` drives the scene's main camera transform directly through a set of `MenuCameraConfigSO` configurations (orbit / cinematic trail / tight chase / top-down pan). Every configuration frames the LOCAL VESSEL — a config carries framing, smoothing, lens, and blend duration only; there is no target field, so a menu camera cannot be authored to point at anything else. Transitions to/from the gameplay camera blend between two live, vessel-anchored endpoints (the menu rig pose and the exact pose `CustomCameraController.SnapToTarget` computes), so the blend rides the moving AI vessel instead of chasing it through world space. The `CinemachineBrain` was removed from Menu_Main's scene camera; the legacy `CM Main Menu` vCam in `CameraManager.prefab` is kept permanently inactive (`CameraManager.SetMainMenuCameraActive` now deactivates it) pending a future prefab cleanup.

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
