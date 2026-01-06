using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Unity.Netcode;
using UnityEngine;

namespace CosmicShore.Game
{
    /// <summary>
    /// Extension of ServerPlayerVesselInitializer:
    /// - Spawns N server-owned AI Players and their Vessels
    /// - Syncs AI to late-joining clients via NetObjectId (NOT clientId)
    ///
    /// REQUIREMENT:
    /// ClientPlayerVesselInitializer must implement:
    /// InitializeAIPlayerAndVesselInThisClient_ClientRpc(ulong aiPlayerNetObjectId, ulong aiVesselNetObjectId, ClientRpcParams rpcParams)
    /// </summary>
    public class ServerPlayerVesselInitializerWithAI : ServerPlayerVesselInitializer
    {
        [Header("AI Settings")]
        [SerializeField] private bool spawnAIOnServerReady = true;

        [Tooltip("NetworkObject prefab that contains your Player component (must be a registered NetworkPrefab).")]
        [SerializeField] private NetworkObject aiPlayerPrefab;

        [Tooltip(("The informations needed to spawn AI"))]
        [SerializeField] private IPlayer.InitializeData[] aiInitializeDatas;

        private readonly List<AISpawnInfo> _aiSpawns = new();

        private struct AISpawnInfo
        {
            public int AiIndex;
            public IPlayer.InitializeData InitializeData;
            public ulong PlayerNetId;
            public ulong VesselNetId;
        }

        protected override void OnServerReady()
        {
            base.OnServerReady();

            if (!spawnAIOnServerReady)
                return;

            SpawnAIs_ServerOwned().Forget();
        }

        protected override void OnAfterInitializeAllPlayersInNewClient(ulong newClientId)
        {
            base.OnAfterInitializeAllPlayersInNewClient(newClientId);

            // Late-join: after human clones init, also init AI clones in the new client.
            SendInitializeAIsToClient(newClientId);
        }

        private async UniTaskVoid SpawnAIs_ServerOwned()
        {
            if (!NetworkManager.Singleton || !NetworkManager.Singleton.IsServer)
                return;

            if (!aiPlayerPrefab)
            {
                Debug.LogError("[ServerPlayerVesselInitializerWithAI] aiPlayerPrefab is not assigned.");
                return;
            }

            _aiSpawns.Clear();

            for (int i = 0; i < aiInitializeDatas.Length; i++)
            {
                var data =  aiInitializeDatas[i];
                if (!data.AllowSpawning)
                    return;
                
                // 1) Spawn AI Player (server-owned)
                var aiPlayerNO = Instantiate(aiPlayerPrefab);

                var spawnT = GetSpawnTransformForAI(i);
                if (spawnT)
                    aiPlayerNO.transform.SetPositionAndRotation(spawnT.position, spawnT.rotation);

                aiPlayerNO.Spawn(true); // server-owned

                var aiPlayer = aiPlayerNO.GetComponent<Player>();
                if (!aiPlayer)
                {
                    Debug.LogError("[ServerPlayerVesselInitializerWithAI] AI Player prefab missing Player component.");
                    aiPlayerNO.Despawn(true);
                    continue;
                }

                // Set AI vessel type (server authority)
                var aiVesselType = data.vesselClass;
                if (aiVesselType == VesselClassType.Any || aiVesselType == VesselClassType.Random)
                    aiVesselType = VesselClassType.Sparrow;
                    
                aiPlayer.NetDefaultShipType.Value = aiVesselType;
                aiPlayer.NetName.Value = data.PlayerName;
                aiPlayer.NetTeam.Value = data.domain;

                // 2) Spawn AI Vessel (server-owned)
                if (!TrySpawnVesselForAI(aiPlayer, out var aiVesselNO))
                {
                    aiPlayerNO.Despawn(true);
                    continue;
                }

                _aiSpawns.Add(new AISpawnInfo
                {
                    AiIndex = i,
                    InitializeData = data,
                    PlayerNetId = aiPlayerNO.NetworkObjectId,
                    VesselNetId = aiVesselNO.NetworkObjectId
                });
            }

            // Give a tiny frame to ensure spawned objects are visible in SpawnManager on host.
            await UniTask.Yield(PlayerLoopTiming.PostLateUpdate);

            // Init AI for all currently connected clients (host included).
            SendInitializeAIsToAllClients();
        }

        private Transform GetSpawnTransformForAI(int aiIndex)
        {
            // Common 2v2 mapping:
            // origins[0]=Host, origins[1]=Client, origins[2]=AI0, origins[3]=AI1
            if (_playerOrigins == null || _playerOrigins.Length == 0)
                return null;

            int idx = 2 + aiIndex;
            idx = Mathf.Clamp(idx, 0, _playerOrigins.Length - 1);
            return _playerOrigins[idx];
        }

        private bool TrySpawnVesselForAI(Player aiPlayer, out NetworkObject vesselNO)
        {
            vesselNO = null;

            var vesselType = aiPlayer.NetDefaultShipType.Value;

            if (!vesselPrefabContainer.TryGetShipPrefab(vesselType, out Transform shipPrefabTransform))
            {
                Debug.LogError($"[ServerPlayerVesselInitializerWithAI] No prefab for AI vessel type {vesselType}");
                return false;
            }

            if (!shipPrefabTransform.TryGetComponent(out NetworkObject shipNetworkObject))
            {
                Debug.LogError($"[ServerPlayerVesselInitializerWithAI] Prefab {shipPrefabTransform.name} missing NetworkObject");
                return false;
            }

            var ship = Instantiate(shipNetworkObject);

            // Place at AI player position (or tweak as needed)
            ship.transform.SetPositionAndRotation(aiPlayer.transform.position, aiPlayer.transform.rotation);

            ship.Spawn(true); // server-owned

            vesselNO = ship;
            return true;
        }

        private void SendInitializeAIsToAllClients()
        {
            if (!NetworkManager.Singleton || !NetworkManager.Singleton.IsServer)
                return;

            foreach (var cc in NetworkManager.Singleton.ConnectedClientsList)
                SendInitializeAIsToClient(cc.ClientId);
        }

        private void SendInitializeAIsToClient(ulong targetClientId)
        {
            if (_aiSpawns.Count == 0 || clientPlayerVesselInitializer == null)
                return;

            var send = new ClientRpcSendParams { TargetClientIds = new[] { targetClientId } };
            var rpcParams = new ClientRpcParams { Send = send };

            foreach (var ai in _aiSpawns)
            {
                clientPlayerVesselInitializer.InitializeAIPlayerAndVesselInThisClient_ClientRpc(
                    ai.PlayerNetId,
                    ai.VesselNetId,
                    rpcParams
                );
            }
        }
    }
}