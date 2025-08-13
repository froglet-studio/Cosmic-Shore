using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(fileName = "ShipDamagePrismEffect", menuName = "ScriptableObjects/Impact Effects/ShipDamagePrismEffectSO")]
    public class ShipDamagePrismEffectSO : ImpactEffectSO<R_ShipImpactor, R_PrismImpactor>
    {
        protected override void ExecuteTyped(R_ShipImpactor shipImpactor, R_PrismImpactor prismImpactor)
        {
            var trailBlockProperties = prismImpactor.TrailBlock.TrailBlockProperties;
            var shipStatus = shipImpactor.Ship.ShipStatus;
            
            trailBlockProperties.trailBlock.Damage(
                shipStatus.Course * shipStatus.Speed * shipStatus.GetInertia, 
                shipStatus.Team, shipStatus.PlayerName);
        }
    }
}
