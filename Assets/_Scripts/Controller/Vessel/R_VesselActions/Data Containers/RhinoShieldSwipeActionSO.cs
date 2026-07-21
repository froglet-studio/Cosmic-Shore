using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Swordsman shield swipe for the Rhino: a trigger pull sweeps the ForceFieldSkimmer
    /// capsule (the "sword") through a wide yaw+roll arc, then settles it into a held
    /// stance; the local pilot's camera rotates to meet the stance at the animation's end
    /// point. Right swipe = rightward yaw + counterclockwise roll (from the pilot's seat);
    /// left is the mirror in both axes. Releasing the trigger returns sword and camera to
    /// center. Config only - all per-vessel runtime state lives in ShieldSwipeActionExecutor.
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
        [Tooltip("Peak yaw of the swipe arc before settling back to the stance.")]
        [SerializeField] float swipeYawDegrees = 90f;
        [Tooltip("Peak roll of the swipe arc before settling back to the stance.")]
        [SerializeField] float swipeRollDegrees = 90f;
        [Tooltip("Yaw of the held stance the sword settles into.")]
        [SerializeField] float restYawDegrees = 45f;
        [Tooltip("Roll of the held stance the sword settles into.")]
        [SerializeField] float restRollDegrees = 45f;

        [Header("Timing (seconds)")]
        [Tooltip("Sweep out to the peak angle.")]
        [SerializeField] float swipeOutSeconds = 0.18f;
        [Tooltip("Settle back from the peak to the stance. The camera arrives at the same moment.")]
        [SerializeField] float settleSeconds = 0.22f;
        [Tooltip("Return to center after the trigger is released.")]
        [SerializeField] float returnSeconds = 0.3f;

        [Header("Camera (local pilot only)")]
        [Tooltip("Camera yaw at the stance; rotates over the full swipe so it meets the sword as it settles.")]
        [SerializeField] float cameraYawDegrees = 45f;
        [Tooltip("Camera roll at the stance. Set 0 to keep the horizon level.")]
        [SerializeField] float cameraRollDegrees = 45f;

        /// <summary>+1 for a right swipe, -1 for a left swipe. Yaw uses this sign directly
        /// (positive about up = nose right); roll uses the negated sign (negative about
        /// forward = counterclockwise from the pilot's seat in Unity's left-handed space).</summary>
        public float DirectionSign => direction == SwipeDirection.Right ? 1f : -1f;

        public float SwipeYawDegrees => swipeYawDegrees;
        public float SwipeRollDegrees => swipeRollDegrees;
        public float RestYawDegrees => restYawDegrees;
        public float RestRollDegrees => restRollDegrees;
        public float SwipeOutSeconds => swipeOutSeconds;
        public float SettleSeconds => settleSeconds;
        public float ReturnSeconds => returnSeconds;
        public float CameraYawDegrees => cameraYawDegrees;
        public float CameraRollDegrees => cameraRollDegrees;

        public override void StartAction(ActionExecutorRegistry execs, IVesselStatus vesselStatus)
            => execs?.Get<ShieldSwipeActionExecutor>()?.BeginSwipe(this, vesselStatus);

        public override void StopAction(ActionExecutorRegistry execs, IVesselStatus vesselStatus)
            => execs?.Get<ShieldSwipeActionExecutor>()?.EndSwipe(this, vesselStatus);
    }
}
