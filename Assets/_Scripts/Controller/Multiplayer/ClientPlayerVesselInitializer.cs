using System;
using System.Collections.Generic;
using System.Threading;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using CosmicShore.Utility;
using CosmicShore.Utility.PerformanceBenchmark;
using Cysharp.Threading.Tasks;
using Reflex.Attributes;
using Reflex.Core;
using Reflex.Injectors;
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
    /// re-processing of the queue - zero WaitUntil polling.
    /// </summary>
    public class ClientPlayerVesselInitializer : NetworkBehaviour
    {
        [SerializeField] ThemeManagerDataContainerSO themeManagerData;

        [Inject] protected GameDataSO gameData;
        [Inject] Container _container;

        readonly List<(ulong playerNetId, ulong vesselNetId)> _pendingPairs = new();
        readonly List<(ulong playerNetId, ulong vesselNetId)> _pendingSwaps = new();
        bool _signalClientReadyWhenDone;

        // Client-pull bootstrap state (see RosterPullRetryLoop).
        CancellationTokenSource _rosterRetryCts;
        bool _localPairResolved;

        // Load Time Insights span: open while pairs are queued waiting for replication.
        int _pendingWaitSpan = -1;

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

            // Client-pull bootstrap (unbreakable join): ask the host for the current
            // roster from inside our own OnNetworkSpawn. Because this object now
            // provably exists on the client, the host's reply ClientRpc cannot be
            // dropped for "target not spawned" - the root cause of the legacy
            // one-shot-push hang. A bounded retry re-asks if the request or reply is
            // lost, so convergence never depends on catching a transient SOAP event.
            _localPairResolved = false;
            _rosterRetryCts = new CancellationTokenSource();
            RosterPullRetryLoop(_rosterRetryCts.Token).Forget();
        }

        public override void OnNetworkDespawn()
        {
            gameData.OnPlayerNetworkSpawnedUlong.OnRaised -= OnPlayerNetworkSpawnedForPending;
            gameData.OnVesselNetworkSpawned.OnRaised -= ProcessPendingPairs;
            gameData.OnVesselNetworkSpawned.OnRaised -= ProcessPendingSwaps;
            _pendingPairs.Clear();
            _pendingSwaps.Clear();

            _rosterRetryCts?.Cancel();
            _rosterRetryCts?.Dispose();
            _rosterRetryCts = null;
            _localPairResolved = false;

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
        /// Server-side callback registered by <see cref="ServerPlayerVesselInitializer"/>
        /// to build and (re)send the full roster to a requesting client. Invoked by
        /// <see cref="RequestRosterFromHost_ServerRpc"/>.
        /// </summary>
        public Action<ulong> OnRosterRequested;

        /// <summary>
        /// Server-side callback registered by <see cref="MenuServerPlayerVesselInitializer"/> to
        /// release a freestyle AI companion. Parameters: vesselClass, domain, spawn pose.
        /// </summary>
        public Action<VesselClassType, Domains, Pose> OnAiCompanionRequested;

        /// <summary>
        /// Direct server-side vessel replacement (called by MenuServerPlayerVesselInitializer on host).
        /// The player already has a vessel - this wires the new one in place.
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
        /// Called by a non-host client to release an AI companion (the freestyle Lifeform Matrix's
        /// VESSELS branch). Spawning a <see cref="Player"/> + vessel is server-only, exactly like a
        /// vessel swap, so a client asks and the server does it - never a locally-spawned bot that
        /// nobody else in the party can see.
        /// </summary>
        [ServerRpc(RequireOwnership = false)]
        internal void RequestAiCompanion_ServerRpc(
            VesselClassType vesselClass,
            Domains domain,
            Vector3 spawnPos,
            Quaternion spawnRot,
            ServerRpcParams rpcParams = default)
        {
            OnAiCompanionRequested?.Invoke(vesselClass, domain, new Pose(spawnPos, spawnRot));
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
        // CLIENT-PULL ROSTER BOOTSTRAP (client-side)
        // ---------------------------------------------------------

        /// <summary>
        /// Client → host request for the current player-vessel roster. The client
        /// calls this from its own OnNetworkSpawn (and on a bounded retry), so the
        /// host's reply (<see cref="InitializeAllPlayersAndVessels_ClientRpc"/>) is
        /// delivered to an object that provably exists and cannot be dropped.
        /// </summary>
        [ServerRpc(RequireOwnership = false)]
        internal void RequestRosterFromHost_ServerRpc(ServerRpcParams rpcParams = default)
        {
            OnRosterRequested?.Invoke(rpcParams.Receive.SenderClientId);
        }

        /// <summary>
        /// Bounded self-healing loop: re-asks the host for the roster until the local
        /// player's pair resolves. Recovers a dropped request, a dropped reply, or a
        /// late host-side spawn. Cancelled once the local pair is initialised
        /// (<see cref="InitializePair"/>) or on despawn.
        /// </summary>
        async UniTaskVoid RosterPullRetryLoop(CancellationToken ct)
        {
            // These must cover MORE wall-clock than PartyInviteController.joinReadyTimeoutSeconds,
            // or the loop that is supposed to recover the join gives up while the watchdog that
            // BOUNCES the player is still counting. At the shipped 4 x 1500ms the retry died at
            // 6s against a 10s watchdog - four seconds in which nothing was retrying and the only
            // possible outcome was a bounce to the solo menu.
            //
            // The two clocks do not even START together: this loop starts in OnNetworkSpawn, which
            // on a joining client runs DURING Netcode synchronization, while the watchdog starts
            // only after IsConnectedClient - i.e. after synchronization completes, plus the
            // connect wait. So "24s against a 30s watchdog" was still short by the whole sync
            // time on the one link that needs it. 60s outlives every watchdog in the project
            // (connect 30s + ready 30s) whatever the sync took; the loop is cancelled the moment
            // the local pair resolves, and on despawn, so the cap only ever bounds a join that
            // was already lost.
            const int maxAttempts = 40;
            const int intervalMs = 1500;

            for (int attempt = 0; attempt < maxAttempts && !_localPairResolved; attempt++)
            {
                if (IsSpawned)
                    RequestRosterFromHost_ServerRpc();

                try
                {
                    using (LoadInsights.Measure(LoadInsightCategory.Netcode,
                               $"Roster pull retry wait (client, {intervalMs}ms)", isWait: true))
                    {
                        await UniTask.Delay(intervalMs, DelayType.UnscaledDeltaTime, cancellationToken: ct);
                    }
                }
                catch (OperationCanceledException)
                {
                    return;
                }

                if (_localPairResolved) return;

                // Re-attempt resolution against current authoritative state in case a
                // reply arrived but its SOAP nudge fired before the tuple was queued.
                ReRegisterPersistentPlayers();
                ProcessPendingPairs();
            }
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

                // The replication wait for this pair is over — close the wait span before
                // InitializePair, which may raise OnClientReady (the recording endpoint).
                if (_pendingWaitSpan >= 0)
                {
                    LoadInsights.End(_pendingWaitSpan);
                    _pendingWaitSpan = -1;
                }

                InitializePair(player, vessel);
                _pendingPairs.RemoveAt(i);
            }

            // Load Time Insights: keep one wait-span open for exactly as long as pairs sit
            // queued waiting for their NetworkObjects to replicate — the client's main
            // invisible wait during a multiplayer load. Managed BEFORE the client-ready
            // fallback below: InvokeClientReady is the recording endpoint, so the span must
            // already be closed when it fires.
            if (_pendingPairs.Count > 0 && _pendingWaitSpan < 0)
            {
                _pendingWaitSpan = LoadInsights.Begin(LoadInsightCategory.Netcode,
                    "Waiting for player/vessel NetworkObjects to replicate (pending pairs)", isWait: true);
            }
            else if (_pendingPairs.Count == 0 && _pendingWaitSpan >= 0)
            {
                LoadInsights.End(_pendingWaitSpan);
                _pendingWaitSpan = -1;
            }

            if (_pendingPairs.Count == 0 && _signalClientReadyWhenDone)
            {
                // The batch only counts as complete once the LOCAL pair actually
                // resolved. The client-pull roster request fires from our own
                // OnNetworkSpawn, so the host's reply can legitimately predate our
                // vessel spawn (preSpawnDelay + spawn + postSpawnDelay on the host)
                // - that roster contains every pair EXCEPT ours. Declaring ready on
                // it would raise OnClientReady with no local vessel (the party
                // accept flow completes early) and, worse, cancel the
                // RosterPullRetryLoop - so a lost follow-up push would strand this
                // client vessel-less with no self-heal left. Most likely for the
                // 2nd+ joiner into an existing party (Docs/PartySystem/BUGS.md B5).
                // Keep the flag armed: the retry loop re-asks, and the next
                // full-roster reply (or the host's push) completes the batch.
                if (gameData.LocalPlayer?.Vessel == null)
                    return;

                _signalClientReadyWhenDone = false;
                _localPairResolved = true;
                _rosterRetryCts?.Cancel();
                // Fallback: if the local player's pair was skipped via the
                // "already initialized" branch above, InvokeClientReady was
                // never called inside InitializePair.  Call it here so the
                // loading screen always clears when all pairs are resolved.
                gameData.InvokeClientReady();
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

        /// <summary>
        /// Netcode-replicated vessels bypass Reflex: NetworkManager instantiates the
        /// prefab directly, so every [Inject] field on the vessel's components
        /// (ActionExecutorRegistry.AudioSystem, executor AudioSystems, GameDataSO
        /// mirrors, …) is null on non-server peers. The server/host injects at
        /// instantiation time (ServerPlayerVesselInitializer.SpawnVesselForPlayer),
        /// so only client-side instances need it here - before vessel.Initialize()
        /// so executors see their dependencies during their own Initialize.
        /// </summary>
        void InjectVesselDependencies(IVessel vessel)
        {
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
                return;

            GameObjectInjector.InjectRecursive(vessel.Transform.gameObject, _container);
        }

        void InitializePair(IPlayer player, IVessel vessel)
        {
            CSDebug.LogVerbose(CSLogChannel.NetworkFlow, $"<color=#00FF00>[FLOW-6] [ClientVesselInit] InitializePair - Player={player.Name}, IsLocalUser={player.IsLocalUser}, IsAI={player.IsInitializedAsAI}</color>");
            // Explicit handle (not `using`): the local pair raises OnClientReady - the visual-ready
            // milestone - from inside this method, so the span must close before that call.
            int pairSpan = LoadInsights.Begin(
                player.IsInitializedAsAI ? LoadInsightCategory.AiBackfill : LoadInsightCategory.Vessels,
                $"Pair init - inject + vessel.Initialize ({player.Name})");
            InjectVesselDependencies(vessel);
            player.InitializeForMultiplayerMode(vessel);
            vessel.Initialize(player);
            ShipHelper.SetShipProperties(themeManagerData, vessel);
            // Stash the theme reference on Player so OnNetDomainChanged can repaint
            // the vessel materials when NetDomain replicates after spawn (modal Blue
            // reset, NormalizeUnassignedHumans reroll, shape-mode SetDomain, etc).
            if (player is Player p) p._vesselThemeManagerData = themeManagerData;
            gameData.AddPlayer(player);
            CSDebug.LogVerbose(CSLogChannel.NetworkFlow, $"<color=#00FF00>[FLOW-6] [ClientVesselInit] AddPlayer done. Players.Count={gameData.Players.Count}, LocalPlayer={gameData.LocalPlayer?.Name}</color>");

            // Signal this specific player-vessel pair is fully initialized.
            // Subscribers (e.g. MainMenuController) activate non-local players
            // individually when their own pair resolves, avoiding the race
            // condition of batch-activating players whose vessels haven't
            // replicated yet.
            gameData.InvokePlayerPairInitialized(player.PlayerNetId);

            if (player.IsLocalUser && CameraManager.Instance)
                CameraManager.Instance.SnapPlayerCameraToTarget();

            LoadInsights.End(pairSpan);

            if (player.IsLocalUser)
            {
                // Local pair resolved - stop the client-pull retry loop and clear the splash.
                _localPairResolved = true;
                _rosterRetryCts?.Cancel();
                CSDebug.LogVerbose(CSLogChannel.NetworkFlow, "<color=#FFFFFF><b>[FLOW-6] [ClientVesselInit] Raising OnClientReady (local player initialized)</b></color>");
                gameData.InvokeClientReady();
            }
        }

        /// <summary>
        /// Re-initializes a player-vessel pair during a vessel swap.
        /// Unlike <see cref="InitializePair"/>, the player is already in
        /// <see cref="GameDataSO.Players"/> and has domain/name set -
        /// only the vessel reference needs to change.
        /// </summary>
        void ReInitializePair(IPlayer player, IVessel newVessel)
        {
            InjectVesselDependencies(newVessel);
            player.ChangeVessel(newVessel);

            // Keep the swapped-in vessel on the player's CHOSEN domain - a vessel swap must never
            // repaint the hull back to the Jade menu default, or desync the domain-changer toy
            // (which reads the live Player.Domain). The new vessel paints its domain during
            // Initialize/SetShipProperties below, reading status.Domain (= Player.Domain), which
            // falls back to Jade if it lags the authoritative NetDomain. Re-sync the mirror from
            // NetDomain first so both the host and the client swap paths paint the current colour.
            if (player is Player netP) netP.SetDomain(netP.NetDomain.Value);

            newVessel.Initialize(player);
            ShipHelper.SetShipProperties(themeManagerData, newVessel);
            // Re-stash so a later NetDomain change keeps the SWAPPED vessel in sync.
            if (player is Player p) p._vesselThemeManagerData = themeManagerData;

            // Signal the (re)initialized pair exactly like InitializePair does. The new vessel's
            // VesselHUDController was just initialized HIDDEN by VesselController.Initialize, and the
            // swap path never re-enters freestyle, so nothing would re-show it - leaving the swapped
            // ship with no working HUD. MenuMiniGameHUD listens for this and re-shows the local HUD
            // while in freestyle; MainMenuController re-activates non-local swapped vessels.
            gameData.InvokePlayerPairInitialized(player.PlayerNetId);

            if (player.IsLocalUser && CameraManager.Instance)
                CameraManager.Instance.SnapPlayerCameraToTarget();
        }
    }
}
