using System;
using CosmicShore.Data;
using CosmicShore.Utility;
using Cysharp.Threading.Tasks;
using Unity.Netcode;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Menu_Main vessel initializer. After the base class spawns the vessel,
    /// activates autopilot for the player.
    ///
    /// Also handles runtime vessel swaps requested by any client via
    /// ClientPlayerVesselInitializer.RequestVesselSwap_ServerRpc.
    ///
    /// Game data configuration (vessel class, player count, intensity) is handled
    /// by MainMenuController — this class only handles the network spawn chain,
    /// autopilot activation, and vessel swap.
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
        /// Override: after spawning vessel, wait for it to be ready, then activate autopilot.
        /// </summary>
        protected override void OnClientConnected(ulong clientId)
        {
            SpawnAndActivateAutopilot(clientId).Forget();
        }

        async UniTaskVoid SpawnAndActivateAutopilot(ulong clientId)
        {
            try
            {
                // Wait for vessel spawn (500ms delay) + spawn vessel + notify clients (500ms delay)
                await UniTask.Delay(500, DelayType.UnscaledDeltaTime, cancellationToken: _cts.Token);

                var playerNetObj = NetworkManager.Singleton.SpawnManager.GetPlayerNetworkObject(clientId);
                if (!playerNetObj || !playerNetObj.TryGetComponent<Player>(out var player))
                {
                    CSDebug.LogError($"[MenuServerVesselInit] Player not found for client {clientId}");
                    return;
                }

                player.NetDomain.Value = DomainAssigner.GetDomainsByGameModes(gameData.GameMode);
                player.NetIsAI.Value = false;

                if (player.NetDefaultVesselType.Value == VesselClassType.Random)
                    player.NetDefaultVesselType.Value = VesselClassType.Dolphin;

                SpawnVesselForPlayer(clientId, player);

                // Wait for vessel to replicate
                await UniTask.Delay(500, DelayType.UnscaledDeltaTime, cancellationToken: _cts.Token);

                // Activate autopilot
                ActivateAutopilot(player);

                // Notify clients
                NotifyClients(clientId);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                CSDebug.LogError($"[MenuServerVesselInit] Error in SpawnAndActivateAutopilot: {ex}");
            }
        }

        // ---------------------------------------------------------
        // VESSEL SWAP (server-side)
        // ---------------------------------------------------------

        /// <summary>
        /// Entry point for the host's UI: request a vessel swap for the local player.
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
                SwapVesselAsync(
                    netPlayer.OwnerClientId,
                    netPlayer.NetworkObjectId,
                    targetClass,
                    pose,
                    _cts.Token).Forget();
            }
            else
            {
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
            System.Threading.CancellationToken ct)
        {
            _isSwapping = true;

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

            DespawnVessel(oldVessel);

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

            clientPlayerVesselInitializer.ReplaceVesselForPlayer(player, newVessel);
            newVessel.SetPose(snapshotPose);
            ActivateAutopilot(player);

            await UniTask.Delay(200, cancellationToken: ct);
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
            player.InputController.SetPause(true);
        }
    }
}
