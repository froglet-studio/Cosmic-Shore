using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(fileName = "VesselResetBoostPrismEffect", menuName = "ScriptableObjects/Impact Effects/Vessel - Prism/VesselResetBoostPrismEffectSO")]
    public class VesselResetBoostPrismEffectSO
        : VesselPrismEffectSO // Assuming you have this base class
    {
        public override void Execute(VesselImpactor impactor, PrismImpactor prismImpactee)
        {
            var vesselStatus = impactor.Vessel.VesselStatus;

            // Reset boost multiplier to default
            vesselStatus.BoostMultiplier = 1f;
        }
    }
}