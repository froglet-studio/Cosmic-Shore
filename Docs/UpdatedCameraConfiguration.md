# Updated Camera Configuration

This document summarizes the final state of the camera migration.

## Prefab and Scripts

- `Assets/_Prefabs/CORE/CustomCameraSetup.prefab` provides the hierarchy of cameras used at runtime.
- `Assets/_Scripts/Game/Managers/CustomCameraController.cs` drives camera state transitions and zoom input.
- `Assets/_Scripts/Game/Ship/ShipCameraCustomizer.cs` allows each ship prefab to override follow targets and zoom distances.
- Configuration ScriptableObjects live in `Assets/_SO_Assets/CameraConfigs`.

## Typical Scene Setup

1. Open any gameplay or sandbox scene such as `Assets/_Scenes/TestScenes/Ig/IgSandbox.unity`.
2. Drag **CustomCameraSetup.prefab** into the hierarchy if not already present.
3. Ensure `CustomCameraController` is referenced by the `GameManager` or loaded via `StartFromAnyScene`.
4. Press Play and confirm the main camera switches between menu, gameplay, death and end game states when triggered.

Consult [CustomCameraSetupGuide](CustomCameraSetupGuide.md) for detailed integration steps.
