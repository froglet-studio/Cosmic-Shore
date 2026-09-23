using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// A vessel's half of the WAKE (Docs/PRISM_ANIMATION.md §4.7.3): while this vessel is moving
    /// fast enough, it reports its hull, its heading and its wave phase to <see cref="PrismWake"/>
    /// every frame, and the mass around the path it just flew ripples on the GPU.
    ///
    /// <para><b>Ensured, not authored, and not local-pilot-gated.</b>
    /// <see cref="VesselController.Initialize"/> adds this component when the prefab does not carry
    /// one, so a wake cannot be omitted from a vessel by wiring. Unlike the occlusion corridor, the
    /// speed tunnel and the vessel vision band — all bound under <c>IsLocalPilot</c> because they
    /// describe what the LOCAL CAMERA sees — a wake is a thing OTHER pilots see you leaving behind
    /// you, exactly like the vessel tail. So every vessel on every machine publishes one, and
    /// <c>VesselStatus.Speed</c> and <c>.Course</c> both replicate
    /// (<c>VesselController.n_Speed</c> / <c>n_Course</c>), which is what makes a remote replica's
    /// wake run on the same numbers its owner's does.</para>
    ///
    /// <para><b>The speed window is ABSOLUTE.</b> <c>PrismWakeConfigSO.StrengthForSpeed</c> takes a
    /// speed and nothing else, so the same speed on any hull leaves the same wake and a player
    /// learns the cue once — the same argument, and the same refusal to normalise against a
    /// vessel's own top speed, that Docs/SPEED_TUNNEL.md records. Most hulls cruise well below the
    /// engage speed, so a wake means "that ship is boosting" rather than "that ship exists", which
    /// is simultaneously what makes it worth looking at and what keeps the shared residency budget
    /// spent on the few ships that have one.</para>
    ///
    /// <para><b>The axis is the COURSE, reversed.</b> A wake is about the direction of TRAVEL, not
    /// the direction the nose is pointing: during a drift those differ, and it is the path the ship
    /// actually took that the ribbon was laid along. It is published from <c>Update</c> and the
    /// hull's POSITION is sampled at flush time in LateUpdate — a heading that is one frame stale
    /// is a fraction of a degree, where a position that is one frame stale is several units at
    /// these speeds.</para>
    ///
    /// <para><b>The phase is integrated here.</b> It advances at this ship's own wavenumber times
    /// its own speed (<see cref="PrismWake.PhaseRateFor"/>), so at a travel factor of 1 a crest
    /// sits STILL in the world and the ship flies out from under it. Integrating rather than
    /// evaluating <c>k·distance</c> is what lets the factor exist at all, and it is what keeps the
    /// wave continuous when the speed changes instead of jumping the whole train.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public class PrismWakeSource : MonoBehaviour
    {
        [Tooltip("The hull's radius in world units — the scale the wake's reach and train length " +
                 "are multiples of, so a big ship makes a big wake. 0 (the default) measures it " +
                 "ONCE from the hull's own renderers, the same measurement the occlusion corridor " +
                 "uses, and caches it.")]
        [Min(0f)]
        [SerializeField] float hullRadius = 0f;

        IVesselStatus _status;
        float _strength;
        float _phase;
        float _resolvedRadius;
        bool _radiusResolved;

        /// <summary>The radius the wake is using, once resolved; 0 before the first fast frame.</summary>
        public float HullRadius => _radiusResolved ? _resolvedRadius : hullRadius;

        /// <summary>The eased strength being published this frame, 0..1.</summary>
        public float Strength => _strength;

        /// <summary>The wave's phase in radians, as integrated so far.</summary>
        public float Phase => _phase;

        void Awake()
        {
            _status = GetComponent<IVesselStatus>();
        }

        void Update()
        {
            var config = PrismWake.Config;
            float speed = _status != null ? _status.Speed : 0f;

            float target = config.Enabled ? config.StrengthForSpeed(speed) : 0f;
            // Ease toward the window's answer rather than snapping to it. The window is already
            // smooth in speed, so this is the guard against speed JUMPING — a respawn, a teleport,
            // a vessel swap — putting a full wake on screen in one frame.
            float seconds = target > _strength ? config.EngageSeconds : config.ReleaseSeconds;
            _strength = seconds > 0f
                ? Mathf.MoveTowards(_strength, target, Time.deltaTime / seconds)
                : target;

            float radius = ResolveRadius();

            // The phase keeps advancing while the wake fades out, so a ship that slows down leaves
            // a wave that settles rather than one that freezes mid-crest and dims.
            if (radius > 0f)
                _phase += PrismWake.PhaseRateFor(radius, speed, config) * Time.deltaTime;

            // Radians are periodic, so the accumulator can be wrapped with no visible effect — and
            // must be, or a long match drifts it into the range where a float's spacing is coarser
            // than the wave and the ripple visibly quantises.
            if (_phase > TwoPi || _phase < -TwoPi)
                _phase = Mathf.Repeat(_phase, TwoPi);

            if (_strength <= 0.001f)
            {
                PrismWake.Clear(GetInstanceID());
                return;
            }

            Vector3 course = _status != null ? _status.Course : transform.forward;
            if (course.sqrMagnitude <= 1e-6f)
                course = transform.forward;

            // Behind the ship: the wake trails the direction of travel.
            PrismWake.Publish(GetInstanceID(), transform, radius, -course, _phase, _strength);
        }

        void OnDisable()
        {
            _strength = 0f;
            PrismWake.Clear(GetInstanceID());
        }

        const float TwoPi = 2f * Mathf.PI;

        float ResolveRadius()
        {
            if (_radiusResolved) return _resolvedRadius;

            _resolvedRadius = hullRadius > 0f
                ? hullRadius
                : PrismOcclusionCorridor.MeasureCircumscribedRadius(transform);
            _radiusResolved = _resolvedRadius > 0f;
            if (_radiusResolved)
            {
                CSDebug.LogVerbose(CSLogChannel.PrismRuntime,
                    $"[PrismWakeSource] {name}: wake radius {_resolvedRadius:0.00} u " +
                    (hullRadius > 0f ? "(authored)" : "(measured from the hull)"));
            }
            return _resolvedRadius;
        }
    }
}
