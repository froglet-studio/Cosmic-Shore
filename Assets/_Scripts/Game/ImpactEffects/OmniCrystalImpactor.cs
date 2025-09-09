using System;
using CosmicShore.Core;
using CosmicShore.Utilities;
using UnityEngine;

namespace CosmicShore.Game
{
    public class OmniCrystalImpactor : CrystalImpactor
    {
        VesselCrystalEffectSO[] omniCrystalShipEffects;

        [SerializeField]
        ScriptableEventCrystalStats OnCrystalCollected;

        protected virtual void Awake()
        {
            base.Awake();
        }

        protected override void AcceptImpactee(IImpactor impactee)
        {
            switch (impactee)
            {
                case VesselImpactor shipImpactee:
                {
                    ExecuteEffect(shipImpactee);
                    
                    if(!DoesEffectExist(omniCrystalShipEffects)) return;
                    foreach (var effect in omniCrystalShipEffects)
                    {
                        effect.Execute(shipImpactee,this);
                    }
                    break;
                }
            }
        }
        
        void ExecuteEffect(VesselImpactor vesselImpactee)
        {
            var shipStatus = vesselImpactee.Ship.ShipStatus;

            if (!Crystal.CanBeCollected(shipStatus.Team))
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
            OnCrystalCollected?.Raise(
                new CrystalStats()
                {
                    PlayerName = shipStatus.PlayerName,
                    Element =  Crystal.crystalProperties.Element,
                    Value =  Crystal.crystalProperties.crystalValue,
                }
            );
            /*if (StatsManager.Instance)
                StatsManager.Instance.CrystalCollected(shipStatus.Ship, crystal.crystalProperties);*/

            if (shipStatus.ShipType != ShipClassType.Manta)
            {
                Crystal.Explode(shipStatus);
                Crystal.PlayExplosionAudio();
            }

            Crystal.CrystalRespawn();
        }
    }
}