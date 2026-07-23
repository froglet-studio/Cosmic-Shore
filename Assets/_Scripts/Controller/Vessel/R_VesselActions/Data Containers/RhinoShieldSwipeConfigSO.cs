using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Shared tuning for the Rhino shield swipe. One asset serves both trigger
    /// directions and the analog drive, so left/right can never drift apart.
    /// The analog triggers are reparameterized: the DIFFERENCE (right - left)
    /// drives the lateral swipe and the SUM drives the downward chop, so both
    /// triggers together hold the sword centered but fully down. The raised rest
    /// pose is authored on the skimmer transform in the Rhino prefab.
    /// </summary>
    [CreateAssetMenu(fileName = "RhinoShieldSwipeConfig", menuName = "ScriptableObjects/Vessel Actions/RhinoShieldSwipeConfigSO")]
    public class RhinoShieldSwipeConfigSO : ScriptableObject
    {
        [Header("Swipe (difference axis: right trigger - left trigger, -1..1)")]
        [Tooltip("Yaw at a full single-trigger pull. Positive = rightward for the right trigger.")]
        [SerializeField] float swipeYawDegrees = 90f;
        [Tooltip("Roll at a full single-trigger pull. Counterclockwise from the pilot's seat for the right trigger.")]
        [SerializeField] float swipeRollDegrees = 90f;

        [Header("Chop (sum axis: right trigger + left trigger, 0..2)")]
        [Tooltip("Downward pitch when BOTH triggers are fully pulled. A single full pull reaches half of this.")]
        [SerializeField] float chopPitchDegrees = 65f;

        [Header("Analog Tracking")]
        [Tooltip("Time constant for the pose tracking the physical triggers - the finger is the animation. Keep tiny; it only filters sensor jitter.")]
        [SerializeField] float analogSmoothingSeconds = 0.04f;

        [Header("Event Swing (binary inputs only: remote replay, touch)")]
        [Tooltip("Swing-out time for event-driven presses, whose targets snap instead of being pulled.")]
        [SerializeField] float swipeOutSeconds = 0.18f;
        [Tooltip("Return time after an event-driven release.")]
        [SerializeField] float returnSeconds = 0.3f;

        [Header("Energy Sword — gesture thresholds (RHINO_ENERGY_SWORD.md)")]
        [Tooltip("Difference magnitude (single-trigger pull) above which the blade is SLASHING — the " +
                 "gesture that damages/pops prisms (rate-limited by the slash cooldown).")]
        [SerializeField, Range(0f, 1f)] float slashTriggerThreshold = 0.4f;
        [Tooltip("Sum (both triggers) above which the blade is in the LOWER/CHOP stance — the energize " +
                 "gesture. Sum ranges 0..2, so ~1.5 requires both triggers meaningfully pulled.")]
        [SerializeField, Range(0f, 2f)] float stanceSumThreshold = 1.5f;
        [Tooltip("Difference magnitude below which the stance counts as centered (both triggers even). " +
                 "Keeps a lopsided pull from reading as the energize stance.")]
        [SerializeField, Range(0f, 1f)] float stanceCenterEpsilon = 0.4f;

        public float SwipeYawDegrees => swipeYawDegrees;
        public float SwipeRollDegrees => swipeRollDegrees;
        public float ChopPitchDegrees => chopPitchDegrees;
        public float AnalogSmoothingSeconds => analogSmoothingSeconds;
        public float SwipeOutSeconds => swipeOutSeconds;
        public float ReturnSeconds => returnSeconds;

        public float SlashTriggerThreshold => slashTriggerThreshold;
        public float StanceSumThreshold => stanceSumThreshold;
        public float StanceCenterEpsilon => stanceCenterEpsilon;
    }
}
