using System;
using System.Threading;
using CosmicShore.Data;
using CosmicShore.Utility;
using Cysharp.Threading.Tasks;
using Reflex.Attributes;
using Unity.Netcode;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Initializes player-vessel pairs on clients.
    ///
    /// Server/host path:
    ///   Called directly by ServerPlayerVesselInitializer via InitializePlayerAndVessel().
    ///
    /// Client path (RPCs):
    ///   InitializeAllPlayersAndVesselsInThisNewClient_ClientRpc → new client initializes ALL pairs
    ///   InitializeNewClientsOwnerPlayerAndVesselInExistingClient_ClientRpc → existing client initializes one new pair
    ///   ReplaceVesselForPlayer_ClientRpc → swap: re-initialize with a new vessel
    ///
    /// Uses UniTask.WaitUntil to wait for NetworkObjects to replicate before initializing.
    /// </summary>
    public class ClientPlayerVesselInitializer : NetworkBehaviour
    {
        [SerializeField] ThemeManagerDataContainerSO themeManagerData;

        [Inject] protected GameDataSO gameData;

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
        /// Iterates gameData.Players which were already added via Player.OnNetworkSpawn().
        /// </summary>
        [ClientRpc]
        internal void InitializeAllPlayersAndVesselsInThisNewClient_ClientRpc(
            ClientRpcParams clientRpcParams = default)
        {
            foreach (var networkPlayer in gameData.Players)
            {
                ulong playerId = networkPlayer.PlayerNetId;
                ulong vesselId = networkPlayer.VesselNetId;

                InitializePlayerAndVesselByNetIds(playerId, vesselId,
                    this.GetCancellationTokenOnDestroy()).Forget();
            }

            DelayInvokeClientReady(this.GetCancellationTokenOnDestroy()).Forget();
        }

        /// <summary>
        /// RPC sent to EXISTING clients: initialize just the new player-vessel pair.
        /// </summary>
        [ClientRpc]
        internal void InitializeNewClientsOwnerPlayerAndVesselInExistingClient_ClientRpc(
            ulong newJoinedClientId,
            ClientRpcParams clientRpcParams = default)
        {
            if (!gameData.TryGetPlayerByOwnerClientId(newJoinedClientId, out var player))
            {
                CSDebug.LogError($"[ClientPlayerVesselInitializer] No player found for client Id: {newJoinedClientId}");
                return;
            }

            var ownerPlayerId = player.PlayerNetId;
            var ownerVesselId = player.VesselNetId;
            InitializePlayerAndVesselByNetIds(ownerPlayerId, ownerVesselId,
                this.GetCancellationTokenOnDestroy()).Forget();
        }

        async UniTaskVoid InitializePlayerAndVesselByNetIds(
            ulong playerId,
            ulong vesselId,
            CancellationToken token)
        {
            try
            {
                // Wait until BOTH are available (or cancel)
                await UniTask.WaitUntil(() =>
                        gameData.TryGetPlayerByNetworkObjectId(playerId, out _) &&
                        gameData.TryGetVesselByNetworkObjectId(vesselId, out _),
                    cancellationToken: token);

                if (!gameData.TryGetPlayerByNetworkObjectId(playerId, out var player))
                    return;
                if (!gameData.TryGetVesselByNetworkObjectId(vesselId, out var vessel))
                    return;

                InitializePair(player, vessel);
            }
            catch (OperationCanceledException) { }
        }

        async UniTaskVoid DelayInvokeClientReady(CancellationToken token)
        {
            try
            {
                await UniTask.Delay(1000, DelayType.UnscaledDeltaTime,
                    PlayerLoopTiming.LastPostLateUpdate, token);
                if (token.IsCancellationRequested)
                    return;

                gameData.InvokeClientReady();
            }
            catch (OperationCanceledException) { }
        }

        // ---------------------------------------------------------
        // VESSEL SWAP (player already exists, vessel changed)
        // ---------------------------------------------------------

        /// <summary>
        /// Server-side callback registered by MenuServerPlayerVesselInitializer
        /// to handle the actual despawn/spawn when a swap request arrives via ServerRpc.
        /// </summary>
        public Action<ulong, ulong, VesselClassType, Pose> OnSwapRequested;

        /// <summary>
        /// Direct server-side vessel replacement (called by MenuServerPlayerVesselInitializer on host).
        /// </summary>
        public void ReplaceVesselForPlayer(IPlayer player, IVessel newVessel)
        {
            ReInitializePair(player, newVessel);
        }

        /// <summary>
        /// Called by any client to request a vessel swap.
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
        /// </summary>
        [ClientRpc]
        internal void ReplaceVesselForPlayer_ClientRpc(
            ulong playerNetId, ulong newVesselNetId,
            ClientRpcParams rpcParams = default)
        {
            ProcessSwap(playerNetId, newVesselNetId, this.GetCancellationTokenOnDestroy()).Forget();
        }

        async UniTaskVoid ProcessSwap(ulong playerNetId, ulong vesselNetId, CancellationToken token)
        {
            try
            {
                await UniTask.WaitUntil(() =>
                        gameData.TryGetPlayerByNetworkObjectId(playerNetId, out _) &&
                        gameData.TryGetVesselByNetworkObjectId(vesselNetId, out _),
                    cancellationToken: token);

                if (!gameData.TryGetPlayerByNetworkObjectId(playerNetId, out var player))
                    return;
                if (!gameData.TryGetVesselByNetworkObjectId(vesselNetId, out var vessel))
                    return;

                ReInitializePair(player, vessel);
            }
            catch (OperationCanceledException) { }
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

            gameData.InvokePlayerPairInitialized(player.PlayerNetId);

            if (player.IsLocalUser && CameraManager.Instance)
                CameraManager.Instance.SnapPlayerCameraToTarget();

            if (player.IsLocalUser)
                gameData.InvokeClientReady();
        }

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
