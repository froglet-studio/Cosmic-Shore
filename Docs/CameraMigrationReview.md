# Camera Migration Review

This document tracks the migration to the Cinemachine-based camera system.

## Architecture

The camera system uses **Cinemachine 3.1.2** with per-vessel `CameraSettingsSO` ScriptableObject assets. Ships apply their settings through `ShipCameraCustomizer` via the `ICameraConfigurator` interface. Runtime cameras implement `ICameraController` to consume settings directly.

## Files Added

- `Assets/_Prefabs/Cameras/NewShipCamera.prefab` – Cinemachine-driven prefab.
- `Assets/_Scripts/Game/Camera/CustomCameraController.cs` – controller for runtime input and zoom.
- `Assets/_Scripts/Game/Ship/ShipCameraCustomizer.cs` – exposes per-ship overrides.

## Files Removed

- `Assets/_Scripts/Game/Camera/LegacyCameraController.cs`

## Classes

| Class | Responsibility |
|---|---|
| `CustomCameraController` | Manages camera movement, input, and zoom at runtime |
| `ShipCameraCustomizer` | Configures follow targets and offsets per vessel class |
| `CameraRigAnchor` | Helper for look-at and follow transforms |
| `CameraSettingsSO` | ScriptableObject with per-vessel camera values (follow distance, FOV, damping, etc.) |
| `ICameraController` | Interface implemented by camera controllers |
| `ICameraConfigurator` | Interface for applying `CameraSettingsSO` |

## Per-Vessel Camera Assets

Each vessel class has its own `CameraSettingsSO` asset instance, allowing designers to tune follow distance, FOV, damping, and offsets independently per ship.

## Testing the Prefab

1. Open `Assets/_Scenes/TestScenes/CameraTesting.unity`.
2. Place **NewShipCamera** into the scene.
3. Play the scene and swap ships to verify the camera follows correctly.
4. Use the mouse wheel or gamepad triggers to zoom in and out.
5. Respawn the player to ensure orientation resets.

## Integration Notes

- The camera system integrates with the **Input Strategy Pattern** (`IInputStrategy`) for platform-agnostic zoom/orbit controls.
- `CameraSettingsSO` follows the project's ScriptableObject config separation pattern — tunable values live in the SO asset, not in MonoBehaviours.
- Camera state can be observed by other systems via SOAP `ScriptableVariable` if needed.
