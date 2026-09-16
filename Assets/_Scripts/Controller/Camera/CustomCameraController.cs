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
        /// so the pilot sees their own nose against whatever is chasing them. Driven only by
        /// <c>VesselRearView</c> (Docs/REAR_VIEW.md); nothing else may write it.
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
        /// First person: pose the camera AT the vessel — the cockpit — and aim it where the ship
        /// is pointing rather than back at the hull. Driven only by <c>VesselFirstPersonView</c>
        /// (the Serpent's scope, R_VesselActions/SERPENT_SNIPER_SCOPE.md); nothing else may
        /// write it.
        ///
        /// <para>A FLAG for exactly the reason <see cref="RearView"/> is one: the pose is applied
        /// at the point of use and <see cref="_followOffset"/> is left alone, so the four systems
        /// that legitimately write that field while a pilot flies — the zoom-out abilities,
        /// adaptive zoom, the skimmer's camera-scaling prism effect, and a vessel swap
        /// re-applying its own <c>CameraSettingsSO</c> — keep working, and the first of them to
        /// fire cannot silently drop the pilot out of the cockpit.</para>
        ///
        /// <para>It BEATS <see cref="RearView"/> rather than composing with it: the z-mirror of a
        /// cockpit offset is another point inside the same hull, so "look behind from the
        /// cockpit" is not a vantage the mirror can express. A pilot who scopes while looking
        /// back gets the scope, and gets the look-back again on release.</para>
        /// </summary>
        public bool FirstPerson { get; set; }

        /// <summary>
        /// The cockpit offset in the vessel's own local space, used while
        /// <see cref="FirstPerson"/> is set. Published by <c>VesselFirstPersonView</c> from the
        /// vessel's MEASURED hull radius, so the eye sits just past the nose on a hull of any
        /// size rather than at a constant that is inside one ship and far ahead of another.
        /// </summary>
        public Vector3 FirstPersonOffset { get; set; }

        /// <summary>
        /// The offset actually used to pose the camera this frame: the cockpit while
        /// <see cref="FirstPerson"/> is set, else the authored one, or its z-mirror while
        /// <see cref="RearView"/> is set. Mirroring z alone — not x, not y — is
        /// what puts the camera directly ahead at the same distance and the same height, rather
        /// than at some reflected vantage the vessel's settings never described.
        /// </summary>
        private Vector3 EffectiveOffset =>
            FirstPerson
                ? FirstPersonOffset
                : RearView
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

            // FIRST PERSON IS A RIGID ATTACHMENT, AND BOTH HALVES OF THAT ARE LOAD-BEARING.
            //
            // The look vector below is target-position-minus-camera-position, which in the
            // cockpit is very nearly ZERO — SafeLookRotation would decline it and the camera
            // would hold whatever rotation it last had, i.e. the view would stop turning with
            // the ship. So first person aims along the TARGET'S OWN forward instead: the pilot
            // looks where the nose points, which is also what makes the shot land where the
            // reticle is.
            //
            // And it does not SmoothDamp. A lagging chase camera is a feature at 250 units back
            // and a defect at zero: any lag at all puts the camera inside the hull it is trying
            // to see past, so the eye is written outright every frame.
            if (FirstPerson)
            {
                transform.position = desiredPos;
                transform.rotation = _followTarget.rotation;
                _velocity = Vector3.zero;
                _lateralDominance = 0f;
                _lastTargetPos = _followTarget.position;
                ApplyShake();
                return;
            }

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

            ApplyShake();
        }

        /// <summary>
        /// Decaying random displacement, applied after the pose is settled. Factored out so the
        /// first-person path — which writes its own pose and returns early — still recoils.
        /// </summary>
        private void ApplyShake()
        {
            if (_shakeTimeRemaining <= 0f) return;

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

        public void ApplySettings(CameraSettingsSO settings)
        {
            _currentSettings = settings;
            if (!_currentSettings) return;

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

            // Same degenerate look vector as UpdateCamera's first-person branch — aim along the
            // ship rather than at it, or the snap leaves the cockpit facing wherever it was.
            if (FirstPerson)
                transform.rotation = _followTarget.rotation;
            else if (SafeLookRotation.TryGet(_followTarget.position - transform.position, _followTarget.up, out var targetRot, this, logError: false))
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
