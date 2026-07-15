// Cell.cs
using System.Collections.Generic;
using System.Linq;
using CosmicShore.Data;
using CosmicShore.Game;
using CosmicShore.Gameplay;
using CosmicShore.Utility;
using Reflex.Attributes;
using UnityEngine;
using Random = UnityEngine.Random;
namespace CosmicShore.Gameplay
{
    public class Cell : MonoBehaviour
    {
        enum CellTypeChoiceOptions { Random, IntensityWise }

        [SerializeField] public int ID;

        [Header("Cell Config Selection")]
        [SerializeField] List<CellConfigDataSO> CellConfigs;   // NEW (replaces CellTypes)
        [SerializeField] CellTypeChoiceOptions cellTypeChoiceOptions = CellTypeChoiceOptions.Random;

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

        // Optional target WORLD radius for the nucleus, requested by a mode (e.g. Astro League uses
        // the nucleus as its spherical play boundary). 0 = use the prefab/multiplier size as-is.
        // Cached so it survives the nucleus-spawn-vs-request ordering race (applied in SpawnVisuals).
        float _pendingNucleusWorldRadius;

        // Optional replacement MESH for the nucleus, requested by a mode that repurposes the nucleus as
        // a NON-spherical play boundary (Astro League's ricochet courts — box/octagon/etc.). The mesh
        // is already in world units (centered on origin), so the nucleus renders it at unit scale with
        // its existing material. Cached so it survives the same nucleus-spawn-vs-request ordering race.
        Mesh _pendingNucleusMesh;

        public float NucleusRadius => nucleus ? nucleus.transform.localScale.x : 0f;
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
        /// Radius used for mass SENSING — prism registration (<see cref="ContainsPosition"/>)
        /// and the density grids that fauna seek mass with. Defaults to the visual
        /// <see cref="MembraneRadius"/>, but a CellConfig can override it
        /// (<c>SenseRadiusOverride</c>) to sense across a larger arena than the membrane
        /// visual — e.g. the Skim Race track — so fauna find + seek mass track-wide instead
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
        // one — so steals / ChangeTeam between Add and Remove can't desync the
        // per-domain bookkeeping (the §2.3.1 phantom-count class of bug).
        readonly Dictionary<Prism, Domains> trackedBlocks = new();

        // EVERY prism bound to this cell — trail, flora, AND fauna bodies — for the
        // per-domain VOLUME accounting ("volume is the spine": all prisms add to the
        // cell's mass regardless of source). Superset of trackedBlocks: fauna bodies
        // live here but stay out of the targeting grids/counts above (a forager swarm
        // must not read as its own mass concentration, and fauna bodies are not
        // edible prey). Volume sums are recomputed from live prism state
        // (Prism.CurrentVolume + live Domain) on a short cadence, so growth, steals,
        // and consumption are all reflected without incremental-drift bookkeeping.
        readonly HashSet<Prism> massTracked = new();
        readonly Dictionary<Domains, float> liveVolumeByDomain = new();
        readonly Dictionary<Domains, float> liveEnvVolumeByDomain = new();
        float liveVolumeTotal;
        float liveEnvVolumeTotal;

        // ------------------------------------------------------------------
        //  Nucleus control zone — "node control" lives INSIDE the nucleus.
        //  Per-domain ENVIRONMENT volume (trail + flora; fauna bodies excluded —
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
        // prisms are volume/count-tracked but never grid-tracked — fauna must not
        // be led to mass they cannot eat — so RemoveBlock has to know which
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
        static readonly List<Prism> s_deadMassScratch = new(32);
        SnowChanger spawnedCytoplasm;

        // ---------------------------------------------------------------------
        // Static spatial registry. Pooled prefab-spawned objects (trail prisms)
        // use this to find their containing cell — they have no scene identity
        // to wire a CellRuntimeDataSO into, and the per-prefab-asset alternative
        // breaks in multi-cell scenes where one prefab would need to point at
        // every cell's runtime SO at once.
        // ---------------------------------------------------------------------
        static readonly List<Cell> ActiveCells = new();

        /// <summary>
        /// Read-only view of the enabled cells in the scene. Exposed for read-only
        /// diagnostics (e.g. <see cref="EcosystemPerfProbe"/> summing prisms + live
        /// fauna across cells); do not mutate or cache across frames.
        /// </summary>
        public static IReadOnlyList<Cell> ActiveCellsSnapshot => ActiveCells;

        /// <summary>
        /// The enabled cell whose membrane contains <paramref name="position"/>,
        /// or null when the position is in open space. O(cells-in-scene) — call
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
        /// Live leader by per-domain prism VOLUME — "volume is the spine" (locked
        /// invariant). NODE CONTROL IS THE NUCLEUS: when this cell has a nucleus
        /// control zone, only the ENVIRONMENT volume INSIDE the nucleus counts —
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
                Domains[] order = { Domains.Jade, Domains.Ruby, Domains.Gold, Domains.Blue };
                foreach (var d in order)
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
        /// True when this cell has a spawned nucleus with a measurable world radius —
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
        /// nucleus control zone OR nobody has laid environment mass inside it yet —
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
        /// domain (the exterior is the contested feeding ground — extends the Boid
        /// forager's existing any-domain grazing to all herbivores), while mass
        /// INSIDE the nucleus is the territorial claim and is never fauna-consumed
        /// (players contest it with abilities and by out-laying volume). Without a
        /// nucleus zone the legacy rule stands: herbivores eat opposing-domain mass.
        /// </summary>
        public bool IsPreyForHerbivore(Vector3 position, Domains faunaDomain, Domains preyDomain)
        {
            if (HasNucleusControlZone)
                return !IsInsideNucleus(position);
            return preyDomain != faunaDomain;
        }

        /// <summary>
        /// True while the targeting grids hold any environment mass — with a nucleus
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

        // ------------------------------------------------------------------
        //  Live volume — the spine. Recomputed from live prism state on a short
        //  cadence (growth animates continuously, so event-driven deltas would
        //  drift); O(massTracked) per recompute, a few times a second at most.
        // ------------------------------------------------------------------

        void EnsureVolumeFresh()
        {
            if (Time.time < _nextVolumeRecomputeAt) return;
            _nextVolumeRecomputeAt = Time.time + VolumeRecomputeIntervalSeconds;

            liveVolumeByDomain.Clear();
            liveEnvVolumeByDomain.Clear();
            nucleusEnvVolumeByDomain.Clear();
            liveVolumeTotal = 0f;
            liveEnvVolumeTotal = 0f;
            liveExteriorEnvVolumeTotal = 0f;
            s_deadMassScratch.Clear();

            Vector3 centre = transform.position;
            float nucleusSqr = _nucleusControlRadiusSqr;

            foreach (var prism in massTracked)
            {
                // Destroyed-but-untracked refs (scene teardown paths that skipped
                // RemoveBlock) are collected here instead of leaking forever.
                if (!prism) { s_deadMassScratch.Add(prism); continue; }

                float v = prism.CurrentVolume;
                if (v <= 0f) continue; // destroyed / not yet grown

                var domain = prism.Domain; // LIVE domain — steals re-attribute next tick
                liveVolumeByDomain.TryGetValue(domain, out float dv);
                liveVolumeByDomain[domain] = dv + v;
                liveVolumeTotal += v;

                if (trackedBlocks.ContainsKey(prism))
                {
                    liveEnvVolumeByDomain.TryGetValue(domain, out float ev);
                    liveEnvVolumeByDomain[domain] = ev + v;
                    liveEnvVolumeTotal += v;

                    // Node control vs feeding ground: environment mass inside the
                    // nucleus claims control; everything outside is edible prey.
                    if (nucleusSqr > 0f &&
                        (prism.transform.position - centre).sqrMagnitude <= nucleusSqr)
                    {
                        nucleusEnvVolumeByDomain.TryGetValue(domain, out float nv);
                        nucleusEnvVolumeByDomain[domain] = nv + v;
                    }
                    else
                    {
                        liveExteriorEnvVolumeTotal += v;
                    }
                }
            }

            // Without a nucleus zone the whole cell is the feeding ground for the
            // legacy opposing-domain prey math (OpposingVolume's else-branch).
            if (nucleusSqr <= 0f)
                liveExteriorEnvVolumeTotal = liveEnvVolumeTotal;

            foreach (var dead in s_deadMassScratch)
            {
                massTracked.Remove(dead);
                gridTracked.Remove(dead);
            }
        }

        /// <summary>
        /// Total live prism volume in this cell — ALL prisms (trail, flora, fauna
        /// bodies). THE phase-ladder measure ("volume is the spine").
        /// </summary>
        public float LiveVolume
        {
            get { EnsureVolumeFresh(); return liveVolumeTotal; }
        }

        /// <summary>Live volume tracked under <paramref name="domain"/> — all prism sources.</summary>
        public float GetDomainVolume(Domains domain)
        {
            EnsureVolumeFresh();
            return liveVolumeByDomain.GetValueOrDefault(domain, 0f);
        }

        /// <summary>
        /// The herbivore PREY signal in volume units (fauna bodies excluded — not
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
        /// itself runs on volume — see <see cref="FrenzyEnterVolume"/>.
        /// </summary>
        public int FrenzyEnterThreshold => ResolveThresholds().FrenzyEnter;

        /// <summary>
        /// LiveVolume at which the cell crosses into Frenzy. HUD widgets use this as
        /// the "max" — when summed mass approaches it, the cell is about to enter Level2
        /// aggression (and flora freeze) and the UI should communicate that.
        /// </summary>
        public float FrenzyEnterVolume => ResolveThresholds().FrenzyEnterVolume;

        /// <summary>
        /// The full resolved phase-threshold table for this cell (config table, or
        /// <see cref="CellPhaseThresholds.Default"/> when no config / legacy zeroed
        /// asset). Exposed so the concentric-hexagon volume indicator can draw one
        /// ring per phase boundary (Restless, then Frenzy at the centre) at a radius
        /// proportional to its enter threshold, lighting each ring as the cell's
        /// summed mass crosses it. Read-only — the cell is the single writer.
        /// </summary>
        public CellPhaseThresholds ResolvedThresholds => ResolveThresholds();

        /// <summary>
        /// True once this cell's CellConfig has been assigned (Initialize ran). While
        /// false, threshold reads fall back to CellPhaseThresholds.Default — HUD
        /// diagnostics surface this so a mis-scaled indicator is explainable at a
        /// glance instead of looking like dead data.
        /// </summary>
        public bool HasConfigAssigned => cellConfigData != null;

        // ---------------------------------------------------------------------
        //  Fauna spawn cycle telemetry — read by the volume-indicator ring HUD.
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
        /// Fixed period (seconds) between this cell's periodic fauna population spawns —
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
        /// <see cref="ApplyAuthoritativePhaseAndDomain"/> — the server's compute on a
        /// networked cell, or the local-only fallback in single-player. Cell never
        /// recomputes phase itself; it just exposes the inputs.
        /// </summary>
        public CellPhase Phase => phase;

        // ---------------------------------------------------------------------
        // Derived gates — projections of Phase the consumers actually care about.
        // Flora planting and growing now share ONE rule (steady until Frenzy); fauna
        // read the aggression band. These properties give each consumer exactly the
        // boolean it needs without re-deriving phase semantics.
        // ---------------------------------------------------------------------

        /// <summary>
        /// True while new flora may be planted AND existing flora may grow: the cell is
        /// below Frenzy. Planting and growth run at a STEADY rate all the way up — there
        /// is no early planting cap and no mid-range growth cap (those staggered phase
        /// gates were a growth-side cheat: a hard-coded self-limit faking the homeostasis
        /// the food web is meant to produce). The only down-force on flora is the food web
        /// (opposing-domain fauna grazing the prisms) or vessel abilities. Once a cell
        /// fills to Frenzy, growth stops and stays stopped until an ACTIVE force lowers the
        /// live prism count back below the Frenzy exit threshold (hysteresis), at which
        /// point growth resumes on its own. Mass is conserved: no passive decay, no growth
        /// oscillator — a frozen-solid cell is a valid state, not a defect to auto-correct.
        /// See Docs/ECOSYSTEM.md §0/§5.
        /// </summary>
        public bool FloraGrowingEnabled => phase < CellPhase.Frenzy;

        /// <summary>
        /// True while new flora may be planted. Identical to <see cref="FloraGrowingEnabled"/>
        /// — planting and growth share the single "below Frenzy" rule now (steady until
        /// frenzy). Kept as a separate name so spawner code reads intent at the call site.
        /// </summary>
        public bool FloraPlantingEnabled => FloraGrowingEnabled;

        /// <summary>
        /// True once the cell holds any ENVIRONMENT mass — the spawn floor for the
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
        /// Fauna aggression level derived from <see cref="Phase"/> — a 1:1 mapping now
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
        /// sentinel) — callers can use it directly without further branching.
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
            // — server's compute wins when the two diverge — but for single-player and
            // for the server itself this is the only path that advances phase. Without
            // it, no fauna ever spawn because phase stays at Calm forever.
            if (Time.time < _nextPhaseTickAt) return;
            _nextPhaseTickAt = Time.time + PhaseTickIntervalSeconds;

            var thresholds = ResolveThresholds();
            // Volume is the spine: the ladder climbs on live volume; prism count is
            // only the Frenzy perf backstop inside Compute.
            var newPhase = CellPhaseRules.Compute(LiveVolume, LiveBlockCount, phase, in thresholds);
            ApplyAuthoritativePhaseAndDomain(newPhase, DominantDomain);
        }

        CellPhaseThresholds ResolveThresholds()
        {
            var cfg = cellConfigData;
            if (!cfg) return CellPhaseThresholds.Default;

            // Existing CellConfig assets serialized before PhaseThresholds existed
            // deserialize as struct zero — Unity does not apply the C# initializer.
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
            // Spatial registry — lets pooled, prefab-spawned objects (trail prisms)
            // find which cell contains them without per-prefab SO wiring or the
            // deprecated CellControlManager singleton. See FindCellContaining.
            if (!ActiveCells.Contains(this))
                ActiveCells.Add(this);

            // Clear stale config BEFORE subscribing to events.
            // CellRuntimeDataSO is a shared SO asset — Menu_Main's Cell sets
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

            StopSpawner();
            runtime?.ResetRuntimeData();
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
            massTracked.Clear();
            gridTracked.Clear();
            _replicatedDominantDomain = null;
            _nextVolumeRecomputeAt = float.NegativeInfinity; // resum on next read
            liveFaunaCounts.Clear();
            liveFauna.Clear();
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
        //  Live fauna registry — instances plus per-species counts (keyed by the
        //  FaunaConfigurationSO that defines the species). Fauna register on
        //  AssignLineage (spawner and reproduction paths both) and unregister in
        //  OnDestroy. This registry is the cell "sensing" its inhabitants — the
        //  fauna analogue of the prism density grid: counts feed the seeder
        //  (top up to seed floor) and reproduction (MaxLivePopulation backstop);
        //  instances feed predator prey-seeking (nearest live herbivore) and the
        //  predator seeding gate. Manager-spawned fauna (no lineage) are invisible
        //  to it — acceptable, those legacy populations never instantiate (§7).
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
        /// Live herbivores still eligible as prey — the prey signal for predator
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
            // `is null` guard only — a destroyed-but-non-null fauna must still be
            // removable from the registry during teardown.
            if (fauna is null || !fauna.SourceConfig) return;
            if (liveFaunaCounts.TryGetValue(fauna.SourceConfig, out int c) && c > 0)
                liveFaunaCounts[fauna.SourceConfig] = c - 1;
            liveFauna.Remove(fauna);
        }

        void Initialize()
        {
            spawnedLifeForms.Clear();
            trackedBlocks.Clear();
            domainBlockCounts.Clear();
            massTracked.Clear();
            gridTracked.Clear();
            _replicatedDominantDomain = null;
            _nextVolumeRecomputeAt = float.NegativeInfinity; // resum on next read
            liveFaunaCounts.Clear();
            liveFauna.Clear();
            phase = CellPhase.Calm;

            // Bind runtime -> this cell
            runtime.Cell = this;
            runtime.EnsureCellStats(ID);

            AssignConfig();
            // SpawnVisuals must run before SetupDensityGrids: the density grids
            // are now sized to the cell's membrane radius, and MembraneRadius
            // reads the membrane GameObject that SpawnVisuals instantiates.
            SpawnVisuals();
            SetupDensityGrids();
            ResetVolumes();

            UpdateCellStats();
        }
        
        void InitilizePostFirstCellItem()
        {
            postInitilized = true;
            if (!cellConfigData)
            {
                CSDebug.LogWarning($"[Cell {ID}] Crystal spawned before Cell Initialized. Attempting lazy init.");
                Initialize();
                if (!cellConfigData) return;
            }

            SpawnCytoplasm();
            ApplyModifiers();
            SpawnCytoplasm();
            StartSpawnerForMode();
        }

        void OnCellItemUpdated()
        {
            if (postInitilized)
                return;
            InitilizePostFirstCellItem();
        }

        void AssignConfig()
        {
            if (CellConfigs == null || CellConfigs.Count == 0)
            {
                CSDebug.LogError($"{nameof(Cell)}: No CellConfigs found to assign.");
                return;
            }

            var index = cellTypeChoiceOptions switch
            {
                CellTypeChoiceOptions.Random => Random.Range(0, CellConfigs.Count),
                CellTypeChoiceOptions.IntensityWise => Mathf.Clamp(gameData.SelectedIntensity.Value - 1, 0, CellConfigs.Count - 1),
                _ => 0
            };

            runtime.Config = CellConfigs[index];
        }

        void SetupDensityGrids()
        {
            // Size the density grids to the cell's SENSE radius (membrane radius by
            // default, or a CellConfig override for large arenas like the Skim Race track).
            // With a 1200m membrane the old fixed cube saw only ~14% of the cell — outer
            // mass was invisible to FindDensestRegion so fauna never sought it. See
            // Docs/DENSITY_PARTITIONING_AUDIT.md.
            float membraneRadius = SenseRadius;
            float worldDiameter = membraneRadius > 0f
                ? membraneRadius * 2f
                : 2400f; // fallback when the membrane prefab is missing
            Vector3 cellCenter = transform.position;

            // Dispose any existing grids before replacing them — each holds
            // persistent NativeArrays, and Initialize() can run more than once
            // across a session (e.g. replay).
            foreach (var existing in countGrids.Values)
                existing?.Dispose();

            Domains[] teams = { Domains.Jade, Domains.Ruby, Domains.Gold };
            countGrids.Clear();
            foreach (Domains t in teams)
                countGrids[t] = new BlockCountDensityGrid(t, cellCenter, worldDiameter);

            // Blue-keyed grid accumulates every block regardless of domain so
            // GetDensestRegionAnyDomain() can answer aggression-2 fauna's "head toward
            // nearest centroid" goal — friendly + enemy mass both count. Blue is the
            // "no specific team" sentinel; this grid does double duty as the wildcard.
            countGrids[Domains.Blue] = new BlockCountDensityGrid(Domains.Blue, cellCenter, worldDiameter);
        }

        void SpawnVisuals()
        {
            if (!cellConfigData) return;

            if (cellConfigData.MembranePrefab != null)
                membrane = Instantiate(cellConfigData.MembranePrefab, transform.position, Quaternion.identity);

            if (cellConfigData.NucleusPrefab == null) return;
            nucleus = Instantiate(cellConfigData.NucleusPrefab, transform.position, Quaternion.identity);
            nucleus.transform.localScale *= nucleusScaleMultiplier;
            ApplyNucleusWorldRadius(); // honor any radius a mode requested before the nucleus existed
            ApplyNucleusMesh();        // ...or a replacement boundary mesh (non-spherical court)
            RefreshNucleusControlRadius();
        }

        /// <summary>
        /// Re-measure the nucleus' WORLD radius (renderer bounds — mesh-agnostic, so
        /// morphed court meshes and world-radius requests are honored) and cache it
        /// as the node-control zone boundary. Called whenever the nucleus spawns or
        /// is resized/re-meshed; a missing nucleus clears the zone (legacy behavior).
        /// </summary>
        void RefreshNucleusControlRadius()
        {
            _nucleusControlRadiusSqr = 0f;
            if (nucleus == null) return;

            var r = nucleus.GetComponentInChildren<Renderer>();
            if (r == null) return;

            Vector3 ext = r.bounds.extents;
            float radius = Mathf.Max(ext.x, Mathf.Max(ext.y, ext.z));
            if (radius <= 1e-3f) return;

            _nucleusControlRadiusSqr = radius * radius;
        }

        /// <summary>
        /// Resize the nucleus marker to a target WORLD radius (mesh-agnostic, via renderer bounds, so
        /// it works regardless of the prefab mesh's base size). Lets a mode repurpose the nucleus as
        /// its play boundary — e.g. Astro League uses it for the Sphere-shaped court (see
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
        /// (e.g. Astro League's ricochet courts — box/octagon/etc.), keeping the prefab's material so the
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
            // The mesh is already world-sized, so render at unit scale — overrides the prefab's base
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

            spawnedCytoplasm = Instantiate(cellConfigData.CytoplasmPrefab, transform.position, Quaternion.identity);
            spawnedCytoplasm.SetOrigin(transform.position);
            spawnedCytoplasm.Initialize();
        }

        void StartSpawnerForMode()
        {
            StopSpawner();

            activeSpawner = cellTypeChoiceOptions == CellTypeChoiceOptions.IntensityWise
                ? intensitySpawner
                : randomSpawner;

            activeSpawner.Start(this, cellConfigData, runtime, gameData);

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
        /// (Brood Rush realigns the 30s wave clock to the GO of the countdown —
        /// the spawner otherwise starts when the first crystal registers, which is
        /// during the ready screen). No-op until the cell has post-initialized.
        /// Note: a profile with flora re-runs its initial flora batch on restart —
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
        /// Files a prism into this cell's bookkeeping. ALL prisms enter the volume
        /// accounting ("volume is the spine" — trail, flora, and fauna bodies alike
        /// feed <see cref="LiveVolume"/>). Only ENVIRONMENT mass
        /// (<paramref name="environmentMass"/> true, the default) also enters the
        /// targeting grids and per-domain counts: fauna bodies are volume, but they
        /// are neither fauna-seekable mass concentrations nor edible prey.
        /// </summary>
        public void AddBlock(Prism block, bool environmentMass = true)
        {
            // `is null` (not `!block`) so destroyed-but-non-null Unity refs can still be
            // removed from trackedBlocks via the matching RemoveBlock path; otherwise
            // LiveBlockCount drifts upward when prisms die outside the normal flow.
            if (block is null) return;

            // Volume membership (all sources). Sums refresh on their own cadence.
            massTracked.Add(block);

            if (!environmentMass) return;
            if (trackedBlocks.ContainsKey(block)) return; // already counted

            // Snapshot the domain at registration time — RemoveBlock uses this snapshot
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
                if (!IsInsideNucleus(block.transform.position))
                {
                    gridTracked.Add(block);

                    Domains[] teams = { Domains.Jade, Domains.Ruby, Domains.Gold };
                    foreach (var t in teams)
                        if (t != registeredDomain) countGrids[t].AddBlock(block);

                    if (countGrids.TryGetValue(Domains.Blue, out var anyGrid))
                        anyGrid.AddBlock(block);
                }

                domainBlockCounts.TryGetValue(registeredDomain, out int count);
                domainBlockCounts[registeredDomain] = count + 1;
            }
        }

        public void RemoveBlock(Prism block)
        {
            if (block is null) return;

            // Volume membership goes first — fauna bodies are in massTracked but not
            // trackedBlocks, and must not survive the early-return below.
            massTracked.Remove(block);

            if (!trackedBlocks.Remove(block, out Domains registeredDomain)) return; // not counted

            // Drop grid membership even for destroyed-but-non-null refs so the
            // sensed-mass signal (gridTracked.Count) can't leak upward.
            bool wasGridTracked = gridTracked.Remove(block);

            if (block)
            {
                // Only grid-registered prisms leave the grids (nucleus-interior mass
                // never entered them — see AddBlock).
                if (wasGridTracked)
                {
                    Domains[] teams = { Domains.Jade, Domains.Ruby, Domains.Gold };
                    foreach (Domains t in teams)
                        if (t != registeredDomain) countGrids[t].RemoveBlock(block);

                    if (countGrids.TryGetValue(Domains.Blue, out var anyGrid))
                        anyGrid.RemoveBlock(block);
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
        /// Densest region of all blocks NOT belonging to the given domain — the
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
            // LastResultDensity is the peak smoothed density the job found — 0 means
            // the grid is empty. (Checking GetDensityAtPosition(region) here instead
            // would false-negative when sub-voxel interp / mean-shift lands the
            // answer in a low-count voxel adjacent to the true mass.)
            if (grid.LastResultDensity <= 0f)
                return GetCellAnchorPosition();
            return region;
        }

        /// <summary>
        /// Densest region across all domains — the "nearest centroid of any color"
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
        /// Alias for <see cref="GetDensestRegionAnyDomain"/> — historical name from
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
            // so prisms across the whole sensed space register with the cell — not just
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