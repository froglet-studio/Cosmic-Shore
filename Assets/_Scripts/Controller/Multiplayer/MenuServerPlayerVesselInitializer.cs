using System.Threading;
using CosmicShore.Data;
using CosmicShore.Utility;
using Cysharp.Threading.Tasks;
using Reflex.Injectors;
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

        [Header("Freestyle AI Companions")]
        [Tooltip("Pilot skill (0..1) for AI companions released by the freestyle Lifeform Matrix " +
                 "toy. The menu has no intensity to derive one from, so it is authored here.")]
        [SerializeField, Range(0f, 1f)] float companionSkill = 0.5f;

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

            // Register the swap + AI-companion callbacks so client-originated requests
            // route to the server-side handlers.
            if (NetworkManager.Singleton.IsServer)
            {
                clientPlayerVesselInitializer.OnSwapRequested += HandleSwapRequest;
                clientPlayerVesselInitializer.OnAiCompanionRequested += HandleAiCompanionRequest;
            }
        }

        protected override void OnNetworkDespawn()
        {
            if (clientPlayerVesselInitializer)
            {
                clientPlayerVesselInitializer.OnSwapRequested -= HandleSwapRequest;
                clientPlayerVesselInitializer.OnAiCompanionRequested -= HandleAiCompanionRequest;
            }

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
        // AI COMPANIONS (freestyle)
        // ---------------------------------------------------------

        // Menu bots are named, not numbered, so two of the same hull are tellable apart in the
        // lava lamp AND so GameDataSO.AddPlayer's name-keyed dedup can never collapse them onto
        // one roster entry. Instance-scoped: the initializer dies with the scene, and so do its
        // bots (SceneLoader.ClearPlayerVesselReferences despawns every AI on the way out).
        int _aiCompanionCount;

        /// <summary>
        /// Release an AI-piloted vessel into the menu cell - the freestyle Lifeform Matrix's
        /// VESSELS branch. Host does it directly; a party client asks the host over the same
        /// request/handler shape as <see cref="RequestSwap"/>, so the bot exists once, on the
        /// server, and replicates to everyone (a locally-spawned one would be invisible to the
        /// rest of the party).
        /// </summary>
        public void RequestSpawnAiCompanion(VesselClassType vesselClass, Domains domain, Pose pose)
        {
            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening || _cts == null)
            {
                CSDebug.LogWarning("[MenuServerVesselInit] No live network session - cannot release an AI companion.");
                return;
            }

            if (NetworkManager.Singleton.IsServer)
                SpawnAiCompanionAsync(vesselClass, domain, pose, _cts.Token).Forget();
            else
                clientPlayerVesselInitializer.RequestAiCompanion_ServerRpc(
                    vesselClass, domain, pose.position, pose.rotation);
        }

        void HandleAiCompanionRequest(VesselClassType vesselClass, Domains domain, Pose pose)
        {
            SpawnAiCompanionAsync(vesselClass, domain, pose, _cts.Token).Forget();
        }

        /// <summary>
        /// The server-side release. Deliberately the SAME chain a backfill AI goes through in
        /// <see cref="ServerPlayerVesselInitializerWithAI"/> - spawn the Player NetworkObject,
        /// stamp its NetworkVariables, spawn its vessel, initialize the pair, configure the pilot,
        /// autopilot it - so a menu companion is an ordinary networked AI player rather than a
        /// second, parallel kind of bot.
        /// </summary>
        async UniTaskVoid SpawnAiCompanionAsync(
            VesselClassType vesselClass, Domains domain, Pose pose, CancellationToken ct)
        {
            var playerPrefab = ResolveAiPlayerPrefab();
            if (!playerPrefab)
            {
                CSDebug.LogError("[MenuServerVesselInit] No AI Player prefab available - cannot release a companion.");
                return;
            }

            if (!vesselPrefabContainer.TryGetShipPrefab(vesselClass, out _))
            {
                CSDebug.LogError($"[MenuServerVesselInit] No prefab for vessel type {vesselClass} - companion not released.");
                return;
            }

            var aiPlayerNO = Instantiate(playerPrefab);
            GameObjectInjector.InjectRecursive(aiPlayerNO.gameObject, _container);
            // destroyWithScene: false, matching every other menu spawn - the explicit
            // SceneLoader.ClearPlayerVesselReferences despawn is what removes these.
            aiPlayerNO.Spawn(false);

            if (!aiPlayerNO.TryGetComponent(out Player aiPlayer))
            {
                CSDebug.LogError("[MenuServerVesselInit] AI Player prefab is missing its Player component.");
                aiPlayerNO.Despawn(true);
                return;
            }

            // Claim it in the SAME frame as the spawn. A server-owned Player carries the HOST's
            // OwnerClientId, and Player.OnNetworkSpawn has already raised the spawn event from
            // inside Spawn() above (its owner branch fills in a name and vessel type), so without
            // this the human path would read that event as the host asking for a second vessel.
            // The handler defers 200ms for NetworkVariable replication, which is what gives this
            // synchronous claim time to land.
            ClaimExternallySpawnedPlayer(aiPlayer);

            aiPlayer.NetIsAI.Value = true;
            aiPlayer.NetDefaultVesselType.Value = vesselClass;
            aiPlayer.NetDomain.Value = domain;
            aiPlayer.NetName.Value = $"{vesselClass} Bot {++_aiCompanionCount}";

            var vesselNO = SpawnVesselForPlayer(aiPlayer.OwnerClientId, aiPlayer, vesselClass);
            if (!vesselNO)
            {
                aiPlayerNO.Despawn(true);
                return;
            }

            if (!vesselNO.TryGetComponent(out IVessel vessel))
            {
                CSDebug.LogError("[MenuServerVesselInit] Spawned companion vessel missing IVessel component.");
                vesselNO.Despawn(true);
                aiPlayerNO.Despawn(true);
                return;
            }

            clientPlayerVesselInitializer.InitializePlayerAndVessel(aiPlayer, vessel);

            // AddPlayer (inside the pair init) hands out one of the menu's authored spawn poses.
            // Override it with the pose the player actually asked for, so the bot appears at the
            // station they flew rather than across the cell.
            vessel.SetPose(pose);

            ConfigureCompanionPilot(vesselNO);
            ActivateAutopilot(aiPlayer);

            CSDebug.Log($"[MenuServerVesselInit] Released AI companion '{aiPlayer.NetName.Value}' " +
                        $"({vesselClass}, {domain}) at {pose.position}.");

            // Let the vessel NetworkObject replicate before telling clients to bind the pair.
            await UniTask.Delay(postSpawnDelayMs, DelayType.UnscaledDeltaTime, cancellationToken: ct);
            NotifyClients(aiPlayer);
        }

        /// <summary>
        /// The AI Player prefab, resolved from the live <see cref="NetworkConfig.PlayerPrefab"/>.
        /// That IS the prefab every game scene wires by hand into
        /// <c>ServerPlayerVesselInitializerWithAI.aiPlayerPrefab</c> (<c>_Prefabs/CORE/Player</c>),
        /// so reading it here keeps the menu's companion path from carrying a second scene
        /// reference that can drift out of sync with the registered NetworkPrefab.
        /// </summary>
        static NetworkObject ResolveAiPlayerPrefab()
        {
            var prefab = NetworkManager.Singleton != null
                ? NetworkManager.Singleton.NetworkConfig?.PlayerPrefab
                : null;
            return prefab ? prefab.GetComponent<NetworkObject>() : null;
        }

        /// <summary>
        /// A menu companion flies the lava lamp: it seeks the cell's crystals and mass like any
        /// backfill bot, and it never seeks PILOTS - freestyle has no objective, and a bot the
        /// player released should not then hunt them.
        /// </summary>
        void ConfigureCompanionPilot(NetworkObject vesselNO)
        {
            var aiPilot = vesselNO.GetComponentInChildren<AIPilot>();
            if (!aiPilot) return;
            aiPilot.ConfigureForGameMode(gameData, shouldSeekPlayers: false, companionSkill);
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
