# Camera Migration Review

This document tracks the migration to a new camera system.

## Files Added

- `Assets/_Prefabs/Cameras/NewShipCamera.prefab` – new Cinemachine driven prefab.
- `Assets/_Scripts/Game/Camera/CustomCameraController.cs` – controller for runtime input and zoom.
- `Assets/_Scripts/Game/Ship/ShipCameraCustomizer.cs` – exposes per ship overrides.

## Files Removed

- `Assets/_Scripts/Game/Camera/LegacyCameraController.cs`

## New Classes

- **`CustomCameraController`** – manages camera movement and input.
- **`ShipCameraCustomizer`** – configures follow targets and offsets for each ship.
- **`CameraRigAnchor`** – helper for look‑at and follow transforms.
- **`CameraSettingsSO`** – scriptable object containing per‑ship camera values.
- **`ICameraController`** – interface implemented by camera controllers.
- **`ICameraConfigurator`** – interface for applying `CameraSettingsSO`.

## Testing the Prefab

1. Open `Assets/_Scenes/TestScenes/CameraTesting.unity`.
2. Place **NewShipCamera** into the scene.
3. Play the scene and swap ships to verify the camera follows correctly.
4. Use the mouse wheel or gamepad triggers to zoom in and out.
5. Respawn the player to ensure orientation resets.

For more information on the underlying system, see
[CameraSystemAnalysis](CameraSystemAnalysis.md) and
[CustomCameraSetupGuide](CustomCameraSetupGuide.md).

### 2024 Update

The camera architecture now relies on **CameraSettingsSO** assets. Ships use
`ShipCameraCustomizer` to apply these settings through the new
`ICameraConfigurator` interface. Runtime cameras implement `ICameraController`
to consume the settings directly.
