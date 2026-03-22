// NetworkCrystalManager.cs
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using CosmicShore.Utility;

namespace CosmicShore.Game
{
    public class NetworkCrystalManager : CrystalManager
    {
        [Header("Early Spawn")]
        [Tooltip("When true, crystals spawn once all players are ready (before the ready button) instead of on turn start.")]
        [SerializeField] private bool spawnOnClientReady;

        private NetworkList<Vector3> n_Positions;

        // Anchor used for the current initial batch so late-joining players
        // spawn around the same position as the first crystal.
        private Vector3 _initialBatchAnchor;
        private bool _initialBatchStarted;

        protected override void Awake()
        {
            base.Awake();
            n_Positions = new NetworkList<Vector3>();
        }

        public override void OnNetworkSpawn()
        {
            if (spawnOnClientReady)
            {
                gameData.OnClientReady += OnClientReadySpawn;
                // Spawn each player's crystal as they join, and catch up
                // on turn start in case OnPlayerAdded was missed.
                gameData.OnPlayerAdded += OnPlayerAddedSpawn;
                gameData.OnMiniGameTurnStarted.OnRaised += OnTurnStartedCatchUp;
            }
            else
                gameData.OnMiniGameTurnStarted.OnRaised += OnTurnStarted;

            gameData.OnResetForReplay.OnRaised += OnResetForReplay;
            n_Positions.OnListChanged += OnPositionsChanged;
        }

        public override void OnNetworkDespawn()
        {
            if (spawnOnClientReady)
            {
                gameData.OnClientReady -= OnClientReadySpawn;
                gameData.OnPlayerAdded -= OnPlayerAddedSpawn;
                gameData.OnMiniGameTurnStarted.OnRaised -= OnTurnStartedCatchUp;
            }
            else
                gameData.OnMiniGameTurnStarted.OnRaised -= OnTurnStarted;

            gameData.OnResetForReplay.OnRaised -= OnResetForReplay;

            if (n_Positions != null)
                n_Positions.OnListChanged -= OnPositionsChanged;
        }

        private void OnClientReadySpawn()
        {
            // Server: spawn crystals for the current player count.
            OnTurnStarted();

            // Client: catch up on crystals already in the NetworkList.
            // OnListChanged does NOT fire for entries that existed before
            // this client subscribed, so we must read them manually.
            SyncExistingCrystals();
        }

        private void OnPlayerAddedSpawn(string playerName, Domains domain)
        {
            if (!IsServer) return;
            SpawnMissingCrystals();
        }

        /// <summary>
        /// Final fallback: if any crystals are still missing when the turn
        /// starts (all players guaranteed present), spawn them now.
        /// </summary>
        private void OnTurnStartedCatchUp()
        {
            if (IsServer)
                SpawnMissingCrystals();

            // All clients (including host) sync any crystals they missed.
            SyncExistingCrystals();
        }

        /// <summary>
        /// Reads all existing non-zero positions from the NetworkList and
        /// spawns crystals that this client hasn't created yet. Handles the
        /// case where a client joins after the server already set positions.
        /// </summary>
        private void SyncExistingCrystals()
        {
            for (int i = 0; i < n_Positions.Count; i++)
            {
                Vector3 pos = n_Positions[i];
                if (pos == Vector3.zero) continue;

                int crystalId = i + 1;
                if (!cellData.TryGetCrystalById(crystalId, out _))
                {
                    var crystal = Spawn(crystalId, pos);
                    cellData.AddCrystalToList(crystal);
                }
            }
        }

        // ---------------- Replay Reset ----------------

        void OnResetForReplay()
        {
            if (!IsServer) return;
            serverBatchAnchorIndex = 0;
            _initialBatchStarted = false;

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

            // Remember this anchor so late-joining players spawn at the
            // same position cluster (see SpawnMissingCrystals).
            _initialBatchAnchor = batchAnchor;
            _initialBatchStarted = true;

            for (int i = 0; i < n_Positions.Count; i++)
                n_Positions[i] = GetSpawnPointAroundAnchor(batchAnchor);

            AdvanceBatchAnchor_ForNetworkTurnStart();
        }

        /// <summary>
        /// Adds crystals for players who joined after the initial batch,
        /// reusing the same batch anchor so all crystals cluster together.
        /// </summary>
        private void SpawnMissingCrystals()
        {
            int expected = GetCrystalCountToSpawn();
            if (n_Positions.Count >= expected) return;
            if (!_initialBatchStarted) return;

            EnsureListSizedToSelectedPlayerCount();

            for (int i = 0; i < n_Positions.Count; i++)
            {
                if (n_Positions[i] == Vector3.zero)
                    n_Positions[i] = GetSpawnPointAroundAnchor(_initialBatchAnchor);
            }
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