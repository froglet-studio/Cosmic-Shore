using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Swordsman shield swipe for the Rhino: a trigger pull sweeps the ForceFieldSkimmer
    /// capsule (the "sword") through a wide yaw+roll arc and holds it there while the
    /// trigger is held; releasing returns the sword to center. Right swipe = rightward yaw
    /// + counterclockwise roll (from the pilot's seat); left is the mirror in both axes.
    /// Config only - all per-vessel runtime state lives in ShieldSwipeActionExecutor.
    /// </summary>
    [CreateAssetMenu(fileName = "RhinoShieldSwipeAction", menuName = "ScriptableObjects/Vessel Actions/RhinoShieldSwipeActionSO")]
    public class RhinoShieldSwipeActionSO : ShipActionSO
    {
        public enum SwipeDirection
        {
            Right = 0,
            Left = 1
        }

        [Header("Direction")]
        [Tooltip("Right = rightward yaw + counterclockwise roll; Left mirrors both axes.")]
        [SerializeField] SwipeDirection direction = SwipeDirection.Right;

        [Header("Shield Sweep (degrees, about the shield parent's up/forward axes)")]
        [Tooltip("Yaw of the held stance - the sword sweeps here and stays while the trigger is held.")]
        [SerializeField] float swipeYawDegrees = 90f;
        [Tooltip("Roll of the held stance - the sword sweeps here and stays while the trigger is held.")]
        [SerializeField] float swipeRollDegrees = 90f;

        [Header("Timing (seconds)")]
        [Tooltip("Sweep out to the full stance.")]
        [SerializeField] float swipeOutSeconds = 0.18f;
        [Tooltip("Return to center after the trigger is released.")]
        [SerializeField] float returnSeconds = 0.3f;

        /// <summary>+1 for a right swipe, -1 for a left swipe. Both axes use this sign
        /// directly: positive about up = nose right, and positive about forward =
        /// counterclockwise from the pilot's seat (AngleAxis(+90, forward) maps right to up).</summary>
        public float DirectionSign => direction == SwipeDirection.Right ? 1f : -1f;

        public float SwipeYawDegrees => swipeYawDegrees;
        public float SwipeRollDegrees => swipeRollDegrees;
        public float SwipeOutSeconds => swipeOutSeconds;
        public float ReturnSeconds => returnSeconds;

        public override void StartAction(ActionExecutorRegistry execs, IVesselStatus vesselStatus)
            => execs?.Get<ShieldSwipeActionExecutor>()?.BeginSwipe(this, vesselStatus);

        public override void StopAction(ActionExecutorRegistry execs, IVesselStatus vesselStatus)
            => execs?.Get<ShieldSwipeActionExecutor>()?.EndSwipe(this, vesselStatus);
    }
}
