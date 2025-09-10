using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(fileName = "ShipFXPrismEffect",
        menuName = "ScriptableObjects/Impact Effects/Vessel/VesselFXPrismEffectSO")]
    public class VesselFXPrismEffectSO : VesselPrismEffectSO
    {
        [SerializeField] private float particleDurationAtSpeedOne = 300f;

        public override void Execute(VesselImpactor impactor, PrismImpactor prismImpactee)
        {
            var shipStatus = impactor?.Ship?.ShipStatus;
            var trailBlock = prismImpactee?.Prism?.TrailBlockProperties?.trailBlock;
            SkimFxRunner.RunAsync(shipStatus, trailBlock, particleDurationAtSpeedOne).Forget();
        }
    }
}
