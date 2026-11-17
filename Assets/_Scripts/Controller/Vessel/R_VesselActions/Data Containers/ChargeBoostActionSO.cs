using CosmicShore.Gameplay;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    [CreateAssetMenu(fileName = "ChargeBoostAction", menuName = "ScriptableObjects/Actions/Charge Boost")]
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

        [Header("Elemental (Time)")]
        [Tooltip("TIME -> charge RATE: multiplier on the fill rate at Time level 10 (1 at the " +
                 "resting level, extrapolating into the deficit band so debuffed Time fills " +
                 "slower). Authored HERE rather than through the map's generic multiplier, which " +
                 "VesselTransformer already consumes for boost SPEED - reading both would drive " +
                 "two unrelated parameters off one number.")]
        [SerializeField] float chargeRateMultiplierAtFullTime = 1.5f;
        [Tooltip("TIME -> charge RATE at the RESTING level (Time 0). 1 keeps the old shape (Time only " +
                 "adds); above 1 the fill is faster from the first second, so the lever reaches a " +
                 "hull that starts a race at Time 0 (the Dolphin's Regatta card start, 2026-11-17).")]
        [SerializeField] float chargeRateMultiplierAtRestTime = 1f;
        [Tooltip("Floor for the Time charge-rate multiplier so a deficit can never stall the fill.")]
        [SerializeField] float minChargeRateMultiplier = 0.5f;

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
        public float ChargeRateMultiplierAtFullTime => chargeRateMultiplierAtFullTime;
        public float ChargeRateMultiplierAtRestTime => chargeRateMultiplierAtRestTime;
        public float MinChargeRateMultiplier => Mathf.Max(0.01f, minChargeRateMultiplier);
        public float RechargeCooldownSeconds=> rechargeCooldownSeconds;
        public bool Verbose                 => verbose;

        public override void StartAction(ActionExecutorRegistry execs, IVesselStatus vesselStatus)
            => execs?.Get<ChargeBoostActionExecutor>()?.BeginCharge(this, vesselStatus);

        public override void StopAction(ActionExecutorRegistry execs, IVesselStatus vesselStatus)
            => execs?.Get<ChargeBoostActionExecutor>()?.BeginDischarge(this, vesselStatus);
    }
}
