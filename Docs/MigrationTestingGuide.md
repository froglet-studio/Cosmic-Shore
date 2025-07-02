# Migration Validation & Testing

This document lists procedures for verifying the custom camera system matches the original Cinemachine behaviour.

## Automated PlayMode Tests

1. **Camera State Transitions**
   - Load a test scene and call each of the public API methods on `CustomCameraController`.
   - Assert that the controller's internal state matches the requested state and that the camera transform values update accordingly.
2. **Ship Camera Settings**
   - Instantiate Serpent and Sparrow prefabs.
   - Confirm that `ApplyShipCameraSettings` sets follow offsets and zoom ranges.
3. **Zoom Action**
   - Simulate `CustomZoomAction` and verify that zoom distance approaches expected values within time limits.
4. **Performance Measurement**
   - Measure frame time before and after migration to ensure no regression.

These tests should be added to Unity's PlayMode test suite.

## Manual Checklist

- Compare camera behaviour in the main menu, gameplay, death, and end game with pre-migration videos.
- Verify Serpent and Sparrow prefabs still override camera distances correctly.
- Rapidly toggle camera states and ensure no jitter or null reference errors.
- Start a recording session and ensure the recording camera follows the player ship.

## Rollback Procedure

1. Revert the `CustomCameraController` related commits.
2. Restore `Cinemachine Vcams.prefab` in each scene.
3. Reattach `CameraManager` references on all prefabs and scripts.
