using System.Collections;
using CosmicShore.Utility;
using UnityEngine;
using Camera = UnityEngine.Camera;

namespace CosmicShore.Gameplay
{
    [RequireComponent(typeof(Camera))]
    public class CustomCameraController : MonoBehaviour, ICameraController
    {
        private Transform _followTarget;
        private Vector3 _followOffset = new(0f, 10f, 0f); 

        // --- Smoothing and Update Control ---
        private float _followSmoothTime = 0.2f;
        private float _rotationSmoothTime = 5f;
        private bool _disableRotationLerp = false;
        private const bool UseFixedUpdate = false;

        private Vector3 _velocity;
        private Vector3 _lastTargetPos;
        private float _lateralDominance; // low-pass-filtered 0..1: how lateral the ship's motion is
        private CameraSettingsSO _currentSettings;
        private Coroutine _distanceLerpRoutine;
        public bool adaptiveZoomEnabled;
        private float _neutralOffsetZ;

        /// <summary>
        /// Look behind: pose the camera at the MIRROR of its follow offset — the same distance
        /// ahead of the vessel that it normally sits behind — while still looking at the ship,
        /// so the pilot sees their own nose against whatever is chasing them. Held, not toggled.
        /// Driven only by <c>VesselRearView</c> (Docs/REAR_VIEW.md); nothing else may write it,
        /// and it is only ever raised on a vessel whose settings opt in
        /// (<see cref="RearViewSupported"/>).
        ///
        /// <para>It is a FLAG rather than a written offset on purpose. The mirror is applied at
        /// the point of use (<see cref="EffectiveOffset"/>) and <see cref="_followOffset"/> is
        /// left alone, so everything that legitimately moves this camera keeps writing that
        /// field and keeps working while the rear view is up — the zoom-out abilities, adaptive
        /// zoom, the skimmer's camera-scaling prism effect, and a vessel swap re-applying its
        /// own settings. Writing a mirrored offset instead would mean the first of those to
        /// fire silently put the camera back behind the ship.</para>
        /// </summary>
        public bool RearView { get; set; }

        /// <summary>
        /// Whether the vessel currently configured on this rig HAS a rear view
        /// (<c>CameraSettingsSO.enableRearView</c>). Opt-in per vessel — only Manta and Scarab
        /// today — because the look-back is a hull's own affordance rather than a platform law:
        /// it reads completely differently at the Urchin's 6.67-unit follow distance and at the
        /// Serpent's 250, and a vessel whose silhouette fills the frame from in front has
        /// nothing to show the pilot.
        ///
        /// <para>It lives on the per-vessel <c>CameraSettingsSO</c> rather than on this
        /// component because this component is ONE rig shared by every vessel — the camera is a
        /// child of <c>CameraManager</c>, not of the hull — so a field here would be a property
        /// of the camera, not of the ship, and would survive a vessel swap onto a hull that
        /// never asked for it. Coming through <c>ApplySettings</c> means it is re-answered by
        /// whichever vessel is configured, on every swap, with nothing to keep in step.</para>
        /// </summary>
        public bool RearViewSupported { get; private set; }

        /// <summary>
        /// The offset actually used to pose the camera this frame: the authored one, or its
        /// z-mirror while <see cref="RearView"/> is set. Mirroring z alone — not x, not y — is
        /// what puts the camera directly ahead at the same distance and the same height, rather
        /// than at some reflected vantage the vessel's settings never described.
        /// </summary>
        private Vector3 EffectiveOffset =>
            RearView
                ? new Vector3(_followOffset.x, _followOffset.y, -_followOffset.z)
                : _followOffset;

        // --- Camera Shake ---
        private float _shakeTimeRemaining;
        private float _shakeDuration;
        private float _shakeIntensity;

        private void Awake()
        {
            Camera = GetComponent<Camera>();
            Camera.useOcclusionCulling = false;
        }

        private void LateUpdate()
        {
            if (!UseFixedUpdate)
                UpdateCamera();
        }
        
        private void UpdateCamera()
        {
            if (!_followTarget) return;

            if (_lastTargetPos == Vector3.zero)
                _lastTargetPos = _followTarget.position;

            Vector3 desiredPos = _followTarget.position + _followTarget.rotation * EffectiveOffset;
            Vector3 shipDelta = _followTarget.position - _lastTargetPos;

            // Teleport guard: on a kickoff park / fresh spawn the follow target jumps a long way in one
            // frame (normal flight is only a few units/frame). Snap the camera into place instead of
            // SmoothDamping a wild swing across the arena - that swing read as a "wonky, jittery start".
            const float teleportStep = 50f;
            if (shipDelta.sqrMagnitude > teleportStep * teleportStep)
            {
                transform.position = desiredPos;
                if (SafeLookRotation.TryGet(_followTarget.position - transform.position, _followTarget.up, out var snapRot, this, logError: false))
                    transform.rotation = snapRot;
                _velocity = Vector3.zero;
                _lateralDominance = 0f;
                _lastTargetPos = _followTarget.position;
                return;
            }

            float fwd = Vector3.Dot(shipDelta, _followTarget.forward);
            float lat = Vector3.Dot(shipDelta, _followTarget.right);

            // How lateral the ship's motion is (0 = pure forward, 1 = pure strafe), LOW-PASS FILTERED so
            // it can't flip frame-to-frame. The old code hard-SNAPPED the camera when |lat| > |fwd| and
            // SMOOTHED otherwise; on an agile, banking vessel (Manta) whose lateral ≈ forward motion that
            // binary flipped every few frames, alternating instant vs lagged position+rotation = visible
            // jitter. Here we blend the responsiveness CONTINUOUSLY (snappier on strafes, smoother on
            // forward) so there is no discontinuity to stutter on.
            float rawDominance = Mathf.Abs(lat) / (Mathf.Abs(fwd) + Mathf.Abs(lat) + 1e-4f);
            _lateralDominance = Mathf.Lerp(_lateralDominance, rawDominance, 1f - Mathf.Exp(-10f * Time.deltaTime));

            if (_disableRotationLerp)
            {
                // Hard-attached camera (no smoothing) - consistent every frame, so it never jitters.
                transform.position = desiredPos;
                _velocity = Vector3.zero;
            }
            else
            {
                // SmoothDamp stays continuous (velocity preserved); just shorten its time constant as the
                // motion gets more lateral so the camera keeps up with strafes without ever jumping.
                float posSmoothTime = Mathf.Lerp(_followSmoothTime, _followSmoothTime * 0.1f, _lateralDominance);
                transform.position = Vector3.SmoothDamp(
                    transform.position, desiredPos, ref _velocity, Mathf.Max(1e-4f, posSmoothTime)
                );
            }

            if (!SafeLookRotation.TryGet(_followTarget.position - transform.position, _followTarget.up, out var targetRot, this, logError: false))
                targetRot = transform.rotation;

            if (_disableRotationLerp)
            {
                transform.rotation = targetRot;
            }
            else
            {
                // Blend the Slerp factor from the smooth base toward instant (1) as motion gets lateral -
                // continuous, so no snap/smooth flip. This is the main fix for the Manta rotation jitter.
                float baseT = 1f - Mathf.Exp(-_rotationSmoothTime * Time.deltaTime);
                float t = Mathf.Lerp(baseT, 1f, _lateralDominance);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, t);
            }

            _lastTargetPos = _followTarget.position;

            // Apply camera shake offset (decaying random displacement)
            if (_shakeTimeRemaining > 0f)
            {
                _shakeTimeRemaining -= Time.unscaledDeltaTime;
                float decay = Mathf.Clamp01(_shakeTimeRemaining / _shakeDuration);
                // Perlin-based shake for smoother motion than pure random. ~10 Hz reads as a weighty
                // "thud" rather than the ~25 Hz buzz that looked like high-frequency jitter.
                const float shakeFreq = 10f;
                float t = Time.unscaledTime * shakeFreq;
                float x = (Mathf.PerlinNoise(t, 0f) - 0.5f) * 2f;
                float y = (Mathf.PerlinNoise(0f, t) - 0.5f) * 2f;
                float z = (Mathf.PerlinNoise(t, t) - 0.5f) * 2f;
                transform.position += new Vector3(x, y, z) * (_shakeIntensity * decay);
            }
        }

        public void ApplySettings(CameraSettingsSO settings)
        {
            _currentSettings = settings;

            // Set BEFORE the null return and OUTSIDE the mode branch below: a vessel with no
            // camera settings has no rear view, and one that opts in must get it whether its
            // rig is fixed or dynamic.
            RearViewSupported = false;
            if (!_currentSettings) return;
            RearViewSupported = settings.enableRearView;

            var flags = _currentSettings.mode;

            Camera.nearClipPlane = _currentSettings.nearClipPlane;
            Camera.farClipPlane = _currentSettings.farClipPlane;

            if (flags.HasFlag(CameraMode.DynamicCamera))
            {
                _followOffset.x = settings.followOffset.x;
                _followOffset.y = settings.followOffset.y;

                _followSmoothTime = settings.followSmoothTime;
                _rotationSmoothTime = settings.rotationSmoothTime;
                _disableRotationLerp = settings.disableSmoothing;

                SetCameraDistance(settings.dynamicMinDistance);
            }
            else
            {
                _followOffset = settings.followOffset;
                _disableRotationLerp = true;
                adaptiveZoomEnabled = settings.enableAdaptiveZoom;
                _neutralOffsetZ = _followOffset.z;
            }
        }

        public void SetFollowTarget(Transform target)
        {
            _followTarget = target;
            _lastTargetPos = Vector3.zero;
            _velocity = Vector3.zero;
        }

        /// <summary>
        /// Immediately positions the camera at the correct follow offset from the target,
        /// clearing all smoothing state. Call after configuring settings and follow target.
        /// </summary>
        public void SnapToTarget()
        {
            if (!_followTarget) return;

            transform.position = _followTarget.position + _followTarget.rotation * EffectiveOffset;

            if (SafeLookRotation.TryGet(_followTarget.position - transform.position, _followTarget.up, out var targetRot, this, logError: false))
                transform.rotation = targetRot;

            _lastTargetPos = _followTarget.position;
            _velocity = Vector3.zero;
        }

        public void Activate()
        {
            gameObject.SetActive(true);
            if (!_currentSettings) return;
            
            Camera.nearClipPlane = _currentSettings.nearClipPlane;
            Camera.farClipPlane = _currentSettings.farClipPlane;
        }

        public void Deactivate() => gameObject.SetActive(false);

        public Camera Camera { get; private set; }

        /// <summary>
        /// Sets the distance (Z) behind the target. Always negative.
        /// </summary>
        public void SetCameraDistance(float distance)
        {
            if (_distanceLerpRoutine != null)
            {
                StopCoroutine(_distanceLerpRoutine);
                _distanceLerpRoutine = null;
            }

            _followOffset.z = distance;
        }

        /// <summary>
        /// Gets the current distance (absolute value).
        /// </summary>
        public float GetCameraDistance() => _followOffset.z;

        public float NeutralOffsetZ => _neutralOffsetZ;
        public float ZoomSmoothTime { get; } = 0.2f;
    
        public bool AdaptiveZoomEnabled => adaptiveZoomEnabled;

        /// <summary>
        /// Rarely used override to set full offset directly.
        /// </summary>
        public void SetFollowOffset(Vector3 offset)
        {
            _followOffset = offset;
        }

        /// <summary>
        /// Returns the current full offset vector.
        /// </summary>
        public Vector3 GetFollowOffset() => _followOffset;

        /// <summary>
        /// Switches to orthographic view if requested.
        /// </summary>
        public void SetOrthographic(bool ortho, float size)
        {
            Camera.orthographic = ortho;
            if (ortho) Camera.orthographicSize = size;
        }

        /// <summary>
        /// Triggers a decaying camera shake. Subsequent calls override the current shake
        /// only if the new intensity is stronger.
        /// </summary>
        public void Shake(float intensity, float duration)
        {
            if (intensity <= 0f || duration <= 0f) return;

            // Only override if this shake is stronger than what's already playing
            if (_shakeTimeRemaining > 0f && intensity < _shakeIntensity * (_shakeTimeRemaining / _shakeDuration))
                return;

            _shakeIntensity = intensity;
            _shakeDuration = duration;
            _shakeTimeRemaining = duration;
        }
    }
}
