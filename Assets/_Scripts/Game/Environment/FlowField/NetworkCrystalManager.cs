using CosmicShore.Utility.ClassExtensions;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace CosmicShore.Game
{
    public class NetworkCrystalManager : CrystalManager
    {
        NetworkVariable<Vector3> n_SpawnPos = new(
            writePerm: NetworkVariableWritePermission.Server, 
            readPerm: NetworkVariableReadPermission.Everyone
        );

        bool isExploding;
        
        
        public override void OnNetworkSpawn()
        {
            gameData.OnInitializeGame += OnInitializeGame;
            gameData.OnGameStarted += OnGameStartedInServer;
        }

        public override void OnNetworkDespawn()
        {
            gameData.OnInitializeGame -= OnInitializeGame;
            gameData.OnGameStarted -= OnGameStartedInServer;
            n_SpawnPos.OnValueChanged -= OnSpawnPosChanged;
        }

        public override void RespawnCrystal() =>
            RespawnCrystal_ServerRpc();

        public override void ExplodeCrystal(Crystal.ExplodeParams explodeParams) =>
            ExplodeCrystal_ServerRpc(NetworkExplodeParams.FromExplodeParams(explodeParams));

        protected override void Spawn(Vector3 spawnPos)
        {
            if (cellData.Crystal)
            {
                DebugExtensions.LogErrorColored("Already have a crystal, should not happen!", Color.magenta);
                return;
            }
            
            var crystal = Instantiate(crystalPrefab, spawnPos, Quaternion.identity, transform);
            crystal.InjectDependencies(this);
            cellData.Crystal = crystal;
            TryInitializeAndAdd(crystal);
            cellData.OnCrystalSpawned.Raise();
        }

        [ServerRpc(RequireOwnership = false)]
        void ExplodeCrystal_ServerRpc(NetworkExplodeParams explodeParams)
        {
            if (isExploding)
                return;
            isExploding = true;
            ExplodeCrystal_ClientRpc(explodeParams);
        }

        [ClientRpc]
        void ExplodeCrystal_ClientRpc(NetworkExplodeParams explodeParams) =>
            cellData.Crystal.Explode(explodeParams.ToExplodeParams());

        [ServerRpc(RequireOwnership = false)]
        void RespawnCrystal_ServerRpc() =>
            n_SpawnPos.Value = CalculateNewSpawnPos();
        
        void OnInitializeGame()
        {
            if (IsServer)
                n_SpawnPos.Value = CalculateSpawnPos();
            
            n_SpawnPos.OnValueChanged += OnSpawnPosChanged;
        }

        void OnGameStartedInServer()
        {
            if (!cellData.Crystal)
                Spawn(n_SpawnPos.Value);
        }

        void OnSpawnPosChanged(Vector3 previousValue, Vector3 newValue)
        {
            UpdateCrystalPos(newValue);
            isExploding = false;
        }
    }
    
    public struct NetworkExplodeParams : INetworkSerializable
    {
        Vector3 Course;
        float Speed;
        FixedString64Bytes PlayerName;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Course);
            serializer.SerializeValue(ref Speed);
            serializer.SerializeValue(ref PlayerName);
        }

        NetworkExplodeParams(Vector3 course, float speed, FixedString64Bytes playerName)
        {
            Course = course;
            Speed = speed;
            PlayerName = playerName;
        }

        // ✅ Helper: convert from regular ExplodeParams
        public static NetworkExplodeParams FromExplodeParams(Crystal.ExplodeParams e)
        {
            return new NetworkExplodeParams(
                e.Course,
                e.Speed,
                e.PlayerName
            );
        }

        // ✅ Optional reverse helper
        public Crystal.ExplodeParams ToExplodeParams()
        {
            return new Crystal.ExplodeParams
            {
                Course = Course,
                Speed = Speed,
                PlayerName = PlayerName
            };
        }
    }
}