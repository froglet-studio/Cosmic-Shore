using CosmicShore.Game;
using UnityEngine;

[CreateAssetMenu(fileName = "ChargeBoostAction", menuName = "CosmicShore/Actions/Charge Boost")]
public class ChargeBoostActionSO : ShipActionSO
{
    [Header("Charge Boost Settings")]
    [SerializeField] float maxBoostMultiplier = 2f;
    [SerializeField] float maxNormalizedCharge = 1f;

    [Header("Timing (seconds)")]
    [SerializeField] float chargeTimeToFull = 2f;
    [SerializeField] float dischargeTimeToEmpty = 2f;

    [Tooltip("Tick cadence for UI/physics updates")]
    [SerializeField] float tickSeconds = 0.1f;

    [Header("Resource slot holding the charged units (0..maxNormalizedCharge)")]
    [SerializeField] int boostResourceIndex = 1;

    [Header("Optional Safety")]
    [SerializeField] float rechargeCooldownSeconds = 1f;

    [Header("Debug")]
    [SerializeField] bool verbose = false;

    public float MaxBoostMultiplier     => maxBoostMultiplier;
    public float MaxNormalizedCharge    => maxNormalizedCharge;
    public float ChargeTimeToFull       => chargeTimeToFull;
    public float DischargeTimeToEmpty   => dischargeTimeToEmpty;
    public float TickSeconds            => tickSeconds;
    public int BoostResourceIndex  => boostResourceIndex;
    public float RechargeCooldownSeconds=> rechargeCooldownSeconds;
    public bool Verbose                 => verbose;

    public override void StartAction(ActionExecutorRegistry execs, IVesselStatus vesselStatus)
        => execs?.Get<ChargeBoostActionExecutor>()?.BeginCharge(this, vesselStatus);

    public override void StopAction(ActionExecutorRegistry execs, IVesselStatus vesselStatus)
        => execs?.Get<ChargeBoostActionExecutor>()?.BeginDischarge(this, vesselStatus);
}