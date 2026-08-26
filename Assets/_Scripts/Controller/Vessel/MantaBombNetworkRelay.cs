using CosmicShore.Data;
using Unity.Netcode;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Cross-machine visibility for the Manta's local simulations. Bombs and wake rings are
    /// LOCAL objects on the owning machine (like projectiles), but their consequences are the
    /// whole point of the kit — "five distant blooms firing in a chain across the cell" has to
    /// read on every screen, a Time-5 wake highway has to exist under an ally's wings, and a
    /// bloom that eats a TRAIL prism must eat it on every peer or the ribbons desync.
    ///
    /// So the owner machine simulates, then relays: one small RPC per bloom / per ring, each
    /// peer re-spawning the identical effect locally. Scoring is NOT relayed here — the
    /// server's copy of a remote pilot's bloom is excluded by <c>StatsManager.OwnsAttacker</c>
    /// and the owner's copy credits through the environment-kill RPC family, so each
    /// destruction pays exactly once (the Rampage crediting model).
    ///
    /// Lives on the Manta prefab root beside its NetworkObject. Inert (never spawned) on the
    /// non-networked single-player path — callers guard on <see cref="NetworkBehaviour.IsSpawned"/>.
    /// </summary>
    public class MantaBombNetworkRelay : NetworkBehaviour
    {
        [SerializeField] VesselStatus vesselStatus;
        [SerializeField] MantaStingConfigSO stingConfig;
        [SerializeField] MantaWakeRingConfigSO wakeRingConfig;

        VesselImpactor _vesselImpactor;

        void Awake()
        {
            if (!vesselStatus) vesselStatus = GetComponent<VesselStatus>();
            _vesselImpactor = GetComponent<VesselImpactor>();
        }

        // ── Blooms ───────────────────────────────────────────────────────────

        /// <summary>Owner machine → everyone else: one bomb bloomed at <paramref name="position"/>.</summary>
        public void BroadcastBloom(Vector3 position, float maxScale, bool affectSelf)
        {
            if (!IsSpawned) return;
            ReportBloom_ServerRpc(position, maxScale, affectSelf);
        }

        [ServerRpc]
        void ReportBloom_ServerRpc(Vector3 position, float maxScale, bool affectSelf,
                                   ServerRpcParams rpcParams = default)
        {
            Bloom_ClientRpc(position, maxScale, affectSelf, rpcParams.Receive.SenderClientId);
        }

        [ClientRpc]
        void Bloom_ClientRpc(Vector3 position, float maxScale, bool affectSelf, ulong senderClientId)
        {
            // The originator already bloomed locally — replaying there would double the blast.
            if (NetworkManager != null && NetworkManager.LocalClientId == senderClientId) return;
            if (!stingConfig || vesselStatus == null) return;

            // Domain is a default interface member on IVesselStatus — unreachable through the
            // concrete class, so the read must go through the interface.
            IVesselStatus status = vesselStatus;
            MantaBomb.SpawnBloom(stingConfig, null, position, Quaternion.identity, maxScale,
                status.Domain, vesselStatus.Vessel, affectSelf,
                _vesselImpactor ? _vesselImpactor.DIContainer : null);
        }

        // ── Wake rings ───────────────────────────────────────────────────────

        /// <summary>Owner machine → everyone else: a wake ring was laid at this pose.</summary>
        public void BroadcastWakeRing(Vector3 position, Quaternion rotation)
        {
            if (!IsSpawned) return;
            ReportWakeRing_ServerRpc(position, rotation);
        }

        [ServerRpc]
        void ReportWakeRing_ServerRpc(Vector3 position, Quaternion rotation,
                                      ServerRpcParams rpcParams = default)
        {
            WakeRing_ClientRpc(position, rotation, rpcParams.Receive.SenderClientId);
        }

        [ClientRpc]
        void WakeRing_ClientRpc(Vector3 position, Quaternion rotation, ulong senderClientId)
        {
            if (NetworkManager != null && NetworkManager.LocalClientId == senderClientId) return;
            if (!wakeRingConfig || vesselStatus == null) return;

            var prismController = vesselStatus.VesselPrismController;
            if (!prismController || !prismController.PrismSpawnChannel) return;

            MantaWakeRingActionExecutor.LayRingAt(wakeRingConfig,
                new Pose(position, rotation), vesselStatus, prismController.PrismSpawnChannel,
                registerSwitch: true);
        }
    }
}
