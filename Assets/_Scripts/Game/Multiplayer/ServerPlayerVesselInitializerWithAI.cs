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

        protected override void OnClientConnected(ulong clientId)
        {
            SpawnVesselAndInitializeWithPlayer(clientId).Forget();
        }

        async UniTaskVoid SpawnVesselAndInitializeWithPlayer(ulong clientId)
        {
            // Check if server is joined or other client,
            // if server, then only spawn AIs, for other client, just spawn the player's vessels.
            if (clientId == NetworkManager.Singleton.LocalClientId && spawnAIOnServerReady)
            {
                SpawnAIs_ServerOwned();
            
                // Give a second to ensure spawned AIs are visible in SpawnManager on server and connected clients,
                // then we spawn the player's vessels, and initialize all players and AIs together.
                await UniTask.WaitForSeconds(1f, true);
            }
            
            DelayedSpawnVesselForPlayer(clientId).Forget();
        }

        private void SpawnAIs_ServerOwned()
        {
            if (!NetworkManager.Singleton || !NetworkManager.Singleton.IsServer)
                return;

            if (!aiPlayerPrefab)
            {
                Debug.LogError("[ServerPlayerVesselInitializerWithAI] aiPlayerPrefab is not assigned.");
                return;
            }

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
                    aiVesselType = PickAIVesselType();

                aiPlayer.NetDefaultVesselType.Value = aiVesselType;
                aiPlayer.NetName.Value = data.PlayerName;
                aiPlayer.NetDomain.Value = data.domain;
                aiPlayer.NetIsAI.Value = true;

                // 2) Spawn AI Vessel (server-owned)
                if (!TrySpawnVesselForAI(aiPlayer, out var aiVesselNO))
                {
                    aiPlayerNO.Despawn(true);
                    continue;
                }

                // 3) Configure AI pilot on the spawned vessel
                ConfigureAIPilot(aiVesselNO);
            }
        }

        private Transform GetSpawnTransformForAI(int aiIndex)
        {
            // Common 2v2 mapping:
            // origins[0]=Host, origins[1]=Client, origins[2]=AI0, origins[3]=AI1
            if (_playerOrigins == null || _playerOrigins.Length == 0)
                return null;

            int idx = 2 + aiIndex;
            if (idx >= _playerOrigins.Length)
            {
                Debug.LogWarning($"[ServerPlayerVesselInitializerWithAI] Not enough spawn origins for AI {aiIndex} " +
                                 $"(need index {idx}, have {_playerOrigins.Length}). Wrapping with modulo.");
                idx = idx % _playerOrigins.Length;
            }
            return _playerOrigins[idx];
        }

        private bool TrySpawnVesselForAI(Player aiPlayer, out NetworkObject vesselNO)
        {
            vesselNO = null;

            var vesselType = aiPlayer.NetDefaultVesselType.Value;

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

            vesselNO = Instantiate(shipNetworkObject);

            // Place at AI player position (or tweak as needed)
            vesselNO.transform.SetPositionAndRotation(aiPlayer.transform.position, aiPlayer.transform.rotation);
            vesselNO.Spawn(true); // server-owned
            aiPlayer.NetVesselId.Value = vesselNO.NetworkObjectId;
            return true;
        }
    }
}