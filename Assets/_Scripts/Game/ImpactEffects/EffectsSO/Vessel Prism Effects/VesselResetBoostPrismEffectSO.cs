using System; // Required for Action
using UnityEngine;
using Obvious.Soap;
using CosmicShore.Core;

namespace CosmicShore.Game
{
    [CreateAssetMenu(fileName = "VesselResetBoostPrismEffect",
        menuName = "ScriptableObjects/Impact Effects/Vessel - Prism/VesselResetBoostPrismEffectSO")]
    public class VesselResetBoostPrismEffectSO : VesselPrismEffectSO
    {
        [Header("Shared Config")]
        [SerializeField] private ScriptableVariable<float> boostBaseMultiplier;
        [SerializeField] private ScriptableVariable<float> boostMaxMultiplier;

        [Header("Events")]
        [SerializeField] private ScriptableEventBoostChanged boostChanged;

        // [Visual Note] 1. New Event for the Tracker to listen to
        public static event Action<string> OnPrismCollision;

        public override void Execute(VesselImpactor impactor, PrismImpactor prismImpactee)
        {
            if (!impactor) return;

            // [Visual Note] 2. Fire the Streak Reset Event
            OnPrismCollision?.Invoke(impactor.Vessel.VesselStatus.PlayerName);

            // 3. Stats Logging (Optional, kept for backend data)
            if (StatsManager.Instance)
                StatsManager.Instance.ExecuteSkimmerShipCollision(impactor.Vessel.VesselStatus.PlayerName);

            // 4. Reset Boost Logic
            var status = impactor.Vessel.VesselStatus;
            var baseMult = boostBaseMultiplier != null ? boostBaseMultiplier.Value : 1f;
            float maxMult  = boostMaxMultiplier  != null ? boostMaxMultiplier.Value  : 5f;

            baseMult = Mathf.Max(0.0001f, baseMult);
            maxMult  = Mathf.Max(baseMult, maxMult);

            status.IsBoosting = false;
            status.BoostMultiplier = baseMult;

            boostChanged?.Raise(new BoostChangedPayload
            {
                BoostMultiplier = status.BoostMultiplier,
                MaxMultiplier = maxMult
            });
        }
    }
}