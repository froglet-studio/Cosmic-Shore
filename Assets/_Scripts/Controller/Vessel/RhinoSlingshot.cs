using CosmicShore.Data;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Rhino's TIME level-5, <b>Slingshot</b> (2026-11-19, Garrett: "give it more interesting abilities when it is
    /// upgraded"). The Rhino flies with inertia (<see cref="VesselTransformer"/>'s inertial grip): the nose turns a
    /// little sharper than the travel does, so every hard turn is a short oversteer slide. With Time's upgrade active,
    /// coming OUT of a slide flings the hull forward: when the slip (nose vs travel) has passed
    /// <see cref="entrySlipDegrees"/> and falls back under <see cref="exitSlipDegrees"/>, the hull gains speed in
    /// proportion to the deepest slip of that slide, up to <see cref="maxKickShare"/> of its speed and never past its
    /// ramp's top speed. The high-inertia ramp then holds that speed for seconds instead of dropping it.
    ///
    /// <para>Sits on the vessel ROOT beside <see cref="VesselStatus"/> and runs wherever the transformer moves the hull,
    /// reading the replicated upgrade bit (<see cref="R_VesselElementalAbilityHandler.IsUpgradeActive"/>), as the ramp
    /// and the plough do. Design: <c>R_VesselActions/RHINO_RAMP_BOOST.md</c> § "Inertia and Slingshot".</para>
    /// </summary>
    public sealed class RhinoSlingshot : MonoBehaviour
    {
        [Tooltip("A slide starts when the nose is this many degrees off the travel.")]
        [SerializeField, Range(1f, 90f)] float entrySlipDegrees = 12f;

        [Tooltip("The slide ends (and flings) when the slip falls back under this many degrees.")]
        [SerializeField, Range(0f, 45f)] float exitSlipDegrees = 4f;

        [Tooltip("The deepest slip that earns the whole kick, degrees.")]
        [SerializeField, Range(2f, 90f)] float fullSlipDegrees = 35f;

        [Tooltip("The whole kick, as a share of the speed the hull has when it comes out of the slide.")]
        [SerializeField, Range(0f, 1f)] float maxKickShare = 0.3f;

        [Tooltip("Ceiling on one kick, u/s.")]
        [SerializeField, Min(0f)] float maxKick = 350f;

        [Tooltip("No fling below this speed, u/s (a crawl does not slingshot).")]
        [SerializeField, Min(0f)] float minSpeed = 80f;

        VesselStatus _status;
        ITopSpeedSource _topSpeed;
        float _peakSlip;
        bool _inSlide;

        /// <summary>Raised on the frame a slide flings, with the kick in u/s (the HUD's Time card can flash on it).</summary>
        public event System.Action<float> Flung;

        /// <summary>The deepest slip of the slide in progress, degrees (0 outside a slide).</summary>
        public float PeakSlip => _inSlide ? _peakSlip : 0f;

        void Awake() => _status = GetComponent<VesselStatus>();

        void Update()
        {
            if (_status == null) return;
            var transformer = _status.VesselTransformer;
            if (transformer == null || !transformer.HasInertialGrip) { _inSlide = false; return; }

            float slip = transformer.SlipDegrees;
            if (!_inSlide)
            {
                if (slip < entrySlipDegrees) return;
                _inSlide = true;
                _peakSlip = slip;
                return;
            }

            _peakSlip = Mathf.Max(_peakSlip, slip);
            if (slip > exitSlipDegrees) return;

            _inSlide = false;
            if (!Unlocked()) return;

            float speed = transformer.TrackedSpeed;
            float kick = Kick(_peakSlip, speed, entrySlipDegrees, fullSlipDegrees, maxKickShare, maxKick, minSpeed);
            if (kick <= 0f) return;

            _topSpeed ??= GetComponentInChildren<ITopSpeedSource>(true);
            float ceiling = _topSpeed != null && _topSpeed.TopSpeed > 0f ? _topSpeed.TopSpeed : speed + kick;
            transformer.KickSpeed(kick, ceiling);
            Flung?.Invoke(kick);
        }

        bool Unlocked()
        {
            var abilities = _status.ElementalAbilityHandler;
            // The Time level-5 bit, spelled out so the element table attributes the gate.
            return abilities && abilities.IsUpgradeActive(Element.Time);
        }

        /// <summary>
        /// The fling for a slide whose deepest slip was <paramref name="peakSlip"/>, coming out at
        /// <paramref name="speed"/>: none below <paramref name="minSpeed"/> or a slip under <paramref name="entry"/>,
        /// rising linearly to <paramref name="share"/> of the speed (at most <paramref name="cap"/>) at
        /// <paramref name="full"/> degrees and above. Pure, for the tests.
        /// </summary>
        public static float Kick(float peakSlip, float speed, float entry, float full, float share, float cap, float minSpeed)
        {
            if (speed < minSpeed || peakSlip < entry) return 0f;
            float depth = full > entry ? Mathf.Clamp01((peakSlip - entry) / (full - entry)) : 1f;
            return Mathf.Min(cap, speed * share) * depth;
        }
    }
}
