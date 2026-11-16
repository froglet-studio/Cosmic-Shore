using CosmicShore.Gameplay;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Grizzly Rush — the RAM (Button2). See GRIZZLY_RUSH.md.
    /// Spend Energy to charge forward with a burst of momentum; for the burst's length the hull is a battering ram that
    /// smashes every rival prism it ploughs into (plain and shielded; super-shielded mass deflects it). Banked charges.
    ///
    /// <para><b>Time is how often you can ram</b> (2026-11-16, the weakest-abilities pass: Time only made an invisible
    /// energy cost cheaper). One dial, <see cref="refillSeconds"/>: a spent charge comes back in 6 s at rest, 2.5 s at
    /// Time 10. Time 5 = Vector Control: the ram follows the nose, so a turn mid-rush carves a curved swathe.</para>
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
        [SerializeField, Tooltip("Energy cost per rush.")]
        float energyCost = 0.25f;

        [Header("The ram")]
        [SerializeField, Tooltip("While the burst lasts, rival prisms within this distance of the hull are smashed, u.")]
        float ramRadius = 16f;
        [SerializeField, Tooltip("Seconds between two ram sweeps while the burst lasts.")]
        float ramTickSeconds = 0.05f;
        [SerializeField, Tooltip("The kick a smashed prism's debris takes along the rush, u/s.")]
        float ramImpactSpeed = 60f;

        [Header("Charges (Time)")]
        [SerializeField, Tooltip("Maximum banked rush charges.")]
        int maxCharges = 3;
        [SerializeField, Tooltip("Time: seconds to refill one spent charge (6 at rest -> 2.5 at level 10; slower in " +
                                 "the deficit band, never under 1).")]
        ElementalFloat refillSeconds = ElementalFloat.Multiplier(6f, 2.5f, Element.Time, 1f);
        [SerializeField, Tooltip("Minimum seconds between consecutive rushes.")]
        float cooldownSeconds = 2f;

        [Header("Vector Control (Time 5)")]
        [SerializeField, Tooltip("Sub-pulses the burst is split into when steering mid-rush is unlocked.")]
        int steeringPulses = 4;

        public float Magnitude => magnitude;
        public float Duration => duration;
        public int EnergyIndex => energyIndex;
        public float EnergyCost => energyCost;
        public float RamRadius => ramRadius;
        public float RamTickSeconds => ramTickSeconds;
        public float RamImpactSpeed => ramImpactSpeed;
        public int MaxCharges => maxCharges;
        /// <summary>Seconds to refill one spent charge at this vessel's live Time level.</summary>
        public float RefillSecondsFor(IVesselStatus status) => Mathf.Max(1f, refillSeconds.EvaluateLive(status));
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
