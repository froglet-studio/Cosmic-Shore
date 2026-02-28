// NetworkCrystalManager.cs
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using CosmicShore.Utility;
using System.Linq;

namespace CosmicShore.Gameplay
{
    public class NetworkCrystalManager : CrystalManager
    {
        [Header("Early Spawn")]
        [Tooltip("When true, crystals spawn once all players are ready (before the ready button) instead of on turn start.")]
        [SerializeField] private bool spawnOnClientReady;

        private NetworkList<Vector3> n_Positions;

        protected override void Awake()
        {
            base.Awake();
            n_Positions = new NetworkList<Vector3>();
        }

        public override void OnNetworkSpawn()
        {
            SubscribeToCrystalEvents();
        }

        public override void OnNetworkDespawn()
        {
            UnsubscribeFromCrystalEvents();
        }

        /// <summary>
        /// Re-subscribe when the environment is reactivated (party mode SetActive
        /// toggling). OnDisable already unsubscribes so events from inactive
        /// environments don't fire and spawn invisible crystals.
        /// In party mode, subscribe even when IsSpawned is false — the environment
        /// was deactivated during network spawn so IsSpawned may be unreliable,
        /// but the host is both server and client so direct spawning works.
        /// </summary>
        private void OnEnable()
        {
            if (IsSpawned || IsPartyMode)
                SubscribeToCrystalEvents();
        }

        private void OnDisable()
        {
            UnsubscribeFromCrystalEvents();
        }

        void SubscribeToCrystalEvents()
        {
            // Unsubscribe first to prevent double-subscription when both
            // OnNetworkSpawn and OnEnable fire in the same lifecycle.
            UnsubscribeFromCrystalEvents();

            // In party mode, always use OnMiniGameTurnStarted (not OnClientReady)
            // to avoid timing issues: Cell.Initialize runs after 1s via InitializeAfterDelay,
            // but OnClientReady fires at 0.5s on round 2+. Spawning crystals before Cell
            // init causes null refs in SnowChangerManager and other cell-dependent systems.
            if (spawnOnClientReady && !IsPartyMode)
                gameData.OnClientReady.OnRaised += OnClientReadySpawn;
            else
                gameData.OnMiniGameTurnStarted.OnRaised += OnTurnStarted;

            gameData.OnResetForReplay.OnRaised += OnResetForReplay;

            if (n_Positions != null)
                n_Positions.OnListChanged += OnPositionsChanged;
        }

        void UnsubscribeFromCrystalEvents()
        {
            // Always unsubscribe from both paths to be safe
            if (spawnOnClientReady)
                gameData.OnClientReady.OnRaised -= OnClientReadySpawn;
            gameData.OnMiniGameTurnStarted.OnRaised -= OnTurnStarted;

            gameData.OnResetForReplay.OnRaised -= OnResetForReplay;

            if (n_Positions != null)
                n_Positions.OnListChanged -= OnPositionsChanged;
        }

        private void OnClientReadySpawn() => OnTurnStarted();

        // ---------------- Replay Reset ----------------

        void OnResetForReplay()
        {
            // In party mode, bypass NetworkList and destroy crystals directly.
            // The host is both server and client so no replication is needed.
            // IsSpawned/IsServer may be unreliable after environment deactivation/reactivation.
            if (IsPartyMode)
            {
                ResetSpawnState();
                serverBatchAnchorIndex = 0;
                CSDebug.Log("[NetworkCrystalManager] Party mode reset — crystals destroyed.");
                return;
            }

            // Reset base class spawn state on ALL clients — destroys old crystals,
            // clears anchor/position tracking so spawning starts fresh from index 0.
            ResetSpawnState();

            if (!IsServer) return;
            serverBatchAnchorIndex = 0;

            for (int i = 0; i < n_Positions.Count; i++)
                n_Positions[i] = Vector3.zero;
            CSDebug.Log("[NetworkCrystalManager] Reset for replay — anchor index and positions cleared.");
        }

        // ---------------- Server Turn Start ----------------

        private void EnsureListSizedToSelectedPlayerCount()
        {
            int count = GetCrystalCountToSpawn();

            while (n_Positions.Count < count)
                n_Positions.Add(Vector3.zero);

            while (n_Positions.Count > count)
                n_Positions.RemoveAt(n_Positions.Count - 1);
        }

        private void OnTurnStarted()
        {
            if (!IsServer) return;

            EnsureListSizedToSelectedPlayerCount();

            Vector3 batchAnchor = GetBatchAnchor_ForNetworkTurnStart();
            for (int i = 0; i < n_Positions.Count; i++)
                n_Positions[i] = GetSpawnPointAroundAnchor(batchAnchor);

            AdvanceBatchAnchor_ForNetworkTurnStart();
        }

        // ---------------- Anchor Helpers ----------------

        private int serverBatchAnchorIndex;

        private Vector3 GetBatchAnchor_ForNetworkTurnStart()
        {
            if (!TryGetAnchors(out var anchors) || anchors == null || anchors.Length == 0)
                return Vector3.forward * 30f;

            serverBatchAnchorIndex %= anchors.Length;
            return anchors[serverBatchAnchorIndex];
        }

        private void AdvanceBatchAnchor_ForNetworkTurnStart()
        {
            if (!TryGetAnchors(out var anchors) || anchors == null || anchors.Length == 0)
                return;

            serverBatchAnchorIndex = (serverBatchAnchorIndex + 1) % anchors.Length;
        }

        private bool TryGetAnchors(out Vector3[] anchors)
        {
            anchors = null;
            return TryGetCrystalPositionListByIntensity(out anchors);
        }

        // ---------------- Replication ----------------

        private void OnPositionsChanged(NetworkListEvent<Vector3> e)
        {
            if (e.Type != NetworkListEvent<Vector3>.EventType.Add &&
                e.Type != NetworkListEvent<Vector3>.EventType.Insert &&
                e.Type != NetworkListEvent<Vector3>.EventType.Value)
                return;

            int idx = e.Index;
            if (idx < 0 || idx >= n_Positions.Count) return;

            int crystalId = idx + 1;
            Vector3 pos = e.Value;

            // Zero = placeholder from reset, ignore
            if (pos == Vector3.zero) return;

            if (!cellData.TryGetCrystalById(crystalId, out _))
            {
                var crystal = Spawn(crystalId, pos);
                cellData.AddCrystalToList(crystal);
            }
            else
                UpdateCrystalPos(crystalId, pos);
        }

        // ---------------- Public API ----------------

        public override void RespawnCrystal(int crystalId)
        {
            RespawnCrystal_ServerRpc(crystalId);
        }

        [ServerRpc(RequireOwnership = false)]
        private void RespawnCrystal_ServerRpc(int crystalId)
        {
            int idx = crystalId - 1;
            if (idx < 0 || idx >= n_Positions.Count) return;

            Vector3 newPos = CalculateNewSpawnPos(crystalId);
            n_Positions[idx] = newPos;
        }

        public override void ExplodeCrystal(int crystalId, Crystal.ExplodeParams explodeParams) =>
            ExplodeCrystal_ServerRpc(crystalId, NetworkExplodeParams.FromExplodeParams(explodeParams));

        [ServerRpc(RequireOwnership = false)]
        private void ExplodeCrystal_ServerRpc(int crystalId, NetworkExplodeParams explodeParams)
        {
            ExplodeCrystal_ClientRpc(crystalId, explodeParams);
        }

        [ClientRpc]
        private void ExplodeCrystal_ClientRpc(int crystalId, NetworkExplodeParams explodeParams)
        {
            if (cellData.TryGetCrystalById(crystalId, out var crystal))
                crystal.Explode(explodeParams.ToExplodeParams());
        }

        private void OnDrawGizmosSelected()
        {
            if (n_Positions == null) return;
            Gizmos.color = Color.yellow;
            foreach (var pos in n_Positions)
                Gizmos.DrawWireSphere(pos, 5f);
        }
    }

    public struct NetworkExplodeParams : INetworkSerializable
    {
        private Vector3 Course;
        private float Speed;
        private FixedString64Bytes PlayerName;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Course);
            serializer.SerializeValue(ref Speed);
            serializer.SerializeValue(ref PlayerName);
        }

        private NetworkExplodeParams(Vector3 course, float speed, FixedString64Bytes playerName)
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