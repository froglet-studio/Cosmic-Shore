using CosmicShore.Game.IO;
using UnityEngine;
using CosmicShore.Game.ImpactEffects.EffectsSO.AbstractEffectTypes;
using CosmicShore.Game.ImpactEffects.EffectsSO.Helpers;
using CosmicShore.Game.ImpactEffects.Impactors;
using CosmicShore.Game.Ship;
using CosmicShore.Models.Enums;
using CosmicShore.Utility.Effects;
namespace CosmicShore.Game.ImpactEffects.EffectsSO.VesselPrismEffects
{
    [CreateAssetMenu(fileName = "VesselHapticsByPrismEffect",
        menuName = "ScriptableObjects/Impact Effects/Vessel - Prism/VesselHapticsByPrismEffectSO")]
    public class VesselHapticsByPrismEffectSO : VesselPrismEffectSO
    {
        [SerializeField] HapticSpec _haptic;

        public override void Execute(VesselImpactor vesselImpactor, PrismImpactor prismImpactee)
        {
            _haptic.PlayIfManual(vesselImpactor.Vessel.VesselStatus);
        }
    }
}