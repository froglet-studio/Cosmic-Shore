// Cell.cs
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using CosmicShore.Data;
using CosmicShore.Game;
using CosmicShore.Gameplay;
using CosmicShore.Utility;
using CosmicShore.Utility.PerformanceBenchmark;
using Reflex.Attributes;
using Unity.Collections;
using Unity.Netcode;
using Unity.Jobs;
using Unity.Profiling;
using UnityEngine;
using Random = UnityEngine.Random;
namespace CosmicShore.Gameplay
{
    public class Cell : MonoBehaviour
    {
        enum CellTypeChoiceOptions
        {
            Random = 0,
            IntensityWise = 1,

            /// <summary>
            /// Boot on the first config that carries NO authored EnvironmentPrefab - the
            /// fastest possible entry, because the multi-second prepopulated build never
            /// runs. The environment-bearing configs stay in the list and become OPT-IN:
            /// the player picks one through the freestyle Cell Selector toy
            /// (<see cref="RequestCellSwap"/>), which is the only place the load cost is
            /// paid. Falls back to index 0 when every config carries an environment.
            /// See Docs/ECOSYSTEM.md §19.
            /// </summary>
            EnvironmentFree = 2,
        }

        [SerializeField] public int ID;

        [Header("Cell Config Selection")]
        [SerializeField] List<CellConfigDataSO> CellConfigs;
        [SerializeField] CellTypeChoiceOptions cellTypeChoiceOptions = CellTypeChoiceOptions.Random;

        [Header("Runtime Cell Swap (freestyle Cell Selector toy)")]
        [SerializeField, Min(0f), Tooltip("Seconds the retiring world suctions toward the cell centre " +
                                          "before it is released. Continuity of existence: the old world " +
                                          "shrinks away, it never pops out. 0 = use the built-in default " +
                                          "(a Cell serialized before this field existed reads 0, and an " +
                                          "instant teardown would be a continuity violation).")]
        float retireSuctionSeconds;

        const float DefaultRetireSuctionSeconds = 1.1f;

        [Header("Runtime Data")]
        [SerializeField] CellRuntimeDataSO runtime;
        [Inject] GameDataSO gameData;

        [SerializeField] float nucleusScaleMultiplier = 1f;

        // Local phase recompute interval. Constant rather than a serialized field so
        // existing scene-placed Cells deserialized before this tick existed don't end
        // up with phaseTickIntervalSeconds=0 (the default(float) for new serialized
        // fields), which would silently disable phase advancement.
        const float PhaseTickIntervalSeconds = 0.5f;

        float _nextPhaseTickAt;


        CellConfigDataSO cellConfigData => runtime ? runtime.Config : null;
        public CellConfigDataSO Config => cellConfigData;
        GameObject membrane;
        GameObject nucleus;
        GameObject environment;   // config-authored structural environment (lives/dies with the cell)

        // Optional target WORLD radius for the nucleus, requested by a mode (e.g. Astro League uses
        // the nucleus as its spherical play boundary). 0 = use the prefab/multiplier size as-is.
        // Cached so it survives the nucleus-spawn-vs-request ordering race (applied in SpawnVisuals).
        float _pendingNucleusWorldRadius;

        // Optional replacement MESH for the nucleus, requested by a mode that repurposes the nucleus as
        // a NON-spherical play boundary (Astro League's ricochet courts - box/octagon/etc.). The mesh
        // is already in world units (centered on origin), so the nucleus renders it at unit scale with
        // its existing material. Cached so it survives the same nucleus-spawn-vs-request ordering race.
        Mesh _pendingNucleusMesh;

        public float NucleusRadius => nucleus ? nucleus.transform.localScale.x : 0f;

        /// <summary>
        /// The nucleus control zone's WORLD radius - renderer-bounds derived, so it is correct
        /// regardless of the prefab mesh's base size, and it is the SAME number
        /// <see cref="IsInsideNucleus"/> tests against (unlike <see cref="NucleusRadius"/>, which
        /// is a raw localScale read). 0 when the cell has no nucleus.
        ///
        /// This is the canonical "size of this intensity's cell core": crystal placement and the
        /// cell-relative player spawn ring both measure off it, so they stay consistent with the
        /// nucleus the config actually spawned.
        /// </summary>
        public float NucleusWorldRadius =>
            _nucleusControlRadiusSqr > 0f ? Mathf.Sqrt(_nucleusControlRadiusSqr) : 0f;

        /// <summary>
        /// The nucleus marker's GEOMETRIC world radius — renderer-bounds derived like
        /// <see cref="NucleusWorldRadius"/>, but INDEPENDENT of whether the nucleus is a control
        /// zone. 0 only when the cell genuinely has no nucleus.
        ///
        /// The two differ exactly where <see cref="NucleusIsControlZone"/> is false: a mode that
        /// borrowed the nucleus as PLAY GEOMETRY (Astro League's court) collapses the control
        /// radius to 0 on purpose, so <see cref="NucleusWorldRadius"/> reports 0 while the marker
        /// is still very much there and still very much the size of the arena. Anything asking
        /// "how big is the core, in metres" — a soft boundary, a placement ring, a camera frame —
        /// wants THIS; anything asking "who owns this cell" wants the other one. Reading the
        /// control radius for a geometric question is the §25 mistake in reverse: instead of
        /// inheriting semantics with the geometry, you lose the geometry with the semantics.
        /// </summary>
        public float NucleusVisualWorldRadius { get; private set; }

        /// <summary>
        /// The world radius the nucleus HAS, or WILL have once <see cref="SpawnVisuals"/> runs —
        /// measured off the config's <c>NucleusPrefab</c> asset without instantiating anything.
        ///
        /// This exists because the vessel-spawn chain has to place things relative to the core
        /// LONG before the cell initializes: <c>Cell.Initialize</c> runs on <c>OnInitializeGame</c>,
        /// gated by <c>MultiplayerMiniGameControllerBase.InitDelayMs</c> (1000 ms), while vessels
        /// spawn at <c>preSpawnDelayMs</c> (200 ms) and AI spawn at <c>OnNetworkSpawn</c> (t≈0).
        /// Reading <see cref="NucleusWorldRadius"/> that early silently returns 0 — which is how
        /// the spawn ring first shipped placing players 40u from the cell CENTRE, deep inside the
        /// nucleus. Prefer this property for any placement decision made during the spawn chain.
        ///
        /// 0 when the cell has no nucleus configured, or when the config is not knowable yet
        /// (a multi-config cell that has not rolled) — callers must handle 0 rather than adding
        /// an offset to it.
        /// </summary>
        public float ExpectedNucleusWorldRadius
        {
            get
            {
                if (_nucleusControlRadiusSqr > 0f) return Mathf.Sqrt(_nucleusControlRadiusSqr);

                // Before AssignConfig, only a single-config cell has a knowable answer.
                var cfg = cellConfigData;
                if (cfg == null && CellConfigs != null && CellConfigs.Count == 1) cfg = CellConfigs[0];
                if (cfg == null || cfg.NucleusPrefab == null) return 0f;

                return MeasurePrefabRadius(cfg.NucleusPrefab) * nucleusScaleMultiplier;
            }
        }

        /// <summary>
        /// Max half-extent of a prefab ASSET's meshes about its root, at the authored scale — the
        /// asset-time counterpart of <see cref="RefreshNucleusControlRadius"/>'s
        /// <c>Renderer.bounds</c> read, and equal to it for a centred mesh.
        /// </summary>
        static float MeasurePrefabRadius(GameObject prefab)
        {
            if (prefab == null) return 0f;

            float best = 0f;
            var root = prefab.transform;
            foreach (var mf in prefab.GetComponentsInChildren<MeshFilter>(true))
            {
                var mesh = mf.sharedMesh;
                if (mesh == null) continue;

                var b = mesh.bounds;
                Vector3 ext = Vector3.Scale(b.extents, mf.transform.lossyScale);
                Vector3 centre = root.InverseTransformPoint(mf.transform.TransformPoint(b.center));

                best = Mathf.Max(best, Mathf.Abs(centre.x) + Mathf.Abs(ext.x));
                best = Mathf.Max(best, Mathf.Abs(centre.y) + Mathf.Abs(ext.y));
                best = Mathf.Max(best, Mathf.Abs(centre.z) + Mathf.Abs(ext.z));
            }
            return best;
        }

        /// <summary>
        /// The live cell bound to <paramref name="runtimeData"/>. <c>CellRuntimeDataSO.Cell</c> is
        /// assigned in <see cref="Initialize"/>, so it is null for the first second of a scene;
        /// the static registry is populated in <c>OnEnable</c> and is therefore usable immediately.
        /// </summary>
        public static Cell FindByRuntimeData(CellRuntimeDataSO runtimeData)
        {
            if (runtimeData == null) return null;
            if (runtimeData.Cell) return runtimeData.Cell;

            foreach (var c in ActiveCells)
                if (c && c.runtime == runtimeData) return c;

            return null;
        }
        public float MembraneRadius
        {
            get
            {
                if (!membrane) return 0f;
                if (membrane.TryGetComponent<CapsuleMembrane>(out var cm))
                    return cm.Radius;
                return membrane.transform.localScale.x;
            }
        }

        /// <summary>
        /// Radius used for mass SENSING - prism registration (<see cref="ContainsPosition"/>)
        /// and the density grids that fauna seek mass with. Defaults to the visual
        /// <see cref="MembraneRadius"/>, but a CellConfig can override it
        /// (<c>SenseRadiusOverride</c>) to sense across a larger arena than the membrane
        /// visual - e.g. the Skim Race track - so fauna find + seek mass track-wide instead
        /// of only inside the central bubble. Independent of the membrane so the visual /
        /// its baked animation are untouched. See Docs/ECOSYSTEM.md §7.2.
        /// </summary>
        public float SenseRadius
        {
            get
            {
                float over = cellConfigData != null ? cellConfigData.SenseRadiusOverride : 0f;
                return over > 0f ? over : MembraneRadius;
            }
        }

        public Dictionary<Domains, BlockCountDensityGrid> countGrids = new();
        public Dictionary<Domains, BlockVolumeDensityGrid> volumeGrids = new();
        readonly Dictionary<Domains, float> teamVolumes = new();
        readonly Dictionary<Domains, int> domainBlockCounts = new();

        readonly List<GameObject> spawnedLifeForms = new();
        // Prism → the domain it was REGISTERED under. RemoveBlock decrements the
        // grids/counts for the registration-time domain, not the prism's current
        // one - so steals / ChangeTeam between Add and Remove can't desync the
        // per-domain bookkeeping (the §2.3.1 phantom-count class of bug).
        readonly Dictionary<Prism, Domains> trackedBlocks = new();

        // Per-domain VOLUME accounting ("volume is the spine": trail, flora, AND
        // fauna bodies all add to the cell's mass regardless of source - fauna
        // bodies are volume-only and stay out of the targeting grids/counts above:
        // a forager swarm must not read as its own mass concentration, and fauna
        // bodies are not edible prey). Membership lives in the spatial index's
        // packed summation view (PrismCellData.CellId, written ONLY by
        // AddBlock/RemoveBlock below); sums are recomputed from live prism state
        // (cached volume + live Domain) on a short cadence via one Burst pass
        // (PrismSpatialIndex.SumCellVolumes), so growth, steals, and consumption
        // are all reflected without incremental-drift bookkeeping.
        readonly Dictionary<Domains, float> liveVolumeByDomain = new();
        readonly Dictionary<Domains, float> liveEnvVolumeByDomain = new();
        float liveVolumeTotal;
        float liveEnvVolumeTotal;

        // ------------------------------------------------------------------
        //  Nucleus control zone - "node control" lives INSIDE the nucleus.
        //  Per-domain ENVIRONMENT volume (trail + flora; fauna bodies excluded -
        //  a swimming school must not tip territorial control) inside the
        //  nucleus' world radius determines DominantDomain. Everything OUTSIDE
        //  the nucleus is the contested feeding ground: voraciously edible by
        //  herbivores of ANY domain and the only mass the targeting grids see.
        //  Cells with no nucleus (no NucleusPrefab) have no control zone and
        //  keep the legacy whole-cell behavior. See Docs/ECOSYSTEM.md §13.
        // ------------------------------------------------------------------
        readonly Dictionary<Domains, float> nucleusEnvVolumeByDomain = new();
        float liveExteriorEnvVolumeTotal;
        float _nucleusControlRadiusSqr;

        // Prisms actually registered in the targeting grids. Interior (nucleus)
        // prisms are volume/count-tracked but never grid-tracked - fauna must not
        // be led to mass they cannot eat - so RemoveBlock has to know which
        // prisms the grids really hold (the nucleus radius can change between
        // Add and Remove; re-deriving membership would desync bucket counts).
        readonly HashSet<Prism> gridTracked = new();

        // Server-replicated dominant domain (CellNetworkSync, client side only).
        // Fauna spawn color must match the server's scored control read, so on
        // networked clients the replicated value overrides the locally-computed
        // one (client-local trail reconstruction can drift near the boundary).
        Domains? _replicatedDominantDomain;
        float _nextVolumeRecomputeAt = float.NegativeInfinity;
        const float VolumeRecomputeIntervalSeconds = 0.25f;

        // ---- Burst volume recompute state ----
        // The recompute is one CellVolumeSumJob pass over the spatial index's
        // packed summation view (slot order Jade/Ruby/Gold/Blue, matching
        // s_volumeDomainSlots), published atomically into the dictionaries above.
        // Scheduled on a WORKER thread against the index's snapshot
        // (TryScheduleCellVolumeSum) and harvested on a later read - the
        // main-thread cost per recompute is the snapshot memcpy, whether or not
        // the job executes Burst-compiled (the sync .Run() variant read ~6 ms in
        // editor captures where Burst wasn't applied). Replaces the managed
        // 8000-prisms-per-frame slice, whose per-entry object-graph cost made
        // every recompute a ~10 ms reader-attributed frame spike at high prism
        // counts (Docs/PERFORMANCE_OPTIMIZATION.md).
        static readonly Domains[] s_volumeDomainSlots = { Domains.Jade, Domains.Ruby, Domains.Gold, Domains.Blue };

        /// <summary>The three playable domains, hoisted to a static. These used to be
        /// built as a fresh <c>Domains[3]</c> inside AddBlock / RemoveBlock — i.e. a
        /// managed allocation on the per-prism CREATION and per-prism DEATH paths, so a
        /// 2,400-death AOE frame allocated 2,400 throwaway arrays inside the spatial
        /// index's UnbindCell. Same list, no garbage.</summary>
        static readonly Domains[] s_playableDomains = { Domains.Jade, Domains.Ruby, Domains.Gold };

        /// <summary>Dominant-domain scan order — playable domains first, Blue (the
        /// "no team" sentinel) last. Hoisted for the same reason: DominantDomain is a
        /// hot read (phase ladder, HUD, fauna spawning), not a once-per-round one.</summary>
        static readonly Domains[] s_dominantScanOrder = { Domains.Jade, Domains.Ruby, Domains.Gold, Domains.Blue };
        static readonly ProfilerMarker s_volumeSumMarker = new("Cell.VolumeSum");
        NativeArray<float> _volumeSumNative;
        JobHandle _volumeSumHandle;
        bool _volumeSumPending;

        // Identity in the summation view. Assigned once per Cell instance (never
        // reused within a session) so a stale binding from a destroyed cell can
        // never leak into another cell's sums. 0 = not yet assigned.
        static short s_nextVolumeCellId = 1;
        short _volumeCellId;

        SnowChanger spawnedCytoplasm;

        // ---------------------------------------------------------------------
        // Static spatial registry. Pooled prefab-spawned objects (trail prisms)
        // use this to find their containing cell - they have no scene identity
        // to wire a CellRuntimeDataSO into, and the per-prefab-asset alternative
        // breaks in multi-cell scenes where one prefab would need to point at
        // every cell's runtime SO at once.
        // ---------------------------------------------------------------------
        static readonly List<Cell> ActiveCells = new();

        // OnEnable/OnDisable keep the registry balanced across a clean play exit; the reset
        // covers the unclean one (a crash mid-play) and restarts the id counter so the short
        // can never wrap into PrismSpatialIndex's 0/-1 sentinels across many sessions.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            ActiveCells.Clear();
            s_nextVolumeCellId = 1;
        }

        /// <summary>
        /// Read-only view of the enabled cells in the scene. Exposed for read-only
        /// diagnostics (e.g. <see cref="EcosystemPerfProbe"/> summing prisms + live
        /// fauna across cells); do not mutate or cache across frames.
        /// </summary>
        public static IReadOnlyList<Cell> ActiveCellsSnapshot => ActiveCells;

        /// <summary>
        /// The enabled cell whose membrane contains <paramref name="position"/>,
        /// or null when the position is in open space. O(cells-in-scene) - call
        /// at object lifecycle points (spawn/destroy), not per frame.
        /// </summary>
        public static Cell FindCellContaining(Vector3 position)
        {
            for (int i = 0; i < ActiveCells.Count; i++)
            {
                var c = ActiveCells[i];
                if (c && c.ContainsPosition(position))
                    return c;
            }
            return null;
        }

        /// <summary>
        /// The enabled cell whose transform is closest to <paramref name="position"/>,
        /// or null if no cells are active. Distance is centre-to-point; ties resolve
        /// in iteration order. Useful as a fallback for HUDs that need *a* cell to
        /// read state from when the player isn't inside any (e.g. Menu_Main's
        /// orbital camera, between-cell transit).
        /// </summary>
        public static Cell FindNearestActiveCell(Vector3 position)
        {
            Cell best = null;
            float bestSqr = float.PositiveInfinity;
            for (int i = 0; i < ActiveCells.Count; i++)
            {
                var c = ActiveCells[i];
                if (!c) continue;
                float d = (c.transform.position - position).sqrMagnitude;
                if (d < bestSqr) { bestSqr = d; best = c; }
            }
            return best;
        }

        CellPhase phase = CellPhase.Calm;

        /// <summary>
        /// Live count of unique prisms tracked through Add/RemoveBlock. Read-only signal
        /// for systems that respond to prism load (e.g., LightFaunaManager scales its
        /// fauna population with this so consumption keeps pace with growth, and the
        /// phase system gates flora and fauna behavior on it).
        /// </summary>
        public int LiveBlockCount => trackedBlocks.Count;

        /// <summary>
        /// The shared runtime SO this cell writes to. Read-only handle for residents that
        /// need to raise its events through their host cell (e.g. Fauna's hearts-changed
        /// poke) — more reliable than a per-prefab CellRuntimeDataSO wire, which several
        /// fauna prefabs author as null or dangling.
        /// </summary>
        public CellRuntimeDataSO RuntimeData => runtime;

        /// <summary>
        /// Live leader by per-domain prism VOLUME - "volume is the spine" (locked
        /// invariant). NODE CONTROL IS THE NUCLEUS: when this cell has a nucleus
        /// control zone, only the ENVIRONMENT volume INSIDE the nucleus counts -
        /// lay your mass through the core to claim the cell; the exterior is the
        /// fauna's feeding ground and never sways control. Cells without a nucleus
        /// keep the legacy whole-cell read. On networked clients the server's
        /// replicated answer (CellNetworkSync) overrides the local compute so
        /// fauna spawn color always matches the scored control. Returns
        /// <see cref="Domains.Blue"/> (the "no team" sentinel) when the deciding
        /// volume is empty. Ties resolve in fixed order (Jade > Ruby > Gold > Blue).
        /// </summary>
        public Domains DominantDomain
        {
            get
            {
                if (_replicatedDominantDomain.HasValue)
                    return _replicatedDominantDomain.Value;

                EnsureVolumeFresh();
                var source = HasNucleusControlZone ? nucleusEnvVolumeByDomain : liveVolumeByDomain;
                Domains leader = Domains.Blue;
                float leaderVolume = 0f;
                foreach (var d in s_dominantScanOrder)
                {
                    if (!source.TryGetValue(d, out float v)) continue;
                    if (v > leaderVolume)
                    {
                        leader = d;
                        leaderVolume = v;
                    }
                }
                return leader;
            }
        }

        /// <summary>
        /// True when this cell has a spawned nucleus with a measurable world radius -
        /// the node-control zone. Without one (no NucleusPrefab in the CellConfig)
        /// control and edibility keep their legacy whole-cell semantics.
        /// </summary>
        public bool HasNucleusControlZone => _nucleusControlRadiusSqr > 0f;

        /// <summary>True when <paramref name="position"/> is inside the nucleus control zone (always false without one).</summary>
        public bool IsInsideNucleus(Vector3 position) =>
            _nucleusControlRadiusSqr > 0f &&
            (position - transform.position).sqrMagnitude <= _nucleusControlRadiusSqr;

        /// <summary>
        /// The domain holding the nucleus claim right now. False when the cell has no
        /// nucleus control zone OR nobody has laid environment mass inside it yet -
        /// distinct from <see cref="ControllingDomain"/>, which falls back through
        /// gameData so fauna always get a spawn color. Scoring systems (Brood Rush)
        /// read THIS so an unclaimed nucleus never awards a fallback-team point.
        /// </summary>
        public bool TryGetNucleusClaim(out Domains claimant)
        {
            claimant = Domains.Blue;
            if (!HasNucleusControlZone) return false;
            claimant = DominantDomain;
            return claimant != Domains.Blue;
        }

        /// <summary>
        /// The herbivore diet rule, spatialized. With a nucleus control zone: mass
        /// OUTSIDE the nucleus is voraciously edible by any herbivore REGARDLESS of
        /// domain (the exterior is the contested feeding ground - extends the Boid
        /// forager's existing any-domain grazing to all herbivores), while mass
        /// INSIDE the nucleus is the territorial claim and is never fauna-consumed
        /// (players contest it with abilities and by out-laying volume). Without a
        /// nucleus zone the legacy rule stands: herbivores eat opposing-domain mass.
        /// </summary>
        public bool IsPreyForHerbivore(Vector3 position, Domains faunaDomain, Domains preyDomain)
        {
            // Containment first: a PENNED brood cannot reach the world outside its pen, so
            // nothing out there is food no matter whose domain it wears. Ribcage's cage starts
            // contained - the brood is visibly penned inside and will eat the trail of any
            // vessel that ventures IN (that is the whole point of respecting the cage), but it
            // cannot touch the match going on outside. The 25% release clears the radius and
            // the ordinary rules below resume.
            if (FaunaContainmentRadius > 0f && !IsInsideFaunaContainment(position))
                return false;

            if (HasNucleusControlZone)
                return !IsInsideNucleus(position);
            return preyDomain != faunaDomain;
        }

        /// <summary>
        /// Radius (world units, centred on the cell) the cell's fauna are penned inside, or 0
        /// for no containment - the default, and what every biome that is not a mode's pen
        /// uses. While set: mass outside is not prey (<see cref="IsPreyForHerbivore"/>) and
        /// every creature's goal is clamped inside (<see cref="ClampToFaunaContainment"/>).
        ///
        /// This is a spatial DIET + STEERING rule, not a wall: nothing is teleported and no
        /// collider is added, so a creature can still drift out on its own momentum - it just
        /// has no reason to and nothing to eat there. Ribcage sets it to the cage's shell
        /// radius while the cage is sealed and clears it on the first release.
        /// </summary>
        public float FaunaContainmentRadius { get; set; }

        /// <summary>
        /// The INNER wall of the same pen, or 0 for none (the default, and every biome that is
        /// not a mode's pen). While set, the sphere of this radius around the cell centre is
        /// OUT of bounds: mass inside it is not prey and every fauna goal is pushed back out to
        /// it - the exact mirror of <see cref="FaunaContainmentRadius"/>, on the exact same two
        /// rules (diet + steering), so together they express an annulus at the CELL level the way
        /// <c>FaunaConfigurationSO.BandInner/BandOuterRadius</c> expresses one per SPECIES.
        ///
        /// The band already proved the annulus; the cell pen already proved runtime control. This
        /// is the missing quadrant - the annulus a MODE can open and close while the match runs -
        /// and it exists because Astro League wanted its creatures waiting outside the court and
        /// invading it only once the pitch silts up. The mode drives it off the cell's OWN volume
        /// phase ladder (Calm = closed, Restless+ = open), so "the arena is getting crowded" is
        /// read from the spine rather than from a new signal invented for the mode.
        ///
        /// Same contract as every other pen (Docs/ECOSYSTEM.md §22.2b): a spatial DIET + STEERING
        /// rule, never a wall. Nothing is teleported, no collider is added, nothing is culled for
        /// crossing it - a creature can still drift in on its own momentum, it just has no reason
        /// to and nothing to eat there.
        /// </summary>
        public float FaunaExclusionRadius { get; set; }

        /// <summary>
        /// While the brood is penned, does a creature that DETECTS prey inside the pen go to full
        /// aggression? Off by default. Ribcage turns it on: the cage is meant to be intimidating,
        /// so flying in does not merely put your trail on the menu - it sends the whole penned
        /// population berserk (Frenzy → CellAggressionLevel.Level2: any-colour steering, friendly
        /// avoidance off, danger-immune, fastest cadence and widest consume radius) until you
        /// leave and the mass you laid is gone.
        ///
        /// This is a confinement response, not a new fundamental: it only raises the SAME phase
        /// floor a mode could set by hand, and only while a pen exists.
        /// </summary>
        public bool ContainmentIntruderFrenzy { get; set; }

        /// <summary>
        /// True when the pen currently holds edible mass - i.e. somebody flew in and laid trail.
        /// Sampled on the PHASE tick (not per frame) through the canonical spatial index
        /// (Docs/SPATIAL_INDEX.md), never a physics query, and only while a pen exists.
        ///
        /// The pen radius deliberately sits inside the cage shell, so the cage's own bars - the
        /// shielded ones AND the unshielded danger traps - are outside it and can never register
        /// as an intruder. Shielded mass is filtered anyway (it is not food), which also keeps a
        /// player's own shielded prisms from tripping it.
        /// </summary>
        public bool HasPreyInsideFaunaContainment
        {
            get
            {
                if (FaunaContainmentRadius <= 0f) return false;
                if (Time.time < _nextContainmentProbeAt) return _containmentHasPrey;
                _nextContainmentProbeAt = Time.time + ContainmentProbeIntervalSeconds;

                _containmentProbe.Clear();
                var index = PrismSpatialIndex.Instance;
                _containmentHasPrey = false;
                if (index == null) return false;

                index.QuerySphere(transform.position, FaunaContainmentRadius, _containmentProbe);
                for (int i = 0; i < _containmentProbe.Count; i++)
                {
                    var p = _containmentProbe[i];
                    if (!p || IsShieldedMass(p)) continue;   // shields are not food, so not an intruder
                    _containmentHasPrey = true;
                    break;
                }
                _containmentProbe.Clear();
                return _containmentHasPrey;
            }
        }

        // Reused buffer + cadence for the pen's intruder probe - one Burst sphere query per
        // interval, shared across every reader in the frame.
        static readonly List<Prism> _containmentProbe = new();
        const float ContainmentProbeIntervalSeconds = 0.4f;
        float _nextContainmentProbeAt = float.NegativeInfinity;
        bool _containmentHasPrey;

        /// <summary>
        /// True when <paramref name="position"/> is inside the cell's fauna pen - i.e. within
        /// <see cref="FaunaContainmentRadius"/> AND outside <see cref="FaunaExclusionRadius"/>.
        /// Always true for the biomes that author neither, which is all of them except a mode's
        /// pen, so the common path is two compares against zero.
        /// </summary>
        public bool IsInsideFaunaContainment(Vector3 position)
        {
            float sqr = (position - transform.position).sqrMagnitude;
            if (FaunaContainmentRadius > 0f && sqr > FaunaContainmentRadius * FaunaContainmentRadius)
                return false;
            if (FaunaExclusionRadius > 0f && sqr < FaunaExclusionRadius * FaunaExclusionRadius)
                return false;
            return true;
        }

        /// <summary>
        /// Pulls a fauna goal back into the pen: in past the outer wall
        /// (<see cref="FaunaContainmentRadius"/>) and out past the inner one
        /// (<see cref="FaunaExclusionRadius"/>). Returns the point unchanged when there is no pen
        /// or the goal already sits in it, so the common path costs one compare.
        ///
        /// <paramref name="selfPosition"/> resolves the one degenerate case: a goal AT the centre
        /// is the ecology's "nothing sensed" answer, not a destination, and it has no outward
        /// radial to push along. Pass the creature's own position (every steering caller has it)
        /// and it mills where it already is instead of the whole population collapsing onto one
        /// point on the inner wall - the same reasoning as <c>Fauna.ClampToBand</c>.
        /// </summary>
        public Vector3 ClampToFaunaContainment(Vector3 goal, Vector3? selfPosition = null)
        {
            bool hasOuter = FaunaContainmentRadius > 0f;
            bool hasInner = FaunaExclusionRadius > 0f;
            if (!hasOuter && !hasInner) return goal;

            Vector3 centre = transform.position;
            Vector3 offset = goal - centre;
            float d = offset.magnitude;

            if ((!hasOuter || d <= FaunaContainmentRadius) && (!hasInner || d >= FaunaExclusionRadius))
                return goal;

            if (d <= 0.0001f)
            {
                // Only reachable with an INNER wall (a centre goal is already legal under an outer
                // wall alone): fall back to the creature's own outward radial so an unfed population
                // mills where it is instead of every member collapsing onto one point on the wall.
                offset = (selfPosition ?? centre) - centre;
                d = offset.magnitude;
                if (d <= 0.0001f) return centre + Vector3.up * FaunaExclusionRadius;
            }

            float lo = hasInner ? FaunaExclusionRadius : 0f;
            float hi = hasOuter ? FaunaContainmentRadius : float.PositiveInfinity;
            if (lo > hi) lo = hi; // a pen squeezed shut collapses onto the outer wall, never inverts
            return centre + offset / d * Mathf.Clamp(d, lo, hi);
        }

        /// <summary>
        /// True while the targeting grids hold any environment mass - with a nucleus
        /// zone that means EXTERIOR mass (interior prisms are never grid-tracked).
        /// Fauna use this to hunt the feeding ground even at Calm ("voracious").
        /// </summary>
        public bool HasSensedExteriorMass => gridTracked.Count > 0;

        /// <summary>
        /// Client-side hook for <see cref="CellNetworkSync"/>: pins DominantDomain to
        /// the server's replicated answer so spawn color and control UI can't drift
        /// from the scored value. Pass null (server / single-player) to clear.
        /// </summary>
        public void SetReplicatedDominantDomain(Domains? domain) => _replicatedDominantDomain = domain;

        /// <summary>
        /// SERVER-side hook for a game mode that defines "control" by its own scored rule
        /// rather than by laid volume - the same authority move Brood Rush makes when it
        /// says node control IS the nucleus, expressed here as a pin instead of a
        /// different volume source. Ribcage uses it: the cell's controlling domain is the
        /// team currently leading the cage-destruction race, so the fauna wave that hatches
        /// wears the leader's colour and the untouched legacy herbivore diet (eat
        /// opposing-domain mass) points the swarm at every trailing team's trails. No
        /// bespoke fauna targeting exists anywhere - the diet rule was already this.
        ///
        /// Writes the same field the client-side replication pin uses, and that is
        /// deliberate: only ONE side ever writes it (the mode on the server, where
        /// <see cref="CellNetworkSync"/> deliberately skips its own writes; CellNetworkSync
        /// on clients, mirroring the server's already-pinned answer). So a mode pin
        /// replicates to every peer through the existing phase/domain sync for free.
        ///
        /// Re-colours the LIVE swarm as well as the next wave - the no-domain-asymmetry
        /// invariant says a cell's fauna are the CONTROLLER's fauna, and letting the
        /// standing swarm keep a deposed team's colour would leave two fauna colours in
        /// one cell. Doing it inside this setter is what keeps the two from drifting.
        /// Pass null to release the pin.
        /// </summary>
        public void SetModeControlOverride(Domains? domain)
        {
            _replicatedDominantDomain = domain;

            if (!domain.HasValue || domain.Value == Domains.Blue) return;

            // Re-colour UNCONDITIONALLY - deliberately not gated on "the value changed".
            // On a client both writers touch this field: CellNetworkSync's replication
            // callback (which does NOT re-colour) and this setter, and the NetworkVariable
            // delta can land BEFORE the mode's RPC. An equality early-return would then see
            // the field already correct and skip the swarm, leaving that client's creatures
            // wearing the deposed team's colour - hunting the wrong trails - for the rest of
            // the match. Callers only invoke this on control TRANSITIONS and the loop is
            // O(live fauna) (tens), so paying it every call is the cheap side of the trade.
            for (int i = 0; i < liveFauna.Count; i++)
            {
                var f = liveFauna[i];
                if (f) f.SetTeam(domain.Value);
            }
        }

        /// <summary>
        /// Is this cell's nucleus a NODE-CONTROL ZONE (the default, true), or is it merely
        /// PLAY GEOMETRY a mode has repurposed?
        ///
        /// The invariant "node control is the nucleus" makes the nucleus interior a territorial
        /// claim and a fauna SANCTUARY - <see cref="IsPreyForHerbivore"/> refuses to feed anything
        /// inside it, and <see cref="DominantDomain"/> reads only the volume laid in there. That is
        /// exactly right for a cell whose nucleus is a core somebody contests.
        ///
        /// It is exactly WRONG for a mode that borrowed the nucleus as its playfield boundary.
        /// Astro League morphs the nucleus into its whole ricochet court
        /// (<see cref="SetNucleusMesh"/>), which made the control radius the court's circumscribing
        /// radius - so every prism in the match was "inside the nucleus", nothing on the pitch was
        /// ever food, and the mode's trail-grazing food web could not remove a single prism no
        /// matter how it was tuned. The arena silted up and the fauna starved beside it.
        ///
        /// Setting this false says "this nucleus is a wall, not a claim": the control zone
        /// collapses to nothing and the cell falls back to its whole-cell semantics exactly as if
        /// no NucleusPrefab were authored - herbivores eat opposing-domain mass anywhere,
        /// DominantDomain reads the whole cell. It does not relitigate the invariant; it declares
        /// that this cell has no control zone, which is a state the ecology already supports.
        /// </summary>
        public bool NucleusIsControlZone
        {
            get => _nucleusIsControlZone;
            set
            {
                if (_nucleusIsControlZone == value) return;
                _nucleusIsControlZone = value;
                RefreshNucleusControlRadius();
            }
        }
        bool _nucleusIsControlZone = true;

        /// <summary>
        /// Minimum phase this cell may sit at, or null for "no floor" (the default -
        /// every cell that does not opt in behaves exactly as before). The volume ladder
        /// still runs every tick; the floor only ever RAISES the result, so a mode can
        /// escalate its ecology on its own scored signal without the phase compute
        /// becoming a mode concern.
        ///
        /// Ribcage drives it from race progress: the leader passing 25% of the cage
        /// target floors the cell at Restless (fauna hunt the opposing-colour centroid),
        /// 50% floors it at Frenzy (any-colour steering, no friendly avoidance,
        /// danger-immune). This is NOT a decay/growth oscillator - it is monotonic in an
        /// ACTIVE player force (mass destroyed) and never removes a prism.
        /// </summary>
        public CellPhase? ModePhaseFloor { get; set; }

        /// <summary>
        /// Staged fauna release: a species may seed only when its
        /// <see cref="FaunaConfigurationSO.ReleaseTier"/> is at or below this value.
        /// Defaults to <see cref="int.MaxValue"/> ("everything released"), and every
        /// existing config authors tier 0, so no shipped biome changes behaviour.
        ///
        /// Ribcage holds the cage's brood at -1 (nothing released) until the leader
        /// cracks 25% of the target, then 0 (the grazer swarm), then 1 at 50% (the
        /// predator joins). Gating PRODUCTION is explicitly allowed by the conserved-mass
        /// law - not creating mass is fine, aging it out is not.
        /// </summary>
        public int FaunaReleaseTier { get; set; } = int.MaxValue;

        // ------------------------------------------------------------------
        //  Live volume - the spine. Recomputed from live prism state on a short
        //  cadence (growth animates continuously, so event-driven deltas would
        //  drift). The recompute is ONE Burst pass over the spatial index's packed
        //  summation view (PrismSpatialIndex.SumCellVolumes) and publishes
        //  atomically - the previous managed slice loop over the prism object
        //  graph (null-check + CachedVolume + trackedBlocks lookup +
        //  transform.position per prism, 8000/frame) cost whoever read volume on
        //  a stale tick ~10 ms per slice frame at high prism counts; the Burst
        //  scan is ~0.1-0.3 ms for the same population (the LodClassifyJob
        //  collapse, applied to volume). Reader-driven, at most once per interval.
        // ------------------------------------------------------------------

        void EnsureVolumeFresh()
        {
            // Harvest the async pass scheduled on an earlier read. IsCompleted
            // avoids ever blocking the main thread on the job - readers keep the
            // previously published sums until the worker is done (typically the
            // next frame; the tolerance the 0.25s cadence already declares).
            if (_volumeSumPending && _volumeSumHandle.IsCompleted)
            {
                _volumeSumHandle.Complete(); // required handshake; no-op wait
                _volumeSumPending = false;
                PublishVolumeSums();
            }

            if (Time.time < _nextVolumeRecomputeAt) return;
            if (_volumeSumPending) return; // previous pass still crunching

            var index = PrismSpatialIndex.Instance;
            if (index == null || !index.IsAvailable) return; // no index (tooling scene) - keep published sums, retry next read

            if (!_volumeSumNative.IsCreated)
                _volumeSumNative = new NativeArray<float>(PrismSpatialIndex.CellVolumeResultCount, Allocator.Persistent);

            using (s_volumeSumMarker.Auto())
            {
                if (!index.TryScheduleCellVolumeSum(_volumeCellId, transform.position, _nucleusControlRadiusSqr,
                        _volumeSumNative, out _volumeSumHandle))
                    return;
                _volumeSumPending = true;
                _nextVolumeRecomputeAt = Time.time + VolumeRecomputeIntervalSeconds;
            }
        }

        /// <summary>
        /// Atomic publish of a completed volume pass. Slot order matches
        /// s_volumeDomainSlots (Jade/Ruby/Gold/Blue) on both sides. The job already
        /// folds the no-nucleus case into ExteriorEnvVolumeTotal (== env total),
        /// the legacy opposing-domain prey math's else-branch.
        /// </summary>
        void PublishVolumeSums()
        {
            for (int i = 0; i < PrismSpatialIndex.CellDomainSlotCount; i++)
            {
                liveVolumeByDomain[s_volumeDomainSlots[i]] = _volumeSumNative[PrismSpatialIndex.CellVolumeBySlot + i];
                liveEnvVolumeByDomain[s_volumeDomainSlots[i]] = _volumeSumNative[PrismSpatialIndex.CellEnvVolumeBySlot + i];
                nucleusEnvVolumeByDomain[s_volumeDomainSlots[i]] = _volumeSumNative[PrismSpatialIndex.CellNucleusEnvVolumeBySlot + i];
            }
            liveVolumeTotal = _volumeSumNative[PrismSpatialIndex.CellVolumeTotal];
            liveEnvVolumeTotal = _volumeSumNative[PrismSpatialIndex.CellEnvVolumeTotal];
            liveExteriorEnvVolumeTotal = _volumeSumNative[PrismSpatialIndex.CellExteriorEnvVolumeTotal];
        }

        /// <summary>
        /// Drops all volume accounting - published sums and the recompute timer -
        /// so a cleared cell reads as empty immediately instead of serving a
        /// pre-reset snapshot until the next recompute.
        /// </summary>
        void ResetVolumeAccounting()
        {
            _nextVolumeRecomputeAt = float.NegativeInfinity; // resum on next read
            // Discard (never publish) an in-flight pass - it summed pre-reset
            // state. Complete() first: the results array is about to be re-used
            // by the next scheduled job.
            if (_volumeSumPending)
            {
                _volumeSumHandle.Complete();
                _volumeSumPending = false;
            }
            liveVolumeByDomain.Clear();
            liveEnvVolumeByDomain.Clear();
            nucleusEnvVolumeByDomain.Clear();
            liveVolumeTotal = 0f;
            liveEnvVolumeTotal = 0f;
            liveExteriorEnvVolumeTotal = 0f;
        }

        /// <summary>
        /// Total live prism volume in this cell - ALL prisms (trail, flora, fauna
        /// bodies). THE phase-ladder measure ("volume is the spine").
        /// </summary>
        public float LiveVolume
        {
            get { EnsureVolumeFresh(); return liveVolumeTotal; }
        }

        /// <summary>Live volume tracked under <paramref name="domain"/> - all prism sources.</summary>
        public float GetDomainVolume(Domains domain)
        {
            EnsureVolumeFresh();
            return liveVolumeByDomain.GetValueOrDefault(domain, 0f);
        }

        /// <summary>
        /// The herbivore PREY signal in volume units (fauna bodies excluded - not
        /// edible, counting them would seed fauna against phantom food). With a
        /// nucleus control zone this is ALL environment volume outside the nucleus
        /// (the exterior is voraciously edible regardless of domain); without one it
        /// is the legacy opposing-domain read (env volume not of <paramref name="domain"/>).
        /// </summary>
        public float OpposingVolume(Domains domain)
        {
            EnsureVolumeFresh();
            if (HasNucleusControlZone)
                return liveExteriorEnvVolumeTotal;
            return Mathf.Max(0f, liveEnvVolumeTotal - liveEnvVolumeByDomain.GetValueOrDefault(domain, 0f));
        }

        /// <summary>
        /// Live count of prisms tracked under <paramref name="domain"/>. Mirrors the
        /// per-domain bookkeeping that <see cref="DominantDomain"/> reads, exposed so
        /// HUD widgets (volume wedges, etc.) don't need to walk Add/RemoveBlock state
        /// themselves. Returns 0 for untracked domains.
        /// </summary>
        public int GetDomainBlockCount(Domains domain) =>
            domainBlockCounts.TryGetValue(domain, out int c) ? c : 0;

        /// <summary>
        /// Prism COUNT at which the perf backstop forces Frenzy. The phase ladder
        /// itself runs on volume - see <see cref="FrenzyEnterVolume"/>.
        /// </summary>
        public int FrenzyEnterThreshold => ResolveThresholds().FrenzyEnter;

        /// <summary>
        /// LiveVolume at which the cell crosses into Frenzy. HUD widgets use this as
        /// the "max" - when summed mass approaches it, the cell is about to enter Level2
        /// aggression (and flora freeze) and the UI should communicate that.
        /// </summary>
        public float FrenzyEnterVolume => ResolveThresholds().FrenzyEnterVolume;

        /// <summary>
        /// The full resolved phase-threshold table for this cell (config table, or
        /// <see cref="CellPhaseThresholds.Default"/> when no config / legacy zeroed
        /// asset). Exposed so the concentric-hexagon volume indicator can draw one
        /// ring per phase boundary (Restless, then Frenzy at the centre) at a radius
        /// proportional to its enter threshold, lighting each ring as the cell's
        /// summed mass crosses it. Read-only - the cell is the single writer.
        /// </summary>
        public CellPhaseThresholds ResolvedThresholds => ResolveThresholds();

        /// <summary>
        /// True once this cell's CellConfig has been assigned (Initialize ran). While
        /// false, threshold reads fall back to CellPhaseThresholds.Default - HUD
        /// diagnostics surface this so a mis-scaled indicator is explainable at a
        /// glance instead of looking like dead data.
        /// </summary>
        public bool HasConfigAssigned => cellConfigData != null;

        // ---------------------------------------------------------------------
        //  Fauna spawn cycle telemetry - read by the volume-indicator ring HUD.
        //  Written by IntensityWiseLifeSpawner.SpawnFaunaTypeLoop when it ticks a
        //  periodic fauna spawn. The Cell exposes a 0..1 progress fraction toward
        //  the next spawn so the indicator can draw a rotating ring without
        //  knowing anything about the spawner's internals.
        // ---------------------------------------------------------------------

        float _lastFaunaSpawnTime = -1f;

        /// <summary>
        /// Records that a periodic fauna spawn just happened. The spawn-cycle ring
        /// resets to 0% and counts back up to 100% over the next CurrentFaunaSpawnPeriod
        /// seconds. Called by IntensityWiseLifeSpawner's fauna loop.
        /// </summary>
        public void RecordFaunaSpawn() => _lastFaunaSpawnTime = Time.time;

        /// <summary>
        /// Fixed period (seconds) between this cell's periodic fauna population spawns -
        /// just BaseFaunaSpawnTime. Per the ecology redesign the spawn cadence and swarm
        /// size are FIXED; prism count drives fauna *aggression/behavior*, not spawn rate
        /// (Docs/ECOSYSTEM.md §5). HUDs read this for the spawn-cycle ring. Returns 0 when
        /// no profile is wired (HUD treats 0 as "no cycle to show").
        /// </summary>
        public float CurrentFaunaSpawnPeriod
        {
            get
            {
                var profile = cellConfigData ? cellConfigData.SpawnProfile : null;
                if (!profile) return 0f;
                return Mathf.Max(0.05f, profile.BaseFaunaSpawnTime);
            }
        }

        /// <summary>
        /// 0..1 progress through the current fauna spawn cycle. 0 = just spawned,
        /// 1 = about to spawn. Returns 0 when no period is configured or no spawn
        /// has been recorded yet.
        /// </summary>
        public float FaunaSpawnCycleFraction
        {
            get
            {
                float period = CurrentFaunaSpawnPeriod;
                if (period <= 0f || _lastFaunaSpawnTime < 0f) return 0f;
                return Mathf.Clamp01((Time.time - _lastFaunaSpawnTime) / period);
            }
        }

        /// <summary>
        /// Current phase. Written exclusively by <see cref="CellNetworkSync"/> via
        /// <see cref="ApplyAuthoritativePhaseAndDomain"/> - the server's compute on a
        /// networked cell, or the local-only fallback in single-player. Cell never
        /// recomputes phase itself; it just exposes the inputs.
        /// </summary>
        public CellPhase Phase => phase;

        // ---------------------------------------------------------------------
        // Derived gates - projections of Phase the consumers actually care about.
        // Flora planting and growing now share ONE rule (steady until Frenzy); fauna
        // read the aggression band. These properties give each consumer exactly the
        // boolean it needs without re-deriving phase semantics.
        // ---------------------------------------------------------------------

        /// <summary>
        /// True while new flora may be planted AND existing flora may grow: the cell is
        /// below Frenzy. Planting and growth run at a STEADY rate all the way up - there
        /// is no early planting cap and no mid-range growth cap (those staggered phase
        /// gates were a growth-side cheat: a hard-coded self-limit faking the homeostasis
        /// the food web is meant to produce). The only down-force on flora is the food web
        /// (opposing-domain fauna grazing the prisms) or vessel abilities. Once a cell
        /// fills to Frenzy, growth stops and stays stopped until an ACTIVE force lowers the
        /// live prism count back below the Frenzy exit threshold (hysteresis), at which
        /// point growth resumes on its own. Mass is conserved: no passive decay, no growth
        /// oscillator - a frozen-solid cell is a valid state, not a defect to auto-correct.
        /// See Docs/ECOSYSTEM.md §0/§5.
        /// </summary>
        public bool FloraGrowingEnabled => phase < CellPhase.Frenzy;

        /// <summary>
        /// True while new flora may be planted. Identical to <see cref="FloraGrowingEnabled"/>
        /// - planting and growth share the single "below Frenzy" rule now (steady until
        /// frenzy). Kept as a separate name so spawner code reads intent at the call site.
        /// </summary>
        public bool FloraPlantingEnabled => FloraGrowingEnabled;

        /// <summary>
        /// True once the cell holds any ENVIRONMENT mass - the spawn floor for the
        /// timer-driven IntensityWise fauna loop. Volume-keyed ("volume is the
        /// spine") and environment-only: fauna bodies must not satisfy their own
        /// spawn floor. The prey-linked RandomLifeSpawner gates on
        /// <see cref="OpposingVolume"/> + FaunaFoodFloor instead, which is the real
        /// population bound (Docs/ECOSYSTEM.md §6).
        /// </summary>
        public bool FaunaSpawningEnabled
        {
            get { EnsureVolumeFresh(); return liveEnvVolumeTotal > 0f; }
        }

        /// <summary>
        /// Fauna aggression level derived from <see cref="Phase"/> - a 1:1 mapping now
        /// that flora are no longer staggered on separate rungs:
        ///   Calm     → Level0  (head toward crystal, normal cadence)
        ///   Restless → Level1  (head toward opposing-color centroid)
        ///   Frenzy   → Level2  (any-domain centroid, drop friendly avoidance, danger-immune)
        /// </summary>
        public CellAggressionLevel AggressionLevel => phase switch
        {
            CellPhase.Restless => CellAggressionLevel.Level1,
            CellPhase.Frenzy => CellAggressionLevel.Level2,
            _ => CellAggressionLevel.Level0,
        };

        /// <summary>
        /// "Controlling color" for fauna spawns. Prefers the cell's live
        /// <see cref="DominantDomain"/> (per-domain prism count leader), then falls
        /// back to gameData's controlling team by remaining volume, then to the local
        /// player's domain (useful in Menu_Main where there is no scored controlling
        /// team), then to Jade as a last resort. Never returns Blue (the "no team"
        /// sentinel) - callers can use it directly without further branching.
        /// </summary>
        public Domains ControllingDomain
        {
            get
            {
                var dominant = DominantDomain;
                if (dominant != Domains.Blue)
                    return dominant;

                if (gameData != null)
                {
                    var top = gameData.GetControllingTeamStatsBasedOnVolumeRemaining();
                    if (top.Team != Domains.Blue && top.Volume > 0f)
                        return top.Team;

                    var local = gameData.LocalRoundStats?.Domain
                                ?? gameData.LocalPlayer?.Domain
                                ?? Domains.Blue;
                    if (local != Domains.Blue)
                        return local;
                }
                return Domains.Jade;
            }
        }

        /// <summary>
        /// Sole entry point for phase mutation. Updates the local field and the
        /// runtime SO's per-cell stats; the runtime SO raises <c>OnPhaseChanged</c>
        /// when the value transitions. Both <see cref="CellNetworkSync"/>'s server
        /// tick and its <c>OnValueChanged</c> client listener route through here so
        /// the runtime SO is the single observable source of truth on every machine.
        /// </summary>
        public void ApplyAuthoritativePhaseAndDomain(CellPhase newPhase, Domains newDominantDomain)
        {
            phase = newPhase;
            if (runtime != null)
                runtime.WriteCellRuntimeStats(ID, LiveBlockCount, newPhase, newDominantDomain);
        }

        void Update()
        {
            // Drive phase locally every tick interval. Server-authoritative replication
            // (CellNetworkSync) overlays this on networked clients via OnValueChanged
            // - server's compute wins when the two diverge - but for single-player and
            // for the server itself this is the only path that advances phase. Without
            // it, no fauna ever spawn because phase stays at Calm forever.
            if (Time.time < _nextPhaseTickAt) return;
            _nextPhaseTickAt = Time.time + PhaseTickIntervalSeconds;

            var thresholds = ResolveThresholds();
            // Volume is the spine: the ladder climbs on live volume; prism count is
            // only the Frenzy perf backstop inside Compute.
            var newPhase = CellPhaseRules.Compute(LiveVolume, LiveBlockCount, phase, in thresholds);

            // A mode may hold the cell at or above a phase (see ModePhaseFloor). The
            // ladder is unchanged - the floor can only raise the answer, never lower it,
            // so volume remains the spine and the floor is pure escalation on top.
            if (ModePhaseFloor.HasValue && newPhase < ModePhaseFloor.Value)
                newPhase = ModePhaseFloor.Value;

            // A penned population that detects an intruder's mass goes berserk (see
            // ContainmentIntruderFrenzy). Same ladder, same floor mechanism - just driven by the
            // pen instead of by the mode's progress.
            if (ContainmentIntruderFrenzy && FaunaContainmentRadius > 0f &&
                newPhase < CellPhase.Frenzy && HasPreyInsideFaunaContainment)
                newPhase = CellPhase.Frenzy;

            ApplyAuthoritativePhaseAndDomain(newPhase, DominantDomain);
        }

        CellPhaseThresholds ResolveThresholds()
        {
            var cfg = cellConfigData;
            if (!cfg) return CellPhaseThresholds.Default;

            // Existing CellConfig assets serialized before PhaseThresholds existed
            // deserialize as struct zero - Unity does not apply the C# initializer.
            // Substitute the Default table so legacy biomes don't snap to Frenzy the
            // moment the first prism is added. Assets authored before volume became
            // the spine derive their volume ladder from the count fields (×16).
            var t = cfg.PhaseThresholds;
            return t.IsAllZero ? CellPhaseThresholds.Default : t.WithDerivedVolumeScale();
        }

        readonly ICellLifeSpawner intensitySpawner = new IntensityWiseLifeSpawner();
        readonly ICellLifeSpawner randomSpawner = new RandomLifeSpawner();
        ICellLifeSpawner activeSpawner;
        bool postInitilized = false;

        void OnEnable()
        {
            // Summation-view identity - once per instance, before any prism can
            // bind (AddBlock reads it). See the field remarks.
            if (_volumeCellId == 0)
                _volumeCellId = s_nextVolumeCellId++;

            // Spatial registry - lets pooled, prefab-spawned objects (trail prisms)
            // find which cell contains them without per-prefab SO wiring or the
            // deprecated CellControlManager singleton. See FindCellContaining.
            if (!ActiveCells.Contains(this))
                ActiveCells.Add(this);

            // Clear stale config BEFORE subscribing to events.
            // CellRuntimeDataSO is a shared SO asset - Menu_Main's Cell sets
            // runtime.Config to Blob Cell Config, which persists into the next
            // scene. Without clearing here, OnCellItemsUpdated could fire between
            // OnEnable (subscription) and Start (where the clear previously lived),
            // causing InitilizePostFirstCellItem to use the stale config and spawn
            // flora from the wrong CellConfig. This was the root cause of Gyroids
            // appearing on clients in HexRace despite using a Barren Cell Config.
            if (runtime != null)
                runtime.Config = null;

            if (gameData != null)
                gameData.OnInitializeGame.OnRaised += Initialize;

            if (!runtime) return;

            // We keep events ONLY in runtime.
            if (runtime.OnCellItemsUpdated != null)
                runtime.OnCellItemsUpdated.OnRaised += OnCellItemUpdated;

            if (runtime.OnResetForReplay != null)
                runtime.OnResetForReplay.OnRaised += ResetCell;
        }

        void Start()
        {
            // [Inject] fields aren't available in OnEnable. Retry subscription
            // here with deduplicate guard so Initialize() fires on OnInitializeGame.
            if (gameData != null)
            {
                gameData.OnInitializeGame.OnRaised -= Initialize;
                gameData.OnInitializeGame.OnRaised += Initialize;
            }
        }

        void OnDisable()
        {
            ActiveCells.Remove(this);

            if (gameData != null)
                gameData.OnInitializeGame.OnRaised -= Initialize;

            if (runtime != null)
            {
                if (runtime.OnCellItemsUpdated != null)
                    runtime.OnCellItemsUpdated.OnRaised -= OnCellItemUpdated;

                if (runtime.OnResetForReplay != null)
                    runtime.OnResetForReplay.OnRaised -= ResetCell;
            }

            if (spawnedCytoplasm)
            {
                Destroy(spawnedCytoplasm.gameObject);
                spawnedCytoplasm = null;
            }

            // Settle the in-flight volume pass (never published) - the results
            // array must be quiescent before a re-enable schedules into it again.
            if (_volumeSumPending)
            {
                _volumeSumHandle.Complete();
                _volumeSumPending = false;
            }

            StopSpawner();
            runtime?.ResetRuntimeData();
        }

        void OnDestroy()
        {
            if (_volumeSumPending)
            {
                _volumeSumHandle.Complete();
                _volumeSumPending = false;
            }
            if (_volumeSumNative.IsCreated) _volumeSumNative.Dispose();
        }

        void ResetCell()
        {
            // Destroy all spawned lifeforms
            for (int i = spawnedLifeForms.Count - 1; i >= 0; i--)
            {
                if (spawnedLifeForms[i]) Destroy(spawnedLifeForms[i]);
            }
            spawnedLifeForms.Clear();
            trackedBlocks.Clear();
            domainBlockCounts.Clear();
            // Packed counterpart of the old massTracked.Clear(): drop every
            // summation-view binding this cell holds, so surviving prisms don't
            // keep contributing to the post-reset sums.
            PrismSpatialIndex.Instance?.ClearAllCellBindings(_volumeCellId);
            gridTracked.Clear();
            _replicatedDominantDomain = null;
            ResetVolumeAccounting();
            liveFaunaCounts.Clear();
            liveFloraCounts.Clear();
            liveFauna.Clear();
            // The gyroid colony's frontier is a POPULATION-level book of open octagons, so it
            // outlives any individual plant by design - which means only the cell can retire it.
            // Left behind, the next world grown here inherits the dead one's sites and plants
            // daughters into lattice that no longer exists (the Cell Selector swaps worlds in
            // the very scene this colony ships in). Keyed by cell, so this touches no other.
            GyroidColonyFrontier.Clear(this);
            SchwarzPColonyFrontier.Clear(this);
            SchwarzPTileRegistry.Clear(this);
            QuasicrystalColonyFrontier.Clear(this);
            QuasicrystalHeartRegistry.Clear(this);
            phase = CellPhase.Calm;

            if (spawnedCytoplasm)
            {
                Destroy(spawnedCytoplasm.gameObject);
                spawnedCytoplasm = null;
            }

            StopSpawner();
            AssignConfig();
            ResetVolumes();

            runtime.EnsureCellStats(ID);
            UpdateCellStats();
        }

        void UpdateCellStats()
        {
            if (!runtime) return;

            runtime.EnsureCellStats(ID);
            var cs = runtime.CellStatsList[ID];
            cs.LifeFormsInCell = spawnedLifeForms.Count;
        }

        /// <summary>
        /// Toggles visibility of all spawned lifeforms (flora/fauna).
        /// Used to hide flora during shape drawing mode and restore after.
        /// </summary>
        public void SetLifeFormsActive(bool active)
        {
            for (int i = spawnedLifeForms.Count - 1; i >= 0; i--)
            {
                if (spawnedLifeForms[i])
                    spawnedLifeForms[i].SetActive(active);
            }
        }

        public void RegisterSpawnedObject(GameObject obj)
        {
            if (!obj) return;
            spawnedLifeForms.Add(obj);
            UpdateCellStats();
        }

        public void UnregisterSpawnedObject(GameObject obj)
        {
            if (spawnedLifeForms.Remove(obj))
                UpdateCellStats();
        }

        // ---------------------------------------------------------------------
        //  Live fauna registry - instances plus per-species counts (keyed by the
        //  FaunaConfigurationSO that defines the species). Fauna register on
        //  AssignLineage (spawner and reproduction paths both) and unregister in
        //  OnDestroy. This registry is the cell "sensing" its inhabitants - the
        //  fauna analogue of the prism density grid: counts feed the seeder
        //  (top up to seed floor) and reproduction (MaxLivePopulation backstop);
        //  instances feed predator prey-seeking (nearest live herbivore) and the
        //  predator seeding gate. Manager-spawned fauna (no lineage) are invisible
        //  to it - acceptable, those legacy populations never instantiate (§7).
        //  See Docs/ECOSYSTEM.md §6/§7.
        // ---------------------------------------------------------------------

        readonly Dictionary<FaunaConfigurationSO, int> liveFaunaCounts = new();
        readonly List<Fauna> liveFauna = new();

        /// <summary>Live population of the species defined by <paramref name="config"/> in this cell.</summary>
        public int GetLiveFaunaCount(FaunaConfigurationSO config) =>
            config && liveFaunaCounts.TryGetValue(config, out int c) ? c : 0;

        /// <summary>All lineage-registered live fauna in this cell (any species, any diet).</summary>
        public IReadOnlyList<Fauna> LiveFauna => liveFauna;

        /// <summary>
        /// THIS CELL's take on an authored fauna population number - a seed count
        /// (<c>InitialSpawnCount</c> / <c>PopulationSize</c>) or the hard cap
        /// (<c>MaxLivePopulation</c>) - after its SpawnProfile's
        /// <see cref="SpawnProfileSO.FaunaPopulationScale"/>. A cell with no profile, or the
        /// default scale of 1, returns the authored number untouched, so every biome that
        /// authors nothing is bit-for-bit unchanged.
        ///
        /// <para><b>Every producer must ask the CELL, never the config.</b> There are four
        /// (<c>RandomLifeSpawner</c>, <c>IntensityWiseLifeSpawner</c>, <c>Fauna.TryReproduce</c>
        /// and the freestyle <c>Microscene</c> conveyor), and which SPAWNER a biome runs is
        /// decided by an unrelated field - <c>CellTypeChoiceOptions.IntensityWise</c> silently
        /// swaps the class - so a density rule implemented in one spawner is dead code in
        /// exactly the modes that asked for it. The cell is the one thing all four already
        /// hold. A fifth producer that asks here gets the scalar for free; one that reads
        /// <c>cfg.MaxLivePopulation</c> directly silently opts a species out of it.</para>
        /// </summary>
        public int ResolveFaunaPopulation(int authored)
        {
            var profile = cellConfigData ? cellConfigData.SpawnProfile : null;
            return profile ? profile.ScaleFaunaPopulation(authored) : authored;
        }

        /// <summary>
        /// This cell's live cap for a species: <see cref="FaunaConfigurationSO.MaxLivePopulation"/>
        /// through <see cref="ResolveFaunaPopulation"/>. 0 stays 0 (uncapped).
        /// </summary>
        public int ResolveFaunaCap(FaunaConfigurationSO config) =>
            config ? ResolveFaunaPopulation(config.MaxLivePopulation) : 0;

        /// <summary>
        /// True when this species is already at or over this cell's live cap - the one place
        /// the "cap" comparison is written, so a producer cannot accidentally test the
        /// unscaled authored number. An uncapped species (0) is never full.
        /// </summary>
        public bool IsFaunaAtCap(FaunaConfigurationSO config)
        {
            int cap = ResolveFaunaCap(config);
            return cap > 0 && GetLiveFaunaCount(config) >= cap;
        }

        /// <summary>
        /// Live herbivores still eligible as prey - the prey signal for predator
        /// seeding (a real herbivore count, not the prism-mass proxy).
        /// </summary>
        public int GetLiveHerbivoreCount()
        {
            int n = 0;
            for (int i = 0; i < liveFauna.Count; i++)
            {
                var f = liveFauna[i];
                if (f && f.Diet == FaunaDiet.Herbivore && f.IsAlivePrey) n++;
            }
            return n;
        }

        public void RegisterLiveFauna(Fauna fauna)
        {
            if (!fauna || !fauna.SourceConfig) return;
            liveFaunaCounts.TryGetValue(fauna.SourceConfig, out int c);
            liveFaunaCounts[fauna.SourceConfig] = c + 1;
            liveFauna.Add(fauna);
        }

        public void UnregisterLiveFauna(Fauna fauna)
        {
            // `is null` guard only - a destroyed-but-non-null fauna must still be
            // removable from the registry during teardown.
            if (fauna is null || !fauna.SourceConfig) return;
            if (liveFaunaCounts.TryGetValue(fauna.SourceConfig, out int c) && c > 0)
                liveFaunaCounts[fauna.SourceConfig] = c - 1;
            liveFauna.Remove(fauna);
        }

        // ---------------------------------------------------------------------
        //  Live FLORA registry - the plant-side twin of the fauna registry above,
        //  and it exists for the same reason: flora now reproduce (Flora.TryReproduce),
        //  so the periodic spawner is no longer the only producer and "how many plants
        //  of this species are alive" has to be a fact the cell owns rather than
        //  something a producer guesses. Plants register on Flora.AssignLineage (both
        //  the spawner path and the reproduction path) and unregister in OnDestroy.
        //  A plant spawned with no config (a toy clone, a microscene release) carries
        //  no lineage and is invisible here - same rule fauna follow.
        //  See Docs/ECOSYSTEM.md §32.
        // ---------------------------------------------------------------------

        readonly Dictionary<FloraConfigurationSO, int> liveFloraCounts = new();

        /// <summary>Live plant count for the species defined by <paramref name="config"/> in this cell.</summary>
        public int GetLiveFloraCount(FloraConfigurationSO config) =>
            config && liveFloraCounts.TryGetValue(config, out int c) ? c : 0;

        /// <summary>
        /// THIS CELL's take on an authored flora population number - a seed count
        /// (<c>InitialSpawnCount</c> / <c>PopulationSize</c>) or the hard cap
        /// (<c>MaxLivePopulation</c>) - after its SpawnProfile's
        /// <see cref="SpawnProfileSO.FloraPopulationScale"/>.
        ///
        /// <para><b>Every producer must ask the CELL, never the config</b> - the same rule, for
        /// the same reason, as <see cref="ResolveFaunaPopulation"/>. Flora has FOUR producers
        /// (<c>RandomLifeSpawner</c>, <c>IntensityWiseLifeSpawner</c>, <c>Flora.TryReproduce</c>
        /// and the freestyle <c>Microscene</c> conveyor / Lifeform Matrix toy), and which
        /// SPAWNER a biome runs is decided by an unrelated field - <c>CellTypeChoiceOptions</c>
        /// <c>.IntensityWise</c> silently swaps the class - so a density rule implemented in one
        /// producer is dead code in exactly the modes that asked for it. The cell is the one
        /// thing all four already hold.</para>
        /// </summary>
        public int ResolveFloraPopulation(int authored)
        {
            var profile = cellConfigData ? cellConfigData.SpawnProfile : null;
            return profile ? profile.ScaleFloraPopulation(authored) : authored;
        }

        /// <summary>
        /// This cell's live cap for a flora species: <see cref="FloraConfigurationSO.MaxLivePopulation"/>
        /// through <see cref="ResolveFloraPopulation"/>. 0 stays 0 (uncapped).
        /// </summary>
        public int ResolveFloraCap(FloraConfigurationSO config) =>
            config ? ResolveFloraPopulation(config.MaxLivePopulation) : 0;

        /// <summary>
        /// True when this species is already at or over this cell's live plant cap - the one
        /// place the "cap" comparison is written, so a producer cannot accidentally test the
        /// unscaled authored number. An uncapped species (0) is never full.
        /// </summary>
        public bool IsFloraAtCap(FloraConfigurationSO config)
        {
            int cap = ResolveFloraCap(config);
            return cap > 0 && GetLiveFloraCount(config) >= cap;
        }

        public void RegisterLiveFlora(Flora flora)
        {
            if (!flora || !flora.SourceConfig) return;
            liveFloraCounts.TryGetValue(flora.SourceConfig, out int c);
            liveFloraCounts[flora.SourceConfig] = c + 1;
        }

        public void UnregisterLiveFlora(Flora flora)
        {
            // `is null` guard only - see UnregisterLiveFauna.
            if (flora is null || !flora.SourceConfig) return;
            if (liveFloraCounts.TryGetValue(flora.SourceConfig, out int c) && c > 0)
                liveFloraCounts[flora.SourceConfig] = c - 1;
        }

        void Initialize()
        {
            spawnedLifeForms.Clear();
            trackedBlocks.Clear();
            domainBlockCounts.Clear();
            // Packed counterpart of the old massTracked.Clear() (see ResetCell).
            PrismSpatialIndex.Instance?.ClearAllCellBindings(_volumeCellId);
            gridTracked.Clear();
            _replicatedDominantDomain = null;
            ResetVolumeAccounting();
            liveFaunaCounts.Clear();
            liveFloraCounts.Clear();
            liveFauna.Clear();
            // The gyroid colony's frontier is a POPULATION-level book of open octagons, so it
            // outlives any individual plant by design - which means only the cell can retire it.
            // Left behind, the next world grown here inherits the dead one's sites and plants
            // daughters into lattice that no longer exists (the Cell Selector swaps worlds in
            // the very scene this colony ships in). Keyed by cell, so this touches no other.
            GyroidColonyFrontier.Clear(this);
            SchwarzPColonyFrontier.Clear(this);
            SchwarzPTileRegistry.Clear(this);
            QuasicrystalColonyFrontier.Clear(this);
            QuasicrystalHeartRegistry.Clear(this);
            phase = CellPhase.Calm;

            // Bind runtime -> this cell
            runtime.Cell = this;
            runtime.EnsureCellStats(ID);

            // Elemental integration: any scene with a living cell gets the domain fauna buff
            // system — living fauna hearts empower their domain's vessels, platform-wide.
            DomainFaunaBuffSystem.EnsureExists(gameObject, gameData, runtime);

            AssignConfig();

            // AssignConfig can decline (a client that cannot yet know its intensity - see
            // IntensityChoiceReady). Everything below dereferences the config, so bail and let
            // OnInitializeGame - which fires on EVERY peer a full second after the config
            // broadcast - run this again with an answer.
            if (!cellConfigData)
            {
                postInitDeferred = true;
                return;
            }

            // SpawnVisuals must run before SetupDensityGrids: the density grids
            // are now sized to the cell's membrane radius, and MembraneRadius
            // reads the membrane GameObject that SpawnVisuals instantiates.
            using (LoadInsights.Measure(LoadInsightCategory.Environment,
                       $"Cell membrane+nucleus instantiate (cell {ID})"))
            {
                SpawnVisuals();
            }
            using (LoadInsights.Measure(LoadInsightCategory.Environment,
                       $"Cell density grid allocation (cell {ID})"))
            {
                SetupDensityGrids();
            }
            ResetVolumes();

            UpdateCellStats();

            // Finish a bootstrap the first-crystal path had to defer while it waited for the
            // config. Without this the cell would have a config but no cytoplasm and no spawner.
            if (postInitDeferred) InitilizePostFirstCellItem();
        }
        
        void InitilizePostFirstCellItem()
        {
            if (!cellConfigData)
            {
                CSDebug.LogWarning($"[Cell {ID}] Crystal spawned before Cell Initialized. Attempting lazy init.");
                Initialize();

                // Still no config - AssignConfig deferred an IntensityWise choice it could not
                // make yet. Do NOT latch postInitilized here: that is what made the deferral
                // permanent, leaving the cell with no spawner and no cytoplasm for the match.
                if (!cellConfigData)
                {
                    postInitDeferred = true;
                    return;
                }
            }

            postInitilized = true;
            postInitDeferred = false;

            SpawnCytoplasm();
            ApplyModifiers();
            StartSpawnerForMode();
        }

        // Set when the first-crystal bootstrap ran before this peer could choose a config.
        // Initialize() (OnInitializeGame, a full second after the config broadcast) finishes it.
        bool postInitDeferred;

        void OnCellItemUpdated()
        {
            if (postInitilized)
                return;
            InitilizePostFirstCellItem();
        }

        void AssignConfig()
        {
            // Sticky per scene: OnEnable nulls runtime.Config, so the first Initialize pass
            // rolls fresh - but repeat passes (lazy crystal init + OnInitializeGame both run
            // it) must NOT re-roll. With multiple configs a re-roll could swap the config
            // out from under an already-spawning prepopulated environment (e.g. the Yggdra
            // garden streaming in while the cell re-labels itself Blob), stranding ~950k of
            // environment volume under thresholds authored for an empty cell.
            if (runtime && runtime.Config) return;

            if (CellConfigs == null || CellConfigs.Count == 0)
            {
                CSDebug.LogError($"{nameof(Cell)}: No CellConfigs found to assign.");
                return;
            }

            // A connected CLIENT derives its IntensityWise index from a value only the server can
            // send it, and the choice above is STICKY - so choosing early is choosing wrong,
            // permanently. Bail without latching; the caller retries (see postInitDeferred).
            if (!IntensityChoiceReady)
            {
                CSDebug.LogWarning($"[Cell {ID}] IntensityWise config choice DEFERRED - the " +
                    "server's game config has not replicated to this client yet. Retrying on " +
                    "OnInitializeGame.");
                return;
            }

            var index = cellTypeChoiceOptions switch
            {
                CellTypeChoiceOptions.Random => Random.Range(0, CellConfigs.Count),
                CellTypeChoiceOptions.IntensityWise => IntensityIndex(),
                CellTypeChoiceOptions.EnvironmentFree => FirstEnvironmentFreeIndex(),
                _ => 0
            };

            runtime.Config = CellConfigs[index];

            // Seed the fauna release gate from the biome BEFORE any spawner can tick. A mode
            // that seals its cell (Ribcage) must not depend on its controller's OnNetworkSpawn
            // beating the cell's own bootstrap clock - AssignConfig is upstream of
            // StartSpawnerForMode by construction, so the seal is in place from the first tick.
            // Mode writes (Cell.FaunaReleaseTier) always win afterwards, and RestartSpawnerForMode
            // does not come back through here, so a release is never silently re-sealed.
            var assigned = CellConfigs[index];
            if (assigned && assigned.SpawnProfile)
                FaunaReleaseTier = assigned.SpawnProfile.InitialFaunaReleaseTier;
        }

        /// <summary>
        /// May this cell make its (sticky, unrepeatable) config choice yet? Only IntensityWise
        /// depends on replicated state; Random and EnvironmentFree are answerable from local data
        /// alone, and a server, a single-player scene or a scene with no NetworkManager is
        /// authoritative by definition.
        ///
        /// See <see cref="GameDataSO.GameConfigSynced"/> for what goes wrong without this: a
        /// client's cell bootstraps off its first crystal, which can beat the config broadcast, and
        /// silently builds a different intensity's arena than the host for the whole match.
        /// </summary>
        bool IntensityChoiceReady =>
            cellTypeChoiceOptions != CellTypeChoiceOptions.IntensityWise
            || gameData == null
            || gameData.GameConfigSynced
            || NetworkManager.Singleton == null
            || !NetworkManager.Singleton.IsListening
            || NetworkManager.Singleton.IsServer;

        /// <summary>
        /// The <c>CellConfigs</c> index for the selected intensity, floored at 1 and fail-loud on
        /// over-run. The floor matters because the intensity SOAP asset defaults to 0 (so a scene
        /// opened directly in the editor asks for index -1), and the warning matters because the
        /// clamp is otherwise silent - a mode whose SO_ArcadeGame offers four intensities but whose
        /// cell authors two would quietly serve the same arena for 3 and 4.
        /// </summary>
        int IntensityIndex()
        {
            int intensity = Mathf.Max(1, gameData.SelectedIntensity.Value);
            if (intensity > CellConfigs.Count)
                CSDebug.LogWarning($"[Cell {ID}] Intensity {intensity} selected but only " +
                    $"{CellConfigs.Count} CellConfigs are authored - clamping to the last. " +
                    "Author one config per SO_ArcadeGame.MaxIntensity.");
            return Mathf.Clamp(intensity - 1, 0, CellConfigs.Count - 1);
        }

        /// <summary>
        /// Index of the first config with no authored <c>EnvironmentPrefab</c>, or 0 when
        /// every config carries one. This is what makes entry to a freestyle scene cheap:
        /// the heavy prepopulated worlds are still listed (the Cell Selector toy offers
        /// them), they just are not paid for until the player asks.
        /// </summary>
        int FirstEnvironmentFreeIndex()
        {
            for (int i = 0; i < CellConfigs.Count; i++)
                if (CellConfigs[i] && CellConfigs[i].EnvironmentPrefab == null)
                    return i;

            CSDebug.LogWarning($"[Cell {ID}] Choice mode EnvironmentFree, but every config in " +
                               "CellConfigs authors an EnvironmentPrefab - booting index 0 and paying " +
                               "its build cost. Add an environment-free config (e.g. Blob) to the list.");
            return 0;
        }

        void SetupDensityGrids()
        {
            // Size the density grids to the cell's SENSE radius (membrane radius by
            // default, or a CellConfig override for large arenas like the Skim Race track).
            // With a 1200m membrane the old fixed cube saw only ~14% of the cell - outer
            // mass was invisible to FindDensestRegion so fauna never sought it. See
            // Docs/DENSITY_PARTITIONING_AUDIT.md.
            float membraneRadius = SenseRadius;
            float worldDiameter = membraneRadius > 0f
                ? membraneRadius * 2f
                : 2400f; // fallback when the membrane prefab is missing
            Vector3 cellCenter = transform.position;

            // Dispose any existing grids before replacing them - each holds
            // persistent NativeArrays, and Initialize() can run more than once
            // across a session (e.g. replay).
            foreach (var existing in countGrids.Values)
                existing?.Dispose();

            countGrids.Clear();
            foreach (Domains t in s_playableDomains)
                countGrids[t] = new BlockCountDensityGrid(t, cellCenter, worldDiameter);

            // Blue-keyed grid accumulates every block regardless of domain so
            // GetDensestRegionAnyDomain() can answer aggression-2 fauna's "head toward
            // nearest centroid" goal - friendly + enemy mass both count. Blue is the
            // "no specific team" sentinel; this grid does double duty as the wildcard.
            countGrids[Domains.Blue] = new BlockCountDensityGrid(Domains.Blue, cellCenter, worldDiameter);
        }

        /// <summary>
        /// Spawn the config's membrane, environment, and nucleus.
        /// </summary>
        /// <param name="spawnEnvironment">
        /// True (boot) also kicks off the deferred environment build. A runtime cell swap
        /// passes false and calls <see cref="BuildEnvironmentNow"/> itself AFTER the density
        /// grids are rebuilt - on boot the build is deferred past scene start so the grids are
        /// always ready first, but an immediate build would otherwise register its first prisms
        /// into grids that <see cref="SetupDensityGrids"/> is about to dispose.
        /// </param>
        void SpawnVisuals(bool spawnEnvironment = true)
        {
            if (!cellConfigData) return;

            // Every spawn here is guarded for repeat Initialize passes (the lazy-init nudge in
            // InitilizePostFirstCellItem, then OnInitializeGame). The fields hold ONE of each and
            // every cleanup path - ResetCell, the swap retire, the toy re-parent - reads only the
            // field, so a second Instantiate orphans the first: an untracked membrane/nucleus
            // rendering on top of the real one that nothing can ever collect. Same reason a
            // duplicated 70k-prism environment would double the cell's mass.
            if (cellConfigData.MembranePrefab != null && membrane == null)
                membrane = Instantiate(cellConfigData.MembranePrefab, transform.position, Quaternion.identity);

            if (spawnEnvironment && cellConfigData.EnvironmentPrefab != null && environment == null)
                SpawnEnvironment();

            if (cellConfigData.NucleusPrefab == null || nucleus != null) return;
            nucleus = Instantiate(cellConfigData.NucleusPrefab, transform.position, Quaternion.identity);
            nucleus.transform.localScale *= nucleusScaleMultiplier;
            ApplyNucleusWorldRadius(); // honor any radius a mode requested before the nucleus existed
            ApplyNucleusMesh();        // ...or a replacement boundary mesh (non-spherical court)
            RefreshNucleusControlRadius();
        }

        /// <summary>
        /// Spawn the config's authored structural environment (e.g. the Atlantis garden the
        /// Yggdra cell begins with). Called on the prefab ASSET, mirroring SegmentSpawner:
        /// SpawnableBase.Spawn() creates its own container GameObject, which we parent to the
        /// cell so the environment lives and dies with it. Prisms flow through the canonical
        /// PrismTrailBuilder lay path, register with this cell's volume/density bookkeeping
        /// like any other mass, and are ordinary prey/territory thereafter - prepopulation is
        /// a head start for the ecosystem, not a parallel system. In gate-less scenes the
        /// EnvironmentLoadVeil holds the screen (with the standard prism/percent readout)
        /// until the build settles - the world is never half-built under live play.
        /// </summary>
        void SpawnEnvironment()
        {
            // Edit mode builds synchronously, immediately.
            if (!Application.isPlaying)
            {
                BuildEnvironmentNow();
                return;
            }

            // Play mode: the game connecting screens hold a QUIESCENT, fully-loaded scene -
            // that is why gated minigame builds are smooth. A gate-less scene (Menu_Main) is
            // still BOOTING when the cell initializes: the Netcode vessel-spawn chain, eager
            // Relay/session creation, presence-lobby joins, and audio-bank loads all need
            // responsive frames, and they share the engine's async budget with the batched
            // prism instantiates (building during boot starved audio into underruns and
            // wedged a clone batch mid-integration). So defer until the scene reports ready
            // (local player pair initialized - the same beat OnClientReady fires on) with a
            // hard deadline, THEN raise the veil and build over a settled scene. The
            // environment field is pre-claimed so a repeat Initialize pass can't double-book.
            environment = gameObject;
            _deferredEnvironmentBuild = StartCoroutine(DeferredEnvironmentBuild());
        }

        // Handle on the boot-time deferred build so a runtime cell swap can cancel a build
        // that has not started yet (otherwise it would fire after the swap and stack a
        // second environment on top of the new one).
        Coroutine _deferredEnvironmentBuild;

        IEnumerator DeferredEnvironmentBuild()
        {
            float deadline = Time.unscaledTime + 12f;
            while (gameData != null && gameData.LocalPlayer == null && Time.unscaledTime < deadline)
                yield return null;
            // A settle beat after readiness so spawn-chain tail work (camera snap, autopilot
            // activation, HUD fades) clears the frame before the build takes the gate.
            yield return new WaitForSecondsRealtime(0.75f);
            BuildEnvironmentNow();
        }

        void BuildEnvironmentNow()
        {
            // Cleared unconditionally: a swap into a config with no environment (or none at all)
            // must not leave the previous world's beds addressable.
            ClearPlantingSites();

            if (!cellConfigData || cellConfigData.EnvironmentPrefab == null) return;
            using (LoadInsights.Measure(LoadInsightCategory.Environment,
                       $"Cell environment spawn (cell {ID}, {cellConfigData.EnvironmentPrefab.name})"))
            {
                // Raised BEFORE Spawn() so the first lay slice sees the gate's boosted budget.
                if (Application.isPlaying)
                    EnvironmentLoadVeil.Hold(cellConfigData.CellName);
                environment = cellConfigData.EnvironmentPrefab.Spawn(Mathf.Max(1, cellConfigData.EnvironmentIntensity));
                if (environment == null) return;
                environment.transform.SetParent(transform, false);
                environment.transform.localPosition = Vector3.zero;
                environment.transform.localRotation = Quaternion.identity;
                AdoptPlantingSites();
            }
        }

        // ---------------------------------------------------------------------
        //  Planting sites - an authored GARDEN environment tells the cell where it
        //  prepared ground; the ordinary flora spawner plants there instead of on a
        //  random membrane shell. The environment never spawns a lifeform itself:
        //  the Cell owns the ecology, so the sites flow into the SAME spawn path
        //  every other flora uses and the plants are ordinary food-web citizens.
        // ---------------------------------------------------------------------

        readonly List<FloraPlantingSite> _plantingSites = new();
        int _nextPlantingSite;

        // Sites bucketed by ground kind, each with its own cursor, so a species that prefers
        // basket ground walks the baskets rather than scanning past every terrace bed - and two
        // species preferring different ground never advance each other's rotation.
        readonly Dictionary<FloraSiteKind, List<FloraPlantingSite>> _sitesByKind = new();
        readonly Dictionary<FloraSiteKind, int> _kindCursor = new();
        int _kindRotation;
        static readonly FloraSiteKind[] SiteKinds =
        {
            FloraSiteKind.Bed, FloraSiteKind.Climb, FloraSiteKind.Basket,
            FloraSiteKind.Water, FloraSiteKind.Ledge,
        };

        /// <summary>True when this cell's environment prepared ground for planting.</summary>
        public bool HasPlantingSites => _plantingSites.Count > 0;

        /// <summary>
        /// True while an authored environment has been claimed but not yet built - the boot-path
        /// deferred build (<see cref="DeferredEnvironmentBuild"/>) pre-claims the field with this
        /// cell's own GameObject as a double-book guard. The flora spawner waits on it so plants
        /// seed into a world that exists (and into its prepared beds), instead of dispersing over
        /// empty space seconds before the garden arrives underneath them.
        /// </summary>
        public bool IsEnvironmentBuildPending => environment == gameObject;

        /// <summary>
        /// The next prepared planting spot in WORLD space, walked round-robin. The ring WRAPS
        /// rather than exhausting: a bed whose plant was grazed to nothing is prepared ground
        /// again, so the garden regrows where it was planted. Returns false (and the caller
        /// falls back to the legacy shell dispersal) when the environment prepared none.
        /// </summary>
        public bool TryTakePlantingSite(out Vector3 position, out Vector3 up) =>
            TryTakePlantingSite(FloraSiteKind.Any, out position, out up);

        /// <summary>
        /// The next prepared spot whose ground is one of <paramref name="preferred"/>. A species
        /// that prefers ground the garden doesn't have falls back to any site rather than never
        /// planting - a preference is a preference, not a requirement.
        /// </summary>
        public bool TryTakePlantingSite(FloraSiteKind preferred, out Vector3 position, out Vector3 up)
        {
            position = default;
            up = Vector3.up;
            if (_plantingSites.Count == 0) return false;

            if (preferred != FloraSiteKind.None && preferred != FloraSiteKind.Any &&
                TryTakeFromKinds(preferred, out var match))
            {
                Project(match, out position, out up);
                return true;
            }

            var site = _plantingSites[_nextPlantingSite % _plantingSites.Count];
            _nextPlantingSite++;
            Project(site, out position, out up);
            return true;
        }

        void Project(in FloraPlantingSite site, out Vector3 position, out Vector3 up)
        {
            position = transform.TransformPoint(site.Position);
            up = transform.TransformDirection(site.Up);
        }

        /// <summary>
        /// Round-robin across the preferred kinds AND within each kind: the per-kind cursors
        /// advance together so a species asking for Bed|Ledge alternates between them instead of
        /// draining one. Returns false when the garden prepared none of the preferred kinds.
        /// </summary>
        bool TryTakeFromKinds(FloraSiteKind preferred, out FloraPlantingSite site)
        {
            site = default;
            int matched = 0;
            // Deterministic starting offset that advances per call, so successive plants of the
            // same species rotate through the preferred kinds rather than always draining the
            // first. Its own counter - advancing the generic cursor here would make a
            // preference-matched plant silently skip a site for the species that use the
            // fallback.
            int offset = _kindRotation++;

            for (int pass = 0; pass < SiteKinds.Length; pass++)
            {
                var kind = SiteKinds[(offset + pass) % SiteKinds.Length];
                if ((preferred & kind) == 0) continue;
                if (!_sitesByKind.TryGetValue(kind, out var list) || list.Count == 0) continue;

                int cursor = _kindCursor.TryGetValue(kind, out var c) ? c : 0;
                site = list[cursor % list.Count];
                _kindCursor[kind] = cursor + 1;
                matched++;
                break;
            }
            return matched > 0;
        }

        void AdoptPlantingSites()
        {
            ClearPlantingSites();

            if (cellConfigData.EnvironmentPrefab is not CellEnvironmentSpawnableBase garden) return;
            var sites = garden.PlantingSites;
            if (sites is not { Count: > 0 }) return;

            _plantingSites.AddRange(sites);

            // Deal the sites in a fixed but shuffled order (seeded off the cell so a client
            // can't diverge): planting walks the list, and generation order groups sites by
            // structure - unshuffled, the first seeding batch would fill one terrace solid
            // and leave the rest bare until much later.
            var rng = new System.Random(ID * 7919 + _plantingSites.Count);
            for (int i = _plantingSites.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (_plantingSites[i], _plantingSites[j]) = (_plantingSites[j], _plantingSites[i]);
            }

            // Bucket the (already shuffled) sites by ground kind for the preference path.
            foreach (var site in _plantingSites)
            {
                if (!_sitesByKind.TryGetValue(site.Kind, out var list))
                    _sitesByKind[site.Kind] = list = new List<FloraPlantingSite>();
                list.Add(site);
            }
        }

        void ClearPlantingSites()
        {
            _plantingSites.Clear();
            _sitesByKind.Clear();
            _kindCursor.Clear();
            _nextPlantingSite = 0;
            _kindRotation = 0;
        }

        // =====================================================================
        //  Runtime cell swap - the freestyle Cell Selector toy's one entry point
        // =====================================================================

        /// <summary>
        /// The configs this cell can be. Read-only: the Cell owns the environment, so the
        /// Cell Selector toy READS this rotation instead of authoring a duplicate list
        /// (CLAUDE.md - "the Cell owns the environment, minigames/toys don't build parallel
        /// systems").
        /// </summary>
        public IReadOnlyList<CellConfigDataSO> AvailableConfigs => CellConfigs;

        /// <summary>True while a <see cref="RequestCellSwap"/> is retiring + rebuilding.</summary>
        public bool IsSwappingConfig => _swapping;

        /// <summary>
        /// The first config with no authored <c>EnvironmentPrefab</c> — the world
        /// <see cref="CellTypeChoiceOptions.EnvironmentFree"/> boots into, chosen because an
        /// authored environment costs a multi-second veiled build and entering Menu_Main should
        /// not. Null when every config authors an environment.
        ///
        /// <para><b>Environment-free is NOT the same as BARE</b>, and conflating them shipped a
        /// bug the moment a second environment-free config existed. This property answers "what
        /// is cheap to BUILD" — it says nothing about what the cell then grows. The Lattice cell
        /// authors no environment at all (so it boots instantly, correctly) and then grows a
        /// 21,600-prism forest out of eight seeds. Anything that wants an EMPTY WORLD rather than
        /// a cheap load wants <see cref="BareCanvasConfig"/>.</para>
        /// </summary>
        public CellConfigDataSO EnvironmentFreeConfig
        {
            get
            {
                if (CellConfigs == null) return null;
                for (int i = 0; i < CellConfigs.Count; i++)
                    if (CellConfigs[i] && CellConfigs[i].EnvironmentPrefab == null)
                        return CellConfigs[i];
                return null;
            }
        }

        /// <summary>
        /// The first config that grows NOTHING: no authored <c>EnvironmentPrefab</c> <b>and</b> a
        /// <c>SpawnProfile</c> that lists no flora and no fauna. This is the empty world — what
        /// the Wanderway run hands the cell so a wander happens in open space instead of inside
        /// a world the player is trying to leave.
        ///
        /// <para>It is a PREDICATE over the authored data rather than a new serialized field, so
        /// there is no reference to forget to wire and no way for a cell to claim a canvas that
        /// is not actually bare. Falls back to <see cref="EnvironmentFreeConfig"/> so a cell that
        /// authors no bare config still gets the cheapest world it has rather than nothing —
        /// degraded, never broken.</para>
        ///
        /// <para>Split out from <see cref="EnvironmentFreeConfig"/> when the Lattice cell landed:
        /// it is environment-free (cheap to build) but the opposite of bare (it grows a forest),
        /// so the single "first config with no EnvironmentPrefab" test stopped meaning one thing.
        /// See Docs/ECOSYSTEM.md §36.10.</para>
        /// </summary>
        public CellConfigDataSO BareCanvasConfig
        {
            get
            {
                if (CellConfigs == null) return null;
                for (int i = 0; i < CellConfigs.Count; i++)
                {
                    var cfg = CellConfigs[i];
                    if (!cfg || cfg.EnvironmentPrefab != null) continue;

                    var profile = cfg.SpawnProfile;
                    // No profile at all is as bare as it gets.
                    if (!profile) return cfg;
                    if (profile.SupportedFloras is { Count: > 0 }) continue;
                    if (profile.SupportedFaunas is { Count: > 0 }) continue;
                    return cfg;
                }
                return EnvironmentFreeConfig;
            }
        }

        bool _swapping;

        /// <summary>
        /// Become <paramref name="config"/>: retire the current world and rebuild from the new
        /// config. This is the opt-in half of <see cref="CellTypeChoiceOptions.EnvironmentFree"/>
        /// - freestyle scenes boot empty (fast) and the player pays a load only when they ask
        /// for a world, through the Cell Selector toy.
        ///
        /// Re-selecting the SAME config is legal and meaningful: it is the reset (clear the
        /// world, grow it back fresh).
        ///
        /// <b>Continuity of existence (platform law):</b> the retiring world does not pop out.
        /// Everything the cell owns is gathered under one root that SUCTIONS to a point over
        /// <see cref="retireSuctionSeconds"/> - the same sanctioned transition the microscene
        /// conveyor uses to transport its stock - and is only released once it is gone from
        /// sight. The new world then streams back in behind an <see cref="EnvironmentLoadVeil"/>,
        /// blooming prism by prism through the canonical lay path.
        ///
        /// <b>Mass conservation:</b> this is not decay. Nothing here is on a clock, no prism
        /// ages out, and no population is culled to hit a number. A cell swap is an explicit,
        /// player-initiated world change - the same class of event as a scene load, which has
        /// always ended a cell's mass - and it is the ONLY thing that removes this mass.
        /// See Docs/ECOSYSTEM.md §19.
        /// </summary>
        /// <param name="config">The config to become. Must be non-null.</param>
        /// <param name="clearLooseTrailMass">
        /// Also retire the POOLED prisms the cell tracks (the vessels' accumulated trail) -
        /// the "reset the scene" half of the toy. Instantiated mass that belongs to a closed
        /// toy system (the Wanderway conveyor transports its own fixed stock) is never touched
        /// either way.
        /// </param>
        /// <returns>False when the swap could not start (no config, edit mode, already swapping).</returns>
        public bool RequestCellSwap(CellConfigDataSO config, bool clearLooseTrailMass = true)
        {
            if (!config)
            {
                CSDebug.LogWarning($"[Cell {ID}] RequestCellSwap called with a null config - ignored.");
                return false;
            }
            if (!Application.isPlaying) return false;
            if (_swapping) return false;
            if (!runtime)
            {
                CSDebug.LogWarning($"[Cell {ID}] RequestCellSwap needs a CellRuntimeDataSO - ignored.");
                return false;
            }

            StartCoroutine(SwapCellConfigRoutine(config, clearLooseTrailMass));
            return true;
        }

        IEnumerator SwapCellConfigRoutine(CellConfigDataSO config, bool clearLooseTrailMass)
        {
            _swapping = true;
            CSDebug.Log($"[Cell {ID}] Cell swap → {config.CellName} " +
                        $"(environment: {(config.EnvironmentPrefab ? config.EnvironmentPrefab.name : "none")}).");

            // A boot-time deferred build that has not fired yet would otherwise land AFTER
            // the swap and stack a second environment on the new world.
            if (_deferredEnvironmentBuild != null)
            {
                StopCoroutine(_deferredEnvironmentBuild);
                _deferredEnvironmentBuild = null;
            }

            // Stop producing before retiring, or the spawner seeds lifeforms into a world
            // that is already suctioning away.
            StopSpawner();

            // Trails dereference their prisms without null guards (Trail.LookAhead / Project,
            // the Squirrel's TrailFollower), so every vessel must let go of the retiring mass
            // BEFORE it leaves. Pen-up too: nobody lays new trail into a world being replaced.
            SetVesselTrailsDetached(pauseSpawners: true);

            var retiring = RetireWorldIntoSuctionRoot(clearLooseTrailMass, out var pooledRetiring);

            // ── Suction (continuity law) ──────────────────────────────────────
            float elapsed = 0f;
            float duration = retireSuctionSeconds > 0.01f ? retireSuctionSeconds : DefaultRetireSuctionSeconds;
            while (elapsed < duration && retiring)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                // Smoothstep out, so the world eases away rather than snapping small.
                float eased = 1f - (t * t * (3f - 2f * t));
                retiring.transform.localScale = Vector3.one * Mathf.Max(SuctionFloorScale, eased);
                yield return null;
            }

            // Pooled prisms go back to their pool (destroying one corrupts the pool's
            // accounting). Detach with worldPositionStays:FALSE so the suction factor is not
            // baked into their localScale on the way out.
            for (int i = 0; i < pooledRetiring.Count; i++)
            {
                var block = pooledRetiring[i];
                if (!block) continue;
                block.transform.SetParent(null, false);
                block.ReturnToPool();
            }
            pooledRetiring.Clear();

            yield return ReleaseRetiredWorld(retiring);

            // ── Bookkeeping reset (the same set ResetCell clears) ─────────────
            // After the drain: every prism's OnDestroy routes through RemoveBlock, so
            // clearing first would just be undone (and would mutate mid-iteration).
            trackedBlocks.Clear();
            domainBlockCounts.Clear();
            PrismSpatialIndex.Instance?.ClearAllCellBindings(_volumeCellId);
            gridTracked.Clear();
            _replicatedDominantDomain = null;
            ResetVolumeAccounting();
            liveFaunaCounts.Clear();
            liveFloraCounts.Clear();
            liveFauna.Clear();
            // The gyroid colony's frontier is a POPULATION-level book of open octagons, so it
            // outlives any individual plant by design - which means only the cell can retire it.
            // Left behind, the next world grown here inherits the dead one's sites and plants
            // daughters into lattice that no longer exists (the Cell Selector swaps worlds in
            // the very scene this colony ships in). Keyed by cell, so this touches no other.
            GyroidColonyFrontier.Clear(this);
            SchwarzPColonyFrontier.Clear(this);
            SchwarzPTileRegistry.Clear(this);
            QuasicrystalColonyFrontier.Clear(this);
            QuasicrystalHeartRegistry.Clear(this);
            phase = CellPhase.Calm;
            _nucleusControlRadiusSqr = 0f;

            // ── Become the new config ────────────────────────────────────────
            // Direct write: AssignConfig is deliberately sticky (it must never re-roll under
            // a streaming environment), so a swap is the one sanctioned re-assignment.
            runtime.Config = config;

            // Membrane + nucleus first (the grids are sized off the membrane), THEN the grids,
            // THEN the environment - an immediate build would otherwise file its first prisms
            // into grids SetupDensityGrids is about to dispose.
            SpawnVisuals(spawnEnvironment: false);
            SetupDensityGrids();
            ResetVolumes();
            SpawnCytoplasm();
            ApplyModifiers();

            runtime.EnsureCellStats(ID);
            UpdateCellStats();

            // The veil raised inside BuildEnvironmentNow holds the screen while the world
            // streams in - the same treatment a boot-time build gets.
            BuildEnvironmentNow();

            // Let the lay drain before life returns, so flora/fauna seed into a finished world
            // rather than racing it. The deadline mirrors the veil's own stall cap: a wedged
            // build must never wedge the cell with it.
            float layDeadline = Time.unscaledTime + 240f;
            while (PrismTrailBuilder.IsLayingInProgress && Time.unscaledTime < layDeadline)
                yield return null;

            StartSpawnerForMode();
            SetVesselTrailsDetached(pauseSpawners: false);

            _swapping = false;
            CSDebug.Log($"[Cell {ID}] Cell swap complete → {config.CellName}.");
        }

        /// <summary>
        /// Drain the suctioned world over frames. A 35k-prism world destroyed in one frame is
        /// a multi-second freeze (every <c>Prism.OnDestroy</c> unregisters from the spatial
        /// index and unbinds from this cell), and it would land right where the player is
        /// waiting to see the new world. The root is already suctioned to a point, so nothing
        /// is visible while it drains.
        /// </summary>
        IEnumerator ReleaseRetiredWorld(GameObject retiring)
        {
            if (!retiring) yield break;

            const int PrismsPerFrame = 500;
            var prisms = retiring.GetComponentsInChildren<Prism>(true);
            for (int i = 0; i < prisms.Length; i++)
            {
                if (prisms[i]) Destroy(prisms[i].gameObject);
                if ((i + 1) % PrismsPerFrame == 0) yield return null;
            }

            // Whatever is left (lifeform bodies, the old membrane / nucleus / cytoplasm) dies
            // with the root.
            if (retiring) Destroy(retiring);
            yield return null; // let the deferred destroys land before the bookkeeping reset
        }

        /// <summary>Never exactly zero - a zero-scale parent makes every child's lossyScale degenerate.</summary>
        const float SuctionFloorScale = 0.002f;

        /// <summary>
        /// Gather everything this cell owns under one root so a SINGLE transform write can
        /// suction all of it. The authored environment is one container of tens of thousands
        /// of prisms, so the expensive case costs one re-parent; lifeforms and (optionally)
        /// pooled trail prisms are re-parented individually.
        /// </summary>
        GameObject RetireWorldIntoSuctionRoot(bool clearLooseTrailMass, out List<Prism> pooledRetiring)
        {
            var root = new GameObject($"Cell{ID}_RetiringWorld");
            root.transform.SetPositionAndRotation(transform.position, Quaternion.identity);
            var rootT = root.transform;

            // The authored environment. `environment == gameObject` is SpawnEnvironment's
            // pre-claim sentinel for "a build is pending" - there is nothing to retire.
            if (environment && environment != gameObject)
                environment.transform.SetParent(rootT, true);
            environment = null;

            for (int i = spawnedLifeForms.Count - 1; i >= 0; i--)
            {
                var lifeForm = spawnedLifeForms[i];
                if (lifeForm) lifeForm.transform.SetParent(rootT, true);
            }
            spawnedLifeForms.Clear();

            // Loose POOLED mass - the vessels' accumulated trail. A pooled prism is the one
            // that carries a return handler; instantiated mass (the environment, flora health
            // prisms, and a toy conveyor's transported stock) has none. Skipping the latter is
            // what leaves the Wanderway's closed, conserved belt intact through a cell swap.
            pooledRetiring = new List<Prism>();
            if (clearLooseTrailMass)
            {
                // Snapshot before re-parenting: nothing in SetParent should touch the
                // dictionary, but iterating a live collection while moving its keys around
                // the scene is not a bet worth taking.
                var tracked = new List<Prism>(trackedBlocks.Keys);
                for (int i = 0; i < tracked.Count; i++)
                {
                    var block = tracked[i];
                    if (!block || block.OnReturnToPool == null) continue;
                    pooledRetiring.Add(block);
                    block.transform.SetParent(rootT, true);
                }
            }

            // The old config's own visuals. These are instantiated un-parented (world-space
            // siblings of the cell), so they need explicit collection.
            if (membrane) membrane.transform.SetParent(rootT, true);
            membrane = null;
            if (nucleus) nucleus.transform.SetParent(rootT, true);
            nucleus = null;
            if (spawnedCytoplasm) spawnedCytoplasm.transform.SetParent(rootT, true);
            spawnedCytoplasm = null;

            return root;
        }

        /// <summary>
        /// Release every vessel's trail bookkeeping (and optionally pen-up its spawner) so no
        /// <see cref="Trail"/> or follower holds a reference to mass that is about to leave.
        /// <c>ClearTrails</c> only drops the bookkeeping - it never removes a prism - so this
        /// is not a mass sink.
        ///
        /// NOTE: <c>SetSpawnerPaused</c> is a single last-writer-wins flag, also used by the
        /// painting toy's pen-up between strokes. A swap taken mid-painting therefore un-pens a
        /// run that was between strokes; the runner re-asserts the pen at its next stroke
        /// boundary, so the cost is bounded to a short stretch of unwanted trail.
        /// </summary>
        void SetVesselTrailsDetached(bool pauseSpawners)
        {
            if (gameData?.Players == null) return;

            foreach (var player in gameData.Players)
            {
                var status = player?.Vessel?.VesselStatus;
                if (status == null) continue;
                // Interface refs skip Unity's null overload, so a vessel destroyed mid-swap
                // is still non-null by reference - test the object itself (same guard the toy
                // base uses for its exit gate).
                if (status is UnityEngine.Object destroyed && !destroyed) continue;

                var prismController = status.VesselPrismController;
                if (!prismController) continue;

                prismController.SetSpawnerPaused(pauseSpawners);
                if (!pauseSpawners) continue;

                status.AttachedPrism = null;
                prismController.ClearTrails();
            }
        }

        /// <summary>
        /// Re-measure the nucleus' WORLD radius (renderer bounds - mesh-agnostic, so
        /// morphed court meshes and world-radius requests are honored) and cache it
        /// as the node-control zone boundary. Called whenever the nucleus spawns or
        /// is resized/re-meshed; a missing nucleus clears the zone (legacy behavior).
        /// </summary>
        void RefreshNucleusControlRadius()
        {
            _nucleusControlRadiusSqr = 0f;
            NucleusVisualWorldRadius = MeasureNucleusWorldRadius();   // geometry, always

            // A nucleus a mode borrowed as play geometry is a wall, not a claim - no control
            // zone, so the cell keeps its whole-cell control + diet semantics. See
            // NucleusIsControlZone.
            if (!_nucleusIsControlZone) return;
            if (NucleusVisualWorldRadius <= 1e-3f) return;

            _nucleusControlRadiusSqr = NucleusVisualWorldRadius * NucleusVisualWorldRadius;
        }

        float MeasureNucleusWorldRadius()
        {
            if (nucleus == null) return 0f;

            var r = nucleus.GetComponentInChildren<Renderer>();
            if (r == null) return 0f;

            Vector3 ext = r.bounds.extents;
            return Mathf.Max(ext.x, Mathf.Max(ext.y, ext.z));
        }

        /// <summary>
        /// Resize the nucleus marker to a target WORLD radius (mesh-agnostic, via renderer bounds, so
        /// it works regardless of the prefab mesh's base size). Lets a mode repurpose the nucleus as
        /// its play boundary - e.g. Astro League uses it for the Sphere-shaped court (see
        /// <see cref="SetNucleusMesh"/> for the flat-walled polytope courts).
        /// Safe to call before the nucleus spawns: the target is cached and applied in SpawnVisuals.
        /// </summary>
        public void SetNucleusWorldRadius(float worldRadius)
        {
            if (worldRadius <= 0f) return;
            _pendingNucleusWorldRadius = worldRadius;
            ApplyNucleusWorldRadius();
            RefreshNucleusControlRadius();
        }

        void ApplyNucleusWorldRadius()
        {
            if (nucleus == null || _pendingNucleusWorldRadius <= 0f) return;

            var r = nucleus.GetComponentInChildren<Renderer>();
            if (r == null) return;

            Vector3 ext = r.bounds.extents;
            float current = Mathf.Max(ext.x, Mathf.Max(ext.y, ext.z));
            if (current < 1e-4f) return;

            nucleus.transform.localScale *= _pendingNucleusWorldRadius / current;
        }

        /// <summary>
        /// Replace the nucleus MESH so a mode can repurpose the nucleus as a NON-spherical play boundary
        /// (e.g. Astro League's ricochet courts - box/octagon/etc.), keeping the prefab's material so the
        /// glowing-cage look carries over. The mesh must be in world units centered on the origin; the
        /// nucleus renders it at unit scale. Race-proof: cached and applied in SpawnVisuals if the
        /// nucleus hasn't spawned yet. Pass null to leave the prefab mesh in place.
        /// </summary>
        public void SetNucleusMesh(Mesh mesh)
        {
            _pendingNucleusMesh = mesh;
            ApplyNucleusMesh();
            RefreshNucleusControlRadius();
        }

        void ApplyNucleusMesh()
        {
            if (nucleus == null || _pendingNucleusMesh == null) return;

            var mf = nucleus.GetComponentInChildren<MeshFilter>();
            if (mf == null) return;

            // sharedMesh on an instance only repoints THIS filter (doesn't mutate the prefab asset).
            mf.sharedMesh = _pendingNucleusMesh;
            // The mesh is already world-sized, so render at unit scale - overrides the prefab's base
            // scale and any world-radius request (which targeted the original sphere mesh's bounds).
            nucleus.transform.localScale = Vector3.one;
        }

        void ResetVolumes()
        {
            teamVolumes[Domains.Jade] = 0;
            teamVolumes[Domains.Ruby] = 0;
            teamVolumes[Domains.Gold] = 0;
            teamVolumes[Domains.Blue] = 0;
        }

        void ApplyModifiers()
        {
            var cfg = cellConfigData;
            if (!cfg || cfg.CellModifiers == null) return;

            foreach (var modifier in cfg.CellModifiers)
                modifier.Apply(this);
        }

        void SpawnCytoplasm()
        {
            if (!cellConfigData || cellConfigData.CytoplasmPrefab == null) return;

            // Guarded for repeat passes, exactly like the environment spawn: the field holds ONE
            // cytoplasm and every cleanup path (ResetCell, the swap retire, the toy re-parent) reads
            // only that field, so a second Instantiate would orphan the first - an untracked
            // SnowChanger drifting in the scene forever, invisible to the Cell that made it. The
            // Cell owns its visuals; owning them means never losing one.
            if (spawnedCytoplasm) return;

            using (LoadInsights.Measure(LoadInsightCategory.Environment,
                       $"Cytoplasm (SnowChanger) instantiate+init (cell {ID})"))
            {
                spawnedCytoplasm = Instantiate(cellConfigData.CytoplasmPrefab, transform.position, Quaternion.identity);
                spawnedCytoplasm.SetOrigin(transform.position);
                spawnedCytoplasm.Initialize();
            }
        }

        void StartSpawnerForMode()
        {
            StopSpawner();

            activeSpawner = cellTypeChoiceOptions == CellTypeChoiceOptions.IntensityWise
                ? intensitySpawner
                : randomSpawner;

            activeSpawner.Start(this, cellConfigData, runtime, gameData);

            LoadInsights.Mark($"Flora/fauna spawner started (cell {ID}, {activeSpawner.GetType().Name})");
            CSDebug.Log($"<color=green>[Cell {ID}] Spawner started: {activeSpawner.GetType().Name}</color>");
        }

        void StopSpawner()
        {
            if (activeSpawner == null) return;
            activeSpawner.Stop(this);
            activeSpawner = null;
            CSDebug.Log($"<color=yellow>[Cell {ID}] Spawner stopped</color>");
        }

        /// <summary>
        /// Stops and restarts the life spawner so its fixed-period fauna clock
        /// re-aligns to NOW. Used by modes whose scoring rides the fauna spawn cycle
        /// (Brood Rush realigns the 30s wave clock to the GO of the countdown -
        /// the spawner otherwise starts when the first crystal registers, which is
        /// during the ready screen). No-op until the cell has post-initialized.
        /// Note: a profile with flora re-runs its initial flora batch on restart -
        /// intended callers are fauna-only biomes.
        /// </summary>
        public void RestartSpawnerForMode()
        {
            if (!postInitilized || !cellConfigData) return;
            StartSpawnerForMode();
        }

        internal Transform GetCrystalTransform()
        {
            if (runtime != null && runtime.TryGetLocalCrystal(out var crystal) && crystal)
                return crystal.transform;

            CSDebug.LogWarning($"[Cell {ID}] No crystal found!");
            return null;
        }

        /// <summary>
        /// Sentinel default for AddBlock/RemoveBlock's spatialIndexId parameter:
        /// "resolve from block.SpatialIndexId". PrismSpatialIndex.BindCell passes
        /// the slot explicitly because at Register time the prism hasn't stored its
        /// returned id yet.
        /// </summary>
        internal const int UnknownSpatialIndex = int.MinValue;

        static int ResolveSpatialIndexId(Prism block, int explicitId) =>
            explicitId != UnknownSpatialIndex ? explicitId : block.SpatialIndexId;

        /// <summary>
        /// Files a prism into this cell's bookkeeping. ALL prisms enter the volume
        /// accounting ("volume is the spine" - trail, flora, and fauna bodies alike
        /// feed <see cref="LiveVolume"/>; membership lives in the spatial index's
        /// packed summation view, this method is its single writer). Only
        /// ENVIRONMENT mass (<paramref name="environmentMass"/> true, the default)
        /// also enters the targeting grids and per-domain counts: fauna bodies are
        /// volume, but they are neither fauna-seekable mass concentrations nor
        /// edible prey.
        /// </summary>
        public void AddBlock(Prism block, bool environmentMass = true, int spatialIndexId = UnknownSpatialIndex)
        {
            // `is null` (not `!block`) so destroyed-but-non-null Unity refs can still be
            // removed from trackedBlocks via the matching RemoveBlock path; otherwise
            // LiveBlockCount drifts upward when prisms die outside the normal flow.
            if (block is null) return;

            if (environmentMass && !trackedBlocks.ContainsKey(block))
            {
                // Snapshot the domain at registration time - RemoveBlock uses this snapshot
                // so a team change (steal) between Add and Remove can't desync the grids.
                Domains registeredDomain = block ? block.Domain : Domains.Blue;
                trackedBlocks[block] = registeredDomain;

                if (block)
                {
                    // Nucleus-interior mass is the territorial claim, not prey: it stays
                    // out of the TARGETING grids (fauna must never be led to mass they
                    // cannot eat) while still counting toward volume, per-domain counts,
                    // and the phase backstop. gridTracked remembers the classification so
                    // RemoveBlock stays symmetric even if the nucleus radius changes.
                    //
                    // SHIELDED mass is excluded for exactly the same reason, and it is the
                    // same rule: Docs/ECOSYSTEM.md §16.2 already removed shielded prisms
                    // from every herbivore's DIET (Consume is a no-op on super-shielded and
                    // only sheds the shield on shielded), but they stayed in the grids, so
                    // the density centroids kept STEERING swarms onto mass they had just
                    // been told they cannot eat - the residue behind §16.3's Skim Race
                    // stall, and fatal to a mode like Ribcage whose arena IS a shielded
                    // structure. Shield state can change at runtime, so
                    // NotifyBlockShieldStateChanged re-files the prism on the transition.
                    //
                    // One transform.position read for the nucleus test AND all four
                    // grid writes — it is the same instant, and the read is a
                    // managed→engine interop on the per-prism creation path.
                    Vector3 blockPosition = block.transform.position;
                    if (!IsInsideNucleus(blockPosition) && !IsShieldedMass(block))
                    {
                        gridTracked.Add(block);

                        foreach (var t in s_playableDomains)
                            if (t != registeredDomain) countGrids[t].AddBlockAt(blockPosition);

                        if (countGrids.TryGetValue(Domains.Blue, out var anyGrid))
                            anyGrid.AddBlockAt(blockPosition);
                    }

                    domainBlockCounts.TryGetValue(registeredDomain, out int count);
                    domainBlockCounts[registeredDomain] = count + 1;
                }
            }

            // Volume membership (all sources): bind the prism's summation-view slot
            // to this cell. EnvMass mirrors trackedBlocks so a volume-only re-bind
            // (fauna-body restore) can't clear env status the flora tracker stream
            // set. Sums refresh on their own cadence.
            int slotIndex = ResolveSpatialIndexId(block, spatialIndexId);
            if (slotIndex >= 0)
                PrismSpatialIndex.Instance?.SetCellBinding(slotIndex, _volumeCellId,
                    trackedBlocks.ContainsKey(block), block ? block.Domain : Domains.Blue);
        }

        public void RemoveBlock(Prism block, int spatialIndexId = UnknownSpatialIndex)
        {
            if (block is null) return;

            // Volume membership goes first - fauna bodies are bound in the summation
            // view but not trackedBlocks, and must not survive the early-return below.
            int slotIndex = ResolveSpatialIndexId(block, spatialIndexId);
            if (slotIndex >= 0)
                PrismSpatialIndex.Instance?.ClearCellBinding(slotIndex, _volumeCellId);

            if (!trackedBlocks.Remove(block, out Domains registeredDomain)) return; // not counted

            // Drop grid membership even for destroyed-but-non-null refs so the
            // sensed-mass signal (gridTracked.Count) can't leak upward.
            bool wasGridTracked = gridTracked.Remove(block);

            if (block)
            {
                // Only grid-registered prisms leave the grids (nucleus-interior mass
                // never entered them - see AddBlock).
                if (wasGridTracked)
                {
                    // Read once, not once per grid — this is the per-prism DEATH path
                    // (PrismSpatialIndex.MarkDestroyed → UnbindCell lands here).
                    Vector3 blockPosition = block.transform.position;

                    foreach (Domains t in s_playableDomains)
                        if (t != registeredDomain) countGrids[t].RemoveBlockAt(blockPosition);

                    if (countGrids.TryGetValue(Domains.Blue, out var anyGrid))
                        anyGrid.RemoveBlockAt(blockPosition);
                }

                if (domainBlockCounts.TryGetValue(registeredDomain, out int count) && count > 0)
                    domainBlockCounts[registeredDomain] = count - 1;
            }
        }

        /// <summary>
        /// Re-registers a tracked prism whose domain changed (steal / ChangeTeam) so the
        /// per-domain grids and counts move it from the old domain's buckets to the new
        /// one's. No-op for prisms this cell isn't tracking.
        /// </summary>
        public void NotifyBlockDomainChanged(Prism block)
        {
            if (block is null || !trackedBlocks.ContainsKey(block)) return;
            RemoveBlock(block);
            AddBlock(block);
        }

        /// <summary>
        /// Re-registers a tracked prism whose SHIELD state changed, so it leaves the
        /// targeting grids when a shield engages and re-enters them when one is shed.
        /// Shielded mass is not food (Docs/ECOSYSTEM.md §16.2), so it must not be a
        /// steering target either - see AddBlock. Called from
        /// <c>PrismStateManager.SyncAOERegistryShieldState</c>, the single funnel every
        /// shield transition already passes through. No-op when the classification did
        /// not actually change, so the common "shield re-applied" path costs one bool
        /// compare rather than a grid remove/add.
        /// </summary>
        public void NotifyBlockShieldStateChanged(Prism block)
        {
            if (block is null || !trackedBlocks.ContainsKey(block)) return;

            bool shouldBeGridTracked = !IsShieldedMass(block) && !IsInsideNucleus(block.transform.position);
            if (shouldBeGridTracked == gridTracked.Contains(block)) return;

            RemoveBlock(block);
            AddBlock(block);
        }

        /// <summary>
        /// Shield-state test used for grid membership. Mirrors <c>Fauna.IsShieldedMass</c>
        /// (the diet rule) so "not food" and "not a steering target" can never disagree.
        /// </summary>
        static bool IsShieldedMass(Prism block)
        {
            var props = block ? block.prismProperties : null;
            return props != null && (props.IsShielded || props.IsSuperShielded);
        }

        /// <summary>
        /// Densest region of all blocks NOT belonging to the given domain - the
        /// "nearest opposing-color centroid" for fauna at aggression Level 1.
        /// Empty grids default to the cell anchor (crystal or cell transform)
        /// instead of the grid's bottom-corner sentinel, which otherwise pulled
        /// every fauna querying an empty grid to the world-space −X/−Y/−Z corner.
        /// </summary>
        public Vector3 GetExplosionTarget(Domains domain)
        {
            if (!countGrids.TryGetValue(domain, out var grid) || grid == null)
                return GetCellAnchorPosition();

            var region = grid.FindDensestRegion();
            // LastResultDensity is the peak smoothed density the job found - 0 means
            // the grid is empty. (Checking GetDensityAtPosition(region) here instead
            // would false-negative when sub-voxel interp / mean-shift lands the
            // answer in a low-count voxel adjacent to the true mass.)
            if (grid.LastResultDensity <= 0f)
                return GetCellAnchorPosition();
            return region;
        }

        /// <summary>
        /// Densest region across all domains - the "nearest centroid of any color"
        /// goal for fauna at aggression Level 2. Reads the synthesized
        /// countGrids[Domains.Blue] grid that <see cref="AddBlock"/> populates with
        /// every block regardless of its domain (Blue serves double-duty as the
        /// "no specific team" sentinel and the all-domain wildcard bucket).
        /// </summary>
        public Vector3 GetDensestRegionAnyDomain()
        {
            if (!countGrids.TryGetValue(Domains.Blue, out var anyGrid) || anyGrid == null)
                return GetCellAnchorPosition();

            var region = anyGrid.FindDensestRegion();
            if (anyGrid.LastResultDensity <= 0f)
                return GetCellAnchorPosition();
            return region;
        }

        /// <summary>
        /// Alias for <see cref="GetDensestRegionAnyDomain"/> - historical name from
        /// the gyroid-overflow regulation work, kept so external callers can use
        /// either spelling.
        /// </summary>
        public Vector3 GetPrimaryCentroid() => GetDensestRegionAnyDomain();

        /// <summary>
        /// Fallback position for goal resolution when density grids are empty:
        /// the local crystal if one exists, otherwise the cell's own transform.
        /// Keeps fauna near the cell instead of drifting to the empty-grid corner.
        /// </summary>
        Vector3 GetCellAnchorPosition()
        {
            if (runtime != null && runtime.CrystalTransform)
                return runtime.CrystalTransform.position;
            return transform.position;
        }

        public bool ContainsPosition(Vector3 position)
        {
            // Use SenseRadius (membrane radius, or a CellConfig override for large arenas)
            // so prisms across the whole sensed space register with the cell - not just
            // those inside the visual membrane. This is what lets fauna find + seek mass
            // across the full Skim Race track. See SenseRadius / Docs/ECOSYSTEM.md §7.2.
            float radius = SenseRadius;
            if (radius <= 0f) return false;
            return (position - transform.position).sqrMagnitude < radius * radius;
        }

        public void ChangeVolume(Domains domain, float volume)
        {
            teamVolumes.TryAdd(domain, 0);
            teamVolumes[domain] += volume;
        }

        public float GetTeamVolume(Domains domain)
        {
            return teamVolumes.GetValueOrDefault(domain, 0);
        }


        internal Domains GetHostileDomainToLocalLegacy()
        {
            var local = gameData.LocalRoundStats?.Domain ?? Domains.Jade;
            var candidates = new[] { Domains.Ruby, Domains.Gold, Domains.Blue, Domains.Jade };
            return candidates.First(d => d != local);
        }
    }
}