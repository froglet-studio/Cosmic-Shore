// NetworkCrystalManager.cs
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using CosmicShore.Data;
using CosmicShore.Utility;
using System.Linq;

namespace CosmicShore.Gameplay
{
    public class NetworkCrystalManager : CrystalManager
    {
        [Header("Early Spawn")]
        [Tooltip("When true, crystals spawn once all players are ready (before the ready button) instead of on turn start.")]
        [SerializeField] private bool spawnOnClientReady;

        // Atomic position + domain per crystal slot. Using a single NetworkList
        // guarantees that when a client processes a change callback, both the
        // position and the server-authoritative domain are available together.
        // Two separate NetworkLists can have their callbacks fire independently
        // (positions processed before domains), causing stale domain reads.
        private NetworkList<CrystalSlotData> n_Slots;

        // Anchor used for the current initial batch so late-joining players
        // spawn around the same position as the first crystal.
        private Vector3 _initialBatchAnchor;
        private bool _initialBatchStarted;

        protected override void Awake()
        {
            base.Awake();
            n_Slots = new NetworkList<CrystalSlotData>();
        }

        public override void OnNetworkSpawn()
        {
            if (spawnOnClientReady)
            {
                gameData.OnClientReady.OnRaised += OnClientReadySpawn;
                // Spawn each player's crystal as they join, and catch up
                // on turn start in case OnPlayerAdded was missed.
                gameData.OnPlayerAdded += OnPlayerAddedSpawn;
                gameData.OnMiniGameTurnStarted.OnRaised += OnTurnStartedCatchUp;
            }
            else
                gameData.OnMiniGameTurnStarted.OnRaised += OnTurnStarted;

            gameData.OnResetForReplay.OnRaised += OnResetForReplay;
            n_Slots.OnListChanged += OnSlotsChanged;
        }

        public override void OnNetworkDespawn()
        {
            if (spawnOnClientReady)
            {
                gameData.OnClientReady.OnRaised -= OnClientReadySpawn;
                gameData.OnPlayerAdded -= OnPlayerAddedSpawn;
                gameData.OnMiniGameTurnStarted.OnRaised -= OnTurnStartedCatchUp;
            }
            else
                gameData.OnMiniGameTurnStarted.OnRaised -= OnTurnStarted;

            gameData.OnResetForReplay.OnRaised -= OnResetForReplay;

            if (n_Slots != null)
                n_Slots.OnListChanged -= OnSlotsChanged;
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
        /// Reads all existing non-empty slots from the NetworkList and
        /// spawns crystals that this client hasn't created yet. Handles the
        /// case where a client joins after the server already set slots.
        /// </summary>
        private void SyncExistingCrystals()
        {
            for (int i = 0; i < n_Slots.Count; i++)
            {
                var slot = n_Slots[i];
                if (slot.IsEmpty) continue;

                int crystalId = i + 1;
                if (!cellData.TryGetCrystalById(crystalId, out _))
                {
                    var crystal = SpawnWithDomain(crystalId, slot.Position, (Domains)slot.Domain);
                    cellData.AddCrystalToList(crystal);
                }
            }
        }

        // ---------------- Replay Reset ----------------

        void OnResetForReplay()
        {
            // Reset base class spawn state on ALL clients — destroys old crystals,
            // clears anchor/position tracking so spawning starts fresh from index 0.
            ResetSpawnState();

            if (!IsServer) return;
            serverBatchAnchorIndex = 0;
            _initialBatchStarted = false;

            for (int i = 0; i < n_Slots.Count; i++)
                n_Slots[i] = CrystalSlotData.Empty;
            CSDebug.Log("[NetworkCrystalManager] Reset for replay — anchor index and slots cleared.");
        }

        // ---------------- Server Turn Start ----------------

        private void EnsureListSizedToSelectedPlayerCount()
        {
            int count = GetCrystalCountToSpawn();

            while (n_Slots.Count < count)
                n_Slots.Add(CrystalSlotData.Empty);

            while (n_Slots.Count > count)
                n_Slots.RemoveAt(n_Slots.Count - 1);
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

            for (int i = 0; i < n_Slots.Count; i++)
            {
                var domain = Domains.None;
                if (spawnCrystalWithPlayerDomain && i < gameData.Players.Count)
                    domain = gameData.Players[i].Domain;

                // Write position + domain atomically in a single struct.
                // This guarantees the OnSlotsChanged callback on both server
                // and client always has both values available together.
                n_Slots[i] = new CrystalSlotData
                {
                    Position = GetSpawnPointAroundAnchor(batchAnchor),
                    Domain = (int)domain
                };
            }

            AdvanceBatchAnchor_ForNetworkTurnStart();
        }

        /// <summary>
        /// Adds crystals for players who joined after the initial batch,
        /// reusing the same batch anchor so all crystals cluster together.
        /// </summary>
        private void SpawnMissingCrystals()
        {
            int expected = GetCrystalCountToSpawn();
            if (n_Slots.Count >= expected) return;
            if (!_initialBatchStarted) return;

            EnsureListSizedToSelectedPlayerCount();

            for (int i = 0; i < n_Slots.Count; i++)
            {
                if (n_Slots[i].IsEmpty)
                {
                    var domain = Domains.None;
                    if (spawnCrystalWithPlayerDomain && i < gameData.Players.Count)
                        domain = gameData.Players[i].Domain;

                    n_Slots[i] = new CrystalSlotData
                    {
                        Position = GetSpawnPointAroundAnchor(_initialBatchAnchor),
                        Domain = (int)domain
                    };
                }
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

        /// <summary>
        /// Override base Spawn to use server-authoritative domain from n_Slots
        /// instead of the non-deterministic gameData.Players index.
        /// </summary>
        protected override Crystal Spawn(int crystalId, Vector3 spawnPos)
        {
            int idx = crystalId - 1;
            if (idx >= 0 && idx < n_Slots.Count)
                return SpawnWithDomain(crystalId, spawnPos, (Domains)n_Slots[idx].Domain);

            return base.Spawn(crystalId, spawnPos);
        }

        private void OnSlotsChanged(NetworkListEvent<CrystalSlotData> e)
        {
            if (e.Type != NetworkListEvent<CrystalSlotData>.EventType.Add &&
                e.Type != NetworkListEvent<CrystalSlotData>.EventType.Insert &&
                e.Type != NetworkListEvent<CrystalSlotData>.EventType.Value)
                return;

            int idx = e.Index;
            if (idx < 0 || idx >= n_Slots.Count) return;

            var slot = e.Value;

            // Empty = placeholder from reset, ignore
            if (slot.IsEmpty) return;

            int crystalId = idx + 1;

            if (!cellData.TryGetCrystalById(crystalId, out _))
            {
                var crystal = SpawnWithDomain(crystalId, slot.Position, (Domains)slot.Domain);
                cellData.AddCrystalToList(crystal);
            }
            else
                UpdateCrystalPos(crystalId, slot.Position);
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
            if (idx < 0 || idx >= n_Slots.Count) return;

            Vector3 newPos = CalculateNewSpawnPos(crystalId);
            // Preserve existing domain, only update position
            n_Slots[idx] = new CrystalSlotData
            {
                Position = newPos,
                Domain = n_Slots[idx].Domain
            };
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
            if (n_Slots == null) return;
            Gizmos.color = Color.yellow;
            foreach (var slot in n_Slots)
                Gizmos.DrawWireSphere(slot.Position, 5f);
        }
    }

    /// <summary>
    /// Atomic crystal slot data — position + domain in a single NetworkList entry.
    /// Guarantees that when OnListChanged fires on any client, both the position
    /// and the server-authoritative domain are available in the same callback.
    /// </summary>
    public struct CrystalSlotData : INetworkSerializable, System.IEquatable<CrystalSlotData>
    {
        public Vector3 Position;
        public int Domain;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Position);
            serializer.SerializeValue(ref Domain);
        }

        public bool Equals(CrystalSlotData other) =>
            Position.Equals(other.Position) && Domain == other.Domain;

        public override bool Equals(object obj) => obj is CrystalSlotData other && Equals(other);
        public override int GetHashCode() => Position.GetHashCode() ^ Domain;

        public bool IsEmpty => Position == Vector3.zero;

        public static CrystalSlotData Empty => new()
        {
            Position = Vector3.zero,
            Domain = (int)Domains.None
        };
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
