using Unity.Netcode;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Cross-machine visibility for the Grizzly's bomb pump. A bomb's size is the PEAK trigger
    /// pressure of one pull, which only the simulating machine can read — the input events that
    /// reach other peers carry no analog value. So the simulating machine blows the bomb and
    /// relays it: one small RPC per bomb, each other peer spawning the identical blast locally
    /// (the <see cref="MantaBombNetworkRelay"/> model). The kick is NOT relayed — the vessel's
    /// transform already replicates.
    ///
    /// Lives on the Grizzly prefab root beside its NetworkObject. Inert (never spawned) on the
    /// non-networked single-player path — the executor guards on <see cref="NetworkBehaviour.IsSpawned"/>.
    /// </summary>
    public class GrizzlyBombNetworkRelay : NetworkBehaviour
    {
        VesselStatus _vesselStatus;
        VesselImpactor _vesselImpactor;
        GrizzlyBombPumpExecutor _executor;

        void Awake()
        {
            _vesselStatus = GetComponent<VesselStatus>();
            _vesselImpactor = GetComponent<VesselImpactor>();
            _executor = GetComponentInChildren<GrizzlyBombPumpExecutor>(true);
        }

        /// <summary>Simulating machine → everyone else: one bomb blew.</summary>
        public void BroadcastBomb(Vector3 position, Quaternion rotation, float scale)
        {
            if (!IsSpawned) return;
            ReportBomb_ServerRpc(position, rotation, scale);
        }

        // Default ownership rule holds: the simulating machine IS the owner (a human's client,
        // or the server for an AI), and only it calls this.
        [ServerRpc]
        void ReportBomb_ServerRpc(Vector3 position, Quaternion rotation, float scale,
                                  ServerRpcParams rpcParams = default)
        {
            Bomb_ClientRpc(position, rotation, scale, rpcParams.Receive.SenderClientId);
        }

        [ClientRpc]
        void Bomb_ClientRpc(Vector3 position, Quaternion rotation, float scale, ulong senderClientId)
        {
            // The originator already blew it locally — replaying there would double the blast.
            if (NetworkManager != null && NetworkManager.LocalClientId == senderClientId) return;
            if (_executor == null || _vesselStatus == null) return;

            GrizzlyBombPumpExecutor.SpawnBlast(_executor.Config, _vesselStatus, position, rotation, scale,
                _vesselImpactor ? _vesselImpactor.DIContainer : null);
        }
    }
}
