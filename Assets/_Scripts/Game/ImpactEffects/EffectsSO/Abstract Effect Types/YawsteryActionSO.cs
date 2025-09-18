using UnityEngine;
using CosmicShore.Core;
using CosmicShore.Game;

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

    public string AnimFloat => animatorParamFloat;
    public string AnimStart => animatorParamTriggerStart;
    public string AnimEnd => animatorParamTriggerEnd;

    public override void StartAction(ActionExecutorRegistry execs)
        => execs?.Get<YawsteryActionExecutor>()?.Begin(this, ShipStatus);

    public override void StopAction(ActionExecutorRegistry execs)
        => execs?.Get<YawsteryActionExecutor>()?.End();
}
