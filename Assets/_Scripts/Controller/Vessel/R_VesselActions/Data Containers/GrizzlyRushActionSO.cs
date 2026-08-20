using CosmicShore.Gameplay;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Grizzly Rush — Charge Forward (Button2). See GRIZZLY_RUSH.md.
    /// Spend Energy to charge forward with a burst of momentum. Multiple charges.
    /// Element link: Time reduces the Energy cost; Time 5 = Vector Control (steer mid-rush).
    /// </summary>
    [CreateAssetMenu(fileName = "GrizzlyRushAction", menuName = "ScriptableObjects/Vessel Actions/Grizzly Rush")]
    public class GrizzlyRushActionSO : ShipActionSO
    {
        [Header("Burst")]
        [SerializeField, Tooltip("Forward velocity added by one rush (units/second).")]
        float magnitude = 60f;
        [SerializeField, Tooltip("Seconds the burst persists (cosine ease-out).")]
        float duration = 1f;

        [Header("Economy")]
        [SerializeField, Tooltip("Index of the Grizzly's single Energy resource.")]
        int energyIndex = 0;
        [SerializeField, Tooltip("Base Energy cost per rush, before Time scaling.")]
        float energyCost = 0.25f;
        [SerializeField, Tooltip("Time element multiplier on the cost at level 10 (below 1 = cheaper).")]
        float timeCostAtFull = 0.4f;
        [SerializeField] float timeCostMinMul = 0.25f;

        [Header("Charges")]
        [SerializeField, Tooltip("Maximum banked rush charges.")]
        int maxCharges = 3;
        [SerializeField, Tooltip("Seconds to refill one spent charge.")]
        float chargeRefillSeconds = 6f;
        [SerializeField, Tooltip("Minimum seconds between consecutive rushes.")]
        float cooldownSeconds = 2f;

        [Header("Vector Control (Time 5)")]
        [SerializeField, Tooltip("Sub-pulses the burst is split into when steering mid-rush is unlocked.")]
        int steeringPulses = 4;

        public float Magnitude => magnitude;
        public float Duration => duration;
        public int EnergyIndex => energyIndex;
        public float EnergyCost => energyCost;
        public float TimeCostAtFull => timeCostAtFull;
        public float TimeCostMinMul => timeCostMinMul;
        public int MaxCharges => maxCharges;
        public float ChargeRefillSeconds => chargeRefillSeconds;
        public float CooldownSeconds => cooldownSeconds;
        public int SteeringPulses => steeringPulses;

        public override void StartAction(ActionExecutorRegistry execs, IVesselStatus vesselStatus)
        {
            var exec = execs ? execs.Get<GrizzlyRushActionExecutor>() : null;
            if (exec) exec.TryRush(this, vesselStatus);
        }

        public override void StopAction(ActionExecutorRegistry execs, IVesselStatus vesselStatus)
        {
            // Rush is fire-and-forget.
        }
    }
}
