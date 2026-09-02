using System.Collections.Generic;
using System.Threading;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using CosmicShore.Utility.PerformanceBenchmark;
using Cysharp.Threading.Tasks;
using Reflex.Attributes;
using Reflex.Core;
using Reflex.Injectors;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Serialization;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Server-side vessel spawner.
    ///
    /// Flow:
    ///   OnNetworkSpawn → subscribe to OnPlayerNetworkSpawnedUlong
    ///   OnPlayerNetworkSpawnedUlong(ownerClientId) → wait for NetworkVariables to sync
    ///   → spawn vessel → server-side init
    ///   → wait → notify existing clients about new player
    ///   → notify new client about all players
    ///
    /// RPCs:
    ///   New client   → InitializeAllPlayersAndVessels_ClientRpc (all pairs)
    ///   Existing clients → InitializeNewPlayerAndVessel_ClientRpc (just the new pair)
    /// </summary>
    [RequireComponent(typeof(NetcodeHooks))]
    public class ServerPlayerVesselInitializer : MonoBehaviour
    {
        [Header("Dependencies")]
        [Inject] protected GameDataSO gameData;
        [Inject] protected Container _container;

        [FormerlySerializedAs("clientPlayerSpawner")]
        [SerializeField] protected ClientPlayerVesselInitializer clientPlayerVesselInitializer;

        [SerializeField] protected VesselPrefabContainer vesselPrefabContainer;

        /// <summary>The vessel-prefab registry this spawner uses. Exposed read-only so display-only
        /// consumers (e.g. the freestyle vessel-changer toy's mini models) can look up a ship prefab
        /// without duplicating the mapping.</summary>
        public VesselPrefabContainer VesselPrefabContainer => vesselPrefabContainer;

        [Header("Spawn Points")]
        [Tooltip("Scene-placed spawn transforms. If set, overrides GameDataSO.SpawnPoses on network spawn.")]
        [SerializeField] protected Transform[] playerSpawnPoints;

        [Tooltip("Ignore the authored spawn transforms and COMPUTE the spawn ring from the cell: " +
                 "players are placed symmetrically on a sphere around the cell centre, all facing " +
                 "it, at the cell nucleus radius + Spawn Distance Outside Nucleus. 4 players get " +
                 "tetrahedral symmetry, 3 an equilateral triangle, 2 opposite ends of one axis.")]
        [SerializeField] protected bool arrangeSpawnPointsAroundCell;

        [Tooltip("How far OUTSIDE the cell nucleus surface each player starts. Only used when " +
                 "Arrange Spawn Points Around Cell is on.")]
        [SerializeField, Min(0f)] protected float spawnDistanceOutsideNucleus = 40f;

        [Tooltip("Floor for the computed spawn-ring radius, for a cell whose 'core' is NOT a " +
                 "nucleus. The ring is max(nucleus radius + Spawn Distance Outside Nucleus, this). " +
                 "Ribcage needs it: its cell has no NucleusPrefab (a nucleus control zone would " +
                 "break the mode's fauna diet), so the nucleus radius is 0 and the ring would " +
                 "collapse to the cell centre - INSIDE the 300u cage the players are meant to be " +
                 "attacking from outside. 0 = no floor (every existing scene is unchanged).")]
        [SerializeField, Min(0f)] protected float spawnRingRadiusFloor;

        [Tooltip("How the computed ring distributes players. Symmetric spreads them over a SPHERE " +
                 "(4 tetrahedral, 3 triangle, 2 antipodal). Equatorial Ring puts everyone on one " +
                 "horizontal circle, evenly spaced, the way Joust authors its points by hand - use " +
                 "it when the arena has a meaningful 'up' or a pole feature, so no player is handed " +
                 "a harder approach than the others. Only used when Arrange Spawn Points Around " +
                 "Cell is on.")]
        [SerializeField] protected CellSpawnFormation.Formation spawnFormation =
            CellSpawnFormation.Formation.Symmetric;

        [Tooltip("The cell whose nucleus the computed spawn ring measures off. Only used when " +
                 "Arrange Spawn Points Around Cell is on.")]
        [SerializeField] protected CellRuntimeDataSO cellData;

        // The computed ring is built ONCE per scene: GameDataSO draws spawn poses from a pool it
        // pops from, so recomputing mid-spawn would refill the pool and hand two players the
        // same pose.
        bool _cellSpawnRingBuilt;

        [Header("Timing")]
        [Tooltip("Delay in ms after OnPlayerNetworkSpawned before reading NetworkVariables.")]
        [SerializeField] protected int preSpawnDelayMs = 200;

        [Tooltip("Delay in ms after vessel spawn before notifying clients.")]
        [SerializeField] protected int postSpawnDelayMs = 200;

        // Whether spawned vessels are destroyed with their scene. Game scenes keep
        // the default (true). Menu_Main overrides to false so a joining client's
        // vessel survives the client's scene-synchronize batching destroy - the same
        // race the AI vessels hit (see ServerPlayerVesselInitializerWithAI).
        protected virtual bool DestroyVesselWithScene => true;

        NetcodeHooks _netcodeHooks;
        protected CancellationTokenSource _cts;

        /// <summary>
        /// Tracks players already processed (keyed by NetworkObjectId).
        /// Using NetworkObjectId because server-owned AI players share the host's OwnerClientId.
        /// </summary>
        protected readonly HashSet<ulong> _processedPlayers = new();

        /// <summary>
        /// Owner client ids for which at least one player was spawned by somebody OTHER than this
        /// spawner's human path (see <see cref="ClaimExternallySpawnedPlayer"/>). Server-owned AI
        /// players share the host's owner id, so their spawn event arrives here looking exactly
        /// like the host's own - this is how the handler tells "already handled elsewhere" from
        /// "a player went missing".
        /// </summary>
        readonly HashSet<ulong> _claimedForeignOwners = new();

        /// <summary>
        /// Players whose persistent state has been re-initialized for THIS scene
        /// (<see cref="Player.PrepareForNewScene"/>, which zeroes RoundStats). Separate from
        /// <see cref="_processedPlayers"/> because that set is removed from on the not-ready
        /// retry path - a player can be processed more than once, but must be reset exactly once.
        /// </summary>
        readonly HashSet<ulong> _preparedForScene = new();

        /// <summary>
        /// How many times the spawn event has been re-armed for a player whose owner-written
        /// values had not replicated yet, keyed by NetworkObjectId. Bounds the re-raise loop.
        /// </summary>
        readonly Dictionary<ulong, int> _spawnReArms = new();

        /// <summary>Re-arm budget per player. Each round costs the ~2.2s readiness wait below,
        /// so this covers roughly 13 further seconds of replication delay before giving up.</summary>
        const int MaxSpawnReArms = 6;

        protected virtual void Awake()
        {
            _netcodeHooks = GetComponent<NetcodeHooks>();
            _netcodeHooks.OnNetworkSpawnHook += OnNetworkSpawn;
            _netcodeHooks.OnNetworkDespawnHook += OnNetworkDespawn;
        }

        protected virtual void OnDestroy()
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;

            if (_netcodeHooks)
            {
                _netcodeHooks.OnNetworkSpawnHook -= OnNetworkSpawn;
                _netcodeHooks.OnNetworkDespawnHook -= OnNetworkDespawn;
            }
        }

        protected virtual void OnNetworkSpawn()
        {
            if (!NetworkManager.Singleton.IsServer)
            {
                CSDebug.LogVerbose(CSLogChannel.NetworkFlow, "<color=#00FF00>[FLOW-5] [ServerVesselInit] OnNetworkSpawn - NOT server, disabling</color>");
                enabled = false;
                return;
            }

            CSDebug.LogVerbose(CSLogChannel.NetworkFlow, $"<color=#00FF00>[FLOW-5] [ServerVesselInit] OnNetworkSpawn - IsServer=true, subscribing to OnPlayerNetworkSpawnedUlong. gameData.Players.Count={gameData.Players.Count}</color>");

            // The computed ring needs the cell's nucleus, which the Cell spawns in Initialize -
            // deferred to the first vessel spawn (EnsureSpawnPosesReady) so it can't read a
            // nucleus that doesn't exist yet.
            if (!arrangeSpawnPointsAroundCell && playerSpawnPoints != null && playerSpawnPoints.Length > 0)
                gameData.SetSpawnPositions(playerSpawnPoints);

            _cts = new CancellationTokenSource();
            gameData.OnPlayerNetworkSpawnedUlong.OnRaised += HandlePlayerNetworkSpawned;

            // Client-pull: answer roster requests from freshly-joined clients.
            clientPlayerVesselInitializer.OnRosterRequested = HandleRosterRequest;

            // Process players that were already spawned before this initializer
            // existed (e.g. the host's Player object spawned in the Auth scene
            // before Menu_Main loaded). Their SOAP event was already raised and missed.
            ProcessPreExistingPlayers();
        }

        void ProcessPreExistingPlayers()
        {
            // Stage 1: Check gameData.Players (catches players spawned in THIS scene,
            // e.g. AI players whose OnNetworkSpawn() already added them).
            foreach (var p in gameData.Players)
            {
                if (p is Player netPlayer && netPlayer.IsSpawned)
                    HandlePlayerNetworkSpawned(netPlayer.OwnerClientId);
            }

            // Stage 2: Trigger spawn chain for persistent human Players.
            // Player NetworkObjects survive Netcode scene loads (DestroyWithScene=false)
            // but are cleared from gameData.Players by ResetRuntimeData().
            // Their OnNetworkSpawn() won't re-fire, so we initiate the spawn chain here.
            // Actual re-initialization (PrepareForNewScene) happens in
            // HandlePlayerNetworkSpawnedAsync() after the preSpawnDelay, which ensures it runs
            // after any Start()-based list clearing (e.g. scene-placed
            // MultiplayerSetup.DestroyPlayerAndVessel).
            var nm = NetworkManager.Singleton;
            if (nm == null) return;

            foreach (var kvp in nm.ConnectedClients)
            {
                var playerObj = kvp.Value.PlayerObject;
                if (playerObj == null || !playerObj.TryGetComponent<Player>(out var player))
                    continue;
                if (!player.IsSpawned || _processedPlayers.Contains(player.NetworkObjectId))
                    continue;

                HandlePlayerNetworkSpawned(player.OwnerClientId);
            }
        }

        protected virtual void OnNetworkDespawn()
        {
            gameData.OnPlayerNetworkSpawnedUlong.OnRaised -= HandlePlayerNetworkSpawned;
            if (clientPlayerVesselInitializer != null)
                clientPlayerVesselInitializer.OnRosterRequested = null;
            _processedPlayers.Clear();
            _preparedForScene.Clear();
            _spawnReArms.Clear();
            _cellSpawnRingBuilt = false; // a replay re-spawns the cell; rebuild against the new nucleus

            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;

            // The network is intentionally NOT shut down here. Under the eager-Relay
            // design the party/Relay session persists across every scene transition
            // (game ↔ Menu_Main, replay). Network teardown is owned exclusively by the
            // explicit leave/quit paths (PartyInviteController.LeavePartyAndReturnToMenuAsync,
            // MultiplayerSetup.OnTransportFailure) - never by a vessel spawner's despawn.
        }

        /// <summary>
        /// Called when a Player's OnNetworkSpawn fires. The ownerClientId
        /// identifies which client owns this player. We wait a short delay
        /// for NetworkVariables (NetDomain, NetDefaultVesselType, NetIsAI, NetName)
        /// to replicate, then proceed with vessel spawning.
        /// </summary>
        void HandlePlayerNetworkSpawned(ulong ownerClientId)
        {
            HandlePlayerNetworkSpawnedAsync(ownerClientId, _cts.Token).Forget();
        }

        async UniTaskVoid HandlePlayerNetworkSpawnedAsync(ulong ownerClientId, CancellationToken ct)
        {
            CSDebug.LogVerbose(CSLogChannel.NetworkFlow, $"<color=#00FF00>[FLOW-5] [ServerVesselInit] HandlePlayerNetworkSpawnedAsync - ownerClientId={ownerClientId}, waiting {preSpawnDelayMs}ms for NetworkVariables</color>");
            // Wait for NetworkVariables set in Player.OnNetworkSpawn to sync
            using (LoadInsights.Measure(LoadInsightCategory.ScriptedDelay,
                       $"preSpawnDelayMs before vessel spawn ({preSpawnDelayMs}ms)", isWait: true))
            {
                await UniTask.Delay(preSpawnDelayMs, DelayType.UnscaledDeltaTime, cancellationToken: ct);
            }

            Player player = FindUnprocessedPlayerByOwnerClientId(ownerClientId);
            if (player == null)
            {
                // A SERVER-OWNED Player (AI) shares the host's OwnerClientId, so its spawn event
                // is indistinguishable here from the host's own. Whoever created it has already
                // claimed it (ClaimExternallySpawnedPlayer), so there is genuinely nothing for the
                // human path to do and this is not a fault - only an unclaimed owner is.
                //
                // This is not a rare edge: ProcessPreExistingPlayers calls the handler once per
                // entry in gameData.Players, so a scene with N backfill AI produced N of these
                // warnings on every load. The warning that survives below is the one that was
                // always meant - an owner whose player really did go missing.
                if (_claimedForeignOwners.Contains(ownerClientId))
                    CSDebug.LogVerbose(CSLogChannel.NetworkFlow,
                        $"<color=#FFA500>[FLOW-5] [ServerVesselInit] Spawn event for {ownerClientId} " +
                        "resolved to an already-claimed server-owned player - nothing to do.</color>");
                else
                    Debug.LogWarning($"<color=#FFA500>[FLOW-5] [ServerVesselInit] FindUnprocessedPlayerByOwnerClientId({ownerClientId}) returned NULL</color>");
                return;
            }

            CSDebug.LogVerbose(CSLogChannel.NetworkFlow, $"<color=#00FF00>[FLOW-5] [ServerVesselInit] Found player: Name={player.NetName.Value}, VesselType={player.NetDefaultVesselType.Value}, NetworkObjectId={player.NetworkObjectId}</color>");

            // Re-initialize the PERSISTENT Player for this scene - exactly once, for every player,
            // whichever way it was found. RoundStats lives on the Player NetworkObject and survives
            // every scene load, so skipping this carries the previous game's stats straight into
            // the new one: players began a match with a non-zero score.
            //
            // This used to live inside FindUnprocessedPlayerByOwnerClientId, on its FALLBACK branch
            // only - so whether a player's score started at zero depended on which lookup branch
            // happened to find them, which is why it hit "some" players and not others. A finder
            // must not mutate; the reset belongs on the processing path where it is unconditional.
            //
            // Server-only by design: Cleanup() writes through the RoundStats property setters,
            // which push the server's zeroes onto the NetworkVariables, and replication clears
            // every client's local mirror. Clients never reset stats themselves.
            if (_preparedForScene.Add(player.NetworkObjectId))
                player.PrepareForNewScene();

            // Domain is server-writable: human players route their selections through
            // Player.RequestSetDomain_ServerRpc (called from DomainSelectionPanel and the
            // arcade configure modal); AI players get their domain set in SpawnAIs().
            // No server assignment needed here in the per-player spawn path.

            if (!_processedPlayers.Add(player.NetworkObjectId))
            {
                CSDebug.LogVerbose(CSLogChannel.NetworkFlow, $"<color=#FFA500>[FLOW-5] [ServerVesselInit] Player {player.NetworkObjectId} already processed, skipping</color>");
                return;
            }

            if (!IsReadyToSpawn(player))
            {
                // Vessel type or name hasn't replicated yet. This is expected for:
                //   - Host player: OnNetworkSpawn ran in Auth scene before
                //     MainMenuController.Start() set selectedVesselClass.
                //   - Remote client: NetworkVariables need time to replicate.
                // Retry with backoff, and for the host's own player, apply
                // gameData.selectedVesselClass once it becomes valid.
                const int retryIntervalMs = 100;
                const int maxRetries = 20; // 2 seconds total

                using (LoadInsights.Measure(LoadInsightCategory.Netcode,
                           "Waiting for player NetworkVariables to replicate (retry loop)", isWait: true))
                {
                    for (int i = 0; i < maxRetries; i++)
                    {
                        await UniTask.Delay(retryIntervalMs, DelayType.UnscaledDeltaTime, cancellationToken: ct);

                        // Host owns its own player - push selectedVesselClass when ready
                        if (player.IsOwner
                            && !IsValidVesselType(player.NetDefaultVesselType.Value)
                            && IsValidVesselType(gameData.selectedVesselClass.Value))
                        {
                            player.NetDefaultVesselType.Value = gameData.selectedVesselClass.Value;
                        }

                        if (IsReadyToSpawn(player)) break;
                    }
                }

                if (!IsReadyToSpawn(player))
                {
                    // Still not ready after retries - remove from processed so the deferred spawn
                    // event can retry, and RE-ARM that event. Dropping the processed entry alone
                    // was not enough and silently could not work: the spawn-event latch is
                    // one-shot and this branch is only ever reached AFTER it was spent (the raise
                    // is what started this handler), so the "will retry" below was a promise
                    // nothing could keep. A joining client then never got a vessel, its
                    // OnClientReady never fired, and its 30s join watchdog bounced it back to its
                    // own menu while the host sat there seeing the player object just fine.
                    _processedPlayers.Remove(player.NetworkObjectId);

                    // BOUNDED: re-arming re-raises the event, which re-enters this handler, so an
                    // owner whose values never arrive at all would spin here forever. Each round
                    // costs ~2.2s of real waiting, so a handful of them covers a long link
                    // (~13s on top of the 2s first pass) and then stops. Cleared when the player
                    // finally spawns or despawns, so a later scene starts fresh.
                    _spawnReArms.TryGetValue(player.NetworkObjectId, out int reArms);
                    if (reArms < MaxSpawnReArms)
                    {
                        _spawnReArms[player.NetworkObjectId] = reArms + 1;
                        player.ReArmDeferredSpawnEvent();
                    }
                    else
                    {
                        Debug.LogError($"[FLOW-5] [ServerVesselInit] Player {ownerClientId} never became " +
                                       $"spawn-ready after {MaxSpawnReArms} re-arms - giving up. That client " +
                                       "will bounce: its owner-written NetName / vessel type never replicated.");
                    }
                    Debug.LogWarning($"<color=#FFA500>[FLOW-5] [ServerVesselInit] Player {ownerClientId} NOT ready after {maxRetries * retryIntervalMs}ms - VesselType={player.NetDefaultVesselType.Value}, Name='{player.NetName.Value}'. Will retry on deferred event.</color>");
                    return;
                }
            }

            CSDebug.LogVerbose(CSLogChannel.NetworkFlow, $"<color=#00FF00>[FLOW-5] [ServerVesselInit] Player ready! Spawning vessel for {player.NetName.Value} (type={player.NetDefaultVesselType.Value})</color>");
            // Readiness reached: this player owes no more re-arms.
            _spawnReArms.Remove(player.NetworkObjectId);
            await OnPlayerReadyToSpawnAsync(player, ct);
        }

        /// <summary>
        /// Builds the cell-relative spawn ring on first use. Players land on a sphere of
        /// <c>nucleus radius + spawnDistanceOutsideNucleus</c> centred on the cell, all facing the
        /// centre, arranged by count: 4 tetrahedral, 3 equilateral triangle, 2 opposite ends of one
        /// axis (see <see cref="CellSpawnFormation"/>). Falls back to the authored transforms if the
        /// cell isn't reachable. No-op unless <c>arrangeSpawnPointsAroundCell</c> is on.
        /// </summary>
        protected void EnsureSpawnPosesReady()
        {
            if (!arrangeSpawnPointsAroundCell || _cellSpawnRingBuilt) return;

            // NOT cellData.Cell: that is assigned in Cell.Initialize, which runs on
            // OnInitializeGame behind InitDelayMs (1000 ms), while this runs at preSpawnDelayMs
            // (200 ms) and, for AI, at OnNetworkSpawn. FindByRuntimeData reads the registry the
            // Cell joins in OnEnable, so it resolves immediately.
            var cell = Cell.FindByRuntimeData(cellData);

            // Likewise ExpectedNucleusWorldRadius, not NucleusWorldRadius: the nucleus object does
            // not exist yet this early, and a 0 there would put every player at
            // spawnDistanceOutsideNucleus from the cell CENTRE - inside the core.
            float nucleusRadius = cell ? cell.ExpectedNucleusWorldRadius : 0f;

            // A radius floor makes the ring usable for a cell whose core is a STRUCTURE rather
            // than a nucleus (Ribcage's cage), where nucleusRadius is legitimately 0. Without a
            // floor that case is indistinguishable from "cell not resolvable yet" below.
            if (nucleusRadius <= 0f && spawnRingRadiusFloor <= 0f)
            {
                // Transient (cell not resolvable yet) - do NOT latch, so a later spawn can still
                // install the real ring. Permanent (a cell with no nucleus configured) - latch,
                // because the authored points are then the only answer there is.
                bool permanent = cell != null && cell.HasConfigAssigned;
                _cellSpawnRingBuilt = permanent;

                CSDebug.LogWarning(
                    "[ServerPlayerVesselInitializer] Arrange Spawn Points Around Cell is on but the " +
                    $"cell nucleus radius is unavailable (cell={(cell ? cell.name : "null")}, " +
                    $"configAssigned={(cell && cell.HasConfigAssigned)}) - using authored spawn points" +
                    (permanent ? "." : " for now; will retry on the next spawn."));

                if (playerSpawnPoints != null && playerSpawnPoints.Length > 0)
                    gameData.SetSpawnPositions(playerSpawnPoints);
                return;
            }

            _cellSpawnRingBuilt = true;

            // Total players in the match (humans + AI backfill) - the formation's symmetry is
            // chosen from this, so a 2-player match gets the axis, not two corners of a tetrahedron.
            int count = gameData.SelectedPlayerCount != null
                ? Mathf.Max(1, gameData.SelectedPlayerCount.Value)
                : Mathf.Max(1, gameData.Players.Count);

            float radius = Mathf.Max(nucleusRadius + spawnDistanceOutsideNucleus, spawnRingRadiusFloor);
            gameData.SetSpawnPoses(
                CellSpawnFormation.Build(count, cell.transform.position, radius, spawnFormation));

            CSDebug.Log($"[ServerPlayerVesselInitializer] Spawn ring: {count} players at " +
                        $"{radius:0.#}u (nucleus {nucleusRadius:0.#} + {spawnDistanceOutsideNucleus:0.#}, " +
                        $"floor {spawnRingRadiusFloor:0.#}) around {cell.name}, {spawnFormation}.");
        }

        /// <summary>
        /// Called when a player's vessel type is confirmed.
        /// Spawns the vessel, initializes on server, waits, then notifies clients via RPCs.
        /// Virtual so derived classes (Menu) can add post-init behavior.
        /// </summary>
        protected virtual async UniTask OnPlayerReadyToSpawnAsync(Player player, CancellationToken ct)
        {
            EnsureSpawnPosesReady();

            CSDebug.LogVerbose(CSLogChannel.NetworkFlow, $"<color=#00FF00>[FLOW-5] [ServerVesselInit] OnPlayerReadyToSpawnAsync - SpawnVesselAndInitialize for {player.NetName.Value}</color>");
            SpawnVesselAndInitialize(player.OwnerClientId, player);

            CSDebug.LogVerbose(CSLogChannel.NetworkFlow, $"<color=#00FF00>[FLOW-5] [ServerVesselInit] Vessel spawned. Waiting {postSpawnDelayMs}ms for replication...</color>");
            // Wait for the vessel NetworkObject to fully replicate before telling clients
            using (LoadInsights.Measure(LoadInsightCategory.ScriptedDelay,
                       $"postSpawnDelayMs before NotifyClients ({postSpawnDelayMs}ms)", isWait: true))
            {
                await UniTask.Delay(postSpawnDelayMs, DelayType.UnscaledDeltaTime, cancellationToken: ct);
            }

            CSDebug.LogVerbose(CSLogChannel.NetworkFlow, $"<color=#00FF00>[FLOW-5] [ServerVesselInit] NotifyClients for {player.NetName.Value}</color>");
            NotifyClients(player);
        }

        protected void SpawnVesselAndInitialize(ulong clientId, Player player)
        {
            var vesselNO = SpawnVesselForPlayer(clientId, player);
            if (!vesselNO)
                return;

            if (!vesselNO.TryGetComponent(out IVessel vessel))
            {
                CSDebug.LogError("[ServerPlayerVesselInitializer] Spawned vessel missing IVessel component.");
                return;
            }

            clientPlayerVesselInitializer.InitializePlayerAndVessel(player, vessel);
        }

        /// <summary>
        /// Sends RPCs to non-host clients:
        ///   - Existing clients: "initialize just this new pair"
        ///   - New client: "initialize ALL player-vessel pairs"
        /// </summary>
        protected void NotifyClients(Player newPlayer)
        {
            var newClientId = newPlayer.OwnerClientId;
            var hostClientId = NetworkManager.Singleton.LocalClientId;

            // Tell existing non-host clients about the new player-vessel pair
            foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
            {
                if (client.ClientId == newClientId) continue;
                if (client.ClientId == hostClientId) continue;

                var existingTarget = new ClientRpcParams
                {
                    Send = new ClientRpcSendParams { TargetClientIds = new[] { client.ClientId } }
                };
                clientPlayerVesselInitializer.InitializeNewPlayerAndVessel_ClientRpc(
                    newPlayer.PlayerNetId, newPlayer.VesselNetId, existingTarget);
            }

            // Tell the new client to initialize ALL player-vessel pairs.
            if (newClientId != hostClientId)
                SendFullRosterToClient(newClientId);
        }

        /// <summary>
        /// Sends the full current player-vessel roster to a single client via
        /// <see cref="ClientPlayerVesselInitializer.InitializeAllPlayersAndVessels_ClientRpc"/>.
        /// Used both by <see cref="NotifyClients"/> (new-client push) and by the
        /// client-pull <see cref="HandleRosterRequest"/> (reply / heal a dropped push).
        /// Idempotent on the client - already-initialised pairs are skipped.
        /// </summary>
        protected void SendFullRosterToClient(ulong clientId)
        {
            if (clientId == NetworkManager.Singleton.LocalClientId) return;

            var playerIds = new List<ulong>();
            var vesselIds = new List<ulong>();
            foreach (var p in gameData.Players)
            {
                if (p.VesselNetId == 0) continue;
                playerIds.Add(p.PlayerNetId);
                vesselIds.Add(p.VesselNetId);
            }

            var target = new ClientRpcParams
            {
                Send = new ClientRpcSendParams { TargetClientIds = new[] { clientId } }
            };
            clientPlayerVesselInitializer.InitializeAllPlayersAndVessels_ClientRpc(
                playerIds.ToArray(), vesselIds.ToArray(), target);
        }

        /// <summary>
        /// Client-pull entry point (wired to <see cref="ClientPlayerVesselInitializer.OnRosterRequested"/>).
        /// A freshly-joined client asks for the roster from its own OnNetworkSpawn.
        /// Idempotent ensure-then-send: kick the spawn chain if the requester's vessel
        /// hasn't spawned yet, then (re)send the full roster. Heals a dropped one-shot
        /// push and the host's own deferred-spawn edge.
        /// </summary>
        void HandleRosterRequest(ulong requesterClientId)
        {
            if (!NetworkManager.Singleton.IsServer) return;

            var requester = FindPlayerByOwnerClientId(requesterClientId);
            if (requester != null
                && requester.VesselNetId == 0
                && !_processedPlayers.Contains(requester.NetworkObjectId))
            {
                HandlePlayerNetworkSpawned(requesterClientId);
            }

            SendFullRosterToClient(requesterClientId);
        }

        /// <summary>
        /// Finds a spawned Player owned by <paramref name="ownerClientId"/> regardless of
        /// processed-state (unlike <see cref="FindUnprocessedPlayerByOwnerClientId"/>).
        /// </summary>
        Player FindPlayerByOwnerClientId(ulong ownerClientId)
        {
            foreach (var p in gameData.Players)
                if (p is Player netPlayer && netPlayer.IsSpawned && netPlayer.OwnerClientId == ownerClientId)
                    return netPlayer;

            var nm = NetworkManager.Singleton;
            if (nm != null && nm.ConnectedClients.TryGetValue(ownerClientId, out var client))
            {
                var playerObj = client.PlayerObject;
                if (playerObj != null && playerObj.TryGetComponent<Player>(out var player) && player.IsSpawned)
                    return player;
            }
            return null;
        }

        /// <summary>
        /// Adopt a <see cref="Player"/> that some OTHER code spawned - an AI backfill bot, a
        /// freestyle AI companion - so this spawner never tries to give it a second vessel.
        ///
        /// Call it in the SAME frame as <c>NetworkObject.Spawn()</c>. <c>Player.OnNetworkSpawn</c>
        /// raises <c>OnPlayerNetworkSpawnedUlong</c> from inside that call (and again, deferred,
        /// when the owner-written name / vessel type land), so the handler must already see the
        /// player as processed - which the handler's 200ms replication delay is what allows.
        /// </summary>
        protected void ClaimExternallySpawnedPlayer(Player player)
        {
            if (!player) return;
            _processedPlayers.Add(player.NetworkObjectId);
            _preparedForScene.Add(player.NetworkObjectId);
            _claimedForeignOwners.Add(player.OwnerClientId);
        }

        protected NetworkObject SpawnVesselForPlayer(ulong clientId, Player networkPlayer) =>
            SpawnVesselForPlayer(clientId, networkPlayer, ResolveSpawnVesselType(networkPlayer));

        /// <summary>
        /// The vessel class this player actually spawns in, clamped to what the game MODE allows.
        ///
        /// <c>Player.NetDefaultVesselType</c> is an OWNER-write NetworkVariable: every client
        /// writes its own from its own local <c>gameData.selectedVesselClass</c>, and the menu's
        /// vessel-changer toy writes it too. So a client walks into a restricted mode still
        /// wearing the hull it last flew, and the launcher-side clamp in
        /// <c>GameDataSO.SyncFromArcadeGame</c> never sees it - that call only runs on the machine
        /// that pressed Start, and the config ClientRpc lands later than this spawn. A Dolphin
        /// therefore flew Rhino-only Ribcage on every client while the AI (whose class comes from
        /// the scene's aiInitializeDatas) correctly spawned Rhinos.
        ///
        /// The SERVER is the only authority that sees every player's request and the mode's rules
        /// at the same time, so the clamp belongs here - same principle as never writing domain
        /// state from client code. Empty <c>AllowedVesselClasses</c> = no restriction.
        /// </summary>
        protected virtual VesselClassType ResolveSpawnVesselType(Player networkPlayer)
        {
            var requested = networkPlayer.NetDefaultVesselType.Value;
            var allowed = gameData.ClampVesselToGame(requested);
            if (allowed == requested) return requested;

            CSDebug.LogWarning(
                $"[ServerPlayerVesselInitializer] {gameData.GameMode} does not allow {requested} " +
                $"(player {networkPlayer.NetName.Value}); spawning {allowed} instead.");

            // Keep the NetworkVariable honest too, or anything that re-reads it later (a respawn,
            // a HUD, telemetry) would disagree with the hull that is actually flying. The server
            // cannot write an owner-write variable directly, so Player routes it to the owner.
            networkPlayer.ServerForceVesselType(allowed);
            return allowed;
        }

        /// <summary>
        /// Spawns a vessel of the given type, assigns ownership to <paramref name="clientId"/>,
        /// and updates the player's <see cref="Player.NetVesselId"/>.
        /// </summary>
        protected NetworkObject SpawnVesselForPlayer(ulong clientId, Player networkPlayer, VesselClassType vesselType)
        {
            if (!vesselPrefabContainer.TryGetShipPrefab(vesselType, out Transform shipPrefabTransform))
            {
                CSDebug.LogError($"[ServerPlayerVesselInitializer] No prefab for vessel type {vesselType}");
                return null;
            }

            if (!shipPrefabTransform.TryGetComponent(out NetworkObject shipNetworkObject))
            {
                CSDebug.LogError($"[ServerPlayerVesselInitializer] Prefab {shipPrefabTransform.name} missing NetworkObject");
                return null;
            }

            // AI vessels bill to AI Backfill; humans to Vessels & Players.
            var insightCategory = networkPlayer.NetIsAI.Value
                ? LoadInsightCategory.AiBackfill
                : LoadInsightCategory.Vessels;
            using (LoadInsights.Measure(insightCategory, $"Vessel instantiate+inject+spawn ({vesselType})"))
            {
                var networkVessel = Instantiate(shipNetworkObject);
                GameObjectInjector.InjectRecursive(networkVessel.gameObject, _container);
                networkVessel.SpawnWithOwnership(clientId, DestroyVesselWithScene);
                networkPlayer.NetVesselId.Value = networkVessel.NetworkObjectId;
                LoadInsights.Count("Vessels spawned during load");
                return networkVessel;
            }
        }

        /// <summary>
        /// Despawns and destroys a vessel's <see cref="NetworkObject"/>.
        /// Removes it from <see cref="GameDataSO.Vessels"/> tracking.
        /// </summary>
        protected void DespawnVessel(IVessel vessel)
        {
            gameData.Vessels.Remove(vessel);

            if (vessel is VesselController vc && vc.IsSpawned)
                vc.NetworkObject.Despawn(true);
        }

        /// <summary>
        /// Finds the first unprocessed Player owned by the given clientId.
        /// Falls back to NetworkManager.ConnectedClients for persistent Players
        /// that may have been cleared from gameData.Players during scene transition
        /// (by ResetRuntimeData or DestroyPlayerAndVessel).
        ///
        /// PURE LOOKUP - it must not mutate the player it returns. Re-initializing for the new
        /// scene (PrepareForNewScene) used to happen here on the fallback branch only, which made
        /// the RoundStats reset depend on which branch found the player; it now runs
        /// unconditionally in HandlePlayerNetworkSpawnedAsync.
        /// </summary>
        Player FindUnprocessedPlayerByOwnerClientId(ulong ownerClientId)
        {
            foreach (var p in gameData.Players)
            {
                if (p is Player netPlayer
                    && netPlayer.IsSpawned
                    && netPlayer.OwnerClientId == ownerClientId
                    && !_processedPlayers.Contains(netPlayer.NetworkObjectId))
                {
                    return netPlayer;
                }
            }

            // Fallback: discover persistent Player from ConnectedClients.
            // Player may have been cleared from gameData.Players after
            // ProcessPreExistingPlayers() triggered the spawn chain
            // (e.g. scene-placed MultiplayerSetup.Start() → DestroyPlayerAndVessel).
            var nm = NetworkManager.Singleton;
            if (nm == null) return null;

            if (!nm.ConnectedClients.TryGetValue(ownerClientId, out var client))
                return null;

            var playerObj = client.PlayerObject;
            if (playerObj == null || !playerObj.TryGetComponent<Player>(out var player))
                return null;

            if (!player.IsSpawned || _processedPlayers.Contains(player.NetworkObjectId))
                return null;

            return player;
        }

        /// <summary>
        /// A player is ready to spawn when both vessel type and name are set.
        /// </summary>
        protected bool IsReadyToSpawn(Player player) =>
            IsValidVesselType(player.NetDefaultVesselType.Value)
            && !string.IsNullOrEmpty(player.NetName.Value.ToString());

        protected static bool IsValidVesselType(VesselClassType type) =>
            type != VesselClassType.Random && type != VesselClassType.Any;
    }
}
