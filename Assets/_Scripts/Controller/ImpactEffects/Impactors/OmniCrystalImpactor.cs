using System;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using Cysharp.Threading.Tasks;
using Unity.Netcode;
using UnityEngine;
using CosmicShore.Data;
namespace CosmicShore.Gameplay
{
    public class OmniCrystalImpactor : CrystalImpactor
    {
        [SerializeField]
        VesselCrystalEffectSO[] omniCrystalShipEffects;

        [SerializeField]
        ScriptableEventCrystalStats OnCrystalCollected;

        bool IsImpacting;

        /// <summary>
        /// Omni crystals are collectible by any vessel regardless of domain.
        /// Override in <see cref="TeamCrystalImpactor"/> for strict domain matching.
        /// </summary>
        protected virtual bool IsDomainMatching(Domains domain) => true;

        bool IsNetworkClient() => Crystal.CrystalManager.IsSpawned && !Crystal.CrystalManager.IsServer;

        protected override void AcceptImpactee(IImpactor impactee)
        {
            if (IsNetworkClient())
                return;

            if (IsImpacting)
                return;

            if (Crystal.IsExploding)
                return;

            // Always enforce domain matching — vessels can only collect
            // crystals that belong to their domain (or unowned crystals).
            if (!IsDomainMatching(impactee.OwnDomain))
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
                        CrystalImpactData data = CrystalImpactData.FromCrystal(Crystal);
                        foreach (var effect in omniCrystalShipEffects)
                            effect.Execute(shipImpactee, data);
                    }
                    
                    Crystal.Respawn();
                    break;
                }
            }
        }
        
        void ExecuteEffect(VesselImpactor vesselImpactee)
        {
            var shipStatus = vesselImpactee.Vessel.VesselStatus;

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