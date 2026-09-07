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
    /// One intensity's crystal count, as a function of the roster:
    /// <c>max(1, round(players x CrystalsPerPlayer) + ExtraCrystals)</c>.
    ///
    /// <para>Two numbers rather than one because the useful answers are not all the same shape:
    /// "twice as many as players" is a multiplier, "exactly one, whatever the roster" is a flat
    /// count, and "one fewer than players" is both. Rampage authors all three down its ladder
    /// (2x / 1x / 1x-1 / 0x+1), so intensity there is how CONTESTED the Dolphin's only blast
    /// trigger is rather than how much arena there is.</para>
    /// </summary>
    [Serializable]
    public class IntensityCrystalCount
    {
        [Tooltip("Crystals per player in the roster. 0 = the count does not depend on the roster " +
                 "at all (use ExtraCrystals alone for a flat count).")]
        [Min(0f)] public float CrystalsPerPlayer = 1f;

        [Tooltip("Added to (or subtracted from) the per-player result. -1 gives 'one fewer than " +
                 "players'; the total is always floored at 1, so a solo player still gets a crystal.")]
        public int ExtraCrystals = 0;
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
            IntensityScaled = 2,
        }

        /// <summary>
        /// WHERE a crystal spawns and respawns, as opposed to <see cref="CrystalCountMode"/>'s
        /// HOW MANY.
        /// </summary>
        public enum CrystalPlacementMode
        {
            /// <summary>
            /// The platform default: the authored per-intensity anchor list when a scene has one,
            /// otherwise a random point in the cell's nucleus. Every mode but Drumfire.
            /// </summary>
            AnchorsOrNucleus = 0,

            /// <summary>
            /// One straight FIRING LANE per player, each a line of evenly spaced crystals that
            /// runs from that player's own spawn slot past the cell centre and out the far side.
            /// The lane geometry is derived from <see cref="CellSpawnFormation"/> - the same
            /// function that places the players - so a lane and the pilot who flies it can never
            /// drift apart, and the arrangement re-derives itself for any roster size instead of
            /// being authored per scene. A collected crystal reloads at its OWN slot, so the lane
            /// stays a lane for the whole match. Drumfire's rhythm track; see DRUMFIRE.md.
            /// </summary>
            ApproachLanes = 1,
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

        [Tooltip("FALLBACK ONLY - the anchorless spawn ball's radius for a cell that has NO " +
                 "NucleusPrefab (Dog Fight's Boneyard). A cell WITH a nucleus always spawns its " +
                 "crystals inside that nucleus and ignores this field: the nucleus IS the crystal " +
                 "volume, platform-wide. Leave it 0 unless the cell genuinely has no core.")]
        [SerializeField, Min(0f)] private float noNucleusSpawnRadius;

        [Header("Crystal Count")]
        [SerializeField] private CrystalCountMode crystalCountMode = CrystalCountMode.PlayerCountPlusExtra;
        [SerializeField, Min(0)] private int fixedCrystalCount = 1;
        [SerializeField] private int extraCrystalsToSpawnBeyondPlayerCount = 0;

        [Tooltip("IntensityScaled mode only: one entry per intensity, list order = intensity " +
                 "(index 0 is intensity 1), exactly like Cell.CellConfigs under " +
                 "CellTypeChoiceOptions.IntensityWise. An intensity past the end of the list " +
                 "reuses the last entry.")]
        [SerializeField] private List<IntensityCrystalCount> crystalCountByIntensity = new();

        [Header("Approach Lanes (CrystalPlacementMode.ApproachLanes)")]
        [Tooltip("WHERE crystals go. AnchorsOrNucleus is the platform default; ApproachLanes " +
                 "lays one straight firing lane per player through their own spawn slot.")]
        [SerializeField] private CrystalPlacementMode placementMode = CrystalPlacementMode.AnchorsOrNucleus;

        [Tooltip("Lane mode: the spawn-ring radius the lanes are struck from. MUST match the " +
                 "radius ServerPlayerVesselInitializer resolves (nucleus radius + spawn distance, " +
                 "floored by Spawn Ring Radius Floor) or the lanes will not pass through the " +
                 "players' spawn points.")]
        [SerializeField, Min(1f)] private float laneRingRadius = 1120f;

        [Tooltip("Lane mode: how close the lane passes to the cell CENTRE. This is what makes the " +
                 "lane a fly-PAST rather than a dive - it must clear whatever is in the middle of " +
                 "the arena, and the clearance it leaves is the margin a pilot has to lean in and " +
                 "graze that structure.")]
        [SerializeField, Min(0f)] private float laneOffsetFromCenter = 420f;

        [Tooltip("Lane mode: distance from a player's spawn point to the FIRST crystal of their lane. "
                 + "Together with Lane Length this CENTRES the crystals on the lane's closest "
                 + "approach, which is what keeps every shot in the run at a similar range - see "
                 + "DRUMFIRE.md, where a long-range shot measured seven times a close one.")]
        [SerializeField, Min(0f)] private float laneLeadDistance = 640f;

        [Tooltip("Lane mode: distance from the first crystal to the LAST. The spacing between " +
                 "beats is this divided by (slots - 1), so raising the crystal count tightens the " +
                 "rhythm without moving either end of the run.")]
        [SerializeField, Min(1f)] private float laneLength = 800f;

        [Tooltip("Lane mode: MUST match ServerPlayerVesselInitializer's spawnFormation, or lane k " +
                 "will not pass through spawn slot k.")]
        [SerializeField] private CellSpawnFormation.Formation laneFormation =
            CellSpawnFormation.Formation.Symmetric;

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

        /// <summary>
        /// Asks the machine that OWNS a collecting vessel to run that crystal's vessel-side
        /// effects locally. No-op in a non-networked session (single player, the freestyle
        /// conveyor's manager-less mints), where the collecting machine is the only machine;
        /// <see cref="NetworkCrystalManager"/> overrides it with a targeted ClientRpc.
        ///
        /// Collection is resolved server-only, which is right - one machine must decide who got
        /// the crystal and where it goes next. But the EFFECTS of a pickup are what the pilot
        /// sees and feels, and they were landing only on the server: a remote client's Dolphin
        /// collected the crystal, and the jaw blast, the spent energy meter and the elemental
        /// level all happened on a machine that pilot was not looking at.
        /// </summary>
        public virtual void ReplayVesselCrystalEffects(int crystalId, ulong vesselNetworkObjectId, ulong ownerClientId) { }

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
                    Vector3 spawnPos = InitialSpawnPointFor(id, batchAnchor, hasAnchors);
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
            // Lane mode: a collected crystal RELOADS AT ITS OWN SLOT. Deliberately bypasses the
            // move-away-from-last rule below - here "it came back where it was" is the point, and
            // that rule exists to stop an anchored crystal respawning on top of the pilot who just
            // took it. Camping one slot is self-defeating anyway: a blast fired twice from the
            // same place sweeps mass that is already gone.
            if (UsesApproachLanes)
            {
                Vector3 lanePos = LaneSlotPosition(crystalId);
                lastSpawnPosById[crystalId] = lanePos;
                return lanePos;
            }

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

            int intensity = Mathf.Clamp(CurrentIntensity, 1, listOfCrystalPositions.Count);
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

        /// <summary>
        /// The intensity this manager authors against: the serialized SOAP variable when a scene
        /// wires one, otherwise <see cref="GameDataSO.SelectedIntensity"/> - which is the SAME
        /// asset in every scene that wires it, so this is one source with a fallback rather than
        /// two. Floored at 1.
        ///
        /// <para>No <c>GameConfigSynced</c> gate is needed here even though a client's
        /// <c>SelectedIntensity</c> arrives late (see <see cref="GameDataSO.GameConfigSynced"/> for
        /// the sticky-cell race this resembles): both intensity readers are SERVER-side. The count
        /// is resolved only inside <see cref="NetworkCrystalManager"/>'s <c>IsServer</c> paths and
        /// reaches clients as the replicated slot-list LENGTH, and the anchor list is likewise read
        /// on the server. A client never derives either number for itself.</para>
        /// </summary>
        protected int CurrentIntensity
        {
            get
            {
                var source = intensityLevelData ? intensityLevelData
                                                : (gameData ? gameData.SelectedIntensity : null);
                return Mathf.Max(1, source ? source.Value : 1);
            }
        }

        protected int GetCrystalCountToSpawn()
        {
            int authored = ResolveAuthoredCrystalCount();

            // In lane mode the authored count is the number of crystals in ONE lane, and the
            // total is that times the number of lanes. Keeping the two apart is what lets the
            // existing per-intensity table author the RHYTHM (crystals per lane) while the
            // roster decides how many lanes there are.
            return placementMode == CrystalPlacementMode.ApproachLanes
                ? Mathf.Max(1, LaneCount * Mathf.Max(1, authored))
                : authored;
        }

        /// <summary>Crystals as authored - per lane in lane mode, total otherwise.</summary>
        private int ResolveAuthoredCrystalCount()
        {
            return crystalCountMode switch
            {
                CrystalCountMode.FixedCount => fixedCrystalCount,
                CrystalCountMode.IntensityScaled => IntensityScaledCrystalCount(),
                _ => Mathf.Max(1, gameData.Players.Count + extraCrystalsToSpawnBeyondPlayerCount),
            };
        }

        // ------------------------------------------------------------
        // Approach lanes
        // ------------------------------------------------------------

        /// <summary>True when this manager lays per-player firing lanes rather than anchors.</summary>
        protected bool UsesApproachLanes => placementMode == CrystalPlacementMode.ApproachLanes;

        /// <summary>
        /// How many lanes to strike: the TOTAL players in the match (humans + AI backfill), read
        /// from the same source <c>ServerPlayerVesselInitializer.EnsureSpawnPosesReady</c> reads
        /// so the lane set and the spawn ring are always the same arrangement. Falls back to the
        /// live roster if the launcher never published a count.
        /// </summary>
        private int LaneCount => gameData != null && gameData.SelectedPlayerCount != null
            ? Mathf.Max(1, gameData.SelectedPlayerCount.Value)
            : Mathf.Max(1, gameData != null && gameData.Players != null ? gameData.Players.Count : 1);

        /// <summary>Crystals in one lane (the authored count), floored at 1.</summary>
        private int SlotsPerLane => Mathf.Max(1, ResolveAuthoredCrystalCount());

        /// <summary>
        /// Where crystal <paramref name="crystalId"/> lives on its lane.
        ///
        /// <para>Lane-MAJOR (<c>lane = idx / slots</c>), not slot-major, and that is load-bearing:
        /// <c>NetworkCrystalManager</c> grows the slot list as players arrive and only fills the
        /// entries that are still empty, so a mapping where a new player changed which lane the
        /// EXISTING crystals belong to would strand every one of them on the wrong line until it
        /// was next collected. Appending whole lanes cannot disturb the lanes already laid.</para>
        ///
        /// <para>The lane is the straight line through spawn slot <c>lane</c> whose closest
        /// approach to the cell centre is <see cref="laneOffsetFromCenter"/> - so a pilot who
        /// flies their crystals flies PAST whatever is in the middle rather than into it, and
        /// every shot needs a deliberate turn off the flight vector.</para>
        /// </summary>
        protected Vector3 LaneSlotPosition(int crystalId)
        {
            int idx = Mathf.Max(0, crystalId - 1);
            int slots = SlotsPerLane;
            int lanes = LaneCount;

            Vector3 center = cellData != null && cellData.CellTransform != null
                ? cellData.CellTransform.position
                : transform.position;

            return ApproachLaneGeometry.SlotPosition(
                center,
                ApproachLaneGeometry.LaneOf(idx, slots, lanes), lanes,
                ApproachLaneGeometry.SlotOf(idx, slots), slots,
                laneRingRadius, laneOffsetFromCenter, laneLeadDistance, laneLength,
                laneFormation);
        }

        /// <summary>
        /// The position a crystal is FIRST laid at. One seam for both placement modes, so the
        /// initial batch and every respawn cannot disagree about where a crystal belongs.
        /// </summary>
        protected Vector3 InitialSpawnPointFor(int crystalId, Vector3 batchAnchor, bool hasAnchors)
        {
            if (UsesApproachLanes) return LaneSlotPosition(crystalId);
            return hasAnchors ? GetSpawnPointAroundAnchor(batchAnchor) : GetAnchorlessSpawnPoint();
        }

        /// <summary>
        /// The crystal count for the current intensity and roster. See
        /// <see cref="IntensityCrystalCount"/> for the formula.
        ///
        /// <para>The roster is <c>gameData.Players.Count</c> - the same source
        /// <see cref="CrystalCountMode.PlayerCountPlusExtra"/> uses, and the reason the count can
        /// safely be taken before everyone has arrived: <c>NetworkCrystalManager</c> re-asks on
        /// every <c>OnPlayerAdded</c> and again at turn start, growing the slot list as the roster
        /// fills. AI backfill counts, because an AI is a player holding a Dolphin.</para>
        /// </summary>
        private int IntensityScaledCrystalCount()
        {
            int players = Mathf.Max(1, gameData.Players.Count);

            if (crystalCountByIntensity is not { Count: > 0 })
            {
                CSDebug.LogError($"[CrystalManager] '{name}' is on IntensityScaled crystal count but " +
                                 "authors no crystalCountByIntensity entries; falling back to one " +
                                 "crystal per player.");
                return players;
            }

            return ResolveIntensityCrystalCount(crystalCountByIntensity, CurrentIntensity, players);
        }

        /// <summary>
        /// The pure <see cref="CrystalCountMode.IntensityScaled"/> formula, static so the
        /// edit-mode tests can pin the ladder without a NetworkBehaviour or a live roster.
        /// An intensity past the end of the table reuses the last entry; the result never
        /// drops below 1.
        /// </summary>
        public static int ResolveIntensityCrystalCount(
            IReadOnlyList<IntensityCrystalCount> table, int intensity, int players)
        {
            if (table is not { Count: > 0 }) return Mathf.Max(1, players);

            var entry = table[Mathf.Clamp(intensity, 1, table.Count) - 1];
            if (entry == null) return Mathf.Max(1, players);

            // Round half UP explicitly - Mathf.RoundToInt is banker's rounding, which would send
            // 3 players x 0.5 and 5 players x 0.5 in opposite directions for no stated reason.
            int scaled = Mathf.FloorToInt(Mathf.Max(1, players) * Mathf.Max(0f, entry.CrystalsPerPlayer) + 0.5f);
            return Mathf.Max(1, scaled + entry.ExtraCrystals);
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
        /// a random point in a ball of <see cref="GetAnchorlessSpawnRadius"/> around the cell centre.
        /// This is the SINGLE definition of that volume - the initial batch and every respawn
        /// both draw from it, so the placement radius does not change over a match.
        /// </summary>
        protected Vector3 GetAnchorlessSpawnPoint()
        {
            var centerPos = cellData.CellTransform != null ? cellData.CellTransform.position : transform.position;
            return centerPos + Random.insideUnitSphere * GetAnchorlessSpawnRadius();
        }

        /// <summary>
        /// The reference size for anchorless crystal placement: <b>the CELL NUCLEUS radius, and
        /// nothing may override it.</b>
        ///
        /// <para><b>Platform coupling (LOCKED):</b> a standard respawning crystal always respawns
        /// INSIDE the nucleus, in every mode. The nucleus is the visible marker of the cell's core
        /// and the thing players read as "the middle"; a crystal that respawns anywhere else makes
        /// that marker a lie, and every mode that contests a crystal then has to teach its own
        /// answer to "where do I look". The radius is per-intensity for free (an IntensityWise cell
        /// picks a different config, hence a different nucleus, per level), and it is identical for
        /// the initial batch and every respawn, so the placement volume never changes over a match.
        /// Do NOT reintroduce a per-scene override — a mode that wants crystals somewhere else
        /// should resize its nucleus (author a `CellConfigDataSO` with a resized `NucleusPrefab`,
        /// per CLAUDE.md), which moves BOTH together and keeps them coupled.</para>
        ///
        /// <see cref="noNucleusSpawnRadius"/> is the fallback for a cell that genuinely has no
        /// nucleus at all (Dog Fight's Boneyard); the crystal's own SphereRadius is the last resort.
        /// </summary>
        protected float GetAnchorlessSpawnRadius()
        {
            // Resolved through the registry + ExpectedNucleusWorldRadius so placement never depends
            // on whether Cell.Initialize beat the first crystal spawn (see Cell.ExpectedNucleusWorldRadius).
            var cell = Cell.FindByRuntimeData(cellData);
            if (cell != null)
            {
                float nucleusRadius = cell.ExpectedNucleusWorldRadius;
                if (nucleusRadius > 0f) return nucleusRadius;
            }

            if (noNucleusSpawnRadius > 0f) return noNucleusSpawnRadius;

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
