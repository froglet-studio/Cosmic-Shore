using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using Cysharp.Threading.Tasks;
using Reflex.Injectors;
using Unity.Netcode;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Extension of ServerPlayerVesselInitializer:
    /// - Spawns N server-owned AI Players and their Vessels when the server connects
    /// - Then spawns the human player's vessel normally
    /// - For non-server clients, delegates directly to the base class
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
            // Fresh domain pool before any player/AI spawning.
            DomainAssigner.Initialize();
            base.OnNetworkSpawn();
        }

        protected override void OnClientConnected(ulong clientId)
        {
            SpawnVesselAndInitializeWithPlayer(clientId).Forget();
        }

        async UniTaskVoid SpawnVesselAndInitializeWithPlayer(ulong clientId)
        {
            // If server's own client and AI spawning is enabled, spawn AIs first
            if (clientId == NetworkManager.Singleton.LocalClientId && spawnAIOnServerReady)
            {
                SpawnAIs_ServerOwned();

                // Wait for AI network objects to propagate
                await UniTask.WaitForSeconds(1f, true, cancellationToken: _cts.Token);
            }

            // Then spawn the human player's vessel normally
            DelayedSpawnVesselForPlayer(clientId).Forget();
        }

        void SpawnAIs_ServerOwned()
        {
            if (!NetworkManager.Singleton || !NetworkManager.Singleton.IsServer)
                return;

            if (!aiPlayerPrefab)
            {
                CSDebug.LogError("[ServerPlayerVesselInitializerWithAI] aiPlayerPrefab is not assigned.");
                return;
            }

            // Use AI profile list for names when available
            System.Collections.Generic.List<AIProfile> profiles = null;
            if (aiProfileList != null && aiInitializeDatas != null)
                profiles = aiProfileList.PickRandom(aiInitializeDatas.Length);

            for (int i = 0; i < aiInitializeDatas.Length; i++)
            {
                var data = aiInitializeDatas[i];
                if (!data.AllowSpawning)
                    continue;

                var aiPlayerNO = Instantiate(aiPlayerPrefab);
                GameObjectInjector.InjectRecursive(aiPlayerNO.gameObject, _container);

                var spawnT = GetSpawnTransformForAI(i);
                if (spawnT)
                    aiPlayerNO.transform.SetPositionAndRotation(spawnT.position, spawnT.rotation);

                aiPlayerNO.Spawn(true); // server-owned

                var aiPlayer = aiPlayerNO.GetComponent<Player>();
                if (!aiPlayer)
                {
                    CSDebug.LogError("[ServerPlayerVesselInitializerWithAI] AI Player prefab missing Player component.");
                    aiPlayerNO.Despawn(true);
                    continue;
                }

                var aiVesselType = data.vesselClass;
                if (aiVesselType is VesselClassType.Any or VesselClassType.Random)
                    aiVesselType = PickAIVesselType();

                var aiName = profiles != null && i < profiles.Count
                    ? profiles[i].Name
                    : !string.IsNullOrEmpty(data.PlayerName) ? data.PlayerName : $"AI {i + 1}";

                var aiDomain = DomainAssigner.GetDomainsByGameModes(gameData.GameMode);

                aiPlayer.NetDefaultVesselType.Value = aiVesselType;
                aiPlayer.NetName.Value = aiName;
                aiPlayer.NetDomain.Value = aiDomain;
                aiPlayer.NetIsAI.Value = true;

                if (!TrySpawnVesselForAI(aiPlayer, out var aiVesselNO))
                {
                    aiPlayerNO.Despawn(true);
                    continue;
                }

                ConfigureAIPilot(aiVesselNO);
            }
        }

        Transform GetSpawnTransformForAI(int aiIndex)
        {
            if (playerSpawnPoints == null || playerSpawnPoints.Length == 0)
                return null;

            // origins[0]=Host, origins[1]=Client, origins[2+]=AI
            int idx = 2 + aiIndex;
            if (idx >= playerSpawnPoints.Length)
            {
                CSDebug.LogWarning($"[ServerPlayerVesselInitializerWithAI] Not enough spawn origins for AI {aiIndex} " +
                                 $"(need index {idx}, have {playerSpawnPoints.Length}). Wrapping with modulo.");
                idx = idx % playerSpawnPoints.Length;
            }
            return playerSpawnPoints[idx];
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
            vesselNO.Spawn(true); // server-owned
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
