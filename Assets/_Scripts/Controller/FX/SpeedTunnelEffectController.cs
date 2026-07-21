using CosmicShore.Core;
using Reflex.Attributes;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Quasi dolly zoom sold entirely through optics: as the vessel's LIVE speed climbs
    /// through the effect window, the gameplay camera's field of view widens while the
    /// URP Panini projection distance rises with it. The Panini re-compresses the frame
    /// centre that the wider FOV pushed away, so the periphery stretches and streams while
    /// the focal point holds — tunnel vision, with no camera-distance change. Because the
    /// drive signal is the measured speed (not boost state), the effect automatically
    /// follows the ramp boost's constant acceleration up AND its fast return down.
    ///
    /// Local-human-pilot-only: remote and AI vessels never touch the local camera or the
    /// global post volume. Owns its visual parameters (ForcefieldCrackleController
    /// precedent); lives on the vessel prefab next to VesselStatus.
    /// </summary>
    public class SpeedTunnelEffectController : MonoBehaviour
    {
        [Inject] PostProcessingManager _postProcessing;

        [Header("Speed Window")]
        [Tooltip("Speed at which the tunnel effect starts to appear. Set just above the vessel's " +
                 "un-boosted full-throttle speed so normal flight shows no effect.")]
        [SerializeField] float minEffectSpeed = 70f;

        [Tooltip("Speed at which the effect reaches full strength — typically the ramp boost's " +
                 "top speed (throttle × max boost multiplier).")]
        [SerializeField] float maxEffectSpeed = 210f;

        [Header("Quasi Dolly Zoom")]
        [Tooltip("Degrees added to the camera's base field of view at full effect.")]
        [SerializeField] float fovBoost = 25f;

        [Tooltip("URP Panini projection distance at full effect (0-1).")]
        [SerializeField, Range(0f, 1f)] float maxPaniniDistance = 0.6f;

        [Header("Response")]
        [Tooltip("Per-second exponential tracking of the visual toward the speed-derived value. " +
                 "Speed is already continuous, so this only rounds off discontinuities " +
                 "(teleports, resets); keep it high so the visual matches the speed.")]
        [SerializeField] float responsiveness = 12f;

        VesselStatus _status;
        float _effect01;
        bool _applied;
        Camera _appliedCamera;

        void Awake()
        {
            TryGetComponent(out _status);
        }

        void LateUpdate()
        {
            float target01 = 0f;
            if (_status != null && _status.IsLocalUser && !_status.IsInitializedAsAI)
                target01 = Mathf.InverseLerp(minEffectSpeed, maxEffectSpeed, _status.Speed);

            _effect01 = Mathf.Lerp(_effect01, target01, 1f - Mathf.Exp(-responsiveness * Time.deltaTime));

            if (_effect01 > 0.001f)
                Apply(_effect01);
            else if (_applied)
                Release();
        }

        void OnDisable()
        {
            if (_applied) Release();
            _effect01 = 0f;
        }

        void Apply(float t)
        {
            var cam = ResolveGameplayCamera();

            // The active camera can change mid-effect (end cam, death cam) — restore the
            // one we actually pushed before touching the new one, so no camera is left
            // with a baked-in boosted FOV and no other camera gets our restore write.
            if (_appliedCamera != null && _appliedCamera != cam)
                RestoreFov(_appliedCamera);

            if (cam != null && !cam.orthographic)
            {
                cam.fieldOfView = BaseFov() + fovBoost * t;
                _appliedCamera = cam;
            }
            else
            {
                _appliedCamera = null;
            }

            PostProcessing?.SetSpeedTunnelPanini(maxPaniniDistance * t);
            _applied = true;
        }

        void Release()
        {
            _applied = false;

            if (_appliedCamera != null)
                RestoreFov(_appliedCamera);
            _appliedCamera = null;

            PostProcessing?.SetSpeedTunnelPanini(0f);
        }

        static void RestoreFov(Camera cam)
        {
            if (!cam.orthographic)
                cam.fieldOfView = BaseFov();
        }

        PostProcessingManager PostProcessing =>
            _postProcessing != null ? _postProcessing : PostProcessingManager.Instance;

        /// <summary>The live gameplay follow camera, or null outside gameplay camera mode
        /// (e.g. menu Cinemachine) — there the FOV half of the effect simply doesn't apply.</summary>
        static Camera ResolveGameplayCamera()
        {
            var manager = CameraManager.Instance;
            if (manager == null) return null;
            return (manager.GetActiveController() as CustomCameraController)?.Camera;
        }

        /// <summary>Read the player's configured FOV live each frame (the same source the
        /// settings panel applies), so a mid-boost settings change never bakes a stale base
        /// into the effect.</summary>
        static float BaseFov()
        {
            var settings = DisplayGraphicsSettings.Instance;
            return settings != null ? settings.Current.FieldOfView : 60f;
        }
    }
}
