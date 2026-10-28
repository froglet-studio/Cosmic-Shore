using CosmicShore.Data;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Tiger Moth's <b>Drop</b> — TIME. A tiger moth escapes a bat by shutting its wings and dropping. Hold the
    /// trigger and the hull shuts its wings and falls belly-first along its own down (the way its dust rains),
    /// without turning; the chase camera swings up over it so the fall reads as flying forward. Let go and it
    /// lands: the hull pitches 90 degrees nose-down into the frame the camera is already in and flies straight on
    /// along the line it fell. Design record: <c>R_VesselActions/TIGER_MOTH.md</c>.
    ///
    /// <para>Chosen over the Butterfly's Fold by a blind A/B (Garrett, 2026-10-28,
    /// <c>Docs/Studios/ABTests/README.md</c> "Butterfly fold or drop v1"), with one change from the tested page: the
    /// camera does not level back out after the landing, because the hull turns to meet it instead.</para>
    ///
    /// <para><b>Time's dials are the Fold's</b>, carried over unchanged: the continuous dial is the RECHARGE
    /// (30 s at rest, ×0.5 at Time 10, never worse than ×2), and Time 5 — "Far Drop" — doubles the longest drop,
    /// as Far Fold doubled the reach. Speed and reach default to the Fold's (900 u/s, 1800 u), which is what the
    /// A/B measured: a held second takes you as far either way.</para>
    ///
    /// <para>The asset is SHARED by every Tiger Moth in a match and holds no per-vessel state; the executor owns
    /// the drop.</para>
    /// </summary>
    [CreateAssetMenu(fileName = "TigerMothDropAction",
        menuName = "ScriptableObjects/Vessel Actions/Tiger Moth Drop")]
    public class TigerMothDropActionSO : ShipActionSO
    {
        [Header("Recharge")]
        [Tooltip("Seconds before the Drop can be used again, at the resting Time level. The Fold's 30 s, carried " +
                 "over: the drop crosses a cell, so it is a decision rather than a movement option.")]
        [SerializeField, Min(0f)] float cooldownSeconds = 30f;

        [Tooltip("TIME -> recharge: the cooldown MULTIPLIER at Time level 10. 0.5 halves the wait.")]
        [SerializeField] float cooldownMultiplierAtFullTime = 0.5f;

        [Tooltip("Ceiling on the cooldown multiplier: the worst a Time DEFICIT can make the recharge, so a debuffed " +
                 "Tiger Moth is slowed down rather than losing the ability.")]
        [SerializeField, Min(1f)] float maxCooldownMultiplier = 2f;

        [Header("Drop")]
        [Tooltip("How fast the hull falls belly-first while the trigger is held, world units per second. 900 is the " +
                 "Fold's reach speed, so a held second covers the same ground as a fold did.")]
        [SerializeField, Min(1f)] float dropSpeed = 900f;

        [Tooltip("Longest drop at the resting Time level, world units: the hull lands on its own here even if the " +
                 "trigger is still held. 1800 is the Fold's reach.")]
        [SerializeField, Min(1f)] float dropRange = 1800f;

        [Tooltip("TIME level-5 'Far Drop': multiplies the longest drop.")]
        [SerializeField, Min(1f)] float upgradeRangeMultiplier = 2f;

        [Tooltip("Seconds from the press to full drop speed.")]
        [SerializeField, Min(0f)] float dropEaseSeconds = 0.12f;

        [Header("Landing")]
        [Tooltip("Seconds from letting go to landed. Over this time the hull pitches 90 degrees nose-down into the " +
                 "line it fell on, and its speed along that line eases from the fall speed back to the cruise it " +
                 "had before the press.")]
        [SerializeField, Min(0.01f)] float landSeconds = 0.25f;

        [Header("Camera")]
        [Tooltip("Seconds for the chase camera to swing up over the hull after the press, so down reads as forward. " +
                 "There is no swing back: the landing turns the hull into this frame instead.")]
        [SerializeField, Min(0f)] float cameraEnterSeconds = 0.35f;

        [Header("Wake")]
        [Tooltip("Lay the wake through the drop (a vertical curtain). Off: the pen is up for the drop, as the Fold's " +
                 "was for its hold; the wake comes back on at the landing.")]
        [SerializeField] bool layWakeWhileDropping;

        public float CooldownSeconds => Mathf.Max(0f, cooldownSeconds);
        public float DropSpeed => dropSpeed;
        public float DropRange => dropRange;
        public float DropEaseSeconds => dropEaseSeconds;
        public float LandSeconds => landSeconds;
        public float CameraEnterSeconds => cameraEnterSeconds;

        /// <summary>The recharge at the live TIME level, read at use time (a crystal collected mid-cooldown should
        /// shorten the wait being served).</summary>
        public float ResolveCooldown(IVesselStatus status)
        {
            float multiplier = ElementalScaling.Multiplier(
                status, Element.Time, cooldownMultiplierAtFullTime, cooldownMultiplierAtFullTime);
            return CooldownSeconds * Mathf.Min(multiplier, maxCooldownMultiplier);
        }

        /// <summary>The longest drop for THIS press, including "Far Drop". Gated on the REPLICATED unlock bit: the
        /// drop moves a pose every peer adopts.</summary>
        public float ResolveRange(IVesselStatus status)
        {
            float range = DropRange;
            var abilities = status?.ElementalAbilityHandler;
            if (abilities != null && abilities.IsUpgradeActive(Element.Time))
                range *= Mathf.Max(1f, upgradeRangeMultiplier);
            return range;
        }

        public bool LayWakeWhileDropping => layWakeWhileDropping;

        public override void StartAction(ActionExecutorRegistry execs, IVesselStatus vesselStatus)
            => execs?.Get<TigerMothDropExecutor>()?.Engage(this, vesselStatus);

        public override void StopAction(ActionExecutorRegistry execs, IVesselStatus vesselStatus)
            => execs?.Get<TigerMothDropExecutor>()?.Release(this, vesselStatus);
    }
}
