using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The per-object half of the WAKE (Docs/PRISM_ANIMATION.md §4.7.3): while this object is
    /// moving fast enough, it reports its position, its heading, its size and its wave phase to
    /// <see cref="PrismWake"/> every frame, and the mass around the path it just flew ripples on
    /// the GPU.
    ///
    /// <para><b>It belongs to a FEW things, deliberately.</b> This shipped on every vessel for
    /// exactly one playtest and was pulled: a ripple that follows every hull in the match is
    /// wallpaper, and wallpaper is the one thing a 96-prism high-poly budget cannot afford to be
    /// spent on. The shipped carriers are the Sparrow's SKYBURST MISSILE and the Scarab's BALL —
    /// two objects a whole arena has a reason to watch, each of which crosses open space fast
    /// enough that the mass it passes bending around it reads as the object's own weight. A wake
    /// is an event, not a texture. Adding a third carrier is a design call, not a wiring one.</para>
    ///
    /// <para><b>It asks a CAPABILITY for its motion.</b> Whatever component on this GameObject
    /// answers <see cref="IPrismWakeCarrier"/> supplies the velocity and the radius; with nothing
    /// answering, the source falls back to a frame-to-frame transform delta and a one-time
    /// renderer measurement, so it still does something sensible when dropped on an arbitrary
    /// object. See that interface for why a carrier beats the fallback.</para>
    ///
    /// <para><b>The speed window is ABSOLUTE.</b> <c>PrismWakeConfigSO.StrengthForSpeed</c> takes a
    /// speed and nothing else, so the same speed on any carrier leaves the same wake — the same
    /// argument, and the same refusal to normalise against an object's own top speed, that
    /// Docs/SPEED_TUNNEL.md records. Its first authoring was a cautionary tale worth keeping: the
    /// engage speed was set above the top speed of the hull the mode actually flies, so the window
    /// never opened and the effect was reported as "too subtle" rather than as absent.</para>
    ///
    /// <para><b>The axis is the direction of TRAVEL, reversed</b> — never the nose. The two differ
    /// for a ball that has been batted sideways and for a vessel mid-drift, and it is the path the
    /// object actually took that the mass beside it was passed by. Published from <c>Update</c>
    /// while the POSITION is sampled at flush time in LateUpdate: a heading one frame stale is a
    /// fraction of a degree, where a position one frame stale is several units at these speeds.</para>
    ///
    /// <para><b>The phase is integrated here.</b> It advances at this object's own wavenumber times
    /// its own speed (<see cref="PrismWake.PhaseRateFor"/>), so at a travel factor of 1 a crest
    /// sits STILL in the world and the object flies out from under it. Integrating rather than
    /// evaluating <c>k·distance</c> is what lets the factor exist at all, and it is what keeps the
    /// wave continuous when the speed or the radius changes instead of jumping the whole train.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public class PrismWakeSource : MonoBehaviour
    {
        [Tooltip("Fallback wake radius in world units, used ONLY when no IPrismWakeCarrier " +
                 "answers on this object. The wake's reach and train length are multiples of it, " +
                 "so a big thing makes a big wake. 0 (the default) measures it ONCE from this " +
                 "object's own renderers and caches it.")]
        [Min(0f)]
        [SerializeField] float fallbackRadius = 0f;

        IPrismWakeCarrier _carrier;
        bool _carrierResolved;

        float _strength;
        float _phase;
        float _radius;

        // Fallback-only state. A pooled object is repositioned while disabled, so the previous
        // position is invalid across a reuse and is re-seeded in OnEnable rather than carried.
        Vector3 _previousPosition;
        bool _hasPreviousPosition;
        float _measuredRadius;
        bool _radiusMeasured;

        /// <summary>The radius the wake is using this frame; 0 before the first moving frame.</summary>
        public float WakeRadius => _radius;

        /// <summary>The eased strength being published this frame, 0..1.</summary>
        public float Strength => _strength;

        /// <summary>The wave's phase in radians, as integrated so far.</summary>
        public float Phase => _phase;

        void Awake() => ResolveCarrier();

        void OnEnable()
        {
            // A pooled round comes back out of the pool somewhere else entirely. Everything that
            // describes the LAST flight has to go, or the first frame publishes a wake pointing
            // from the previous detonation to this launch bay at an absurd speed.
            _strength = 0f;
            _hasPreviousPosition = false;
            _previousPosition = transform.position;
        }

        void Update()
        {
            var config = PrismWake.Config;

            if (!TryReadMotion(out Vector3 velocity, out float radius))
            {
                // No motion to report. Ease out rather than cutting, so a ball settling or a round
                // finishing its flight does not blink its wake off (continuity of existence).
                velocity = Vector3.zero;
                radius = _radius;
            }

            _radius = radius;
            float speed = velocity.magnitude;

            float target = config.Enabled ? config.StrengthForSpeed(speed) : 0f;
            // Ease toward the window's answer rather than snapping to it. The window is already
            // smooth in speed, so this is the guard against speed JUMPING — a launch, a strike, a
            // teleport — putting a full wake on screen in one frame.
            float seconds = target > _strength ? config.EngageSeconds : config.ReleaseSeconds;
            _strength = seconds > 0f
                ? Mathf.MoveTowards(_strength, target, Time.deltaTime / seconds)
                : target;

            // The phase keeps advancing while the wake fades out, so something that slows down
            // leaves a wave that settles rather than one that freezes mid-crest and dims.
            if (_radius > 0f)
                _phase += PrismWake.PhaseRateFor(_radius, speed, config) * Time.deltaTime;

            // Radians are periodic, so the accumulator can be wrapped with no visible effect — and
            // must be, or a long match drifts it into the range where a float's spacing is coarser
            // than the wave and the ripple visibly quantises.
            if (_phase > TwoPi || _phase < -TwoPi)
                _phase = Mathf.Repeat(_phase, TwoPi);

            if (_strength <= 0.001f || _radius <= 0f)
            {
                PrismWake.Clear(GetInstanceID());
                return;
            }

            Vector3 course = speed > 1e-4f ? velocity / speed : transform.forward;

            // Behind the object: the wake trails the direction of travel.
            PrismWake.Publish(GetInstanceID(), transform, _radius, -course, _phase, _strength);
        }

        void OnDisable()
        {
            _strength = 0f;
            PrismWake.Clear(GetInstanceID());
        }

        const float TwoPi = 2f * Mathf.PI;

        void ResolveCarrier()
        {
            if (_carrierResolved) return;
            _carrierResolved = true;
            TryGetComponent(out _carrier);
        }

        /// <summary>
        /// This frame's velocity and radius — from the carrier when one answers, else from the
        /// transform's own motion. The carrier is asked EVERY frame because both shipped carriers
        /// change size in flight; only the fallback's measurement is cached.
        /// </summary>
        bool TryReadMotion(out Vector3 velocity, out float radius)
        {
            ResolveCarrier();

            if (_carrier != null)
            {
                if (_carrier.TryGetWakeMotion(out velocity, out radius) && radius > 0f)
                    return true;

                velocity = Vector3.zero;
                radius = 0f;
                return false;
            }

            velocity = Vector3.zero;
            radius = FallbackRadius();

            Vector3 here = transform.position;
            if (_hasPreviousPosition && Time.deltaTime > 0f)
                velocity = (here - _previousPosition) / Time.deltaTime;
            _previousPosition = here;
            _hasPreviousPosition = true;

            return radius > 0f;
        }

        float FallbackRadius()
        {
            if (fallbackRadius > 0f) return fallbackRadius;
            if (_radiusMeasured) return _measuredRadius;

            _measuredRadius = PrismOcclusionCorridor.MeasureCircumscribedRadius(transform);
            _radiusMeasured = _measuredRadius > 0f;
            if (_radiusMeasured)
            {
                CSDebug.LogVerbose(CSLogChannel.PrismRuntime,
                    $"[PrismWakeSource] {name}: no IPrismWakeCarrier — measured wake radius " +
                    $"{_measuredRadius:0.00} u from this object's renderers.");
            }
            return _measuredRadius;
        }
    }
}
