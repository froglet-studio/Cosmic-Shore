using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(fileName = "PrismStolenByShipEffect", menuName = "ScriptableObjects/Impact Effects/PrismStolenByShipEffectSO")]
    public class PrismStolenByShipEffectSO : ImpactEffectSO<PrismImpactor, ShipImpactor>
    {
        protected override void ExecuteTyped(PrismImpactor prismImpactor, ShipImpactor shipImpactee)
        {
            var trailBlockProperties = prismImpactor.Prism.TrailBlockProperties;
            
            if (trailBlockProperties == null)
            {
                Debug.LogError("TrailBlockProperties is null.");
                return;
            }

            var shipStatus = shipImpactee.Ship.ShipStatus;
            trailBlockProperties.trailBlock.Steal(shipStatus.PlayerName, shipStatus.Team);
        }
    }
}
