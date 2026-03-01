using System;
using System.Collections.Generic;
using CosmicShore.Utility;
using Obvious.Soap;
using Reflex.Attributes;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using Random = UnityEngine.Random;
using CosmicShore.Data;

namespace CosmicShore.Gameplay
{
    [Serializable]
    public class CrystalPositionSet
    {
        public List<Vector3> positions;
    }

    /// <summary>
    /// Unified crystal manager that works in both single-player and multiplayer contexts.
    ///
    /// In multiplayer the server computes spawn/respawn positions and broadcasts them via
    /// ClientRpc. Every client (including the host) instantiates purely-local crystal
    /// GameObjects at the received positions — crystals are never NetworkObjects.
    ///
    /// In single-player (no active NetworkManager) the same logic runs locally without RPCs.
    ///
    /// Spawn modes:
    ///   • Crystal Capture / Freestyle — random positions on a unit-sphere around an anchor.
    ///     Crystal count = player count + extraCrystalsToSpawnBeyondPlayerCount.
    ///   • Hex Race — pre-defined anchor positions configured per intensity in the inspector.
    ///     No randomisation; crystals land on the authored positions.
    /// </summary>
    public class CrystalManager : NetworkBehaviour
    {
        private const float MIN_SQR_SPACE_BTWN_CURRENT_AND_LAST_SPAWN_POS = 25f;

        [Header("Dependencies")]
        [Inject] protected GameDataSO gameData;
        [SerializeField] protected CellRuntimeDataSO cellData;

        [Header("Crystal Prefab")]
        [SerializeField] protected Crystal crystalPrefab;

        [Header("Anchor Positions (By Intensity)")]
        [SerializeField] private bool scaleCrystalPositionWithIntensity;
        [SerializeField] private IntVariable intensityLevelData;
        [SerializeField] private List<CrystalPositionSet> listOfCrystalPositions;

        [Header("Crystal Configuration")]
        [SerializeField] private bool spawnCrystalWithPlayerDomain;
        [SerializeField] private int extraCrystalsToSpawnBeyondPlayerCount;

        [Header("Spawn Trigger")]
        [Tooltip("When true, crystals spawn on OnClientReady instead of on turn start.")]
        [SerializeField] private bool spawnOnClientReady;

        // ---------------- Runtime State ----------------

        private readonly Dictionary<int, Vector3> lastSpawnPosById = new();
        private readonly Dictionary<int, int> lastAnchorIndexByCrystalId = new();
        private int batchAnchorIndex;
        private int itemsAdded;

        /// <summary>True when a NetworkManager is active and this object is network-spawned.</summary>
        private bool IsNetworked => IsSpawned;

        // ================================================================
        // Lifecycle
        // ================================================================

        protected virtual void Awake()
        {
            cellData.CellItems = new List<CellItem>();
            cellData.Crystals ??= new List<Crystal>();
        }

        public override void OnNetworkSpawn()
        {
            SubscribeToEvents();
        }

        public override void OnNetworkDespawn()
        {
            UnsubscribeFromEvents();
        }

        protected virtual void OnEnable()
        {
            // Non-networked path: subscribe immediately.
            // Networked path: OnNetworkSpawn handles subscription.
            if (!IsNetworked)
                SubscribeToEvents();
        }

        protected virtual void OnDisable()
        {
            if (!IsNetworked)
                UnsubscribeFromEvents();
        }

        private bool _subscribed;

        private void SubscribeToEvents()
        {
            if (_subscribed) return;
            _subscribed = true;

            if (spawnOnClientReady)
                gameData.OnClientReady.OnRaised += OnSpawnTrigger;
            else
                gameData.OnMiniGameTurnStarted.OnRaised += OnSpawnTrigger;

            gameData.OnMiniGameTurnEnd.OnRaised += OnTurnEnded;
            gameData.OnResetForReplay.OnRaised += OnResetForReplay;
        }

        private void UnsubscribeFromEvents()
        {
            if (!_subscribed) return;
            _subscribed = false;

            if (spawnOnClientReady)
                gameData.OnClientReady.OnRaised -= OnSpawnTrigger;
            else
                gameData.OnMiniGameTurnStarted.OnRaised -= OnSpawnTrigger;

            gameData.OnMiniGameTurnEnd.OnRaised -= OnTurnEnded;
            gameData.OnResetForReplay.OnRaised -= OnResetForReplay;
        }

        // ================================================================
        // Spawn Trigger
        // ================================================================

        private void OnSpawnTrigger()
        {
            if (IsNetworked)
            {
                // Only the server computes positions and broadcasts.
                if (!IsServer) return;
                BroadcastBatchSpawn();
            }
            else
            {
                // Single-player: spawn directly.
                SpawnBatchIfMissing();
            }
        }

        // ================================================================
        // Turn End
        // ================================================================

        private void OnTurnEnded()
        {
            DestroyAllCrystals();
        }

        // ================================================================
        // Replay Reset
        // ================================================================

        private void OnResetForReplay()
        {
            ResetSpawnState();
        }

        // ================================================================
        // Networked Batch Spawn — Server computes, broadcasts via RPC
        // ================================================================

        private void BroadcastBatchSpawn()
        {
            int count = GetCrystalCountToSpawn();
            Vector3 batchAnchor = GetAnchorForBatchIndex(batchAnchorIndex);

            var ids = new int[count];
            var positions = new Vector3[count];

            for (int i = 0; i < count; i++)
            {
                ids[i] = i + 1;
                positions[i] = GetSpawnPointAroundAnchor(batchAnchor);
            }

            batchAnchorIndex = GetNextAnchorIndex(batchAnchorIndex);

            SpawnBatch_ClientRpc(ids, positions);
        }

        [ClientRpc]
        private void SpawnBatch_ClientRpc(int[] crystalIds, Vector3[] positions)
        {
            for (int i = 0; i < crystalIds.Length; i++)
            {
                int id = crystalIds[i];
                Vector3 pos = positions[i];

                if (!cellData.TryGetCrystalById(id, out _))
                {
                    var crystal = SpawnLocal(id, pos);
                    cellData.AddCrystalToList(crystal);
                    lastAnchorIndexByCrystalId[id] = batchAnchorIndex;
                }
                else
                {
                    UpdateCrystalPos(id, pos);
                }
            }
        }

        // ================================================================
        // Respawn — public API called by Crystal on collection
        // ================================================================

        public virtual void RespawnCrystal(int crystalId)
        {
            if (IsNetworked)
            {
                RespawnCrystal_ServerRpc(crystalId);
            }
            else
            {
                var newPos = CalculateNewSpawnPos(crystalId);
                UpdateCrystalPos(crystalId, newPos);
            }
        }

        [ServerRpc(RequireOwnership = false)]
        private void RespawnCrystal_ServerRpc(int crystalId)
        {
            Vector3 newPos = CalculateNewSpawnPos(crystalId);
            UpdateCrystalPos_ClientRpc(crystalId, newPos);
        }

        [ClientRpc]
        private void UpdateCrystalPos_ClientRpc(int crystalId, Vector3 newPos)
        {
            // Update tracking on all clients so anchor progression stays in sync.
            lastSpawnPosById[crystalId] = newPos;
            UpdateCrystalPos(crystalId, newPos);
        }

        // ================================================================
        // Explode — public API called by Crystal on impact
        // ================================================================

        public virtual void ExplodeCrystal(int crystalId, Crystal.ExplodeParams explodeParams)
        {
            if (IsNetworked)
            {
                ExplodeCrystal_ServerRpc(crystalId, NetworkExplodeParams.FromExplodeParams(explodeParams));
            }
            else
            {
                if (cellData.TryGetCrystalById(crystalId, out var crystal) && crystal != null)
                    crystal.Explode(explodeParams);
            }
        }

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

        // ================================================================
        // Local Spawn / Update helpers
        // ================================================================

        /// <summary>
        /// Instantiate a local crystal at spawnPos with the given stable id.
        /// </summary>
        protected virtual Crystal SpawnLocal(int crystalId, Vector3 spawnPos)
        {
            if (cellData.TryGetCrystalById(crystalId, out Crystal existing))
            {
                DebugExtensions.LogErrorColored(
                    $"Crystal with id {crystalId} already exists, skipping spawn.",
                    Color.magenta
                );
                return existing;
            }

            var crystal = Instantiate(crystalPrefab, spawnPos, Quaternion.identity, transform);
            crystal.InjectDependencies(this);

            var domain = Domains.None;
            if (spawnCrystalWithPlayerDomain && crystalId - 1 < gameData.Players.Count)
                domain = gameData.Players[crystalId - 1].Domain;

            crystal.ChangeDomain(domain);

            if (crystal.Id != 0)
            {
                CSDebug.LogError("To initialize a cell item, its default Id must be 0");
                return crystal;
            }

            int id = crystalId != 0 ? crystalId : (++itemsAdded);
            itemsAdded = Mathf.Max(itemsAdded, id);

            crystal.Initialize(id);

            lastSpawnPosById[crystalId] = spawnPos;
            lastAnchorIndexByCrystalId[crystalId] = batchAnchorIndex;
            cellData.OnCrystalSpawned.Raise();
            return crystal;
        }

        /// <summary>
        /// Spawn all missing crystals locally (single-player path).
        /// Uses the SAME anchor for the whole batch, then advances once.
        /// </summary>
        protected void SpawnBatchIfMissing()
        {
            int count = GetCrystalCountToSpawn();
            Vector3 batchAnchor = GetAnchorForBatchIndex(batchAnchorIndex);

            for (int id = 1; id <= count; id++)
            {
                if (!cellData.TryGetCrystalById(id, out _))
                {
                    Vector3 spawnPos = GetSpawnPointAroundAnchor(batchAnchor);
                    var crystal = SpawnLocal(id, spawnPos);
                    cellData.AddCrystalToList(crystal);
                    lastAnchorIndexByCrystalId[id] = batchAnchorIndex;
                }
            }

            batchAnchorIndex = GetNextAnchorIndex(batchAnchorIndex);
        }

        /// <summary>
        /// Update an existing crystal's position locally.
        /// </summary>
        protected void UpdateCrystalPos(int crystalId, Vector3 newPos)
        {
            if (!cellData.TryGetCrystalById(crystalId, out var crystal))
                return;

            crystal.DeactivateModels();
            crystal.MoveToNewPos(newPos);

            cellData.OnCellItemsUpdated.Raise();
        }

        // ================================================================
        // Position Calculation
        // ================================================================

        /// <summary>
        /// Calculate the new spawn position for a specific crystalId.
        /// Moves to the NEXT anchor, randomizes around it, and enforces minimum distance.
        /// </summary>
        protected Vector3 CalculateNewSpawnPos(int crystalId)
        {
            if (!TryGetCrystalPositionListByIntensity(out Vector3[] anchors) || anchors == null || anchors.Length == 0)
            {
                var crystalRadius = cellData.TryGetLocalCrystal(out Crystal crystal) ? crystal.SphereRadius : 10f;
                var centerPos = cellData.CellTransform != null ? cellData.CellTransform.position : transform.position;
                Vector3 fallback = Random.insideUnitSphere * crystalRadius + centerPos;
                lastSpawnPosById[crystalId] = fallback;
                return fallback;
            }

            Vector3 last = lastSpawnPosById.TryGetValue(crystalId, out var lastPos)
                ? lastPos
                : Vector3.positiveInfinity;

            int lastAnchorIndex = lastAnchorIndexByCrystalId.TryGetValue(crystalId, out var idx) ? idx : 0;
            int nextAnchorIndex = (lastAnchorIndex + 1) % anchors.Length;
            Vector3 anchor = anchors[nextAnchorIndex];

            const int MAX_TRIES = 50;
            Vector3 spawnPos = anchor;

            for (int t = 0; t < MAX_TRIES; t++)
            {
                spawnPos = GetSpawnPointAroundAnchor(anchor);
                if (Vector3.SqrMagnitude(last - spawnPos) > MIN_SQR_SPACE_BTWN_CURRENT_AND_LAST_SPAWN_POS)
                    break;
            }

            lastSpawnPosById[crystalId] = spawnPos;
            lastAnchorIndexByCrystalId[crystalId] = nextAnchorIndex;

            return spawnPos;
        }

        // ================================================================
        // Anchor Helpers
        // ================================================================

        protected bool TryGetCrystalPositionListByIntensity(out Vector3[] positions)
        {
            positions = null;

            if (listOfCrystalPositions == null || listOfCrystalPositions.Count == 0)
                return false;

            int intensity = Mathf.Clamp(intensityLevelData ? intensityLevelData.Value : 1, 1, listOfCrystalPositions.Count);
            var set = listOfCrystalPositions[intensity - 1];

            if (set == null || set.positions == null || set.positions.Count == 0)
            {
                positions = Array.Empty<Vector3>();
                return true;
            }

            positions = set.positions.ToArray();
            return true;
        }

        protected int GetCrystalCountToSpawn()
        {
            return Mathf.Max(1, gameData.Players.Count + extraCrystalsToSpawnBeyondPlayerCount);
        }

        private Vector3 GetAnchorForBatchIndex(int index)
        {
            if (!TryGetCrystalPositionListByIntensity(out Vector3[] anchors) || anchors == null || anchors.Length == 0)
                return Vector3.forward * 30f;

            int safeIndex = ((index % anchors.Length) + anchors.Length) % anchors.Length;
            return anchors[safeIndex];
        }

        private int GetNextAnchorIndex(int index)
        {
            if (!TryGetCrystalPositionListByIntensity(out Vector3[] anchors) || anchors == null || anchors.Length == 0)
                return index;

            return (index + 1) % anchors.Length;
        }

        protected Vector3 GetSpawnPointAroundAnchor(Vector3 anchor)
        {
            return anchor + Random.onUnitSphere * 35f;
        }

        // ================================================================
        // Reset / Cleanup
        // ================================================================

        protected void ResetSpawnState()
        {
            if (cellData.Crystals != null)
            {
                for (int i = cellData.Crystals.Count - 1; i >= 0; i--)
                {
                    var crystal = cellData.Crystals[i];
                    if (crystal && crystal.gameObject)
                        Destroy(crystal.gameObject);
                }
                cellData.Crystals.Clear();
            }

            cellData.CellItems?.Clear();

            lastSpawnPosById.Clear();
            lastAnchorIndexByCrystalId.Clear();
            batchAnchorIndex = 0;
            itemsAdded = 0;
        }

        /// <summary>
        /// Destroy all tracked crystals. Called on turn end.
        /// </summary>
        public void DestroyAllCrystals()
        {
            var crystals = cellData.Crystals;
            for (int i = crystals.Count - 1; i >= 0; i--)
            {
                var crystal = crystals[i];
                if (crystal)
                    crystal.DestroyCrystal();
            }
        }

        /// <summary>
        /// Manually trigger turn-end cleanup. Used by SinglePlayerFreestyleController
        /// when transitioning to shape drawing mode.
        /// </summary>
        public void ManualTurnEnded() => OnTurnEnded();
    }

    // ================================================================
    // Network serialisation helper for ExplodeParams
    // ================================================================

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
