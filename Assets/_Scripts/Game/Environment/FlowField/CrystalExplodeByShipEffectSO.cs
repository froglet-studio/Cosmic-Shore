using CosmicShore.Core;
using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(fileName = "CrystalExplodeByShipEffect", menuName = "ScriptableObjects/Impact Effects/CrystalExplodeByShipEffectSO")]
    public class CrystalExplodeByShipEffectSO : ImpactEffectSO<CrystalImpactor, ShipImpactor>
    {
        protected override void ExecuteTyped(CrystalImpactor crystalImpactor, ShipImpactor shipImpactee)
        {
            var shipStatus = shipImpactee.Ship.ShipStatus;
            var crystal = crystalImpactor.Crystal;

            if (crystal.IsOwnTeamSameAsShipTeam(shipStatus.Team))
                return;
            
//             if (allowVesselImpactEffect)
//             {
//                 // TODO - This class should not modify AIPilot's properties directly.
//                 /*if (ship.ShipStatus.AIPilot != null)
//                 {
//                     AIPilot aiPilot = ship.ShipStatus.AIPilot;
//
//                     aiPilot.aggressiveness = aiPilot.defaultAggressiveness;
//                     aiPilot.throttle = aiPilot.defaultThrottle;
//                 }*/
//             }

            // TODO - Add Event channels here rather than calling singletons directly.
            
            if (StatsManager.Instance != null)
                StatsManager.Instance.CrystalCollected(shipStatus.Ship, crystal.crystalProperties);

            // TODO - Handled from R_CrystalImpactor.cs
            // PerformCrystalImpactEffects(crystalProperties, ship);
            // TODO : Pass only ship status

            if (shipStatus.ShipType == ShipClassType.Manta) return;
            crystal.Explode(shipStatus);
            crystal.PlayExplosionAudio();
            crystal.CrystalRespawn();

        }
    }
}