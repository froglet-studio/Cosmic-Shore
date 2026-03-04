using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using Reflex.Injectors;
using Unity.Netcode;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Extension of ServerPlayerVesselInitializer:
    /// ensures AI players exist (persistent across scene transitions) and spawns
    /// their vessels, then delegates human player handling to the base class.
    ///
    /// OnNetworkSpawn flow:
    ///   1. EnsureAIPlayersAndVessels() — finds persistent AI or creates new ones,
    ///      spawns vessels, initializes pairs
    ///   2. Mark AI players in _processedPlayers so the base never processes them
    ///   3. base.OnNetworkSpawn() — subscribes to event + handles human players going forward
    ///
    /// AI players are spawned with DestroyWithScene=false so they persist across
    /// scene transitions. Only vessels are destroyed with the scene and respawned.
    /// </summary>
    public class ServerPlayerVesselInitializerWithAI : ServerPlayerVesselInitializer
    {
        [Header("AI Settings")]
        [SerializeField] bool spawnAIOnServerReady = true;

        [Tooltip("NetworkObject prefab that contains your Player component (must be a registered NetworkPrefab).")]
        [SerializeField] NetworkObject aiPlayerPrefab;

        [Tooltip("The data needed to spawn AI")]
        [SerializeField] IPlayer.InitializeData[] aiInitializeDatas;

        [Header("AI Ship Selection")]
        [Tooltip("Game list used to look up available ships for AI opponents. If unset, AI defaults to Sparrow.")]
        [SerializeField] SO_GameList gameList;

        [Header("AI Profiles")]
        [Tooltip("Optional AI profile list for assigning unique names to AI opponents.")]
        [SerializeField] SO_AIProfileList aiProfileList;

        protected override void OnNetworkSpawn()
        {
            if (!NetworkManager.Singleton.IsServer)
            {
                Debug.Log("<color=#FF00FF>[FLOW-5AI] [ServerVesselInitWithAI] OnNetworkSpawn — NOT server, disabling</color>");
                enabled = false;
                return;
            }

            Debug.Log($"<color=#FF00FF>[FLOW-5AI] [ServerVesselInitWithAI] OnNetworkSpawn — IsServer=true, RequestedAIBackfill={gameData.RequestedAIBackfillCount}, spawnAIOnServerReady={spawnAIOnServerReady}</color>");

            // Fresh domain pool before any player/AI spawning.
            // Previous session's pool state is stale after scene transition.
            DomainAssigner.Initialize();

            // Set scene-specific spawn positions before AI spawning.
            // base.OnNetworkSpawn() also sets them, but AI spawns happen first
            // (before base runs), so positions must be configured here.
            if (playerSpawnPoints != null && playerSpawnPoints.Length > 0)
                gameData.SetSpawnPositions(playerSpawnPoints);

            // Ensure AI players + vessels BEFORE subscribing to OnPlayerNetworkSpawnedUlong.
            // New AI players fire the event during Spawn(), but since we haven't
            // subscribed yet (base.OnNetworkSpawn hasn't run), those events
            // are harmlessly ignored by the base.
            // Wrapped in try-catch to guarantee base.OnNetworkSpawn() always
            // runs — otherwise no human players would be processed.
            if (spawnAIOnServerReady)
            {
                try
                {
                    Debug.Log("<color=#FF00FF>[FLOW-5AI] [ServerVesselInitWithAI] Calling EnsureAIPlayersAndVessels()</color>");
                    EnsureAIPlayersAndVessels();
                    Debug.Log($"<color=#FF00FF>[FLOW-5AI] [ServerVesselInitWithAI] EnsureAIPlayersAndVessels() complete. gameData.Players.Count={gameData.Players.Count}</color>");
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"<color=#FF0000>[FLOW-5AI] [ServerVesselInitWithAI] EnsureAIPlayersAndVessels FAILED: {e.Message}\n{e.StackTrace}</color>");
                    CSDebug.LogError($"[ServerPlayerVesselInitializerWithAI] EnsureAIPlayersAndVessels failed: {e.Message}");
                }
            }

            // Mark all AI players as processed so the base skips them
            int aiMarked = 0;
            foreach (var p in gameData.Players)
            {
                if (p is Player aiPlayer && aiPlayer.NetIsAI.Value)
                {
                    _processedPlayers.Add(aiPlayer.NetworkObjectId);
                    aiMarked++;
                }
            }
            Debug.Log($"<color=#FF00FF>[FLOW-5AI] [ServerVesselInitWithAI] Marked {aiMarked} AI players as processed. Calling base.OnNetworkSpawn()</color>");

            // Now subscribe (via base) and handle human players going forward
            base.OnNetworkSpawn();
        }

        /// <summary>
        /// Finds persistent AI players from previous scene transitions or creates
        /// new ones. Spawns vessels for all AI players and initializes pairs.
        /// AI players are persistent (DestroyWithScene=false). Vessels also use
        /// DestroyWithScene=false to avoid "Invalid Destroy" race conditions on
        /// non-host clients during scene transitions — they are explicitly
        /// despawned by PreDespawnVessels() before scene loads.
        /// </summary>
        void EnsureAIPlayersAndVessels()
        {
            if (!aiPlayerPrefab)
            {
                Debug.LogError("<color=#FF0000>[FLOW-5AI] [ServerVesselInitWithAI] aiPlayerPrefab is NOT assigned!</color>");
                CSDebug.LogError("[ServerPlayerVesselInitializerWithAI] aiPlayerPrefab is not assigned.");
                return;
            }

            int aiCount = gameData.EnsureMinimumAIBackfill();
            Debug.Log($"<color=#FF00FF>[FLOW-5AI] [ServerVesselInitWithAI] EnsureAIPlayersAndVessels — aiCount={aiCount}</color>");
            if (aiCount <= 0)
            {
                Debug.Log("<color=#FF00FF>[FLOW-5AI] [ServerVesselInitWithAI] No AI needed (aiCount <= 0)</color>");
                return;
            }

            // Use AI profile list for names when available; fall back to aiInitializeDatas templates.
            List<AIProfile> profiles = null;
            if (aiProfileList != null)
                profiles = aiProfileList.PickRandom(aiCount);

            // Find persistent AI players from previous scene (survive because Spawn(false))
            var existingAI = FindPersistentAIPlayers();
            Debug.Log($"<color=#FF00FF>[FLOW-5AI] [ServerVesselInitWithAI] Found {existingAI.Count} persistent AI players</color>");

            // Despawn excess AI if fewer needed now
            while (existingAI.Count > aiCount)
            {
                var excess = existingAI[existingAI.Count - 1];
                existingAI.RemoveAt(existingAI.Count - 1);
                Debug.Log($"<color=#FF00FF>[FLOW-5AI] [ServerVesselInitWithAI] Despawning excess AI: {excess.NetName.Value}</color>");
                excess.NetworkObject.Despawn(true);
            }

            // Reconfigure existing persistent AI — prepare for new scene, spawn vessels
            for (int i = 0; i < existingAI.Count; i++)
            {
                var aiPlayer = existingAI[i];
                Debug.Log($"<color=#FF00FF>[FLOW-5AI] [ServerVesselInitWithAI] Reconfiguring persistent AI[{i}]: {aiPlayer.NetName.Value}</color>");

                aiPlayer.PrepareForNewScene();

                // Overwrite PrepareForNewScene's defaults with AI-specific values
                ConfigureAIPlayerData(aiPlayer, i, profiles);

                if (!TrySpawnVesselForAI(aiPlayer, out var vesselNO))
                    continue;

                if (!vesselNO.TryGetComponent(out IVessel vessel))
                {
                    CSDebug.LogError("[ServerPlayerVesselInitializerWithAI] Spawned vessel missing IVessel component.");
                    continue;
                }

                clientPlayerVesselInitializer.InitializePlayerAndVessel(aiPlayer, vessel);
                ConfigureAIPilot(vesselNO);
            }

            // Create new AI players if more needed
            for (int i = existingAI.Count; i < aiCount; i++)
            {
                Debug.Log($"<color=#FF00FF>[FLOW-5AI] [ServerVesselInitWithAI] Creating new AI[{i}]</color>");
                CreateNewAIPlayerAndVessel(i, profiles);
            }
        }

        /// <summary>
        /// Discovers persistent AI Player NetworkObjects from the SpawnManager.
        /// These survived scene transitions because they were spawned with
        /// DestroyWithScene=false.
        /// </summary>
        List<Player> FindPersistentAIPlayers()
        {
            var result = new List<Player>();
            var nm = NetworkManager.Singleton;
            if (nm?.SpawnManager == null) return result;

            foreach (var kvp in nm.SpawnManager.SpawnedObjects)
            {
                if (kvp.Value != null
                    && kvp.Value.TryGetComponent<Player>(out var p)
                    && p.IsSpawned && p.NetIsAI.Value)
                    result.Add(p);
            }
            return result;
        }

        /// <summary>
        /// Creates a brand-new AI player (persistent) and its vessel.
        /// Called on first game launch when no persistent AI exists yet.
        /// </summary>
        void CreateNewAIPlayerAndVessel(int index, List<AIProfile> profiles)
        {
            var aiPlayerNO = Instantiate(aiPlayerPrefab);
            GameObjectInjector.InjectRecursive(aiPlayerNO.gameObject, _container);

            var aiPlayer = aiPlayerNO.GetComponent<Player>();
            if (!aiPlayer)
            {
                CSDebug.LogError("[ServerPlayerVesselInitializerWithAI] AI Player prefab missing Player component.");
                Destroy(aiPlayerNO.gameObject);
                return;
            }

            // Mark as AI BEFORE Spawn() so OnNetworkSpawn() skips Owner writes
            aiPlayer.PreInitializeAsAI();
            aiPlayerNO.Spawn(false); // Persistent — survives scene transitions

            ConfigureAIPlayerData(aiPlayer, index, profiles);

            if (!TrySpawnVesselForAI(aiPlayer, out var aiVesselNO))
            {
                aiPlayerNO.Despawn(true);
                return;
            }

            if (!aiVesselNO.TryGetComponent(out IVessel vessel))
            {
                CSDebug.LogError("[ServerPlayerVesselInitializerWithAI] Spawned vessel missing IVessel component.");
                return;
            }

            clientPlayerVesselInitializer.InitializePlayerAndVessel(aiPlayer, vessel);
            ConfigureAIPilot(aiVesselNO);
        }

        /// <summary>
        /// Sets AI-specific NetworkVariable values: vessel type, name, domain, AI flag.
        /// Used for both new and persistent (reconfigured) AI players.
        /// </summary>
        void ConfigureAIPlayerData(Player aiPlayer, int index, List<AIProfile> profiles)
        {
            var hasTemplate = aiInitializeDatas != null && index < aiInitializeDatas.Length;

            var aiVesselType = hasTemplate ? aiInitializeDatas[index].vesselClass : VesselClassType.Random;
            if (aiVesselType is VesselClassType.Any or VesselClassType.Random)
                aiVesselType = PickAIVesselType();

            var aiName = profiles != null && index < profiles.Count
                ? profiles[index].Name
                : hasTemplate ? aiInitializeDatas[index].PlayerName : $"AI {index + 1}";

            var aiDomain = DomainAssigner.GetDomainsByGameModes(gameData.GameMode);

            aiPlayer.NetDefaultVesselType.Value = aiVesselType;
            aiPlayer.NetName.Value = aiName;
            aiPlayer.NetDomain.Value = aiDomain;
            aiPlayer.NetIsAI.Value = true;
        }

        VesselClassType PickAIVesselType()
        {
            if (gameList != null)
            {
                var game = FindGameByMode(gameData.GameMode);
                if (game != null && game.Captains is { Count: > 0 })
                {
                    var captain = game.Captains[Random.Range(0, game.Captains.Count)];
                    if (captain?.Ship != null && vesselPrefabContainer.TryGetShipPrefab(captain.Ship.Class, out _))
                        return captain.Ship.Class;
                }
            }
            return VesselClassType.Sparrow;
        }

        SO_ArcadeGame FindGameByMode(GameModes mode)
        {
            if (gameList?.Games == null) return null;
            foreach (var game in gameList.Games)
            {
                if (game.Mode == mode) return game;
            }
            return null;
        }

        bool TrySpawnVesselForAI(Player aiPlayer, out NetworkObject vesselNO)
        {
            vesselNO = null;
            var vesselType = aiPlayer.NetDefaultVesselType.Value;

            if (!vesselPrefabContainer.TryGetShipPrefab(vesselType, out Transform shipPrefabTransform))
            {
                CSDebug.LogError($"[ServerPlayerVesselInitializerWithAI] No prefab for AI vessel type {vesselType}");
                return false;
            }

            if (!shipPrefabTransform.TryGetComponent(out NetworkObject shipNetworkObject))
            {
                CSDebug.LogError($"[ServerPlayerVesselInitializerWithAI] Prefab {shipPrefabTransform.name} missing NetworkObject");
                return false;
            }

            vesselNO = Instantiate(shipNetworkObject);
            GameObjectInjector.InjectRecursive(vesselNO.gameObject, _container);
            vesselNO.Spawn(true);
            aiPlayer.NetVesselId.Value = vesselNO.NetworkObjectId;
            return true;
        }

        void ConfigureAIPilot(NetworkObject aiVesselNO)
        {
            var aiPilot = aiVesselNO.GetComponentInChildren<AIPilot>();
            if (aiPilot == null) return;

            bool shouldSeekPlayers = gameData.GameMode == GameModes.MultiplayerJoust;
            float skill = Mathf.Clamp01(gameData.SelectedIntensity.Value * 0.25f);
            aiPilot.ConfigureForGameMode(gameData, shouldSeekPlayers, skill);
        }
    }
}
