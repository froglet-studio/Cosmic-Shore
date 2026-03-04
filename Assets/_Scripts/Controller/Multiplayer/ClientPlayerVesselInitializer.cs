using System;
using System.Threading;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using CosmicShore.Utility;
using Cysharp.Threading.Tasks;
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
    /// When an RPC arrives but objects haven't replicated yet, pairs are resolved
    /// via UniTask.WaitUntil polling until the NetworkObjects appear in gameData.
    /// </summary>
    public class ClientPlayerVesselInitializer : NetworkBehaviour
    {
        [SerializeField] ThemeManagerDataContainerSO themeManagerData;

        [Inject] protected GameDataSO gameData;

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            if (NetworkManager.Singleton.IsServer)
                return;

            // Re-register persistent Players that survived the Netcode scene load
            // but were cleared from gameData.Players by ResetRuntimeData().
            ReRegisterPersistentPlayers();
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
        /// Fires ClientReady after a delay when all pairs should be initialized.
        /// </summary>
        [ClientRpc]
        internal void InitializeAllPlayersAndVessels_ClientRpc(
            ulong[] playerNetIds, ulong[] vesselNetIds,
            ClientRpcParams rpcParams = default)
        {
            var ct = this.GetCancellationTokenOnDestroy();

            for (int i = 0; i < playerNetIds.Length; i++)
                InitializePlayerAndVesselByNetIds(playerNetIds[i], vesselNetIds[i], ct).Forget();

            DelayInvokeClientReady(ct).Forget();
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
            InitializePlayerAndVesselByNetIds(playerNetId, vesselNetId,
                this.GetCancellationTokenOnDestroy()).Forget();
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
        /// Polls until the new vessel appears, then re-initializes.
        /// </summary>
        [ClientRpc]
        internal void ReplaceVesselForPlayer_ClientRpc(
            ulong playerNetId, ulong newVesselNetId,
            ClientRpcParams rpcParams = default)
        {
            WaitAndReplaceVessel(playerNetId, newVesselNetId,
                this.GetCancellationTokenOnDestroy()).Forget();
        }

        // ---------------------------------------------------------
        // POLLING-BASED PAIR RESOLUTION
        // ---------------------------------------------------------

        /// <summary>
        /// Polls until both player and vessel NetworkObjects are available in gameData,
        /// then initializes the pair. Used by client RPCs when objects haven't replicated yet.
        /// </summary>
        async UniTaskVoid InitializePlayerAndVesselByNetIds(
            ulong playerId, ulong vesselId, CancellationToken token)
        {
            await UniTask.WaitUntil(() =>
                    gameData.TryGetPlayerByNetworkObjectId(playerId, out _) &&
                    gameData.TryGetVesselByNetworkObjectId(vesselId, out _),
                cancellationToken: token);

            if (!gameData.TryGetPlayerByNetworkObjectId(playerId, out var player))
                return;
            if (!gameData.TryGetVesselByNetworkObjectId(vesselId, out var vessel))
                return;

            // Already initialized (e.g., duplicate event)
            if (player.Vessel != null)
                return;

            InitializePair(player, vessel);
        }

        /// <summary>
        /// Polls until both player and new vessel are available, then re-initializes.
        /// Used for vessel swaps where the player already exists.
        /// </summary>
        async UniTaskVoid WaitAndReplaceVessel(
            ulong playerNetId, ulong newVesselNetId, CancellationToken token)
        {
            await UniTask.WaitUntil(() =>
                    gameData.TryGetPlayerByNetworkObjectId(playerNetId, out _) &&
                    gameData.TryGetVesselByNetworkObjectId(newVesselNetId, out _),
                cancellationToken: token);

            if (!gameData.TryGetPlayerByNetworkObjectId(playerNetId, out var player))
                return;
            if (!gameData.TryGetVesselByNetworkObjectId(newVesselNetId, out var vessel))
                return;

            ReInitializePair(player, vessel);
        }

        /// <summary>
        /// Delays then fires ClientReady. Gives time for all pending pairs
        /// to resolve after receiving InitializeAllPlayersAndVessels_ClientRpc.
        /// </summary>
        async UniTaskVoid DelayInvokeClientReady(CancellationToken token)
        {
            await UniTask.Delay(1000, DelayType.UnscaledDeltaTime,
                PlayerLoopTiming.LastPostLateUpdate, token);

            if (token.IsCancellationRequested)
                return;

            gameData.InvokeClientReady();
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
