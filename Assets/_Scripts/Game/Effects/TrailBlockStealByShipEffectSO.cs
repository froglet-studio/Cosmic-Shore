using CosmicShore.Core;
using Unity.Services.Matchmaker.Models;
using UnityEngine;
using UnityEngine.Rendering;

namespace CosmicShore.Game
{
    [CreateAssetMenu(fileName = "TrailBlockStealByShipEffect", menuName = "ScriptableObjects/Impact Effects/TrailBlockStealByShipEffectSO")]
    public class TrailBlockStealByShipEffectSO : ImpactEffectSO<R_PrismImpactor, R_ShipImpactor>
    {
        protected override void ExecuteTyped(R_PrismImpactor prismImpactor, R_ShipImpactor shipImpactee)
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
