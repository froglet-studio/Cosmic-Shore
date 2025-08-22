using CosmicShore.Core;
using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(fileName = "OmniCrystalExplodeByShipEffect", menuName = "ScriptableObjects/Impact Effects/Crystal/OmniCrystalExplodeByShipEffectSO")]
    public class OmniCrystalExplodeByShipEffectSO : ImpactEffectSO<OmniCrystalImpactor, ShipImpactor>
    {
        protected override void ExecuteTyped(OmniCrystalImpactor crystalImpactor, ShipImpactor shipImpactee)
        {
            var shipStatus = shipImpactee.Ship.ShipStatus;
            var crystal = crystalImpactor.Crystal;

            if (!crystal.CanBeCollected(shipStatus.Team))
                return;
            
//             if (allowVesselImpactEffect)
//             {
//                 // TODO - Need to create architecture on how to handle AI based on impact effects
//                 /*if (ship.ShipStatus.AIPilot != null)
//                 {
//                     AIPilot aiPilot = ship.ShipStatus.AIPilot;
//
//                     aiPilot.aggressiveness = aiPilot.defaultAggressiveness;
//                     aiPilot.throttle = aiPilot.defaultThrottle;
//                 }*/
//             }

            // TODO - Add Event channels here rather than calling singletons directly.
            if (StatsManager.Instance)
                StatsManager.Instance.CrystalCollected(shipStatus.Ship, crystal.crystalProperties);

            if (shipStatus.ShipType != ShipClassType.Manta)
            {
                crystal.Explode(shipStatus);
                crystal.PlayExplosionAudio();
            }

            crystal.CrystalRespawn();
        }
    }
}