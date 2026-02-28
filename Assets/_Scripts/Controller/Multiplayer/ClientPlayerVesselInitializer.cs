using System.Collections.Generic;
using CosmicShore.Gameplay;
using CosmicShore.Utility;
using Reflex.Attributes;
using Unity.Netcode;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Initializes player-vessel pairs.
    ///
    /// Server/host path:
    ///   Called directly by ServerPlayerVesselInitializer.
    ///
    /// Client path (RPCs):
    ///   InitializeAllPlayersAndVessels_ClientRpc → new client initializes ALL pairs
    ///   InitializeNewPlayerAndVessel_ClientRpc   → existing client initializes one new pair
    ///
    /// When an RPC arrives but objects haven't replicated yet, pairs are queued.
    /// OnPlayerNetworkSpawnedUlong + OnVesselNetworkSpawned SOAP events trigger
    /// re-processing of the queue — zero WaitUntil polling.
    /// </summary>
    public class ClientPlayerVesselInitializer : NetworkBehaviour
    {
        [SerializeField] ThemeManagerDataContainerSO themeManagerData;

        [Inject] protected GameDataSO gameData;

        readonly List<(ulong playerNetId, ulong vesselNetId)> _pendingPairs = new();
        bool _signalClientReadyWhenDone;

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            if (NetworkManager.Singleton.IsServer)
                return;

            // Subscribe to SOAP events so we can process pending pairs
            // when objects replicate (event-driven, no polling)
            gameData.OnPlayerNetworkSpawnedUlong.OnRaised += OnPlayerNetworkSpawnedForPending;
            gameData.OnVesselNetworkSpawned.OnRaised += ProcessPendingPairs;
        }

        public override void OnNetworkDespawn()
        {
            gameData.OnPlayerNetworkSpawnedUlong.OnRaised -= OnPlayerNetworkSpawnedForPending;
            gameData.OnVesselNetworkSpawned.OnRaised -= ProcessPendingPairs;
            _pendingPairs.Clear();
            base.OnNetworkDespawn();
        }

        /// <summary>
        /// Direct server-side initialization (called by ServerPlayerVesselInitializer on host).
        /// </summary>
        public void InitializePlayerAndVessel(Player player, NetworkObject vesselNO)
        {
            if (!vesselNO.TryGetComponent(out IVessel vessel))
            {
                CSDebug.LogError("[ClientPlayerVesselInitializer] Spawned vessel missing IVessel component.");
                return;
            }

            InitializePair(player, vessel);
        }

        /// <summary>
        /// RPC sent to NEW client: initialize ALL existing player-vessel pairs.
        /// Fires ClientReady when all pairs are initialized.
        /// </summary>
        [ClientRpc]
        internal void InitializeAllPlayersAndVessels_ClientRpc(
            ulong[] playerNetIds, ulong[] vesselNetIds,
            ClientRpcParams rpcParams = default)
        {
            _signalClientReadyWhenDone = true;

            for (int i = 0; i < playerNetIds.Length; i++)
                _pendingPairs.Add((playerNetIds[i], vesselNetIds[i]));

            ProcessPendingPairs();
        }

        /// <summary>
        /// RPC sent to EXISTING clients: initialize just the new player-vessel pair.
        /// Does not fire ClientReady (already fired on initial join).
        /// </summary>
        [ClientRpc]
        internal void InitializeNewPlayerAndVessel_ClientRpc(
            ulong playerNetId, ulong vesselNetId,
            ClientRpcParams rpcParams = default)
        {
            _pendingPairs.Add((playerNetId, vesselNetId));
            ProcessPendingPairs();
        }

        void OnPlayerNetworkSpawnedForPending(ulong _) => ProcessPendingPairs();

        /// <summary>
        /// Tries to resolve pending (playerNetId, vesselNetId) pairs.
        /// Called when RPCs arrive AND when SOAP events fire (objects replicate).
        /// </summary>
        void ProcessPendingPairs()
        {
            for (int i = _pendingPairs.Count - 1; i >= 0; i--)
            {
                var (pId, vId) = _pendingPairs[i];

                if (!gameData.TryGetPlayerByNetworkObjectId(pId, out var player))
                    continue;
                if (!gameData.TryGetVesselByNetworkObjectId(vId, out var vessel))
                    continue;

                // Already initialized (e.g., duplicate event)
                if (player.Vessel != null)
                {
                    _pendingPairs.RemoveAt(i);
                    continue;
                }

                InitializePair(player, vessel);
                _pendingPairs.RemoveAt(i);
            }

            if (_pendingPairs.Count == 0 && _signalClientReadyWhenDone)
            {
                _signalClientReadyWhenDone = false;
                gameData.InvokeClientReady();
            }
        }

        void InitializePair(IPlayer player, IVessel vessel)
        {
            player.InitializeForMultiplayerMode(vessel);
            vessel.Initialize(player);
            ShipHelper.SetShipProperties(themeManagerData, vessel);
            gameData.AddPlayer(player);

            if (player.IsLocalUser && CameraManager.Instance)
                CameraManager.Instance.SnapPlayerCameraToTarget();
        }
    }
}
