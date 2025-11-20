using CosmicShore.Utility.ClassExtensions;
using Cysharp.Threading.Tasks;
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
        
        public override void OnNetworkSpawn()
        {
            gameData.OnMiniGameTurnStarted.OnRaised += MiniGameTurnStarted;
            
            if (IsServer)
                n_SpawnPos.Value = CalculateSpawnPos();
            
            n_SpawnPos.OnValueChanged += OnSpawnPosChanged;
        }

        public override void OnNetworkDespawn()
        {
            gameData.OnMiniGameTurnStarted.OnRaised -= MiniGameTurnStarted;
            n_SpawnPos.OnValueChanged -= OnSpawnPosChanged;
        }

        public override void RespawnCrystal() =>
            RespawnCrystal_ServerRpc();

        public override void ExplodeCrystal(Crystal.ExplodeParams explodeParams) =>
            ExplodeCrystal_ServerRpc(NetworkExplodeParams.FromExplodeParams(explodeParams));

        [ServerRpc(RequireOwnership = false)]
        void ExplodeCrystal_ServerRpc(NetworkExplodeParams explodeParams)
        {
            ExplodeCrystal_ClientRpc(explodeParams);
        }

        [ClientRpc]
        void ExplodeCrystal_ClientRpc(NetworkExplodeParams explodeParams) =>
            cellData.Crystal.Explode(explodeParams.ToExplodeParams());

        [ServerRpc(RequireOwnership = false)]
        void RespawnCrystal_ServerRpc() =>
            n_SpawnPos.Value = CalculateNewSpawnPos();

        void MiniGameTurnStarted()
        {
            if (!cellData.Crystal)
                Spawn(n_SpawnPos.Value);
        }

        void OnSpawnPosChanged(Vector3 previousValue, Vector3 newValue)
        {
            UpdateCrystalPos(newValue);
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