using System;
using CosmicShore.Core;
using CosmicShore.Utilities;
using Cysharp.Threading.Tasks;
using Unity.Netcode;
using UnityEngine;

namespace CosmicShore.Game
{
    public class OmniCrystalImpactor : CrystalImpactor
    {
        VesselCrystalEffectSO[] omniCrystalShipEffects;

        [SerializeField]
        ScriptableEventCrystalStats OnCrystalCollected;

        bool IsImpacting;
        
        protected override void AcceptImpactee(IImpactor impactee)
        {
            if (IsImpacting)
                return;
            
            if (Crystal.IsExploding)
                return;
            
            switch (impactee)
            {
                case VesselImpactor shipImpactee:
                {
                    IsImpacting = true;
                    WaitForImpact().Forget();
                    
                    ExecuteEffect(shipImpactee);

                    if (DoesEffectExist(omniCrystalShipEffects))
                    {
                        foreach (var effect in omniCrystalShipEffects)
                            effect.Execute(shipImpactee,this);
                    }
                    
                    Crystal.Respawn();
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
        }
        
        /// <summary>
        /// This is to forbid multiple impacts due to multiple vessel colliders
        /// </summary>
        async UniTask WaitForImpact()
        {
            await UniTask.WaitForSeconds(0.5f);
            IsImpacting = false;
        }
    }
}