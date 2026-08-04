using System;
using System.Collections.Generic;
using CosmicShore.Utility;
using CosmicShore.Utility.PerformanceBenchmark;
using Obvious.Soap;
using Reflex.Attributes;
using Unity.Netcode;
using UnityEngine;
using Random = UnityEngine.Random;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using System.Linq;
namespace CosmicShore.Gameplay
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
        public enum CrystalCountMode
        {
            FixedCount = 0,
            PlayerCountPlusExtra = 1,
        }

        // IMPORTANT:
        // We compare Vector3.SqrMagnitude(...) <= MIN_SQR_DISTANCE.
        // So this constant is "distance squared".
        // If you want minimum distance of 25 units, set this to 25f * 25f = 625.
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

        [Header("Spawn Volume")]
        [Tooltip("Radius of the random shell around an authored anchor that a crystal spawns on. " +
                 "Only used when listOfCrystalPositions has anchors for the current intensity.")]
        [SerializeField, Min(0f)] private float anchorJitterRadius = 35f;

        [Tooltip("Overrides the radius of the ball around the cell centre that crystals spawn in " +
                 "when NO anchors are authored (Scurry / Crystal Capture). Used by BOTH the initial " +
                 "batch and every respawn, so the placement volume never changes over a match. " +
                 "0 (default) = the cell's NUCLEUS radius, which is per-intensity - leave it at 0 " +
                 "unless a mode genuinely needs to decouple crystals from its cell core.")]
        [SerializeField, Min(0f)] private float anchorlessSpawnRadius;

        [Header("Crystal Count")]
        [SerializeField] private CrystalCountMode crystalCountMode = CrystalCountMode.PlayerCountPlusExtra;
        [SerializeField, Min(0)] private int fixedCrystalCount = 1;
        [SerializeField] private int extraCrystalsToSpawnBeyondPlayerCount = 0;

        [Header("Crystal Domain")]
        [SerializeField] protected bool spawnCrystalWithPlayerDomain;
        
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

        // Cached copy of the current intensity's anchor list. The authored lists
        // are runtime-static, so the array only rebuilds when intensity selects a
        // different source list — the callers (anchor pick + advance, both hit on
        // every crystal respawn) previously allocated a fresh ToArray() each call.
        private Vector3[] _cachedAnchors;
        private List<Vector3> _cachedAnchorSource;
        
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
        /// Domain is resolved from gameData.Players by index.
        /// If it already exists, returns existing.
        /// </summary>
        protected virtual Crystal Spawn(int crystalId, Vector3 spawnPos)
        {
            var domain = Domains.Blue;
            if (spawnCrystalWithPlayerDomain && crystalId - 1 < gameData.Players.Count)
                domain = gameData.Players[crystalId - 1].Domain;

            return SpawnWithDomain(crystalId, spawnPos, domain);
        }

        /// <summary>
        /// Spawn a crystal with an explicit domain, bypassing the player list index lookup.
        /// Used by NetworkCrystalManager to apply server-authoritative domains.
        /// </summary>
        protected Crystal SpawnWithDomain(int crystalId, Vector3 spawnPos, Domains domain)
        {
            if (cellData.TryGetCrystalById(crystalId, out Crystal existing))
            {
                DebugExtensions.LogErrorColored(
                    $"Crystal with id {crystalId} already exists, skipping spawn.",
                    Color.magenta
                );
                return existing;
            }

            // IsRecording-guarded label: crystals respawn on every collection during gameplay,
            // so the disarmed path must not pay the interpolated-string allocation.
            using var _ = LoadInsights.IsRecording
                ? LoadInsights.Measure(LoadInsightCategory.Crystals, $"Crystal spawn ({crystalPrefab.name})")
                : LoadSpanScope.None;
            LoadInsights.Count("Crystals spawned during load");

            var crystal = Instantiate(crystalPrefab, spawnPos, Quaternion.identity, transform);
            crystal.InjectDependencies(this);
            crystal.ChangeDomain(domain);

            if (crystal.Id != 0)
            {
                CSDebug.LogError("To initialize a cell item, its default Id must be 0");
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
            bool hasAnchors = HasAuthoredAnchors();
            Vector3 batchAnchor = GetAnchorForBatchIndex(batchAnchorIndex);

            // 2) Spawn each missing crystal around that same anchor
            for (int id = 1; id <= count; id++)
            {
                if (!cellData.TryGetCrystalById(id, out _))
                {
                    // With no authored anchors the batch anchor is a placeholder, so the
                    // initial batch draws from the SAME volume every respawn draws from.
                    Vector3 spawnPos = hasAnchors
                        ? GetSpawnPointAroundAnchor(batchAnchor)
                        : GetAnchorlessSpawnPoint();
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
            // Last position this crystal spawned at (for distance check)
            Vector3 last = lastSpawnPosById.TryGetValue(crystalId, out var lastPos)
                ? lastPos
                : Vector3.positiveInfinity;

            // If no anchor list exists, draw from the anchorless volume - the same volume
            // SpawnBatchIfMissing seeds the initial batch from.
            if (!TryGetCrystalPositionListByIntensity(out Vector3[] anchors) || anchors == null || anchors.Length == 0)
            {
                Vector3 fallback = PickSpawnPointAwayFromLast(last, GetAnchorlessSpawnPoint);
                lastSpawnPosById[crystalId] = fallback;
                return fallback;
            }

            // Get last anchor index used by this crystal (default 0)
            int lastAnchorIndex = lastAnchorIndexByCrystalId.TryGetValue(crystalId, out var idx) ? idx : 0;

            // Always move to NEXT anchor
            int nextAnchorIndex = (lastAnchorIndex + 1) % anchors.Length;
            Vector3 anchor = anchors[nextAnchorIndex];

            // Try multiple random points around the same anchor
            Vector3 spawnPos = PickSpawnPointAwayFromLast(last, () => GetSpawnPointAroundAnchor(anchor));

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

            if (!ReferenceEquals(_cachedAnchorSource, set.positions)
                || _cachedAnchors == null
                || _cachedAnchors.Length != set.positions.Count)
            {
                _cachedAnchorSource = set.positions;
                _cachedAnchors = set.positions.ToArray();
            }

            positions = _cachedAnchors;
            return true;
        }

        protected int GetCrystalCountToSpawn()
        {
            return crystalCountMode switch
            {
                CrystalCountMode.FixedCount => fixedCrystalCount,
                _ => Mathf.Max(1, gameData.Players.Count + extraCrystalsToSpawnBeyondPlayerCount),
            };
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
            return anchor + Random.onUnitSphere * anchorJitterRadius;
        }

        /// <summary>True when the current intensity has an authored anchor list to spawn against.</summary>
        protected bool HasAuthoredAnchors() =>
            TryGetCrystalPositionListByIntensity(out var anchors) && anchors != null && anchors.Length > 0;

        /// <summary>
        /// The spawn point used when NO anchors are authored (Scurry / Crystal Capture):
        /// a random point in a ball of <see cref="anchorlessSpawnRadius"/> around the cell centre.
        /// This is the SINGLE definition of that volume - the initial batch and every respawn
        /// both draw from it, so the placement radius does not change over a match.
        /// </summary>
        protected Vector3 GetAnchorlessSpawnPoint()
        {
            var centerPos = cellData.CellTransform != null ? cellData.CellTransform.position : transform.position;
            return centerPos + Random.insideUnitSphere * GetAnchorlessSpawnRadius();
        }

        /// <summary>
        /// The reference size for anchorless crystal placement: the CELL NUCLEUS radius, which is
        /// per-intensity (an IntensityWise cell picks a different config, hence a different nucleus,
        /// per level). Crystals therefore live inside the cell core at whatever scale that
        /// intensity's core is, and the reference is identical for the initial batch and every
        /// respawn. The serialized override wins when non-zero; the crystal's own SphereRadius is
        /// the last-resort fallback for a cell with no nucleus at all.
        /// </summary>
        protected float GetAnchorlessSpawnRadius()
        {
            if (anchorlessSpawnRadius > 0f) return anchorlessSpawnRadius;

            // Resolved through the registry + ExpectedNucleusWorldRadius so placement never depends
            // on whether Cell.Initialize beat the first crystal spawn (see Cell.ExpectedNucleusWorldRadius).
            var cell = Cell.FindByRuntimeData(cellData);
            if (cell != null)
            {
                float nucleusRadius = cell.ExpectedNucleusWorldRadius;
                if (nucleusRadius > 0f) return nucleusRadius;
            }

            if (crystalPrefab != null) return crystalPrefab.SphereRadius;
            return cellData.TryGetLocalCrystal(out Crystal crystal) ? crystal.SphereRadius : 10f;
        }

        /// <summary>
        /// Draws candidate spawn points until one is far enough from this crystal's previous
        /// position (or the try budget runs out). Shared by the anchored and anchorless paths so
        /// both honour MIN_SQR_SPACE_BTWN_CURRENT_AND_LAST_SPAWN_POS.
        /// </summary>
        static Vector3 PickSpawnPointAwayFromLast(Vector3 last, Func<Vector3> draw)
        {
            const int MAX_TRIES = 50;
            Vector3 spawnPos = draw();

            for (int t = 1; t < MAX_TRIES; t++)
            {
                if (Vector3.SqrMagnitude(last - spawnPos) > MIN_SQR_SPACE_BTWN_CURRENT_AND_LAST_SPAWN_POS)
                    break;
                spawnPos = draw();
            }

            return spawnPos;
        }

        // ------------------------------------------------------------
        // Reset
        // ------------------------------------------------------------

        /// <summary>
        /// Resets all runtime spawn-tracking state so crystals start from anchor 0 on replay.
        /// Destroys existing crystal GameObjects and clears cellData lists.
        /// Call this from subclass replay handlers before the next turn spawns new crystals.
        /// </summary>
        protected void ResetSpawnState()
        {
            // Destroy existing crystal GameObjects
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

            // Clear anchor/position tracking so spawning starts fresh from index 0
            lastSpawnPosById.Clear();
            lastAnchorIndexByCrystalId.Clear();
            batchAnchorIndex = 0;
            itemsAdded = 0;
        }

        // ------------------------------------------------------------
        // Abstract API used by gameplay code
        // ------------------------------------------------------------

        public abstract void RespawnCrystal(int crystalId);
        public abstract void ExplodeCrystal(int crystalId, Crystal.ExplodeParams explodeParams);
    }
}
