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
    /// spawns server-owned AI players and their vessels, then delegates
    /// human player handling to the base class via OnPlayerNetworkSpawned.
    ///
    /// OnNetworkSpawn flow:
    ///   1. SetupSpawnPositions()
    ///   2. SpawnAIs() — creates AI players + vessels (fires OnPlayerNetworkSpawned
    ///      for each, but we haven't subscribed yet so the base ignores them)
    ///   3. Mark AI players in _processedPlayers so the base never processes them
    ///   4. SubscribeAndProcessPlayers() — subscribes to event + processes human players
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
                enabled = false;
                return;
            }

            SetupSpawnPositions();

            // Spawn AIs BEFORE subscribing to OnPlayerNetworkSpawned.
            // AI players fire the event during Spawn(), but since we haven't
            // subscribed yet, those events are harmlessly ignored.
            if (spawnAIOnServerReady)
                SpawnAIs();

            // Mark all AI players as processed so the base skips them
            foreach (var p in gameData.Players)
            {
                if (p is Player aiPlayer && aiPlayer.NetIsAI.Value)
                    _processedPlayers.Add(aiPlayer.NetworkObjectId);
            }

            // Now subscribe and handle human players (host + future remote clients)
            SubscribeAndProcessPlayers();
        }

        void SpawnAIs()
        {
            if (!aiPlayerPrefab)
            {
                CSDebug.LogError("[ServerPlayerVesselInitializerWithAI] aiPlayerPrefab is not assigned.");
                return;
            }

            for (int i = 0; i < aiInitializeDatas.Length; i++)
            {
                var data = aiInitializeDatas[i];
                if (!data.AllowSpawning)
                    return;

                var aiPlayerNO = Instantiate(aiPlayerPrefab);
                GameObjectInjector.InjectRecursive(aiPlayerNO.gameObject, _container);

                var spawnT = GetSpawnTransformForAI(i);
                if (spawnT)
                    aiPlayerNO.transform.SetPositionAndRotation(spawnT.position, spawnT.rotation);

                aiPlayerNO.Spawn(true);

                var aiPlayer = aiPlayerNO.GetComponent<Player>();
                if (!aiPlayer)
                {
                    CSDebug.LogError("[ServerPlayerVesselInitializerWithAI] AI Player prefab missing Player component.");
                    aiPlayerNO.Despawn(true);
                    continue;
                }

                var aiVesselType = data.vesselClass;
                if (aiVesselType == VesselClassType.Any || aiVesselType == VesselClassType.Random)
                    aiVesselType = PickAIVesselType();

                aiPlayer.NetDefaultVesselType.Value = aiVesselType;
                aiPlayer.NetName.Value = data.PlayerName;
                aiPlayer.NetDomain.Value = data.domain;
                aiPlayer.NetIsAI.Value = true;

                if (!TrySpawnVesselForAI(aiPlayer, out var aiVesselNO))
                {
                    aiPlayerNO.Despawn(true);
                    continue;
                }

                // Server-side initialization of the AI player-vessel pair
                clientPlayerVesselInitializer.InitializePlayerAndVessel(aiPlayer, aiVesselNO);
                ConfigureAIPilot(aiVesselNO);
            }
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
            vesselNO.transform.SetPositionAndRotation(aiPlayer.transform.position, aiPlayer.transform.rotation);
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

        Transform GetSpawnTransformForAI(int aiIndex)
        {
            if (_playerOrigins == null || _playerOrigins.Length == 0)
                return null;

            int idx = 2 + aiIndex;
            if (idx >= _playerOrigins.Length)
                idx %= _playerOrigins.Length;
            return _playerOrigins[idx];
        }
    }
}
