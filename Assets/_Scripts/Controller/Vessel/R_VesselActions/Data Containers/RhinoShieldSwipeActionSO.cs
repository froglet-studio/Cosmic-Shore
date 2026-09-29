using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Swordsman shield swipe binding for one Rhino trigger. On analog devices the
    /// executor reads the triggers directly every frame (difference = lateral swipe,
    /// sum = downward chop) and these press/release events only replicate the swipe to
    /// remote peers; on binary devices (touch, future bindings) the press itself drives
    /// a full swipe. Right = rightward yaw + counterclockwise roll (from the pilot's
    /// seat); left mirrors both axes. All tuning lives in RhinoShieldSwipeConfigSO.
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

        /// <summary>+1 for a right swipe, -1 for a left swipe. Both axes use this sign
        /// directly: positive about up = nose right, and positive about forward =
        /// counterclockwise from the pilot's seat (AngleAxis(+90, forward) maps right to up).</summary>
        public float DirectionSign => direction == SwipeDirection.Right ? 1f : -1f;

        public override void StartAction(ActionExecutorRegistry execs, IVesselStatus vesselStatus)
            => execs?.Get<ShieldSwipeActionExecutor>()?.BeginSwipe(this, vesselStatus);

        public override void StopAction(ActionExecutorRegistry execs, IVesselStatus vesselStatus)
            => execs?.Get<ShieldSwipeActionExecutor>()?.EndSwipe(this, vesselStatus);
    }
}
