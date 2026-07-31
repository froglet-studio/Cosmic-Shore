using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Rigid-body velocity model for a skimmer that MOVES relative to its vessel - the
    /// Rhino's sword, whose pose is swept every frame by <see cref="ShieldSwipeActionExecutor"/>
    /// and whose length is driven by <see cref="ShieldSkimmerScaleDriver"/>.
    ///
    /// The blade is a rigid segment, so the velocity of a material point on it is the
    /// standard composition
    ///
    ///     v(P) = v_vessel  +  omega_vessel x (P - vesselOrigin)  +  R_vessel * v_rel(P)
    ///     v_rel(P) = v_bladeOrigin/vessel + omega_blade/vessel x (P - bladeOrigin) + (dL/dt)*f*axis
    ///
    /// where the last term is the radial push a lengthening blade gives a point sitting at
    /// normalized fraction f along it. Every rate is obtained by differentiating the blade's
    /// pose IN THE VESSEL'S FRAME, so vessel translation, teleports and respawns never leak
    /// into the swing - only the sword's own motion does.
    ///
    /// Consequence, and the point of the whole model: the hilt travels at roughly the
    /// vessel's speed while the TIP - tens of units out on the lever arm - travels many
    /// times faster during a swipe. Impact effects read the contact point's velocity here
    /// instead of the vessel's, so a tip strike drives prism debris far harder, and along
    /// the swing tangent rather than along the ship's course.
    ///
    /// Sampling happens in LateUpdate, after the swipe executor and scale driver have
    /// written this frame's pose, which is also the pose the NEXT FixedUpdate evaluates
    /// triggers against - so impact queries read the rates that produced their own contact.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SkimmerSwingKinematics : MonoBehaviour
    {
        public enum BladeAxis { X = 0, Y = 1, Z = 2 }

        [Header("Config")]
        [SerializeField] SkimmerSwingKinematicsConfigSO config;

        [Header("Structure")]
        [Tooltip("The blade transform. Defaults to this GameObject (the skimmer root).")]
        [SerializeField] Transform blade;

        [Tooltip("Which local axis runs along the blade's length. The Rhino sword is elongated on Y (Skimmer.elongateYOnly).")]
        [SerializeField] BladeAxis bladeAxis = BladeAxis.Y;

        [Tooltip("Pivot the blade swings about - used ONLY to decide which end is the tip (the end farther from it). Defaults to the blade's parent, which is what the swipe executor pivots around.")]
        [SerializeField] Transform pivot;

        [Tooltip("Blade length as a fraction of the transform's world scale along the blade axis. The sword mesh is a unit primitive centered on its origin, so the default 1 makes the segment span the full scaled extent.")]
        [SerializeField] float lengthScale = 1f;

        const float Epsilon = 1e-5f;

        Skimmer _skimmer;
        SkimmerSwingKinematicsConfigSO _runtimeConfig;

        // Last sampled blade pose in the VESSEL's frame (rotation-only, so vessel scale
        // can never distort a velocity), plus the sampled rates.
        Vector3 _localPos;
        Quaternion _localRot = Quaternion.identity;
        float _halfLength;

        Vector3 _localLinearVelocity;
        Vector3 _localAngularVelocity;   // radians/sec, vessel-local axes
        float _halfLengthRate;

        // The vessel pose the local blade pose above was measured against - kept as a pair
        // so a query always un-maps through the same frame it was mapped into, whatever
        // phase of the player loop asks.
        Vector3 _vesselPos;
        Quaternion _vesselRot = Quaternion.identity;
        Vector3 _vesselAngularVelocity;  // radians/sec, world axes

        bool _hasPreviousSample;

        SkimmerSwingKinematicsConfigSO Config
        {
            get
            {
                if (config) return config;
                if (!_runtimeConfig)
                {
                    CSDebug.LogError($"[{nameof(SkimmerSwingKinematics)}] No {nameof(SkimmerSwingKinematicsConfigSO)} wired on '{name}' - falling back to built-in defaults so impacts keep resolving.", this);
                    _runtimeConfig = ScriptableObject.CreateInstance<SkimmerSwingKinematicsConfigSO>();
                }
                return _runtimeConfig;
            }
        }

        Transform VesselTransform => _skimmer && _skimmer.VesselStatus != null ? _skimmer.VesselStatus.ShipTransform : null;

        /// <summary>True once the blade and its vessel are resolvable - until then callers keep their pre-model behaviour.</summary>
        public bool IsReady => Blade != null && VesselTransform != null;

        // Resolved rather than cached in Awake so every read - including edit-mode gizmos
        // and any query that lands before Awake - sees the same blade the model samples.
        Transform Blade => blade ? blade : transform;
        Transform Pivot => pivot ? pivot : Blade.parent;

        // ------------------------------------------------------------------ geometry

        /// <summary>Unit world direction the blade's length runs along (its local blade axis).</summary>
        public Vector3 BladeAxisWorld => Blade ? Blade.rotation * LocalAxis : Vector3.up;

        /// <summary>Half the blade's world length - the sword grows on this axis, so it tracks the shield driver live.</summary>
        public float HalfLength
        {
            get
            {
                var b = Blade;
                if (!b) return 0f;
                var s = b.lossyScale;
                float extent = bladeAxis switch
                {
                    BladeAxis.X => s.x,
                    BladeAxis.Z => s.z,
                    _ => s.y,
                };
                return 0.5f * Mathf.Abs(extent) * Mathf.Max(0f, lengthScale);
            }
        }

        Vector3 LocalAxis => bladeAxis switch
        {
            BladeAxis.X => Vector3.right,
            BladeAxis.Z => Vector3.forward,
            _ => Vector3.up,
        };

        /// <summary>
        /// +1 when the blade's local axis points AWAY from the pivot (so +axis is the tip),
        /// -1 when it points back toward it. Derived from geometry each call rather than
        /// authored, so re-posing or re-parenting the sword can't invert hilt and tip.
        /// </summary>
        float TipSign
        {
            get
            {
                var b = Blade;
                if (!b) return 1f;
                var p = Pivot;
                if (!p) return 1f;
                Vector3 offset = b.position - p.position;
                float d = Vector3.Dot(offset, BladeAxisWorld);
                return d < 0f ? -1f : 1f;
            }
        }

        /// <summary>World position of the blade's hilt end (the end nearest the swing pivot).</summary>
        public Vector3 HiltPoint => Blade ? Blade.position - BladeAxisWorld * (TipSign * HalfLength) : Vector3.zero;

        /// <summary>World position of the blade's tip (the end farthest from the swing pivot - the fast end).</summary>
        public Vector3 TipPoint => Blade ? Blade.position + BladeAxisWorld * (TipSign * HalfLength) : Vector3.zero;

        /// <summary>World point at normalized length <paramref name="t01"/>: 0 = hilt, 0.5 = mid-blade, 1 = tip.</summary>
        public Vector3 PointAlongBlade(float t01)
        {
            var b = Blade;
            if (!b) return Vector3.zero;
            float t = Mathf.Clamp01(t01);
            // hilt at -half, tip at +half along the tip-facing axis
            return b.position + BladeAxisWorld * (TipSign * HalfLength * (2f * t - 1f));
        }

        /// <summary>
        /// The point on the blade's centreline closest to <paramref name="worldPoint"/>,
        /// clamped to the segment - i.e. WHICH PART of the sword a contact landed on.
        /// The sword's trigger is a sphere centred on the blade's midpoint, so this
        /// projection is what turns "something is inside the sword" into "the tip hit it".
        /// </summary>
        public Vector3 ClosestBladePoint(Vector3 worldPoint)
        {
            var b = Blade;
            if (!b) return worldPoint;
            Vector3 axis = BladeAxisWorld;
            float half = HalfLength;
            float along = Mathf.Clamp(Vector3.Dot(worldPoint - b.position, axis), -half, half);
            return b.position + axis * along;
        }

        /// <summary>Normalized position along the blade of the closest point to <paramref name="worldPoint"/>: 0 = hilt, 1 = tip.</summary>
        public float NormalizedAlongBlade(Vector3 worldPoint)
        {
            var b = Blade;
            if (!b) return 0f;
            float half = HalfLength;
            if (half <= Epsilon) return 0f;
            float along = Mathf.Clamp(Vector3.Dot(worldPoint - b.position, BladeAxisWorld), -half, half);
            return Mathf.Clamp01(0.5f * (TipSign * along / half + 1f));
        }

        // ------------------------------------------------------------------ velocity

        /// <summary>
        /// The vessel's own translation - the canonical world velocity the transformer
        /// integrates every frame (<c>Speed * Course + VelocityShift</c>). This is the
        /// "rhino velocity" half of an impact.
        /// </summary>
        public Vector3 VesselVelocity
        {
            get
            {
                var status = _skimmer ? _skimmer.VesselStatus : null;
                if (status == null) return Vector3.zero;
                Vector3 v = status.Course * status.Speed;
                if (status.VesselTransformer) v += status.VesselTransformer.VelocityShift;
                return v;
            }
        }

        /// <summary>Angular velocity of the blade relative to the vessel, world axes, radians/sec.</summary>
        public Vector3 SwingAngularVelocity => _hasPreviousSample ? _vesselRot * _localAngularVelocity : Vector3.zero;

        /// <summary>Rate the blade's half-length is growing, world units/sec (the shield scale driver).</summary>
        public float ElongationRate => _hasPreviousSample ? _halfLengthRate : 0f;

        /// <summary>
        /// One frame's differentiated state - everything <see cref="RelativeVelocity"/> needs.
        /// Split out from the MonoBehaviour so the composition is pure and unit-testable
        /// (see <c>SkimmerSwingKinematicsTests</c>) rather than reachable only through a
        /// live vessel in play mode.
        /// </summary>
        public struct Sample
        {
            public Vector3 VesselPosition;
            public Quaternion VesselRotation;
            public Vector3 VesselAngularVelocity;   // world axes, rad/s

            public Vector3 BladeLocalPosition;      // blade origin in the vessel's frame
            public Quaternion BladeLocalRotation;
            public Vector3 BladeLinearVelocity;     // of the blade origin, vessel frame
            public Vector3 BladeAngularVelocity;    // vessel-local axes, rad/s

            public float HalfLength;
            public float HalfLengthRate;
        }

        /// <summary>The state the model differentiated on its last frame - the input to <see cref="RelativeVelocity"/>.</summary>
        public Sample CurrentSample => new()
        {
            VesselPosition = _vesselPos,
            VesselRotation = _vesselRot,
            VesselAngularVelocity = _vesselAngularVelocity,
            BladeLocalPosition = _localPos,
            BladeLocalRotation = _localRot,
            BladeLinearVelocity = _localLinearVelocity,
            BladeAngularVelocity = _localAngularVelocity,
            HalfLength = _halfLength,
            HalfLengthRate = _halfLengthRate,
        };

        /// <summary>
        /// The rigid-body composition itself: everything <paramref name="worldPoint"/> has ON TOP
        /// of the vessel's translation - the hull's own rotation carrying it, plus the blade's
        /// motion relative to the hull (origin drift + spin + elongation), rotated back to world.
        /// Pure: no Unity object state, so it is directly testable against an analytic ground truth.
        /// </summary>
        public static Vector3 RelativeVelocity(in Sample s, Vector3 worldPoint, Vector3 bladeLocalAxis,
                                               bool includeVesselRotation, bool includeElongation)
        {
            Vector3 fromVessel = worldPoint - s.VesselPosition;

            Vector3 velocity = includeVesselRotation
                ? Vector3.Cross(s.VesselAngularVelocity, fromVessel)
                : Vector3.zero;

            // Blade motion measured in the vessel's frame, then rotated back out to world.
            Vector3 pointLocal = Quaternion.Inverse(s.VesselRotation) * fromVessel;
            Vector3 fromBladeOrigin = pointLocal - s.BladeLocalPosition;

            Vector3 local = s.BladeLinearVelocity + Vector3.Cross(s.BladeAngularVelocity, fromBladeOrigin);

            if (includeElongation && s.HalfLength > Epsilon && Mathf.Abs(s.HalfLengthRate) > Epsilon)
            {
                // A material point holds its fraction along the blade, so its along-axis
                // offset grows at (fraction x dHalfLength/dt).
                Vector3 axisLocal = s.BladeLocalRotation * bladeLocalAxis;
                float fraction = Vector3.Dot(fromBladeOrigin, axisLocal) / s.HalfLength;
                local += axisLocal * (s.HalfLengthRate * Mathf.Clamp(fraction, -1f, 1f));
            }

            return velocity + s.VesselRotation * local;
        }

        /// <summary>
        /// Everything <paramref name="worldPoint"/> has ON TOP of the vessel's translation.
        /// Add to <see cref="VesselVelocity"/> for the point's true world velocity.
        /// </summary>
        public Vector3 RelativeVelocityAt(Vector3 worldPoint)
        {
            if (!_hasPreviousSample) return Vector3.zero;
            var cfg = Config;
            return RelativeVelocity(CurrentSample, worldPoint, LocalAxis,
                                    cfg.IncludeVesselRotation, cfg.IncludeElongation);
        }

        /// <summary>True world velocity of the sword's material point at <paramref name="worldPoint"/>.</summary>
        public Vector3 VelocityAt(Vector3 worldPoint) => VesselVelocity + RelativeVelocityAt(worldPoint);

        /// <summary>Speed of the blade at normalized length <paramref name="t01"/> (0 = hilt, 1 = tip) - the headline read of this model.</summary>
        public float SpeedAlongBlade(float t01) => VelocityAt(PointAlongBlade(t01)).magnitude;

        /// <summary>Speed the blade adds at normalized length <paramref name="t01"/>, excluding the vessel's translation.</summary>
        public float SwingSpeedAlongBlade(float t01) => RelativeVelocityAt(PointAlongBlade(t01)).magnitude;

        /// <summary>Speed of the sword tip - the fastest point on the blade.</summary>
        public float TipSpeed => VelocityAt(TipPoint).magnitude;

        /// <summary>
        /// Speed of the sword's hilt end. NOT simply the vessel's speed: the Rhino's blade is
        /// mounted ~23 units out from its swing pivot, so even the near end rides a lever arm -
        /// and once the shield grows the blade past that offset, the hilt swings on the far side
        /// of the pivot and moves fast in the opposite direction.
        /// </summary>
        public float HiltSpeed => VelocityAt(HiltPoint).magnitude;

        // ------------------------------------------------------------------ sampling

        void Awake()
        {
            TryGetComponent(out _skimmer);
            if (!_skimmer)
                CSDebug.LogError($"[{nameof(SkimmerSwingKinematics)}] No {nameof(Skimmer)} on '{name}' - the model cannot resolve a vessel and will report zero swing.", this);
        }

        // A pooled / swapped vessel restarts from a clean slate: a stale previous sample
        // would differentiate across the teleport and emit one absurd velocity frame.
        void OnEnable() => _hasPreviousSample = false;

        void OnDestroy()
        {
            if (_runtimeConfig) Destroy(_runtimeConfig);
        }

        void LateUpdate()
        {
            var vesselTf = VesselTransform;
            var b = Blade;
            if (!vesselTf || !b) { _hasPreviousSample = false; return; }

            float dt = Time.deltaTime;
            var cfg = Config;

            Quaternion vesselRot = vesselTf.rotation;
            Quaternion toLocal = Quaternion.Inverse(vesselRot);
            Vector3 localPos = toLocal * (b.position - vesselTf.position);
            Quaternion localRot = toLocal * b.rotation;
            float halfLength = HalfLength;

            bool sampleable = _hasPreviousSample
                              && dt > Epsilon
                              && dt <= Mathf.Max(Epsilon, cfg.MaxSampleDeltaSeconds);

            if (sampleable)
            {
                float invDt = 1f / dt;
                Vector3 linear = (localPos - _localPos) * invDt;
                Vector3 angular = AngularVelocity(_localRot, localRot, invDt, cfg.MaxAngularSpeedDegrees);
                Vector3 vesselAngular = AngularVelocity(_vesselRot, vesselRot, invDt, cfg.MaxAngularSpeedDegrees);
                float lengthRate = (halfLength - _halfLength) * invDt;

                float blend = SmoothingBlend(cfg.SmoothingSeconds, dt);
                _localLinearVelocity = Vector3.Lerp(_localLinearVelocity, linear, blend);
                _localAngularVelocity = Vector3.Lerp(_localAngularVelocity, angular, blend);
                _vesselAngularVelocity = Vector3.Lerp(_vesselAngularVelocity, vesselAngular, blend);
                _halfLengthRate = Mathf.Lerp(_halfLengthRate, lengthRate, blend);
            }
            else if (!_hasPreviousSample)
            {
                _localLinearVelocity = Vector3.zero;
                _localAngularVelocity = Vector3.zero;
                _vesselAngularVelocity = Vector3.zero;
                _halfLengthRate = 0f;
            }

            _localPos = localPos;
            _localRot = localRot;
            _halfLength = halfLength;
            _vesselPos = vesselTf.position;
            _vesselRot = vesselRot;
            _hasPreviousSample = true;
        }

        /// <summary>
        /// Angular velocity (radians/sec) carrying <paramref name="from"/> to <paramref name="to"/>.
        /// The delta is left-multiplied, so its axis is expressed in the same frame both
        /// rotations are - vessel-local for the blade, world for the vessel itself.
        /// </summary>
        public static Vector3 AngularVelocity(Quaternion from, Quaternion to, float invDt, float maxDegreesPerSecond)
        {
            Quaternion delta = to * Quaternion.Inverse(from);
            // Keep the short way around: a quaternion and its negation are the same
            // rotation, but ToAngleAxis reads the negated one as ~360 deg the other way.
            if (delta.w < 0f) delta = new Quaternion(-delta.x, -delta.y, -delta.z, -delta.w);
            delta.ToAngleAxis(out float degrees, out Vector3 axis);
            if (degrees > 180f) degrees -= 360f;
            if (float.IsNaN(degrees) || axis.sqrMagnitude < Epsilon) return Vector3.zero;

            float degreesPerSecond = degrees * invDt;
            if (maxDegreesPerSecond > 0f)
                degreesPerSecond = Mathf.Clamp(degreesPerSecond, -maxDegreesPerSecond, maxDegreesPerSecond);

            return axis.normalized * (degreesPerSecond * Mathf.Deg2Rad);
        }

        static float SmoothingBlend(float tau, float dt) =>
            tau <= Epsilon ? 1f : 1f - Mathf.Exp(-dt / tau);

#if UNITY_EDITOR
        // Blade segment + a speed profile from hilt to tip, so the model is inspectable
        // in play mode without wiring a HUD.
        void OnDrawGizmosSelected()
        {
            if (!Blade) return;

            Vector3 hilt = HiltPoint;
            Vector3 tip = TipPoint;

            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(hilt, tip);

            if (!Application.isPlaying || !IsReady) return;

            const int samples = 6;
            float tipSpeed = Mathf.Max(TipSpeed, Epsilon);
            for (int i = 0; i <= samples; i++)
            {
                float t = i / (float)samples;
                Vector3 p = PointAlongBlade(t);
                Vector3 v = VelocityAt(p);
                Gizmos.color = Color.Lerp(Color.green, Color.red, v.magnitude / tipSpeed);
                Gizmos.DrawRay(p, v * 0.05f);
            }
        }
#endif
    }
}
