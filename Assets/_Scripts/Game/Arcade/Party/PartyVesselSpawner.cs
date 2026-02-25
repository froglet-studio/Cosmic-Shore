using System;
using System.Threading;
using CosmicShore.Core;
using CosmicShore.Game.AI;
using CosmicShore.Soap;
using Cysharp.Threading.Tasks;
using Unity.Netcode;
using UnityEngine;
using CosmicShore.Utility;

namespace CosmicShore.Game.Arcade.Party
{
    /// <summary>
    /// Lives on PartyGameManager (active at scene load → registered with Netcode).
    /// Handles vessel spawning for party mode so we never call RPCs on
    /// NetworkBehaviours that were inactive during the initial spawn sweep.
    /// </summary>
    public class PartyVesselSpawner : NetworkBehaviour
    {
        [Header("Dependencies")]
        [SerializeField] GameDataSO gameData;
        [SerializeField] VesselPrefabContainer vesselPrefabContainer;
        [SerializeField] ThemeManagerDataContainerSO themeManagerData;

        [Header("AI Ship Selection")]
        [SerializeField] SO_GameList gameList;

        [Header("AI Profiles")]
        [SerializeField] SO_AIProfileList aiProfileList;

        public event Action OnAllVesselsSpawned;

        /// <summary>
        /// Called by PartyGameController on the first round.
        /// Spawns the host player's vessel + AI opponents using the given spawn origins.
        /// </summary>
        public void SpawnVesselsForParty(Transform[] spawnOrigins)
        {
            if (!IsServer) return;
            if (spawnOrigins is not { Length: > 0 })
            {
                CSDebug.LogError("[PartyVesselSpawner] No spawn origins provided!");
                return;
            }

            gameData.SetSpawnPositions(spawnOrigins);

            ulong hostClientId = NetworkManager.Singleton.LocalClientId;
            SpawnPlayerThenAI(hostClientId, spawnOrigins).Forget();
        }

        /// <summary>
        /// Called by PartyGameController on rounds 2+ to reposition existing vessels.
        /// </summary>
        public void RepositionForNewRound(Transform[] spawnOrigins)
        {
            if (spawnOrigins is not { Length: > 0 })
            {
                CSDebug.LogWarning("[PartyVesselSpawner] No spawn origins for repositioning.");
                return;
            }

            gameData.SetSpawnPositions(spawnOrigins);
            gameData.ResetPlayers();

            if (CameraManager.Instance)
                CameraManager.Instance.SnapPlayerCameraToTarget();

            CSDebug.Log($"[PartyVesselSpawner] Repositioned players ({spawnOrigins.Length} origins).");
        }

        async UniTaskVoid SpawnPlayerThenAI(ulong clientId, Transform[] spawnOrigins)
        {
            // Spawn host player vessel
            await SpawnVesselForPlayerAsync(clientId);
            await UniTask.WaitForSeconds(1f, true);

            // Spawn AI opponents
            SpawnAIOpponents(spawnOrigins);
            await UniTask.WaitForSeconds(0.5f, true);

            // Initialize all players on the client — this RPC is safe because
            // PartyVesselSpawner lives on an always-active GameObject.
            InitializeAllPlayersForParty_ClientRpc();

            OnAllVesselsSpawned?.Invoke();
            CSDebug.Log("[PartyVesselSpawner] Party vessel spawning complete.");
        }

        async UniTask SpawnVesselForPlayerAsync(ulong clientId)
        {
            await UniTask.Delay(500, DelayType.UnscaledDeltaTime);

            var playerNetObj = NetworkManager.Singleton.SpawnManager.GetPlayerNetworkObject(clientId);
            if (!playerNetObj)
            {
                CSDebug.LogError($"[PartyVesselSpawner] Player object not found for client {clientId}");
                return;
            }

            var player = playerNetObj.GetComponent<Player>();
            if (!player)
            {
                CSDebug.LogError($"[PartyVesselSpawner] Player component missing on client {clientId}");
                return;
            }

            player.NetDomain.Value = DomainAssigner.GetDomainsByGameModes(gameData.GameMode);
            player.NetIsAI.Value = false;

            if (player.NetDefaultVesselType.Value == VesselClassType.Random)
            {
                CSDebug.LogWarning("[PartyVesselSpawner] Vessel type not set, defaulting to Dolphin");
                player.NetDefaultVesselType.Value = VesselClassType.Dolphin;
            }

            VesselClassType vesselType = player.NetDefaultVesselType.Value;

            if (!vesselPrefabContainer.TryGetShipPrefab(vesselType, out Transform shipPrefabTransform))
            {
                CSDebug.LogError($"[PartyVesselSpawner] No prefab for vessel type {vesselType}");
                return;
            }

            if (!shipPrefabTransform.TryGetComponent(out NetworkObject shipNetworkObject))
            {
                CSDebug.LogError($"[PartyVesselSpawner] Prefab {shipPrefabTransform.name} missing NetworkObject");
                return;
            }

            var networkVessel = Instantiate(shipNetworkObject);
            networkVessel.SpawnWithOwnership(clientId, true);
            player.NetVesselId.Value = networkVessel.NetworkObjectId;
        }

        void SpawnAIOpponents(Transform[] spawnOrigins)
        {
            if (!NetworkManager.Singleton || !NetworkManager.Singleton.IsServer)
                return;

            var playerPrefabGO = NetworkManager.Singleton.NetworkConfig.PlayerPrefab;
            if (!playerPrefabGO)
            {
                CSDebug.LogError("[PartyVesselSpawner] No player prefab configured in NetworkManager.");
                return;
            }

            var playerPrefabNO = playerPrefabGO.GetComponent<NetworkObject>();
            if (!playerPrefabNO)
            {
                CSDebug.LogError("[PartyVesselSpawner] Player prefab missing NetworkObject component.");
                return;
            }

            int aiCount = 1;
            var game = FindGameByMode(gameData.GameMode);
            if (game != null && game.MaxPlayers > 1)
                aiCount = game.MaxPlayers - 1;

            var pickedProfiles = aiProfileList != null ? aiProfileList.PickRandom(aiCount) : null;

            for (int i = 0; i < aiCount; i++)
            {
                var aiPlayerNO = Instantiate(playerPrefabNO);

                int spawnIndex = 1 + i;
                if (spawnOrigins != null && spawnOrigins.Length > 0)
                {
                    if (spawnIndex >= spawnOrigins.Length)
                        spawnIndex %= spawnOrigins.Length;
                    aiPlayerNO.transform.SetPositionAndRotation(
                        spawnOrigins[spawnIndex].position,
                        spawnOrigins[spawnIndex].rotation);
                }

                aiPlayerNO.Spawn(true);

                var aiPlayer = aiPlayerNO.GetComponent<Player>();
                if (!aiPlayer)
                {
                    CSDebug.LogError("[PartyVesselSpawner] AI Player prefab missing Player component.");
                    aiPlayerNO.Despawn(true);
                    continue;
                }

                var aiDomain = DomainAssigner.GetDomainsByGameModes(gameData.GameMode);
                var aiVesselType = PickAIVesselType();

                string aiName = (pickedProfiles != null && i < pickedProfiles.Count && !string.IsNullOrEmpty(pickedProfiles[i].Name))
                    ? pickedProfiles[i].Name
                    : $"AI Pilot {i + 1}";

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
                CSDebug.Log($"[PartyVesselSpawner] Spawned AI {i + 1}/{aiCount}: domain={aiDomain}, vessel={aiVesselType}");
            }
        }

        VesselClassType PickAIVesselType()
        {
            if (gameList != null)
            {
                var game = FindGameByMode(gameData.GameMode);
                if (game != null && game.Captains is { Count: > 0 })
                {
                    var captain = game.Captains[UnityEngine.Random.Range(0, game.Captains.Count)];
                    if (captain?.Ship != null)
                    {
                        var shipType = captain.Ship.Class;
                        if (vesselPrefabContainer.TryGetShipPrefab(shipType, out _))
                            return shipType;
                    }
                }
            }
            return VesselClassType.Sparrow;
        }

        SO_ArcadeGame FindGameByMode(GameModes mode)
        {
            if (gameList?.Games == null) return null;
            foreach (var game in gameList.Games)
            {
                if (game.Mode == mode)
                    return game;
            }
            return null;
        }

        bool TrySpawnVesselForAI(Player aiPlayer, out NetworkObject vesselNO)
        {
            vesselNO = null;
            var vesselType = aiPlayer.NetDefaultVesselType.Value;

            if (!vesselPrefabContainer.TryGetShipPrefab(vesselType, out Transform shipPrefabTransform))
            {
                CSDebug.LogError($"[PartyVesselSpawner] No prefab for AI vessel type {vesselType}");
                return false;
            }

            if (!shipPrefabTransform.TryGetComponent(out NetworkObject shipNetworkObject))
            {
                CSDebug.LogError($"[PartyVesselSpawner] Prefab {shipPrefabTransform.name} missing NetworkObject");
                return false;
            }

            vesselNO = Instantiate(shipNetworkObject);
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

        // ==================== Client RPC ====================
        // This is the key fix: this ClientRpc lives on PartyVesselSpawner which is
        // on an always-active GameObject, so it's registered with Netcode at startup.
        // The environment's CPVI can't be used because it's on a disabled GameObject.

        [ClientRpc]
        void InitializeAllPlayersForParty_ClientRpc()
        {
            foreach (var networkPlayer in gameData.Players)
            {
                ulong playerId = networkPlayer.PlayerNetId;
                ulong vesselId = networkPlayer.VesselNetId;
                InitializePlayerAndVessel(playerId, vesselId, this.GetCancellationTokenOnDestroy()).Forget();
            }

            DelayInvokeClientReady(this.GetCancellationTokenOnDestroy()).Forget();
        }

        async UniTaskVoid InitializePlayerAndVessel(ulong playerId, ulong vesselId, CancellationToken token)
        {
            await UniTask.WaitUntil(() =>
                    gameData.TryGetPlayerByNetworkObjectId(playerId, out _) &&
                    gameData.TryGetVesselByNetworkObjectId(vesselId, out _),
                cancellationToken: token);

            if (!gameData.TryGetPlayerByNetworkObjectId(playerId, out var player))
                return;
            if (!gameData.TryGetVesselByNetworkObjectId(vesselId, out var vessel))
                return;

            player.InitializeForMultiplayerMode(vessel);
            vessel.Initialize(player);
            ShipHelper.SetShipProperties(themeManagerData, vessel);
            gameData.AddPlayer(player);

            if (player.IsLocalUser && CameraManager.Instance)
                CameraManager.Instance.SnapPlayerCameraToTarget();
        }

        async UniTaskVoid DelayInvokeClientReady(CancellationToken token)
        {
            await UniTask.Delay(1000, DelayType.UnscaledDeltaTime, PlayerLoopTiming.LastPostLateUpdate, token);
            if (token.IsCancellationRequested) return;
            gameData.InvokeClientReady();
        }
    }
}
