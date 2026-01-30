using System;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using Random = UnityEngine.Random;

namespace CosmicShore.Game
{
    public class NetworkCrystalManager : CrystalManager
    {
        private NetworkList<Vector3> n_Positions;

        protected override void Awake()
        {
            base.Awake();
            n_Positions = new NetworkList<Vector3>();
        }

        public override void OnNetworkSpawn()
        {
            gameData.OnMiniGameTurnStarted.OnRaised += OnTurnStarted;

            if (IsServer)
            {
                int count = Mathf.Max(1, gameData.SelectedPlayerCount.Value);
                while (n_Positions.Count < count)
                    n_Positions.Add(Vector3.zero);
                while (n_Positions.Count > count)
                    n_Positions.RemoveAt(n_Positions.Count - 1);
            }

            n_Positions.OnListChanged += OnPositionsChanged;
        }


        public override void OnNetworkDespawn()
        {
            gameData.OnMiniGameTurnStarted.OnRaised -= OnTurnStarted;
            if (n_Positions != null)
                n_Positions.OnListChanged -= OnPositionsChanged;
        }

        // ---------------- Turn Start ----------------
        void OnTurnStarted()
        {
            if (!IsServer) return;

            for (int idx = 0; idx < n_Positions.Count; idx++)
                n_Positions[idx] = GetSpawnPointBasedOnCurrentAnchor();
            
            AdvanceSpawnAnchorIndex();
        }

        // ---------------- Replication ----------------
        void OnPositionsChanged(NetworkListEvent<Vector3> e)
        {
            // Only react to add/insert/value updates
            if (e.Type != NetworkListEvent<Vector3>.EventType.Add &&
                e.Type != NetworkListEvent<Vector3>.EventType.Insert &&
                e.Type != NetworkListEvent<Vector3>.EventType.Value)
                return;

            int idx = e.Index;
            if (idx < 0 || idx >= n_Positions.Count)
                return;

            int crystalId = idx + 1;

            // Prefer e.Value when available; fallback to list read
            Vector3 pos = (e.Type == NetworkListEvent<Vector3>.EventType.Value ||
                           e.Type == NetworkListEvent<Vector3>.EventType.Add ||
                           e.Type == NetworkListEvent<Vector3>.EventType.Insert)
                ? e.Value
                : n_Positions[idx];

            // If you use Vector3.zero as placeholder, don't spawn/move yet
            if (pos == Vector3.zero)
                return;

            if (!cellData.TryGetCrystalById(crystalId, out _))
                Spawn(crystalId, pos);
            else
                UpdateCrystalPos(crystalId, pos);
        }


        // ---------------- Public API ----------------
        public override void RespawnCrystal(int crystalId) => RespawnCrystal_ServerRpc(crystalId);

        [ServerRpc(RequireOwnership = false)]
        void RespawnCrystal_ServerRpc(int crystalId)
        {
            int idx = crystalId - 1;
            if (idx < 0 || idx >= n_Positions.Count) return;

            n_Positions[idx] = CalculateNewSpawnPos(crystalId); // <-- anchor-based
        }

        public override void ExplodeCrystal(int crystalId, Crystal.ExplodeParams explodeParams) =>
            ExplodeCrystal_ServerRpc(crystalId, NetworkExplodeParams.FromExplodeParams(explodeParams));

        [ServerRpc(RequireOwnership = false)]
        void ExplodeCrystal_ServerRpc(int crystalId, NetworkExplodeParams explodeParams)
        {
            ExplodeCrystal_ClientRpc(crystalId, explodeParams);
        }

        [ClientRpc]
        void ExplodeCrystal_ClientRpc(int crystalId, NetworkExplodeParams explodeParams)
        {
            if (cellData.TryGetCrystalById(crystalId, out var crystal))
                crystal.Explode(explodeParams.ToExplodeParams());
        }

        private void OnDrawGizmosSelected()
        {
            if (n_Positions == null)
                return;
            
            Gizmos.color = Color.yellow;
            
            foreach (var pos in n_Positions)
            {
                Gizmos.DrawWireSphere(pos, 5f);    
            }
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

        public static NetworkExplodeParams FromExplodeParams(Crystal.ExplodeParams e) =>
            new NetworkExplodeParams(e.Course, e.Speed, e.PlayerName);

        public Crystal.ExplodeParams ToExplodeParams() =>
            new Crystal.ExplodeParams { Course = Course, Speed = Speed, PlayerName = PlayerName };
    }
}
