using CosmicShore.Core;
using Unity.Services.Matchmaker.Models;
using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(fileName = "PrismDeactivateByShipEffect", menuName = "ScriptableObjects/Impact Effects/PrismDeactivateByShipEffectSO")]
    public class PrismDeactivateByShipEffectSO : ImpactEffectSO<R_PrismImpactor, R_ShipImpactor>
    {
        protected override void ExecuteTyped(R_PrismImpactor prismImpactor, R_ShipImpactor shipImpactor)
        {
            var trailBlockProperties = prismImpactor.TrailBlock.TrailBlockProperties;
            var shipStatus = shipImpactor.Ship.ShipStatus;
            
            trailBlockProperties.trailBlock.Damage(
                shipStatus.Course * shipStatus.Speed * shipStatus.GetInertia, 
                shipStatus.Team, shipStatus.PlayerName);
        }
        
    }
}
