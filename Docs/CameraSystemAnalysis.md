# Camera System Analysis

This document summarizes the existing Cinemachine based camera setup.

## 1. Virtual Camera Settings

### Main Menu Camera
- **Field of View:** 60
- **Clip Planes:** near 0.1, far 8000
- **Follow Offset:** {x:0, y:0, z:0}
- **Lens Mode:** Perspective (ModeOverride 0)
- **Dutch:** 0.02

### Player Camera
- **Field of View:** 120
- **Clip Planes:** near 0.5, far 8000
- **Follow Offset:** {x:0, y:0, z:-8}
- **Lens Mode:** Orthographic (ModeOverride 1)
- **Orthographic Size:** 1300

### End Camera
- **Field of View:** 40
- **Clip Planes:** near 0.1, far 5000
- **Lens Mode:** Perspective

### Death Camera
- **Field of View:** 40
- **Clip Planes:** near 0.1, far 5000
- **Lens Mode:** Perspective

Values extracted from `Assets/_Prefabs/CORE/Cinemachine Vcams.prefab`.

## 2. Ship Camera Overrides

### Serpent Prefab
- **ControlOverrides:** `03000000` → `CloseCam`
- **closeCamDistance:** -90
- **FollowTargetPosition:** {x:0, y:0, z:0}

### Sparrow Prefab
- **ControlOverrides:** `08000000` → `SetFixedFollowOffset`
- **closeCamDistance:** 3
- **FollowTarget:** prefab object `{fileID: 3967031065131343057}`
- **FollowTargetPosition:** {x:0, y:5, z:-25}

## 3. CameraManager Public API
```
Transform GetCloseCamera()
void OnMainMenu()
void SetupGamePlayCameras()
void SetupGamePlayCameras(Transform)
void SetMainMenuCameraActive()
void SetCloseCameraActive()
void SetDeathCameraActive()
void SetEndCameraActive()
void SetFixedFollowOffset(Vector3)
void SetNormalizedCloseCameraDistance(float)
void SetOffsetPosition(Vector3)
void ZoomCloseCameraOut(float)
void ResetCloseCameraToNeutral(float)
```

## 4. Reflection Workarounds
`CameraManager` uses reflection to set the private field `m_FollowOffset` on `CinemachineFollow` so the scene does not become dirty. Reflection occurs in `ApplyRuntimeOffset` and `RestoreOriginalOffset`.

## 5. Settings Mapping
| Cinemachine Setting | Custom System Equivalent |
|---------------------|-------------------------|
| Lens.FieldOfView    | CameraConfig.fieldOfView |
| Lens.NearClipPlane  | CameraConfig.nearClip    |
| Lens.FarClipPlane   | CameraConfig.farClip     |
| Lens.OrthographicSize | CameraConfig.orthoSize |
| Lens.ModeOverride (1) | CameraConfig.orthographic=true |
| FollowOffset        | CameraConfig.followOffset |

