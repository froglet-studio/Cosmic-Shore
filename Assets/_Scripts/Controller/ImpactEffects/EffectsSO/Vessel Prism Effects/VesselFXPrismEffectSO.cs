using UnityEngine;
using CosmicShore.Gameplay;
using CosmicShore.Data;
using CosmicShore.Utility;
namespace CosmicShore.Gameplay
{
    [CreateAssetMenu(fileName = "VesselFXPrismEffect",
        menuName = "ScriptableObjects/Impact Effects/Vessel - Prism/VesselFXPrismEffectSO")]
    public class VesselFXPrismEffectSO : VesselPrismEffectSO
    {
        [SerializeField] private float particleDurationAtSpeedOne = 300f;

        public override void Execute(VesselImpactor impactor, PrismImpactor prismImpactee)
        {
            var shipStatus = impactor?.Vessel?.VesselStatus;
            var trailBlock = prismImpactee?.Prism?.prismProperties?.prism;
            SkimFxRunner.RunAsync(shipStatus, trailBlock, particleDurationAtSpeedOne).Forget();
        }
    }
}
