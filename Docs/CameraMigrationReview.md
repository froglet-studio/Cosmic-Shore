# Camera Migration Review

This document tracks the migration to the Cinemachine-based camera system. Updated March 2026 to reflect current file paths after the `_Scripts/Game/` → `_Scripts/Controller/` reorganization.

## Architecture

The camera system uses **Cinemachine 3.1.2** with per-vessel `CameraSettingsSO` ScriptableObject assets. Vessels apply their settings through `VesselCameraCustomizer` via the `ICameraConfigurator` interface. Runtime cameras implement `ICameraController` to consume settings directly. `CameraManager` (DI singleton) manages the overall camera lifecycle and provides utility methods like `SnapPlayerCameraToTarget()` and `SetupEndCameraFollow()`.

## Key Files

| File | Location | Purpose |
|---|---|---|
| `CustomCameraController.cs` | `Assets/_Scripts/Controller/Camera/` | Runtime camera controller: input, zoom, Cinemachine integration |
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

In multiplayer (including Menu_Main with party members), each client has its own independent Cinemachine camera following its own vessel. No camera state is synced across the network — each client controls their own camera independently. `MenuCrystalClickHandler` retargets the Cinemachine vCam between the crystal target (menu mode) and the vessel follow target (freestyle mode) using Cinemachine priorities.

## Integration Notes

- The camera system integrates with the **Input Strategy Pattern** (`IInputStrategy`) for platform-agnostic zoom/orbit controls.
- `CameraSettingsSO` follows the project's ScriptableObject config separation pattern — tunable values live in the SO asset, not in MonoBehaviours.
- `CameraManager` is registered as a DI singleton via `AppManager.InstallBindings()` and is accessed via `[Inject]` throughout the codebase.
- Camera state can be observed by other systems via SOAP `ScriptableVariable` if needed.
