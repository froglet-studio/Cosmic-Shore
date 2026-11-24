using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The hull-hit feel for a hull whose VERB is contact (a rammer: the Rhino ploughs every prism it hits aside).
    /// Where every other hull's body-into-prism is a mistake and plays the punish thud (<see cref="VesselHapticsByPrismEffectSO"/>),
    /// a rammer is doing what it is for, so it plays the REWARD feel instead: the skim pulse, at one fixed strength
    /// (Caleb, 2026-11-12: "vessels that want contact should feel rewarded for doing the thing they want, not
    /// punished. Not even punished lightly."). A rammer's container carries this INSTEAD of the punish effect, never
    /// beside it. Local human pilot only.
    ///
    /// <para>Spaced to at least the skim clip's length (<see cref="minIntervalSeconds"/>), because a plough through a
    /// wall lands many contacts a second, and pulses closer than the 70 ms clip flatten into a steady hum (the Manta's
    /// minutes-long rumble, Docs/HAPTICS.md). Shield and danger tiers are not read yet: whether a rammer's reward grows
    /// on armour is an open decision.</para>
    /// </summary>
    [CreateAssetMenu(fileName = "VesselRamRewardByPrismEffect",
        menuName = "ScriptableObjects/Impact Effects/Vessel - Prism/VesselRamRewardByPrismEffectSO")]
    public class VesselRamRewardByPrismEffectSO : VesselPrismEffectSO
    {
        [Header("Reward pulse")]
        [Tooltip("Skim pulse strength for every hull contact (0..1). One fixed value for now; the skimmers' pulse runs " +
                 "0.35 (sphere edge) to 1 (dead centre).")]
        [Range(0f, 1f)] [SerializeField] float strength = 0.7f;

        [Tooltip("Seconds between reward pulses. Keep at or above the skim clip (0.07 s) or a dense plough becomes a hum.")]
        [Min(0.07f)] [SerializeField] float minIntervalSeconds = 0.1f;

        // One local human pilot per machine, so one clock is enough.
        static float s_lastPulseTime = -999f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => s_lastPulseTime = -999f;

        public override void Execute(VesselImpactor vesselImpactor, PrismImpactor prismImpactee)
        {
            var status = vesselImpactor.Vessel.VesselStatus;
            if (status == null || status.Player == null || !status.IsLocalUser || status.AutoPilotEnabled) return;

            float now = Time.unscaledTime;
            if (now - s_lastPulseTime < minIntervalSeconds) return;
            s_lastPulseTime = now;

            HapticController.PlaySkim(strength);
        }
    }
}
