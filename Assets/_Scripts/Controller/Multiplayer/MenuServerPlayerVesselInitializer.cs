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
    /// by <see cref="Core.MainMenuController"/> - this class only handles the
    /// network spawn chain, autopilot activation, and vessel swap.
    ///
    /// Listens to <see cref="GameDataSO.OnPlayerNetworkSpawnedUlong"/> via the base class,
    /// which waits for NetworkVariables to sync before spawning.
    /// </summary>
    public class MenuServerPlayerVesselInitializer : ServerPlayerVesselInitializer
    {
        [Header("Menu Domain")]
        [Tooltip("Domain (team color) forced on every human's menu vessel. Jade by default " +
                 "so the autopilot renders green and the cell's flora/fauna react to a " +
                 "consistent domain. Server-write only: replicates to all peers via " +
                 "Player.NetDomain → OnNetDomainChanged (mirrors + full repaint). This is " +
                 "the ONLY menu domain reset - client code must never write domain locally.")]
        [SerializeField] Domains menuVesselDomain = Domains.Jade;

        bool _isSwapping;

        /// <summary>Whether a vessel swap is currently in progress.</summary>
        public bool IsSwapping => _isSwapping;

        // Menu vessels spawn with destroyWithScene=false so a joining client's vessel
        // survives the client's Single-mode Menu_Main scene-synchronize, which would
        // otherwise batch with and destroy the just-spawned vessel (the AI-vessel race).
        // Menu→game and leave-party paths despawn all vessels explicitly
        // (SceneLoader.ClearPlayerVesselReferences / GameDataSO.DestroyPlayerAndVessel),
        // so there is no leak.
        protected override bool DestroyVesselWithScene => false;

        /// <summary>
        /// Menu override: NO mode restriction. GameDataSO.AllowedVesselClasses deliberately
        /// survives ResetRuntimeData (it is pre-launch config the game scene must still read), so
        /// after playing a single-vessel mode it would still hold that mode's hull when the player
        /// returns here - and the lava-lamp vessel would silently be clamped to it. The menu is
        /// where you are ALLOWED to fly anything, so it takes the player's request verbatim.
        /// </summary>
        protected override VesselClassType ResolveSpawnVesselType(Player networkPlayer) =>
            networkPlayer.NetDefaultVesselType.Value;

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
        /// Menu override: reset the player's domain to the menu domain BEFORE the base
        /// spawns + paints the vessel, then activate autopilot after.
        /// Server-authoritative - the ONLY menu domain reset. Runs on every menu entry
        /// path (fresh start, party join, host-return from a game) and applies identically
        /// to the solo host and to party members: there is no separate single-player path.
        /// Writing before base means the vessel paints the menu domain at init; if the
        /// delta reaches a client after its pair-init instead, Player.OnNetDomainChanged
        /// re-syncs the mirrors and repaints (ShipHelper.SetShipProperties).
        /// </summary>
        protected override async UniTask OnPlayerReadyToSpawnAsync(Player player, CancellationToken ct)
        {
            if (!player.NetIsAI.Value && player.NetDomain.Value != menuVesselDomain)
                player.NetDomain.Value = menuVesselDomain;

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

            // Everything below runs inside try/finally. This method is async void in effect
            // (UniTaskVoid): an exception anywhere in the swap - a component throwing during
            // the new vessel's Initialize was the live case - is logged by the runtime and
            // then SWALLOWED, and without the finally `_isSwapping` stayed true forever. That
            // turned one broken vessel into a bricked changer: every later swap of ANY vessel
            // was silently refused by the IsSwapping gate until the scene reloaded.
            try
            {
                // 1. Find the player
                if (!gameData.TryGetPlayerByNetworkObjectId(playerNetId, out var iPlayer)
                    || iPlayer is not Player player)
                {
                    CSDebug.LogError($"[MenuServerVesselInit] Player {playerNetId} not found.");
                    return;
                }

                var oldVessel = player.Vessel;
                if (oldVessel == null)
                {
                    CSDebug.LogError($"[MenuServerVesselInit] Player {playerNetId} has no vessel to swap.");
                    return;
                }

                // Inherit the outgoing ship's velocity so the swap is seamless - the new vessel
                // continues at the same speed instead of the post-init dead stop (position + orientation
                // are inherited via SetPose below). Captured before despawn while the old vessel is valid.
                float snapshotSpeed = oldVessel.VesselStatus.Speed;

                // 2. Despawn old vessel
                DespawnVessel(oldVessel);

                // 3. Spawn new vessel
                var vesselNO = SpawnVesselForPlayer(ownerClientId, player, targetClass);
                if (!vesselNO)
                {
                    return;
                }

                if (!vesselNO.TryGetComponent(out IVessel newVessel))
                {
                    CSDebug.LogError("[MenuServerVesselInit] Spawned vessel missing IVessel component.");
                    return;
                }

                // 4. Re-initialize on host
                clientPlayerVesselInitializer.ReplaceVesselForPlayer(player, newVessel);
                newVessel.SetPose(snapshotPose);
                newVessel.SetInitialSpeed(snapshotSpeed);
                ActivateAutopilot(player);

                // 5. Wait for replication, then notify all non-host clients
                await UniTask.Delay(postSpawnDelayMs, cancellationToken: ct);
                NotifyClientsOfSwap(player, newVessel);

            }
            catch (System.OperationCanceledException) { }
            catch (System.Exception ex)
            {
                // Name the vessel and rethrow nothing: the finally has already unlatched the
                // changer, so the player can try another ship (or the same one after a fix)
                // instead of finding every station silently dead.
                CSDebug.LogError(
                    $"[MenuServerVesselInit] Swap to {targetClass} FAILED mid-flight: {ex}");
            }
            finally
            {
                _isSwapping = false;
            }
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

            // Camera setup is handled by MainMenuCameraController on OnClientReady -
            // it drives the Menu_Main scene camera directly (vessel-framing rig, no
            // Cinemachine) once the pair is ready.
        }
    }
}
