using UnityEngine;
using CosmicShore.Data;
using CosmicShore.Gameplay;


namespace CosmicShore.Gameplay
{
    [CreateAssetMenu(fileName = "YawsteryAction", menuName = "ScriptableObjects/Vessel Actions/Yawstery (Hold-To-Yaw)")]
    public class YawsteryActionSO : ShipActionSO
    {
        public enum Direction { Left = -1, Right = 1 }

        [Header("Steer Direction")]
        [SerializeField] Direction steerDirection = Direction.Left;

        [Header("Steer Response")]
        [Tooltip("Max yaw speed (deg/sec) when fully ramped in.")]
        [SerializeField] float maxYawDegPerSec = 120f;
        [Tooltip("Time to ramp from 0 → 1 intensity while holding.")]
        [SerializeField] float rampInSeconds = 0.35f;
        [Tooltip("Time to ramp from current intensity → 0 when released.")]
        [SerializeField] float rampOutSeconds = 0.25f;

        [Header("Optional: Speed Coupling")]
        [SerializeField] float speedScale = 1.0f;
        [SerializeField, Range(0f, 2f)] float speedExp = 0.25f;

        [Header("Lock-to-Angle (optional)")]
        [SerializeField] bool lockToAngle = false;
        [SerializeField, Min(1f)] float maxTurnDegrees = 45f;

        [Header("Elemental")]
        [Tooltip("Element scaling on the turn rate. On the Manta's two Yawstery assets it is TIME, x1 at rest -> x1.5 at " +
                 "Time 10 (floor x0.5): a Time-heavy Manta whips its flat turns round faster, a tempo dial beside the cruise " +
                 "and soar speed Time already raises (2026-11-18, the element-placeholder pass). Disabled = a fixed rate.")]
        [SerializeField] ElementalFloat turnRateMultiplier = new(1f);

        [Header("Trail")]
        [Tooltip("Drive the vessel's turn-trail state from this turn's intensity — the outer-" +
                 "lane prism flare, and the Mass-5 Shielded Turn Trails window " +
                 "(VesselPrismController.SetTurnTrail). The Manta's Yawstery assets author ON.")]
        [SerializeField] bool driveTrailFlare = false;

        [Header("Animation (future)")]
        [SerializeField] string animatorParamFloat = "";
        [SerializeField] string animatorParamTriggerStart = "";
        [SerializeField] string animatorParamTriggerEnd = "";

        public Direction Steer => steerDirection;
        public float MaxYawDegPerSec => maxYawDegPerSec;
        public float RampInSeconds => rampInSeconds;
        public float RampOutSeconds => rampOutSeconds;
        public float SpeedScale => speedScale;
        public float SpeedExp => speedExp;

        public bool LockToAngle => lockToAngle;
        public float MaxTurnDegrees => maxTurnDegrees;
        /// <summary>The live element multiplier on the turn rate; exactly 1 while disabled.</summary>
        public float TurnRateMultiplier(IVesselStatus status)
            => turnRateMultiplier.EvaluateLive(status);
        public bool DriveTrailFlare => driveTrailFlare;

        public string AnimFloat => animatorParamFloat;
        public string AnimStart => animatorParamTriggerStart;
        public string AnimEnd => animatorParamTriggerEnd;

        public override void StartAction(ActionExecutorRegistry execs, IVesselStatus vesselStatus)
            => execs?.Get<YawsteryActionExecutor>()?.Begin(this, vesselStatus);

        public override void StopAction(ActionExecutorRegistry execs, IVesselStatus vesselStatus)
            => execs?.Get<YawsteryActionExecutor>()?.End();
    }
}
