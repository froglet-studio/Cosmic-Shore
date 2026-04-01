using System.Threading;
using CosmicShore.Data;
using CosmicShore.Utility;
using Cysharp.Threading.Tasks;
using Unity.Netcode;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Menu_Main vessel initializer. Spawns the host vessel on the network,
    /// initializes it, then activates autopilot.
    ///
    /// Also handles runtime vessel swaps requested by any client via
    /// <see cref="ClientPlayerVesselInitializer.RequestVesselSwap_ServerRpc"/>.
    /// The swap despawns the old vessel, spawns a new one with the same ownership,
    /// re-initializes on the host, then notifies all clients via
    /// <see cref="ClientPlayerVesselInitializer.ReplaceVesselForPlayer_ClientRpc"/>.
    ///
    /// Game data configuration (vessel class, player count, intensity) is handled
    /// by <see cref="Core.MainMenuController"/> — this class only handles the
    /// network spawn chain, autopilot activation, and vessel swap.
    ///
    /// Listens to <see cref="GameDataSO.OnPlayerNetworkSpawnedUlong"/> via the base class,
    /// which waits for NetworkVariables to sync before spawning.
    /// </summary>
    public class MenuServerPlayerVesselInitializer : ServerPlayerVesselInitializer
    {
        bool _isSwapping;

        /// <summary>Whether a vessel swap is currently in progress.</summary>
        public bool IsSwapping => _isSwapping;

        protected override void Awake()
        {
            // Menu_Main must NOT shutdown NetworkManager on despawn —
            // the host must persist across scene transitions to game scenes.
            shutdownNetworkOnDespawn = false;
            base.Awake();
        }

        protected override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            // Register the swap callback so client-originated swap requests
            // route to HandleSwapRequest on the server.
            if (NetworkManager.Singleton.IsServer)
                clientPlayerVesselInitializer.OnSwapRequested += HandleSwapRequest;
        }

        protected override void OnNetworkDespawn()
        {
            if (clientPlayerVesselInitializer)
                clientPlayerVesselInitializer.OnSwapRequested -= HandleSwapRequest;

            base.OnNetworkDespawn();
        }

        /// <summary>
        /// Menu override: after the base spawns + initializes the vessel, activate autopilot.
        /// </summary>
        protected override async UniTask OnPlayerReadyToSpawnAsync(Player player, CancellationToken ct)
        {
            await base.OnPlayerReadyToSpawnAsync(player, ct);
            ActivateAutopilot(player);
        }

        // ---------------------------------------------------------
        // VESSEL SWAP (server-side)
        // ---------------------------------------------------------

        /// <summary>
        /// Entry point for the host's UI: request a vessel swap for the local player.
        /// Can also be called by remote clients via <see cref="ClientPlayerVesselInitializer.RequestVesselSwap_ServerRpc"/>.
        /// </summary>
        public void RequestSwap(VesselClassType targetClass)
        {
            if (_isSwapping) return;

            var localPlayer = gameData.LocalPlayer;
            if (localPlayer?.Vessel == null) return;

            var currentClass = localPlayer.Vessel.VesselStatus.VesselType;
            if (targetClass == currentClass) return;

            if (localPlayer is not Player netPlayer || !netPlayer.IsSpawned)
            {
                CSDebug.LogError("[MenuServerVesselInit] LocalPlayer is not a networked Player.");
                return;
            }

            var vs = localPlayer.Vessel.VesselStatus;
            var pose = new Pose(vs.Transform.position, vs.Transform.rotation);

            if (NetworkManager.Singleton.IsServer)
            {
                // Host path: swap directly
                SwapVesselAsync(
                    netPlayer.OwnerClientId,
                    netPlayer.NetworkObjectId,
                    targetClass,
                    pose,
                    _cts.Token).Forget();
            }
            else
            {
                // Client path: send RPC to server
                clientPlayerVesselInitializer.RequestVesselSwap_ServerRpc(
                    netPlayer.NetworkObjectId,
                    targetClass,
                    pose.position,
                    pose.rotation);
            }
        }

        void HandleSwapRequest(ulong senderClientId, ulong playerNetId, VesselClassType targetClass, Pose snapshotPose)
        {
            SwapVesselAsync(senderClientId, playerNetId, targetClass, snapshotPose, _cts.Token).Forget();
        }

        async UniTaskVoid SwapVesselAsync(
            ulong ownerClientId,
            ulong playerNetId,
            VesselClassType targetClass,
            Pose snapshotPose,
            CancellationToken ct)
        {
            _isSwapping = true;

            // 1. Find the player
            if (!gameData.TryGetPlayerByNetworkObjectId(playerNetId, out var iPlayer)
                || iPlayer is not Player player)
            {
                CSDebug.LogError($"[MenuServerVesselInit] Player {playerNetId} not found.");
                _isSwapping = false;
                return;
            }

            var oldVessel = player.Vessel;
            if (oldVessel == null)
            {
                CSDebug.LogError($"[MenuServerVesselInit] Player {playerNetId} has no vessel to swap.");
                _isSwapping = false;
                return;
            }

            // 2. Despawn old vessel
            DespawnVessel(oldVessel);

            // 3. Spawn new vessel
            var vesselNO = SpawnVesselForPlayer(ownerClientId, player, targetClass);
            if (!vesselNO)
            {
                _isSwapping = false;
                return;
            }

            if (!vesselNO.TryGetComponent(out IVessel newVessel))
            {
                CSDebug.LogError("[MenuServerVesselInit] Spawned vessel missing IVessel component.");
                _isSwapping = false;
                return;
            }

            // 4. Re-initialize on host
            clientPlayerVesselInitializer.ReplaceVesselForPlayer(player, newVessel);
            newVessel.SetPose(snapshotPose);
            ActivateAutopilot(player);

            // 5. Wait for replication, then notify all non-host clients
            await UniTask.Delay(postSpawnDelayMs, cancellationToken: ct);
            NotifyClientsOfSwap(player, newVessel);

            _isSwapping = false;
        }

        void NotifyClientsOfSwap(Player player, IVessel newVessel)
        {
            var hostClientId = NetworkManager.Singleton.LocalClientId;

            foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
            {
                if (client.ClientId == hostClientId)
                    continue;

                var target = new ClientRpcParams
                {
                    Send = new ClientRpcSendParams { TargetClientIds = new[] { client.ClientId } }
                };

                clientPlayerVesselInitializer.ReplaceVesselForPlayer_ClientRpc(
                    player.PlayerNetId, newVessel.VesselNetId, target);
            }
        }

        // ---------------------------------------------------------
        // AUTOPILOT
        // ---------------------------------------------------------

        void ActivateAutopilot(Player player)
        {
            if (player?.Vessel == null)
            {
                CSDebug.LogError("[MenuServerVesselInit] Player or Vessel not available after initialization.");
                return;
            }

            player.StartPlayer();
            player.Vessel.ToggleAIPilot(true);
            player.Vessel.VesselStatus.AIPilot.IgnoreItemDomainFilter = true;
            player.InputController.SetPause(true);

            // Camera setup is handled by MainMenuController.HandleMenuReady()
            // which activates the CM Main Menu Cinemachine camera for menu state.
        }
    }
}
