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

        // --- Anchor hold (an ability spins the vessel; the view must not spin with it) ---
        private Transform _anchor;
        private float _anchorBlend;        // 0 = normal follow, 1 = fully held on the anchor
        private float _anchorBlendTarget;
        private float _anchorBlendRate = 4f;
        private Vector3 _anchorDir = Vector3.back;   // stable world direction anchor -> camera
        private Vector3 _anchorUp = Vector3.up;      // stable world up while held
        private float _anchorDistance;

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

            Vector3 desiredPos = _followTarget.position + _followTarget.rotation * _followOffset;
            Vector3 shipDelta = _followTarget.position - _lastTargetPos;

            // ── ANCHOR HOLD ────────────────────────────────────────────────────────────────
            // While an ability spins the vessel in place (the Scarab's ball grapple), following
            // its rotation is what makes players sick: BOTH the camera's position and its roll are
            // derived from the target's rotation above, so a hull orbiting a ball drags the whole
            // view around with it. Held, the camera keeps its distance but takes its position from
            // a STABLE direction off the anchor and looks at the anchor — so the ship visibly
            // spins in front of a still frame, which is the shot the player needs to time a
            // release. Blended rather than switched, and blended at the INPUTS (where the camera
            // wants to be, what it looks at, which way is up) so the existing SmoothDamp/Slerp
            // machinery carries the transition and there is no second smoothing model to tune.
            Vector3 lookAt = _followTarget.position;
            Vector3 lookUp = _followTarget.up;
            UpdateAnchorBlend();
            if (_anchorBlend > 0f && _anchor)
            {
                Vector3 anchorPos = _anchor.position + _anchorDir * _anchorDistance;
                desiredPos = Vector3.Lerp(desiredPos, anchorPos, _anchorBlend);
                lookAt = Vector3.Lerp(lookAt, _anchor.position, _anchorBlend);
                Vector3 blendedUp = Vector3.Slerp(lookUp, _anchorUp, _anchorBlend);
                if (blendedUp.sqrMagnitude > 1e-6f) lookUp = blendedUp;
            }

            // Teleport guard: on a kickoff park / fresh spawn the follow target jumps a long way in one
            // frame (normal flight is only a few units/frame). Snap the camera into place instead of
            // SmoothDamping a wild swing across the arena - that swing read as a "wonky, jittery start".
            const float teleportStep = 50f;
            if (shipDelta.sqrMagnitude > teleportStep * teleportStep)
            {
                transform.position = desiredPos;
                if (SafeLookRotation.TryGet(lookAt - transform.position, lookUp, out var snapRot, this, logError: false))
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

            // A held camera must not inherit the "motion is lateral, so be snappy" boost: the
            // vessel's orbit IS pure lateral motion, so the boost would drive the responsiveness
            // to instant exactly when the point is to be calm. Fade it out with the hold.
            _lateralDominance *= 1f - _anchorBlend;

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

            if (!SafeLookRotation.TryGet(lookAt - transform.position, lookUp, out var targetRot, this, logError: false))
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

        /// <summary>
        /// HOLD the camera on <paramref name="anchor"/> while something else spins the vessel.
        /// The camera keeps the distance it already has (plus <paramref name="extraDistance"/>),
        /// stops deriving its position and roll from the follow target's rotation, and looks at
        /// the anchor — so the vessel rotates in front of a still frame instead of dragging the
        /// frame around with it.
        ///
        /// The stable direction is captured HERE, from wherever the camera already is, so the hold
        /// begins with no positional jump: the vantage the pilot flew in on is the vantage they
        /// watch from. Idempotent — calling it again while held re-aims nothing, so a system that
        /// asserts the hold every frame cannot ratchet the camera around.
        /// </summary>
        public void BeginAnchorHold(Transform anchor, float blendSeconds, float extraDistance = 0f)
        {
            if (!anchor) return;
            _anchorBlendRate = 1f / Mathf.Max(0.01f, blendSeconds);
            _anchorBlendTarget = 1f;
            if (_anchor == anchor) return;

            _anchor = anchor;
            Vector3 toCamera = transform.position - anchor.position;
            float distance = toCamera.magnitude;
            _anchorDir = distance > 1e-3f ? toCamera / distance : -transform.forward;
            // Never closer than the vessel's own follow distance: a grab that happened to catch
            // the camera mid-swing must not park the whole hold inside the ship.
            _anchorDistance = Mathf.Max(distance, _followOffset.magnitude) + Mathf.Max(0f, extraDistance);
            _anchorUp = transform.up;
        }

        /// <summary>Release an anchor hold, easing back to the normal follow over
        /// <paramref name="blendSeconds"/>. Safe to call when not held.</summary>
        public void EndAnchorHold(float blendSeconds)
        {
            _anchorBlendRate = 1f / Mathf.Max(0.01f, blendSeconds);
            _anchorBlendTarget = 0f;
        }

        /// <summary>True while the camera is anchored or still easing out of it.</summary>
        public bool IsAnchorHeld => _anchor && _anchorBlend > 0f;

        void UpdateAnchorBlend()
        {
            // A destroyed anchor (the ball was spent or detonated under the hold) releases the
            // camera rather than stranding it — the same "observe rather than require every force
            // to announce itself" shape the ball's own release uses.
            if (!_anchor) _anchorBlendTarget = 0f;

            _anchorBlend = Mathf.MoveTowards(_anchorBlend, _anchorBlendTarget,
                                             _anchorBlendRate * Time.deltaTime);
            if (_anchorBlend <= 0f && _anchorBlendTarget <= 0f) _anchor = null;
        }

        public void SetFollowTarget(Transform target)
        {
            _followTarget = target;
            _lastTargetPos = Vector3.zero;
            _velocity = Vector3.zero;
            // A new vessel is a new camera: never inherit the previous hull's hold.
            _anchor = null;
            _anchorBlend = 0f;
            _anchorBlendTarget = 0f;
        }

        /// <summary>
        /// Immediately positions the camera at the correct follow offset from the target,
        /// clearing all smoothing state. Call after configuring settings and follow target.
        /// </summary>
        public void SnapToTarget()
        {
            if (!_followTarget) return;

            transform.position = _followTarget.position + _followTarget.rotation * _followOffset;

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
