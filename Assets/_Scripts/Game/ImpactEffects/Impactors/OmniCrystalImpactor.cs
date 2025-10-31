using System;
using CosmicShore.Core;
using CosmicShore.Utilities;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace CosmicShore.Game
{
    public class OmniCrystalImpactor : CrystalImpactor
    {
        VesselCrystalEffectSO[] omniCrystalShipEffects;

        [SerializeField]
        ScriptableEventCrystalStats OnCrystalCollected;

        bool isImpacting;

        protected override void AcceptImpactee(IImpactor impactee)
        {
            if (isImpacting)
                return;
            
            isImpacting = true;
            
            WaitForImpact().Forget();
            
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
            var shipStatus = vesselImpactee.Vessel.VesselStatus;

            if (!Crystal.CanBeCollected(shipStatus.Domain))
                return;

            OnCrystalCollected?.Raise(
                new CrystalStats()
                {
                    PlayerName = shipStatus.PlayerName,
                    Element =  Crystal.crystalProperties.Element,
                    Value =  Crystal.crystalProperties.crystalValue,
                }
            );

            if (shipStatus.VesselType != VesselClassType.Manta)
            {
                Crystal.NotifyManagerToExplodeCrystal(new Crystal.ExplodeParams
                {
                    Course = shipStatus.Course,
                    Speed = shipStatus.Speed,
                    PlayerName = shipStatus.PlayerName,
                });
            }

            Crystal.Respawn();
        }
        
        async UniTask WaitForImpact()
        {
            await UniTask.NextFrame();
            isImpacting = false;
        }
    }
}