using System;
using System.Collections.Generic;
using CosmicShore.Soap;
using CosmicShore.Utility.ClassExtensions;
using Obvious.Soap;
using Unity.Netcode;
using UnityEngine;
using Random = UnityEngine.Random;

namespace CosmicShore.Game
{
    [Serializable]
    public class CrystalPositionSet
    {
        public List<Vector3> positions;
    }

    /// <summary>
    /// Base crystal manager:
    /// - Handles spawn + respawn logic for multiple crystals.
    /// - Provides anchor-based spawn positions (pre-authored anchor lists).
    /// - Batch spawn uses ONE anchor per batch, so all crystals in the batch cluster together.
    /// - Respawn uses per-crystal "next anchor" progression, so each crystal moves along anchors independently.
    /// </summary>
    public abstract class CrystalManager : NetworkBehaviour
    {
        // IMPORTANT:
        // We compare Vector3.SqrMagnitude(...) <= MIN_SQR_DISTANCE.
        // So this constant is "distance squared".
        // If you want minimum distance of 25 units, set this to 25f * 25f = 625.
        private const float MIN_SQR_SPACE_BTWN_CURRENT_AND_LAST_SPAWN_POS = 25f;

        [Header("Dependencies")]
        [SerializeField] protected GameDataSO gameData;
        [SerializeField] protected CellRuntimeDataSO cellData;

        [Header("Crystal Prefab")]
        [SerializeField] protected Crystal crystalPrefab;

        [Header("Anchor Positions (By Intensity)")]
        [SerializeField] private bool scaleCrystalPositionWithIntensity;
        [SerializeField] private IntVariable intensityLevelData;
        [SerializeField] private List<CrystalPositionSet> listOfCrystalPositions;

        [SerializeField] private bool spawnCrystalWithPlayerDomain;
        [SerializeField] private int extraCrystalsToSpawnBeyondPlayerCount = 0;
        
        // ---------------- Runtime State ----------------

        // Tracks the last spawn position per crystal id (used to keep respawns away from their last position).
        private readonly Dictionary<int, Vector3> lastSpawnPosById = new();

        // Tracks the last anchor index used per crystal id.
        // Respawn will increment this to use the NEXT anchor index.
        private readonly Dictionary<int, int> lastAnchorIndexByCrystalId = new();

        // Used ONLY for batch spawns (one anchor per batch).
        // Respawn does NOT use this global index (it uses per-crystal anchor index).
        private int batchAnchorIndex;

        // Used for stable initialization IDs for CellItems
        private int itemsAdded;
        
        protected virtual void Awake()
        {
            // Ensure runtime lists exist
            cellData.CellItems = new List<CellItem>();
            cellData.Crystals ??= new List<Crystal>();
        }

        // ------------------------------------------------------------
        // CellItem management (unchanged conceptually)
        // ------------------------------------------------------------

        

        // ------------------------------------------------------------
        // Spawn / Respawn core
        // ------------------------------------------------------------

        /// <summary>
        /// Spawn a crystal with a stable crystalId at spawnPos.
        /// If it already exists, returns existing.
        /// </summary>
        protected virtual Crystal Spawn(int crystalId, Vector3 spawnPos)
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
            if (spawnCrystalWithPlayerDomain)
                domain = gameData.Players[crystalId - 1].Domain; 
                
            // Keep a list of crystals in the cell data (you already changed this).
            crystal.ChangeDomain(domain);
            
            if (crystal.Id != 0)
            {
                Debug.LogError("To initialize a cell item, its default Id must be 0");
                return crystal;
            }

            int id = crystalId != 0 ? crystalId : (++itemsAdded);
            itemsAdded = Mathf.Max(itemsAdded, id);

            crystal.Initialize(id);

            // Cache last spawn info
            lastSpawnPosById[crystalId] = spawnPos;

            // We also store which anchor index this spawn belongs to (optional, but helpful).
            // For initial spawn we can say the crystal spawned on the current batch anchor index.
            lastAnchorIndexByCrystalId[crystalId] = batchAnchorIndex;
            cellData.OnCrystalSpawned.Raise();
            return crystal;
        }

        /// <summary>
        /// Spawn all missing crystals from 1..SelectedPlayerCount.
        /// IMPORTANT:
        /// - Uses the SAME anchor for the whole batch.
        /// - Advances the batch anchor ONCE after completing the batch.
        /// </summary>
        protected void SpawnBatchIfMissing()
        {
            int count = GetCrystalCountToSpawn();

            // 1) Choose ONE anchor for the whole batch
            Vector3 batchAnchor = GetAnchorForBatchIndex(batchAnchorIndex);

            // 2) Spawn each missing crystal around that same anchor
            for (int id = 1; id <= count; id++)
            {
                if (!cellData.TryGetCrystalById(id, out _))
                {
                    Vector3 spawnPos = GetSpawnPointAroundAnchor(batchAnchor);
                    var crystal = Spawn(id, spawnPos);
                    cellData.AddCrystalToList(crystal);

                    // Remember last anchor index used for this crystal
                    lastAnchorIndexByCrystalId[id] = batchAnchorIndex;
                }
            }

            // 3) Advance ONCE after the batch
            batchAnchorIndex = GetNextAnchorIndex(batchAnchorIndex);
        }

        /// <summary>
        /// Calculate the new spawn position for a specific crystalId:
        /// - Reads the last anchor index used by that crystal.
        /// - Moves to NEXT anchor index.
        /// - Randomizes around that anchor.
        /// - Enforces minimum distance from that crystal's last spawn position.
        /// </summary>
        protected Vector3 CalculateNewSpawnPos(int crystalId)
        {
            // If no anchor list exists, fallback to random in sphere.
            if (!TryGetCrystalPositionListByIntensity(out Vector3[] anchors) || anchors == null || anchors.Length == 0)
            {
                var crystalRadius = cellData.TryGetLocalCrystal(out Crystal crystal) ? crystal.SphereRadius : 10f;
                Vector3 fallback = Random.insideUnitSphere * crystalRadius + cellData.CellTransform.position;
                lastSpawnPosById[crystalId] = fallback;
                return fallback;
            }

            // Last position this crystal spawned at (for distance check)
            Vector3 last = lastSpawnPosById.TryGetValue(crystalId, out var lastPos)
                ? lastPos
                : Vector3.positiveInfinity;

            // Get last anchor index used by this crystal (default 0)
            int lastAnchorIndex = lastAnchorIndexByCrystalId.TryGetValue(crystalId, out var idx) ? idx : 0;

            // Always move to NEXT anchor
            int nextAnchorIndex = (lastAnchorIndex + 1) % anchors.Length;
            Vector3 anchor = anchors[nextAnchorIndex];

            // Try multiple random points around the same anchor
            const int MAX_TRIES = 50;
            Vector3 spawnPos = anchor;

            for (int t = 0; t < MAX_TRIES; t++)
            {
                spawnPos = GetSpawnPointAroundAnchor(anchor);

                // Accept if sufficiently far from last spawn
                if (Vector3.SqrMagnitude(last - spawnPos) > MIN_SQR_SPACE_BTWN_CURRENT_AND_LAST_SPAWN_POS)
                    break;
            }

            // Store new "lasts"
            lastSpawnPosById[crystalId] = spawnPos;
            lastAnchorIndexByCrystalId[crystalId] = nextAnchorIndex;

            return spawnPos;
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

        // ------------------------------------------------------------
        // Anchor helpers
        // ------------------------------------------------------------

        /// <summary>
        /// Return the anchor list for current intensity.
        /// </summary>
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

        /// <summary>
        /// Get anchor for a batch anchor index.
        /// Falls back to forward if no anchors exist.
        /// </summary>
        private Vector3 GetAnchorForBatchIndex(int index)
        {
            if (!TryGetCrystalPositionListByIntensity(out Vector3[] anchors) || anchors == null || anchors.Length == 0)
                return Vector3.forward * 30f;

            int safeIndex = ((index % anchors.Length) + anchors.Length) % anchors.Length;
            return anchors[safeIndex];
        }

        /// <summary>
        /// Advance to next anchor index (wrap).
        /// </summary>
        private int GetNextAnchorIndex(int index)
        {
            if (!TryGetCrystalPositionListByIntensity(out Vector3[] anchors) || anchors == null || anchors.Length == 0)
                return index;

            return (index + 1) % anchors.Length;
        }

        /// <summary>
        /// Given an anchor point, return a randomized spawn point around it.
        /// </summary>
        protected Vector3 GetSpawnPointAroundAnchor(Vector3 anchor)
        {
            // If you ever want different radius, expose this as a serialized field.
            return anchor + Random.onUnitSphere * 35f;
        }

        // ------------------------------------------------------------
        // Abstract API used by gameplay code
        // ------------------------------------------------------------

        public abstract void RespawnCrystal(int crystalId);
        public abstract void ExplodeCrystal(int crystalId, Crystal.ExplodeParams explodeParams);
    }
}
