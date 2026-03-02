using System;
using System.Collections.Generic;
using CosmicShore.Data;
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
    ///   ReplaceVesselForPlayer_ClientRpc         → swap: re-initialize with a new vessel
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
        readonly List<(ulong playerNetId, ulong vesselNetId)> _pendingSwaps = new();
        bool _signalClientReadyWhenDone;

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            if (NetworkManager.Singleton.IsServer)
                return;

            // Re-register persistent Players that survived the Netcode scene load
            // but were cleared from gameData.Players by ResetRuntimeData().
            // Their OnNetworkSpawn() won't re-fire, so we manually re-add them
            // so ProcessPendingPairs() can resolve (playerNetId, vesselNetId) pairs.
            ReRegisterPersistentPlayers();

            // Subscribe to SOAP events so we can process pending pairs
            // when objects replicate (event-driven, no polling)
            gameData.OnPlayerNetworkSpawnedUlong.OnRaised += OnPlayerNetworkSpawnedForPending;
            gameData.OnVesselNetworkSpawned.OnRaised += ProcessPendingPairs;
            gameData.OnVesselNetworkSpawned.OnRaised += ProcessPendingSwaps;
        }

        public override void OnNetworkDespawn()
        {
            gameData.OnPlayerNetworkSpawnedUlong.OnRaised -= OnPlayerNetworkSpawnedForPending;
            gameData.OnVesselNetworkSpawned.OnRaised -= ProcessPendingPairs;
            gameData.OnVesselNetworkSpawned.OnRaised -= ProcessPendingSwaps;
            _pendingPairs.Clear();
            _pendingSwaps.Clear();
            base.OnNetworkDespawn();
        }

        // ---------------------------------------------------------
        // PERSISTENT PLAYER RE-REGISTRATION (client-side)
        // ---------------------------------------------------------

        /// <summary>
        /// Re-registers persistent Player NetworkObjects with gameData.Players on the client.
        /// Player objects survive Netcode scene loads (DestroyWithScene=false) but
        /// gameData.Players was cleared by ResetRuntimeData(). Without re-registration,
        /// TryGetPlayerByNetworkObjectId() fails and pending pairs never resolve.
        /// Also updates owner-writable NetworkVariables for the new game config.
        /// </summary>
        void ReRegisterPersistentPlayers()
        {
            var nm = NetworkManager.Singleton;
            if (nm == null || nm.SpawnManager == null) return;

            foreach (var kvp in nm.SpawnManager.SpawnedObjects)
            {
                var netObj = kvp.Value;
                if (netObj == null || !netObj.TryGetComponent<Player>(out var player))
                    continue;
                if (!player.IsSpawned) continue;

                if (!gameData.Players.Contains(player))
                    gameData.Players.Add(player);

                // Owners update their vessel type to match the new game config
                // (synced via SyncGameConfigToClients_ClientRpc before scene load).
                if (player.IsOwner)
                    player.NetDefaultVesselType.Value = gameData.selectedVesselClass.Value;
            }
        }

        // ---------------------------------------------------------
        // FIRST-TIME INIT (new player joins)
        // ---------------------------------------------------------

        /// <summary>
        /// Direct server-side initialization (called by ServerPlayerVesselInitializer on host).
        /// </summary>
        public void InitializePlayerAndVessel(Player player, IVessel vessel)
        {
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

        // ---------------------------------------------------------
        // VESSEL SWAP (player already exists, vessel changed)
        // ---------------------------------------------------------

        /// <summary>
        /// Server-side callback registered by <see cref="MenuServerPlayerVesselInitializer"/>
        /// to handle the actual despawn/spawn when a swap request arrives via ServerRpc.
        /// Parameters: senderClientId, playerNetId, targetVesselClass, snapshotPose.
        /// </summary>
        public Action<ulong, ulong, VesselClassType, Pose> OnSwapRequested;

        /// <summary>
        /// Direct server-side vessel replacement (called by MenuServerPlayerVesselInitializer on host).
        /// The player already has a vessel — this wires the new one in place.
        /// </summary>
        public void ReplaceVesselForPlayer(IPlayer player, IVessel newVessel)
        {
            ReInitializePair(player, newVessel);
        }

        /// <summary>
        /// Called by any client to request a vessel swap. Forwarded to the server
        /// where <see cref="OnSwapRequested"/> is invoked to perform the actual swap.
        /// </summary>
        [ServerRpc(RequireOwnership = false)]
        internal void RequestVesselSwap_ServerRpc(
            ulong playerNetId,
            VesselClassType targetClass,
            Vector3 snapshotPos,
            Quaternion snapshotRot,
            ServerRpcParams rpcParams = default)
        {
            OnSwapRequested?.Invoke(
                rpcParams.Receive.SenderClientId,
                playerNetId,
                targetClass,
                new Pose(snapshotPos, snapshotRot));
        }

        /// <summary>
        /// RPC sent to ALL non-host clients when a player swaps their vessel.
        /// The old vessel was already despawned by the server; the new one is replicating.
        /// Queued until the new vessel's NetworkObject appears.
        /// </summary>
        [ClientRpc]
        internal void ReplaceVesselForPlayer_ClientRpc(
            ulong playerNetId, ulong newVesselNetId,
            ClientRpcParams rpcParams = default)
        {
            _pendingSwaps.Add((playerNetId, newVesselNetId));
            ProcessPendingSwaps();
        }

        // ---------------------------------------------------------
        // PENDING PAIR RESOLUTION
        // ---------------------------------------------------------

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
            }
        }

        /// <summary>
        /// Tries to resolve pending vessel swaps. Unlike <see cref="ProcessPendingPairs"/>,
        /// the player already exists and has a (now-despawned) vessel reference.
        /// We wait only for the new vessel to replicate.
        /// </summary>
        void ProcessPendingSwaps()
        {
            for (int i = _pendingSwaps.Count - 1; i >= 0; i--)
            {
                var (pId, vId) = _pendingSwaps[i];

                if (!gameData.TryGetPlayerByNetworkObjectId(pId, out var player))
                    continue;
                if (!gameData.TryGetVesselByNetworkObjectId(vId, out var vessel))
                    continue;

                ReInitializePair(player, vessel);
                _pendingSwaps.RemoveAt(i);
            }
        }

        // ---------------------------------------------------------
        // INIT LOGIC
        // ---------------------------------------------------------

        void InitializePair(IPlayer player, IVessel vessel)
        {
            player.InitializeForMultiplayerMode(vessel);
            vessel.Initialize(player);
            ShipHelper.SetShipProperties(themeManagerData, vessel);
            gameData.AddPlayer(player);

            // Signal this specific player-vessel pair is fully initialized.
            // Subscribers (e.g. MainMenuController) activate non-local players
            // individually when their own pair resolves, avoiding the race
            // condition of batch-activating players whose vessels haven't
            // replicated yet.
            gameData.InvokePlayerPairInitialized(player.PlayerNetId);

            if (player.IsLocalUser && CameraManager.Instance)
                CameraManager.Instance.SnapPlayerCameraToTarget();

            if (player.IsLocalUser)
                gameData.InvokeClientReady();
        }

        /// <summary>
        /// Re-initializes a player-vessel pair during a vessel swap.
        /// Unlike <see cref="InitializePair"/>, the player is already in
        /// <see cref="GameDataSO.Players"/> and has domain/name set —
        /// only the vessel reference needs to change.
        /// </summary>
        void ReInitializePair(IPlayer player, IVessel newVessel)
        {
            player.ChangeVessel(newVessel);
            newVessel.Initialize(player);
            ShipHelper.SetShipProperties(themeManagerData, newVessel);

            if (player.IsLocalUser && CameraManager.Instance)
                CameraManager.Instance.SnapPlayerCameraToTarget();
        }
    }
}
