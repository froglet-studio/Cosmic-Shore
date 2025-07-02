# Ship Camera Migration Guide

This guide describes how to convert existing ship prefabs to the new `ShipCameraCustomizer` system.

## Serpent Prefab
- Previous `ControlOverrides` value `03000000` mapped to **CloseCam**
- Set `cameraSettings.closeCamDistance` to `-90`
- Enable `cameraSettings.fixedFollow` and leave `followTarget` empty

## Sparrow Prefab
- Previous `ControlOverrides` value `08000000` mapped to **SetFixedFollowOffset**
- Set `cameraSettings.closeCamDistance` to `3`
- Assign the same object reference used by `FollowTarget` to `cameraSettings.followTarget`
- Set `cameraSettings.followOffset` to `{x:0, y:5, z:-25}`

Add the `ShipCameraCustomizer` component to each prefab if not already present and configure the fields above. No enum overrides are required anymore.
