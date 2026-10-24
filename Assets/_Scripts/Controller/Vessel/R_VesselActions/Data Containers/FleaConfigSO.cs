using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Flea's numbers. Every field is a key of the Vessel Studio's <c>ricochet</c> kind (studio-kinds.js
    /// <c>K.ricochet.spec</c>), its tooltip the artifact's own label, its default the kind's <c>defaults</c>: the artifact is
    /// the source and <c>Tools/Build/studio_to_unity.py</c> writes these (/artifact-to-unity). Design: <c>R_VesselActions/FLEA.md</c>.
    /// </summary>
    [CreateAssetMenu(fileName = "FleaConfig", menuName = "ScriptableObjects/Vessel Actions/Flea Config")]
    public sealed class FleaConfigSO : ScriptableObject
    {
        [Header("Vessel Studio: ricochet (the Flea)")]
        [Tooltip("Crawl speed: speed it settles to between bounces (u/s)")]
        [SerializeField, Min(0f)] float crawl = 50f;
        [Tooltip("Perfect window: press RT within this many seconds of a contact for a perfect bounce")]
        [SerializeField, Min(0.01f)] float window = 0.18f;
        [Tooltip("Perfect gain: speed multiplier on a perfect bounce")]
        [SerializeField, Min(0f)] float perfectGain = 1.35f;
        [Tooltip("Dull bounce: speed multiplier on a bounce with no press")]
        [SerializeField, Min(0.01f)] float dullLoss = 0.85f;
        [Tooltip("Top speed: speed cap (u/s)")]
        [SerializeField, Min(0f)] float topSpeed = 260f;
        [Tooltip("Velocity grip: how fast the velocity turns toward the nose between bounces (per second): low = ballistic")]
        [SerializeField, Min(0f)] float grip = 2.2f;
        [Tooltip("Momentum fade: how fast speed above crawl fades (per second)")]
        [SerializeField, Min(0f)] float drag = 0.25f;
        [Tooltip("Hop charge time: seconds of LT crouch for a full hop")]
        [SerializeField, Min(0.01f)] float hopCharge = 0.6f;
        [Tooltip("The speed of a barely charged hop (u/s). The kind's hopMin; the page has no slider for it")]
        [SerializeField, Min(0f)] float hopMin = 60f;
        [Tooltip("Full hop: speed of a fully charged hop (u/s)")]
        [SerializeField, Min(0f)] float hopMax = 160f;

        [Header("Unity only (not in the artifact)")]
        [Tooltip("Seconds before the same prism can bounce the hull again. The page resolves one contact per pass; Unity's " +
                 "triggers can report one prism twice, so this keeps one contact one bounce.")]
        [SerializeField, Min(0f)] float samePrismGuard = 0.6f;

        public float SamePrismGuard => samePrismGuard;

        public FleaMath.Params Params => new FleaMath.Params
        {
            crawl = crawl, window = window, perfectGain = perfectGain, dullLoss = dullLoss, topSpeed = topSpeed,
            grip = grip, drag = drag, hopCharge = hopCharge, hopMin = hopMin, hopMax = hopMax,
        };
    }
}
