# Camera Migration Review

This document tracks the migration to a new camera system.

## Files Added

- `Assets/_Prefabs/CORE/CustomCameraSetup.prefab` – new camera setup prefab.
- `Assets/_Scripts/Game/Managers/CustomCameraController.cs` – controller for runtime input and zoom.
- `Assets/_Scripts/Game/Ship/ShipCameraCustomizer.cs` – exposes per ship overrides.

## Files Removed

- `Assets/_Scripts/Game/Camera/LegacyCameraController.cs`

## New Classes

- **`CustomCameraController`** – manages camera movement and input.
- **`ShipCameraCustomizer`** – configures follow targets and offsets for each ship.

## Testing the Prefab

1. Open `Assets/_Scenes/TestScenes/Ig/IgSandbox.unity`.
2. Drag **CustomCameraSetup.prefab** into the scene.
3. Play the scene and swap ships to verify the camera follows correctly.
4. Use the mouse wheel or gamepad triggers to zoom in and out.
5. Respawn the player to ensure orientation resets.

For more information on the underlying system, see
[CameraSystemAnalysis](CameraSystemAnalysis.md) and
[CustomCameraSetupGuide](CustomCameraSetupGuide.md).
