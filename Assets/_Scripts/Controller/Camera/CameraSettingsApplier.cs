using CosmicShore.Core;
using CosmicShore.Data;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Drop-on consumer that applies the player's Field-of-View and post-process anti-aliasing
    /// (FXAA/SMAA/TAA) choice to the Camera it sits on, and keeps them live as the settings change.
    ///
    /// Add it to the gameplay player camera (the plain <see cref="Camera"/> driven by
    /// <c>CustomCameraController</c>) - FOV sticks there. On a Cinemachine brain camera the FOV is
    /// re-driven by the active vCam each frame (so FOV is a no-op there), but the AA still applies, so
    /// it's safe to add to the menu camera too if you want SMAA/TAA in the menu. MSAA is global (set
    /// on the URP asset by <see cref="DisplayGraphicsSettings"/>), so this only handles the per-camera
    /// post-AA modes.
    ///
    /// Applying on <c>OnEnable</c> also covers the "new camera spawned" case automatically.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class CameraSettingsApplier : MonoBehaviour
    {
        [SerializeField, Tooltip("Apply the FOV setting to this camera. Turn off for cameras whose " +
                                 "FOV is authored/animated (e.g. cutscene or Cinemachine cameras).")]
        bool applyFieldOfView = true;

        Camera _cam;

        void Awake() => _cam = GetComponent<Camera>();

        void OnEnable()
        {
            DisplayGraphicsSettings.OnFieldOfViewChanged += HandleFovChanged;
            DisplayGraphicsSettings.OnAnySettingChanged += HandleSettingsChanged;
            ApplyCurrent();
        }

        void OnDisable()
        {
            DisplayGraphicsSettings.OnFieldOfViewChanged -= HandleFovChanged;
            DisplayGraphicsSettings.OnAnySettingChanged -= HandleSettingsChanged;
        }

        void ApplyCurrent()
        {
            var s = DisplayGraphicsSettings.Instance;
            if (s != null) Apply(s.Current);
        }

        void HandleSettingsChanged(GraphicsSettingsData d) => Apply(d);
        void HandleFovChanged(float fov) => ApplyFov(fov);

        void Apply(GraphicsSettingsData d)
        {
            ApplyFov(d.FieldOfView);
            GraphicsSettingsApplier.ApplyCameraAntiAliasing(_cam, d.AntiAliasing);
        }

        void ApplyFov(float fov)
        {
            if (applyFieldOfView && _cam != null && !_cam.orthographic)
                _cam.fieldOfView = fov;
        }
    }
}
