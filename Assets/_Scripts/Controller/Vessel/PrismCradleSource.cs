using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Urchin's half of the CRADLE (Docs/PRISM_ANIMATION.md §4.7.2): while this vessel is
    /// RIDING a prismscape — attached, not launched off a ribbon's end, not in free flight — it
    /// reports its hull to <see cref="PrismCradle"/> every frame, and the prism triangles around
    /// it wrap onto the hull on the GPU.
    ///
    /// <para><b>Ensured, not authored.</b> <see cref="GunVesselTransformer.Initialize"/> adds this
    /// component when the prefab does not carry one, so the cradle cannot be omitted from an
    /// Urchin by wiring. The transformer is the one thing that knows whether the vessel is
    /// riding (<see cref="GunVesselTransformer.IsRiding"/>): a launched hull has let go, and a
    /// hull whose ground was destroyed under it has fallen back to free flight, so both read as
    /// not riding here with no extra state.</para>
    ///
    /// <para><b>The radius is a constant.</b> The Urchin is spherical to a good approximation.
    /// <see cref="hullRadius"/> is either authored here or measured ONCE from the hull's own
    /// renderers, the same measurement the occlusion corridor sizes itself with, and cached — it
    /// is never recomputed per frame. The shader takes it beside the centre because a slot is one
    /// float4, and uses it to put the nearest triangle's centroid exactly on the hull's surface.</para>
    ///
    /// <para><b>Strength is eased, never switched.</b> A bare on/off would snap every triangle in the
    /// band on one frame; the strength ramps over the config's engage/release seconds instead,
    /// so a launch reads as the mass releasing the hull. Reported from <c>Update</c> because the
    /// publisher flushes in <c>LateUpdate</c> and reads the hull's LIVE position there — a hull
    /// grinding at 300 u/s must never be published a frame stale.</para>
    ///
    /// <para><b>Machine scope.</b> A vessel's ride is simulated on the machine that owns it (the
    /// owner, or the host for an AI): a remote replica's transformer is inactive and never
    /// attaches, so a remote pilot sees no cradle around another player's Urchin. The ride state
    /// does not replicate today; recorded in the doc, not hidden.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public class PrismCradleSource : MonoBehaviour
    {
        [Tooltip("The hull's radius in world units — the sphere the cradled faces settle onto. " +
                 "0 (the default) measures it ONCE from the hull's own renderers on the first ride, " +
                 "the same measurement the occlusion corridor uses, and caches it.")]
        [Min(0f)]
        [SerializeField] float hullRadius = 0f;

        GunVesselTransformer _rider;
        float _strength;
        float _resolvedRadius;
        bool _radiusResolved;

        /// <summary>The radius the cradle is using, once resolved; 0 before the first ride.</summary>
        public float HullRadius => _radiusResolved ? _resolvedRadius : hullRadius;

        /// <summary>The eased strength being published this frame, 0..1.</summary>
        public float Strength => _strength;

        void Awake()
        {
            _rider = GetComponent<GunVesselTransformer>();
        }

        void Update()
        {
            bool riding = _rider != null && _rider.IsRiding;
            var config = PrismCradle.Config;

            float target = riding ? 1f : 0f;
            float seconds = riding ? config.EngageSeconds : config.ReleaseSeconds;
            _strength = seconds > 0f
                ? Mathf.MoveTowards(_strength, target, Time.deltaTime / seconds)
                : target;

            if (_strength <= 0.001f)
            {
                PrismCradle.Clear(GetInstanceID());
                return;
            }

            PrismCradle.Publish(GetInstanceID(), transform, ResolveRadius(), _strength);
        }

        void OnDisable()
        {
            _strength = 0f;
            PrismCradle.Clear(GetInstanceID());
        }

        float ResolveRadius()
        {
            if (_radiusResolved) return _resolvedRadius;

            _resolvedRadius = hullRadius > 0f
                ? hullRadius
                : PrismOcclusionCorridor.MeasureCircumscribedRadius(transform);
            _radiusResolved = _resolvedRadius > 0f;
            if (_radiusResolved)
            {
                CSDebug.LogVerbose(CSLogChannel.PrismscapeRide,
                    $"[PrismCradleSource] {name}: cradle radius {_resolvedRadius:0.00} u " +
                    (hullRadius > 0f ? "(authored)" : "(measured from the hull)"));
            }
            return _resolvedRadius;
        }
    }
}
