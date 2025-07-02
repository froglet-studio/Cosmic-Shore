# Custom Camera Setup Integration

This guide describes how to replace the legacy `Cinemachine Vcams.prefab` with the new `CustomCameraSetup.prefab`.

1. In each scene that used **Cinemachine Vcams.prefab**, delete the old prefab instance.
2. Drag `Assets/_Prefabs/CORE/CustomCameraSetup.prefab` into the scene hierarchy.
3. Assign the four camera configuration assets located in `Assets/_SO_Assets/CameraConfigs` if not already populated.
4. Ensure `GameManager` references the camera controller by default. Existing scripts already reference `CustomCameraController.Instance`.
5. Connect any rotating targets or additional transforms as children of the prefab if needed.

The prefab contains a camera object with `CustomCameraController` attached. Configuration assets mirror values extracted from the original Cinemachine setup.
