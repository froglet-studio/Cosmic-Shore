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

        // Null manager = a manager-less local mint (e.g. the freestyle conveyor toy's omni pickups):
        // treat it as a local single-machine collect, never a network client.
        bool IsNetworkClient() => Crystal.CrystalManager != null && Crystal.CrystalManager.IsSpawned && !Crystal.CrystalManager.IsServer;

        protected override void AcceptImpactee(IImpactor impactee)
        {
            if (IsNetworkClient())
                return;

            if (IsImpacting)
                return;

            if (Crystal.IsExploding)
                return;

            // Always enforce domain matching - vessels can only collect
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
                    RunVesselEffects(shipImpactee);

                    // The collecting pilot must FEEL the pickup on their own machine. Collection
                    // resolves server-only (above), so for a remote client's vessel every vessel
                    // effect - the Dolphin's jaw blast, the resource spend, the elemental level -
                    // ran on the server and nowhere else: that pilot saw the crystal vanish, no
                    // cone, and a meter that never emptied. Replay them on the owner. The server
                    // stays the sole authority for collection, respawn and every stat.
                    ReplayOnRemoteOwner(shipImpactee);

                    Crystal.Respawn();
                    break;
                }
            }
        }

        /// <summary>
        /// Runs this crystal's vessel-side effects against one vessel, locally. Split out so the
        /// owning client can run the identical list on its own machine (see
        /// <see cref="ReplayOnRemoteOwner"/>) instead of a second, drifting implementation.
        /// </summary>
        public void RunVesselEffects(VesselImpactor shipImpactee)
        {
            if (!DoesEffectExist(omniCrystalShipEffects)) return;

            CrystalImpactData data = CrystalImpactData.FromCrystal(Crystal);
            foreach (var effect in omniCrystalShipEffects)
                effect.Execute(shipImpactee, data);
        }

        void ReplayOnRemoteOwner(VesselImpactor shipImpactee)
        {
            var manager = Crystal.CrystalManager;
            if (manager == null || !manager.IsSpawned) return;

            var vesselObject = shipImpactee.GetComponentInParent<NetworkObject>();
            if (vesselObject == null || !vesselObject.IsSpawned) return;

            // Server-owned vessels (the host's own, and every AI) already ran the effects here.
            if (vesselObject.IsOwner) return;

            manager.ReplayVesselCrystalEffects(Crystal.Id, vesselObject.NetworkObjectId,
                                               vesselObject.OwnerClientId);
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
                var explode = new Crystal.ExplodeParams
                {
                    Course = shipStatus.Course,
                    Speed = shipStatus.Speed,
                    PlayerName = shipStatus.PlayerName,
                };
                // Manager-less local mint (conveyor toy): explode locally so the spent-crystal
                // VFX still plays (continuity of existence) instead of the crystal popping out.
                if (Crystal.CrystalManager != null)
                    Crystal.NotifyManagerToExplodeCrystal(explode);
                else
                    Crystal.Explode(explode);
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