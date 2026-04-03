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

        [SerializeField, Tooltip("When true, ONLY the owning domain can collect. When false (default), domain matching is still enforced — vessels can only collect crystals of their own domain or unowned (None) crystals.")]
        private bool strictDomainOnly;

        bool IsImpacting;

        /// <summary>
        /// Returns true if the impacting vessel's domain is allowed to collect this crystal.
        /// Default: own domain OR unowned (None) crystals are collectible.
        /// Override in TeamCrystalImpactor for strict-only matching.
        /// </summary>
        protected virtual bool IsDomainMatching(Domains domain) =>
            Crystal.ownDomain == domain || Crystal.ownDomain == Domains.None;

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