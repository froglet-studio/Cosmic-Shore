using System.Collections.Generic;
using CosmicShore.Core;
using CosmicShore.Data;
using CosmicShore.Utility;
using Unity.Profiling;
using UnityEngine;
using SVector3 = System.Numerics.Vector3;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// A SWARM - a population of tadpole fauna that grows the body plan of its majority element
    /// (Mass whale, Space jellyfish, Charge pufferfish, Time dragonfly), swims, reacts to vessels,
    /// and MORPHS into another animal when players kill enough of its majority. Docs/SWARM_FAUNA.md.
    ///
    /// This is the population's ANCHOR and brain, in the shape the worm colony established
    /// (Docs/ECOSYSTEM.md §23.3): it is lineage-registered through the ordinary spawner (so the
    /// cell's seed floor, cap and cleanup all apply), it carries NO heart and NO body, and it cannot
    /// be preyed on. The lifeforms are its MEMBERS (<see cref="SwarmTadpoleFauna"/>), each with its
    /// own heart and body prism, each dying through the sealed fauna death path.
    ///
    /// The behaviour is ONE simulation over a struct-of-arrays run at a fixed tick - the config picks
    /// which (<see cref="SwarmFaunaConfigSO.Model"/>): <see cref="SwarmFieldCore"/> (designed fields,
    /// every tadpole owns a slot), <see cref="SwarmGridCore"/> (the grid morphogen, nothing assigns a
    /// place) or <see cref="SwarmSortCore"/> (emergent cell sorting: positional-information wells, fate,
    /// differential adhesion, a lossless molting corrector). Members are posed by interpolating between ticks, so motion is smooth at any frame rate
    /// and no member runs an Update of its own.
    ///
    /// Invariants it touches (Docs/SWARM_FAUNA.md §2): mass is conserved (every egg is PAID for out of
    /// eaten flora volume, 1:1), there is no imposed death (members only die to vessels, predators or
    /// starvation), one colour (every member wears this anchor's domain - the cell's controlling one),
    /// every member drops its crystal, and nothing pops (births bloom, molts re-form, deaths wither).
    /// </summary>
    public class SwarmFauna : Fauna
    {
        [Header("Swarm")]
        [SerializeField] SwarmFaunaConfigSO config;

        ISwarmCore _core;
        SwarmTadpoleFauna[] _members;
        SVector3[] _prevPos, _prevFace;
        float[] _birthTime, _heartFactor;
        bool[] _danger;
        SwarmPlanData[] _plans;
        Vector3[] _defaultHalf;          // per research element: a typical prism of that element
        Vector3 _centre;                 // the host cell's centre (sim origin)
        float _dt, _acc;
        int _biteCursor;
        float _lastFedTime, _lastShedTime, _extinctSince = -1f;
        Element _startElement = Element.Mass;
        bool _seeded;
        string _eaterName;
        FMODUnity.StudioEventEmitter _loop;
        readonly SwarmPredator[] _preds = new SwarmPredator[8];
        int _predCount;
        readonly List<IVesselStatus> _seen = new(8);
        Flora _goalPlant;
        float _atPlantSince = -1f;
        readonly Dictionary<Flora, float> _barren = new();
        // Laid/seeded members waiting for a hatch slot (alive in the sim, no GameObject yet). The
        // budget is shared cell-wide: every swarm draws from the same per-frame allowance.
        readonly Queue<int> _pending = new();
        static int s_spawnFrame = -1, s_spawnsThisFrame;

        // ONE scheduler for every swarm in the scene. Each swarm used to run its own catch-up clock, so a
        // frame slower than 1/TickHz made all 24 of them run MaxStepsPerFrame steps the next frame, which
        // made THAT frame slower: a spiral that locked the Swarm cell at ~3 FPS once the bodies had grown.
        // Now the swarms are stepped round-robin from one shared CPU budget, and a swarm the budget does
        // not reach drops the time instead of banking it (Docs/SWARM_FAUNA.md §13).
        static readonly List<SwarmFauna> s_live = new();
        static int s_schedFrame = -1, s_cursor;
        static bool s_warnedSaturated;
        static float s_saturatedSince = -1f;
        int _poseOffset;
        float _lastPoseTime = -1f;

        static readonly ProfilerMarker s_mSchedule = new("SwarmFauna.Schedule");
        static readonly ProfilerMarker s_mSense = new("SwarmFauna.SenseVessels");
        static readonly ProfilerMarker[] s_mStep =
        {
            new("SwarmFauna.Step.Field"), new("SwarmFauna.Step.Grid"),
            new("SwarmFauna.Step.Sort"), new("SwarmFauna.Step.EvoFate"),
        };
        static readonly ProfilerMarker s_mMembers = new("SwarmFauna.Tick.Members");
        static readonly ProfilerMarker s_mFeed = new("SwarmFauna.Feed");
        static readonly ProfilerMarker s_mHatch = new("SwarmFauna.Hatch");
        static readonly ProfilerMarker s_mPose = new("SwarmFauna.Render.Pose");
        static readonly ProfilerMarker s_mSync = new("SwarmFauna.Render.SyncBodies");

        /// <summary>The live sim (null before the first frame).</summary>
        public ISwarmCore Core => _core;
        public SwarmFaunaConfigSO Config => config;

        /// <summary>The body plan the swarm is currently growing ("mass", "space", "charge", "time").</summary>
        public string CurrentPlan => _core != null ? _core.Plan.Kind : "";

        public override float CurrentSpeed => config ? config.Cruise * config.UnitScale * config.TickHz : 0f;

        /// <summary>A member's swim speed in world units/s - what a jouster has to outrun.</summary>
        public float MemberSpeed(int i) =>
            _core != null && i >= 0 && i < _core.Cap ? _core.Vel[i].Length() * config.UnitScale * config.TickHz : 0f;

        /// <summary>The anchor is a population, not an animal: nothing eats a swarm whole.</summary>
        public override bool Predated(string predatorName, Transform devourTarget) => false;

        /// <summary>
        /// Element-as-data for a POPULATION: the anchor is heartless, so a species config's element
        /// does not provision a crystal here - it names the body the swarm hatches as (its starting
        /// majority). Members carry the hearts.
        /// </summary>
        protected override void ProvisionHeart(Element element)
        {
            if (element is >= Element.Charge and <= Element.Time) _startElement = element;
        }

        protected override void Start()
        {
            base.Start();
            Seed();
        }

        // ───────────────────────────────────────────────────────────────── seeding

        void Seed()
        {
            if (_seeded) return;
            var host = HostCell;
            if (!config || !config.TadpolePrefab || !host)
            {
                CSDebug.LogWarning($"{name}: swarm has no config, tadpole prefab or host cell - it will not hatch.");
                return;
            }
            _plans = SwarmPlanLibrary.Load(config);
            if (_plans == null) return;
            _seeded = true;
            s_live.Add(this);
            _poseOffset = Random.Range(0, 64);

            _defaultHalf = SwarmPlanLibrary.TypicalHalfExtents(_plans);
            _centre = host.transform.position;
            _dt = 1f / config.TickHz;
            _eaterName = "swarm";

            _core = config.Model switch
            {
                SwarmModel.Grid => BuildGridCore(host),
                SwarmModel.Sort => BuildSortCore(host),
                SwarmModel.EvoFate => BuildEvoFateCore(host),
                _ => BuildFieldCore(host),
            };
            int n = _core.Cap;
            _members = new SwarmTadpoleFauna[n];
            _prevPos = new SVector3[n]; _prevFace = new SVector3[n];
            _birthTime = new float[n]; _heartFactor = new float[n];
            _danger = new bool[n];

            var anchor = ToSim(transform.position);
            var radial = transform.position - _centre;
            var tangent = Vector3.Cross(radial.sqrMagnitude > 1f ? radial.normalized : Vector3.forward, Random.onUnitSphere);
            if (tangent.sqrMagnitude < 1e-4f) tangent = Vector3.right;
            _core.Seed(SwarmFaunaConfigSO.ToIndex(_startElement), config.SeedMembers, anchor, Sim(tangent.normalized));
            _core.SwimTarget = anchor;
            for (int i = 0; i < n; i++)
                if (_core.Alive[i]) _pending.Enqueue(i);
            HatchPending();

            _lastFedTime = Time.time;
            _acc = Random.value * _dt;   // stagger the cell's swarms across frames from the first tick
            StartLoop();
            CSDebug.LogVerbose(CSLogChannel.Ecology,
                $"[Swarm] {name} ({config.Model}) hatched as {_core.Plan.Kind} with {_core.AliveCount} tadpoles at r={radial.magnitude:F0}");
        }

        // MembraneRadius reads 0 until the membrane has spawned (Docs/CONNECTING_PANEL.md); the
        // standard membrane's 1200 is the fallback rather than a 0 that would pen the body in a point
        float SimMembrane(Cell host) => (host.MembraneRadius > 1f ? host.MembraneRadius : 1200f) * 0.97f / config.UnitScale;

        int PlanCap => Mathf.Max(_plans[0].N, Mathf.Max(_plans[1].N, Mathf.Max(_plans[2].N, _plans[3].N)));

        bool TryBand(out float inner, out float outer)
        {
            inner = outer = 0f;
            var cfg = SourceConfig;
            if (!cfg || cfg.BandOuterRadius <= 0f) return false;
            inner = Mathf.Min(cfg.BandInnerRadius, cfg.BandOuterRadius) / config.UnitScale;
            outer = Mathf.Max(cfg.BandInnerRadius, cfg.BandOuterRadius) / config.UnitScale;
            return true;
        }

        ISwarmCore BuildFieldCore(Cell host)
        {
            var p = new SwarmFieldParams
            {
                LayRate = config.LayRate, LayMax = config.LayMax,
                KillLayHoldSteps = Mathf.RoundToInt(config.KillLayHoldSeconds * config.TickHz),
                Cruise = config.Cruise, Turn = config.TurnPerStep,
                Membrane = SimMembrane(host),
                CrossCost = config.CrossElementCost,
                Cap = PlanCap,
            };
            for (int e = 0; e < 4; e++) p.EggCost[e] = SwarmFaunaConfigSO.Of(config.EggVolume, SwarmFaunaConfigSO.ToElement(e));
            if (TryBand(out float lo, out float hi)) { p.BandInner = lo; p.BandOuter = hi; }
            return new SwarmFieldCore(_plans, p, Random.Range(1, int.MaxValue));
        }

        /// <summary>
        /// The grid morphogen in its GAME settings (Docs/SWARM_FAUNA.md §8, §10): research `combo` - hgrid2
        /// made LOSSLESS - with one domain, funded laying, molts that animate over GridMoltSteps, an
        /// oriented body that swims, and a plan lock. Nothing dies on a clock: the molt clock only picks
        /// the starvation victim when the host starves the swarm.
        /// </summary>
        ISwarmCore BuildGridCore(Cell host)
        {
            var p = new SwarmGridParams
            {
                G = config.GridSize, Cell = config.GridCell, Quant = false,
                KClass = config.GridKClass, KTotal = config.GridKTotal, Persist = config.GridPersist,
                Noise = config.GridNoise, PLay = config.GridLayChance, PCross = config.GridCrossChance,
                LayMaxPerStep = config.GridLayMaxPerStep, KFine = config.GridKFine, Sigma = config.GridSigma,
                SigmaRel = config.GridSigmaRel, KMig = config.GridMigrate,
                KFF = config.GridFeedForward, Lock = config.GridPlanLock,
                // combo: the lossless corrector (molts animate over GridMoltSteps; one domain makes the
                // orphan proxy and region transfer inert, so they are left at their defaults)
                Molt = config.GridLossless, MoltRate = config.GridMoltRate, MoltSteps = config.GridMoltSteps,
                LayCap = config.GridLossless ? config.GridLayCap : 0f, Ratio = config.GridLossless ? 2 : 0,
                KillLayHoldSteps = Mathf.RoundToInt(config.KillLayHoldSeconds * config.TickHz),
                Periods = new[]
                {
                    Mathf.Max(1, Mathf.RoundToInt(config.GridFramePeriod.x)), Mathf.Max(1, Mathf.RoundToInt(config.GridFramePeriod.y)),
                    Mathf.Max(1, Mathf.RoundToInt(config.GridFramePeriod.z)), Mathf.Max(1, Mathf.RoundToInt(config.GridFramePeriod.w)),
                },
                DomainSlots = false, HungerKills = false, Funded = true, Oriented = true,
                Cruise = config.Cruise, Turn = config.TurnPerStep,
                Membrane = SimMembrane(host), CrossCost = config.CrossElementCost, Cap = PlanCap,
            };
            for (int e = 0; e < 4; e++) p.EggCost[e] = SwarmFaunaConfigSO.Of(config.EggVolume, SwarmFaunaConfigSO.ToElement(e));
            if (TryBand(out float lo, out float hi)) { p.BandInner = lo; p.BandOuter = hi; }
            return new SwarmGridCore(_plans, p, Random.Range(1, int.MaxValue));
        }

        /// <summary>
        /// Emergent cell sorting in its GAME settings (Docs/SWARM_FAUNA.md §9): one region (the one-colour
        /// law), funded laying, molts that animate over SortMoltSteps, wells that ride the plan's
        /// animation and turn and travel with the swimming body, each member wearing its fated well's
        /// look. Molting runs always - this model's corrector is lossless and absolute (harness S4/S5).
        /// </summary>
        ISwarmCore BuildSortCore(Cell host)
        {
            var p = new SwarmSortParams
            {
                K = config.SortWellsPerType, PerWell = config.SortUnitsPerWell, CovScale = config.SortWellWidth,
                KWell = config.SortWellGain, WellClip = config.SortWellClip,
                R0 = config.SortSpacing, KRep = config.SortRepulsion, RAdh = config.SortAdhesionRadius,
                ASame = config.SortAdhesion.x, AElem = config.SortAdhesion.y, ARole = config.SortAdhesion.z, AOther = config.SortAdhesion.w,
                Swap = config.SortSwap, RSwap = config.SortSwapRadius,
                Inertia = config.SortInertia, Noise = config.SortNoise, KWellFF = config.SortFeedForward,
                Dwell = config.SortDwell, LayRate = config.SortLayRate, LayMax = config.SortLayMax,
                PCross = config.SortCrossChance, FillTol = config.SortFillTolerance, Over = config.SortBodyFill,
                Molt = true, Transfer = true, MoltRate = config.SortMoltRate, MoltSteps = config.SortMoltSteps, MoltWindow = -1,
                KillLayHoldSteps = Mathf.RoundToInt(config.KillLayHoldSeconds * config.TickHz),
                Periods = new[]
                {
                    Mathf.Max(1, Mathf.RoundToInt(config.SortFramePeriod.x)), Mathf.Max(1, Mathf.RoundToInt(config.SortFramePeriod.y)),
                    Mathf.Max(1, Mathf.RoundToInt(config.SortFramePeriod.z)), Mathf.Max(1, Mathf.RoundToInt(config.SortFramePeriod.w)),
                },
                DomainSlots = false, Funded = true, Animate = true, WellLook = true, Oriented = true,
                Cruise = config.Cruise, Turn = config.TurnPerStep,
                Membrane = SimMembrane(host), CrossCost = config.CrossElementCost, Cap = PlanCap,
                // round 6 (Docs/SWARM_FAUNA.md §12): sortfeel's flat wells + wander, the 1-in-k update
                WellDead = config.SortWellDead, WellDeadTime = config.SortWellDeadTime,
                Wander = config.SortWander, WanderTau = config.SortWanderTau,
                Frac = Mathf.Max(1, config.SortUpdateFraction),
            };
            for (int e = 0; e < 4; e++) p.EggCost[e] = SwarmFaunaConfigSO.Of(config.EggVolume, SwarmFaunaConfigSO.ToElement(e));
            if (TryBand(out float lo, out float hi)) { p.BandInner = lo; p.BandOuter = hi; }
            return new SwarmSortCore(_plans, p, Random.Range(1, int.MaxValue));
        }

        /// <summary>
        /// The evolved rule given a fate in its GAME settings (Docs/SWARM_FAUNA.md §11): the trained G2 network
        /// moves every member, perceiving and steering in the BODY frame so it sees the frame it was trained in;
        /// a designed pull steers each to its committed well; sort's code and composition (the Sort fields).
        /// Funded laying, animated molts, and an egg the network has not hatched in time HATCHES instead of
        /// vanishing (it was paid for with eaten mass). Null - the swarm does not hatch - without the rule asset.
        /// </summary>
        ISwarmCore BuildEvoFateCore(Cell host)
        {
            if (!config.EvoRule)
            {
                CSDebug.LogError($"{name}: an EvoFate swarm needs SwarmFaunaConfigSO.EvoRule (the trained network) - run " +
                                 "Tools/Build/author_swarm_fauna.py. Falling back to the field model.");
                return BuildFieldCore(host);
            }
            var rule = SwarmEvoRule.Parse(config.EvoRule.text);
            var p = new SwarmEvoFateParams
            {
                Pull = config.EvoPull, E0 = config.EvoDeadZone, Adh = config.EvoAdhesion,
                TMix = config.EvoTimeSpeed, TE0 = config.EvoTimeDeadZone,
                K = config.SortWellsPerType, PerWell = config.SortUnitsPerWell, CovScale = config.SortWellWidth,
                Dwell = config.SortDwell, LayRate = config.SortLayRate, LayMax = config.SortLayMax,
                PCross = config.SortCrossChance, FillTol = config.SortFillTolerance, Over = config.SortBodyFill,
                Molt = true, Transfer = true, MoltRate = config.SortMoltRate, MoltSteps = config.SortMoltSteps,
                KillLayHoldSteps = Mathf.RoundToInt(config.KillLayHoldSeconds * config.TickHz),
                Periods = new[]
                {
                    Mathf.Max(1, Mathf.RoundToInt(config.SortFramePeriod.x)), Mathf.Max(1, Mathf.RoundToInt(config.SortFramePeriod.y)),
                    Mathf.Max(1, Mathf.RoundToInt(config.SortFramePeriod.z)), Mathf.Max(1, Mathf.RoundToInt(config.SortFramePeriod.w)),
                },
                DomainSlots = false, Funded = true, KeepEggs = true, Animate = true, Oriented = true,
                Cruise = config.Cruise, Turn = config.TurnPerStep,
                Membrane = SimMembrane(host), CrossCost = config.CrossElementCost, Cap = PlanCap,
            };
            for (int e = 0; e < 4; e++) p.EggCost[e] = SwarmFaunaConfigSO.Of(config.EggVolume, SwarmFaunaConfigSO.ToElement(e));
            if (TryBand(out float lo, out float hi)) { p.BandInner = lo; p.BandOuter = hi; }
            return new SwarmEvoFateCore(_plans, p, rule, Random.Range(1, int.MaxValue));
        }

        void StartLoop()
        {
            if (config.SwarmLoopEvent.IsNull) return;
            _loop = gameObject.AddComponent<FMODUnity.StudioEventEmitter>();
            _loop.EventReference = config.SwarmLoopEvent;
            gameObject.AddComponent<CosmicShore.Gameplay.Audio.EmitterSfxVolumeBinder>();
            _loop.Play();
        }

        // ───────────────────────────────────────────────────────────────── members

        void SpawnMember(int i, int parent)
        {
            var e = SwarmFaunaConfigSO.ToElement(_core.Elem[i]);
            var pos = ToWorld(_core.Pos[i]);
            var face = Face(_core.Facing[i]);
            var member = Instantiate(config.TadpolePrefab, pos, face);
            member.Bind(HostCell, this, i, e, SwarmFaunaConfigSO.Of(config.HeartWorldScale, e),
                        ShapeFor(i, _core.Elem[i]), PrismZ(i, _core.Elem[i]));
            HostCell.RegisterSpawnedObject(member.gameObject);
            // The replication seam every fauna producer reaches. A swarm member carries no
            // NetworkObject (members are client-local, like every freestyle creature), so this
            // only neutralizes; it is here so the seam gate holds for this producer too.
            FaunaNetworkSync.ServerSpawn(member);

            // continuity of existence: a newborn grows in from nothing
            member.transform.localScale = Vector3.one * 0.001f;
            _members[i] = member;
            _birthTime[i] = Time.time;
            _heartFactor[i] = 1f;
            _danger[i] = false;
            _prevPos[i] = _core.Pos[i];
            _prevFace[i] = _core.Facing[i];
        }

        /// <summary>
        /// Give queued members their bodies, within the cell-wide per-frame budget. A queued index
        /// that died or was already given a body in the meantime is skipped.
        /// </summary>
        void HatchPending()
        {
            int frame = Time.frameCount;
            if (s_spawnFrame != frame) { s_spawnFrame = frame; s_spawnsThisFrame = 0; }
            while (_pending.Count > 0 && s_spawnsThisFrame < config.MaxSpawnsPerFrame)
            {
                int i = _pending.Dequeue();
                if (i < 0 || i >= _members.Length || !_core.Alive[i] || _members[i]) continue;
                SpawnMember(i, -1);
                s_spawnsThisFrame++;
            }
        }

        /// <summary>A member died (any path). The body re-solves its homes around the hole.</summary>
        public void HandleMemberDeath(SwarmTadpoleFauna member)
        {
            if (_core == null || !member) return;
            int i = member.Index;
            if (i < 0 || i >= _members.Length || _members[i] != member) return;
            _core.Kill(i);
            _members[i] = null;
        }

        /// <summary>Body prism shape for member i as element e: the core's look for it (the field core:
        /// its home slot's prism when the slot is of its own element; the grid core: the prism its look
        /// state has eased to), else a typical prism of that element. Local scale, long axis +z.</summary>
        Vector3 ShapeFor(int i, int e)
        {
            Vector3 h = _defaultHalf[e];
            if (_core.TryGetLook(i, e, out var s, out _)) h = new Vector3(s.X, s.Y, s.Z);
            float m = 2f * config.UnitScale * config.PrismScale;
            return new Vector3(h.y * m, h.z * m, h.x * m);
        }

        float PrismZ(int i, int e) =>
            -(SwarmFaunaConfigSO.Of(config.HeartWorldScale, SwarmFaunaConfigSO.ToElement(e))
              + config.HeartPrismGap + 0.5f * ShapeFor(i, e).z);

        // ───────────────────────────────────────────────────────────────── the clock

        // Enter Play Mode without a domain reload keeps statics: start every session clean.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            s_live.Clear();
            s_schedFrame = -1; s_cursor = 0;
            s_warnedSaturated = false; s_saturatedSince = -1f;
            s_spawnFrame = -1; s_spawnsThisFrame = 0;
        }

        protected override void OnDestroy()
        {
            s_live.Remove(this);
            base.OnDestroy();
        }

        void Update()
        {
            if (_core == null) return;

            // whichever swarm updates first this frame steps the whole cell
            if (s_schedFrame != Time.frameCount)
            {
                s_schedFrame = Time.frameCount;
                using (s_mSchedule.Auto()) Schedule();
            }

            using (s_mHatch.Auto()) HatchPending();
            if (PoseThisFrame()) Render(Mathf.Clamp01(_acc / _dt));
        }

        /// <summary>
        /// Step every live swarm that is due, round-robin, until the cell-wide budget is spent. Each swarm
        /// may bank at most MaxStepsPerFrame steps; anything beyond is dropped, so falling behind slows the
        /// swarms down rather than charging the next frame for it. The cursor resumes where the last frame
        /// stopped, so no swarm is starved by its position in the list.
        /// </summary>
        static void Schedule()
        {
            float dt = Time.deltaTime, budgetMs = 0f;
            for (int k = s_live.Count - 1; k >= 0; k--)
            {
                var sw = s_live[k];
                if (!sw) { s_live.RemoveAt(k); continue; }
                if (sw._core == null || !sw.isActiveAndEnabled) continue;
                sw._acc = Mathf.Min(sw._acc + dt, sw._dt * sw.config.MaxStepsPerFrame);
                budgetMs = Mathf.Max(budgetMs, sw.config.SimBudgetMsPerFrame);
            }
            int count = s_live.Count;
            if (count == 0 || budgetMs <= 0f) return;

            long start = System.Diagnostics.Stopwatch.GetTimestamp();
            long budget = (long)(budgetMs * 0.001 * System.Diagnostics.Stopwatch.Frequency);
            bool overBudget = false;
            // passes: one step per due swarm per pass, so a budget that runs out mid-pass has still
            // spread its steps evenly across the cell
            for (bool any = true; any && !overBudget;)
            {
                any = false;
                for (int n = 0; n < count && s_live.Count > 0; n++)
                {
                    if (System.Diagnostics.Stopwatch.GetTimestamp() - start >= budget) { overBudget = true; break; }
                    s_cursor = (s_cursor + 1) % s_live.Count;
                    var sw = s_live[s_cursor];
                    if (!sw || sw._core == null || !sw.isActiveAndEnabled || sw._acc < sw._dt) continue;
                    sw.Tick();
                    sw._acc -= sw._dt;
                    any = true;
                }
            }
            ReportSaturation(overBudget, count, budgetMs);
        }

        static void ReportSaturation(bool overBudget, int swarms, float budgetMs)
        {
            if (!overBudget) { s_saturatedSince = -1f; return; }
            if (s_saturatedSince < 0f) { s_saturatedSince = Time.unscaledTime; return; }
            if (s_warnedSaturated || Time.unscaledTime - s_saturatedSince < 5f) return;
            s_warnedSaturated = true;
            CSDebug.LogWarning($"[Swarm] {swarms} swarms have needed more than the {budgetMs:F1} ms/frame simulation " +
                               "budget for 5 s, so they are swimming in slow motion (time is dropped, never banked). " +
                               "Profile SwarmFauna.Step.* to see which model is dear; in the Editor, check Code " +
                               "Optimization is set to Release (the bug icon, bottom right) - Debug codegen runs " +
                               "the swarm cores several times slower.");
        }

        /// <summary>Far swarms are re-posed every FarPoseInterval frames, staggered so they do not all land
        /// on one frame. Colliders and index entries move with the transforms, so they never disagree.</summary>
        bool PoseThisFrame()
        {
            int every = config.FarPoseInterval;
            if (every <= 1) return true;
            var cam = Camera.main;
            if (!cam) return true;
            float near = config.PoseEveryFrameWithin + _core.Plan.Radius * config.UnitScale;
            if ((cam.transform.position - transform.position).sqrMagnitude <= near * near) return true;
            return (Time.frameCount + _poseOffset) % every == 0;
        }

        void Tick()
        {
            int n = _core.Cap;
            for (int i = 0; i < n; i++) { _prevPos[i] = _core.Pos[i]; _prevFace[i] = _core.Facing[i]; }

            using (s_mSense.Auto()) SenseVessels();
            _core.SwimTarget = ToSim(Goal);
            using (s_mStep[Mathf.Clamp((int)config.Model, 0, s_mStep.Length - 1)].Auto())
                _core.Step(new System.ReadOnlySpan<SwarmPredator>(_preds, 0, _predCount));

            var events = _core.Events;
            for (int q = 0; q < events.Count; q++)
            {
                var ev = events[q];
                switch (ev.Kind)
                {
                    case SwarmEventKind.Laid:
                        _pending.Enqueue(ev.Index);
                        break;
                    case SwarmEventKind.MoltBegan:
                        if (_members[ev.Index]) _members[ev.Index].SetShape(ShapeFor(ev.Index, ev.Other), PrismZ(ev.Index, ev.Other));
                        break;
                    case SwarmEventKind.Switched:
                        OnMorph(ev.Index, ev.Other);
                        break;
                    case SwarmEventKind.Starved:
                        // only a research-mode grid core kills by itself; the game never builds one
                        // (BuildGridCore sets HungerKills = false). Kept so the death is never silent.
                        if (_members[ev.Index]) _members[ev.Index].Starve();
                        break;
                }
            }
            events.Clear();

            // shapes and tiers follow the homes, at the homes' own cadence
            using (s_mMembers.Auto())
            {
                bool reshape = _core.Clock % 8 == 0;
                for (int i = 0; i < n; i++)
                {
                    var m = _members[i];
                    if (!m || !_core.Alive[i]) continue;
                    int e = _core.EffectiveElement(i);
                    if (reshape && _core.Molt[i] <= 0f) m.SetShape(ShapeFor(i, e), PrismZ(i, e));
                    UpdateTier(i, m, e);
                }
            }

            using (s_mFeed.Auto()) Feed();
            Starvation();
            Extinction();
        }

        void OnMorph(int fromPlan, int toPlan)
        {
            CSDebug.LogVerbose(CSLogChannel.Ecology,
                $"[Swarm] {name} morphs {_plans[fromPlan].Kind} -> {_plans[toPlan].Kind} ({_core.AliveCount} tadpoles)");
            if (!config.MorphEvent.IsNull && AudioSystem.Instance)
                AudioSystem.Instance.PlaySFXEvent(config.MorphEvent, transform.position);
        }

        void UpdateTier(int i, SwarmTadpoleFauna m, int e)
        {
            if (e != 0) { m.SetTier(false, false); _danger[i] = false; return; }   // only Charge changes state
            float st = _core.Startle[i];
            if (_danger[i]) { if (st < config.DangerExit) _danger[i] = false; }
            else if (st > config.DangerEnter) _danger[i] = true;
            bool shield = _core.TryGetLook(i, 0, out _, out int tier) && tier == 2;
            m.SetTier(_danger[i], shield);
        }

        // ───────────────────────────────────────────────────────────────── render

        void Render(float alpha)
        {
            float now = Time.time;
            // a far swarm is posed every few frames, so the molt advances by the time since ITS last pose
            float dt = _lastPoseTime < 0f ? Time.deltaTime : Mathf.Min(now - _lastPoseTime, 0.5f);
            _lastPoseTime = now;
            s_mPose.Begin();
            float bloom = config.BirthBloomSeconds, moltRate = dt / config.MoltHeartSeconds;
            for (int i = 0; i < _core.Cap; i++)
            {
                var m = _members[i];
                if (!m || !_core.Alive[i]) continue;

                var p = SVector3.Lerp(_prevPos[i], _core.Pos[i], alpha);
                var f = SVector3.Lerp(_prevFace[i], _core.Facing[i], alpha);
                m.transform.SetPositionAndRotation(ToWorld(p), Face(f));

                float age = now - _birthTime[i];
                if (age < bloom)
                {
                    float a = Mathf.Clamp01(age / bloom);
                    m.transform.localScale = Vector3.one * Mathf.Max(0.001f, a * a * (3f - 2f * a));
                }
                else if (m.transform.localScale.x != 1f) m.transform.localScale = Vector3.one;

                // MOLT, made visible: the heart shrinks away, re-forms as the new element, grows back.
                var want = SwarmFaunaConfigSO.ToElement(_core.Molt[i] >= 0.5f ? _core.MoltTo[i] : _core.Elem[i]);
                float h = _heartFactor[i];
                if (m.HeartElement != want)
                {
                    h -= moltRate;
                    if (h <= 0f) { h = 0f; m.ReformHeart(want, SwarmFaunaConfigSO.Of(config.HeartWorldScale, want)); }
                    m.SetHeartDisplay(h);
                }
                else if (h < 1f)
                {
                    h = Mathf.Min(1f, h + moltRate);
                    m.SetHeartDisplay(h);
                }
                _heartFactor[i] = h;
            }
            s_mPose.End();

            // the mover contract (spatial index + render-entity matrix), measured on its own
            using (s_mSync.Auto())
                for (int i = 0; i < _core.Cap; i++)
                {
                    var m = _members[i];
                    if (m && _core.Alive[i]) m.SyncBodyToIndex();
                }
            transform.position = ToWorld(_core.Anchor);
        }

        // ───────────────────────────────────────────────────────────────── vessels

        void SenseVessels()
        {
            _predCount = 0;
            _seen.Clear();
            float radius = _core.Plan.Radius * config.UnitScale * 1.6f + config.SenseMargin;
            int hits = Physics.OverlapSphereNonAlloc(transform.position, radius, OverlapScratch, NonPrismOverlapMask);
            float toSimVel = 1f / (config.UnitScale * config.TickHz);
            for (int h = 0; h < hits && _predCount < _preds.Length; h++)
            {
                var col = OverlapScratch[h];
                if (!col) continue;
                if (!col.TryGetComponent(out IVesselStatus status))
                    status = col.GetComponentInParent<IVesselStatus>();
                if (status == null || _seen.Contains(status) || status is not Component c || !c) continue;
                _seen.Add(status);
                var v = status.Course * status.Speed;
                _preds[_predCount++] = new SwarmPredator
                {
                    C = ToSim(c.transform.position),
                    V = Sim(v) * toSimVel,
                    R = config.VesselRadius / config.UnitScale,
                };
            }
        }

        // ───────────────────────────────────────────────────────────────── food

        /// <summary>
        /// Grazing. A few members per step take one bite each of the nearest edible FLORA prism within
        /// reach; the prism is consumed (suctioned into the biter - the food web's sanctioned
        /// down-force on mass) and its volume is banked in the stomach under the PLANT's element,
        /// which is what later pays for eggs. A full stomach stops grazing: a grown body does not
        /// strip its feeding ground for nothing.
        /// </summary>
        void Feed()
        {
            float capacity = config.StomachEggs * (config.EggVolume.x + config.EggVolume.y + config.EggVolume.z + config.EggVolume.w) * 0.25f;
            float banked = _core.Stomach[0] + _core.Stomach[1] + _core.Stomach[2] + _core.Stomach[3];
            if (banked >= capacity) { _lastFedTime = Time.time; return; }

            var index = PrismSpatialIndex.EnsureInstance();
            if (index == null || !index.IsAvailable) return;

            int n = _core.Cap, bitten = 0;
            for (int tries = 0; tries < n && bitten < config.BitersPerStep; tries++)
            {
                _biteCursor = (_biteCursor + 1) % n;
                int i = _biteCursor;
                var m = _members[i];
                if (!m || !_core.Alive[i]) continue;
                bitten++;
                Vector3 at = m.transform.position;
                int found = index.QuerySphere(at, config.BiteRadius, FeedScratch);
                for (int q = 0; q < found; q++)
                {
                    var prism = FeedScratch[q];
                    if (!IsFood(prism, out int e)) continue;
                    float volume = Mathf.Max(0.001f, prism.Volume);
                    prism.Consume(m.transform, domain, _eaterName, false, true);
                    _core.Stomach[e] += volume;
                    _lastFedTime = Time.time;
                    _atPlantSince = -1f;
                    break;
                }
            }
        }

        /// <summary>
        /// The swarm's diet: living FLORA mass only, through the platform's one edibility rule
        /// (<see cref="Fauna.IsPreyForMe"/> = the cell's spatial diet + this species' band) and the
        /// shielded-mass rule (<see cref="Fauna.IsShieldedMass"/> - which is why a Charge plant, whose
        /// leaves are armoured, is no food at all). Returns the plant's element as a research index.
        /// </summary>
        bool IsFood(Prism prism, out int element)
        {
            element = -1;
            if (!prism || prism.destroyed || prism is not HealthPrism hp) return false;
            if (hp.ResolveOwnerFauna() != null) return false;          // another creature's body
            if (hp.LifeForm is not Flora plant || !plant) return false;  // flora only
            if (IsShieldedMass(prism)) return false;
            if (!IsPreyForMe(prism.transform.position, prism.Domain)) return false;
            element = SwarmFaunaConfigSO.ToIndex(plant.Element);
            return element >= 0;
        }

        /// <summary>
        /// Where to swim: the nearest living plant inside this swarm's band that it has not just
        /// found grazed bare; else a fresh point in the band. Runs on the base class's goal clock.
        /// </summary>
        protected override Vector3 ResolveGoal()
        {
            if (_core == null || !HostCell) return Goal;
            Vector3 here = transform.position;
            float now = Time.time;

            // a plant the body has hovered over for a while without a single bite is grazed out
            if (_goalPlant && _atPlantSince >= 0f && now - _atPlantSince > 10f)
            {
                _barren[_goalPlant] = now;
                _goalPlant = null;
                _atPlantSince = -1f;
            }

            var plant = FloraHeartRegistry.NearestToPoint(here, f =>
                f.IsDying || !IsInsideBand(f.HeartTransform.position) ||
                HostCell.IsInsideNucleus(f.HeartTransform.position) ||
                (_barren.TryGetValue(f, out float t) && now - t < 45f));
            if (plant)
            {
                if (plant != _goalPlant) { _goalPlant = plant; _atPlantSince = -1f; }
                Vector3 target = plant.HeartTransform.position;
                if (_atPlantSince < 0f && (target - here).sqrMagnitude < 60f * 60f) _atPlantSince = now;
                return target;
            }

            _goalPlant = null;
            if ((Goal - here).sqrMagnitude > 40f * 40f) return Goal;   // still travelling to the last point
            Vector3 radial = here - _centre;
            Vector3 wander = here + Random.onUnitSphere * config.WanderReach;
            // keep the wander on this swarm's shell; the Goal setter clamps it into the band
            if (radial.sqrMagnitude > 1f) wander = _centre + (wander - _centre).normalized * radial.magnitude;
            return wander;
        }

        // ───────────────────────────────────────────────────────────────── starvation

        void Starvation()
        {
            float now = Time.time;
            if (now - _lastFedTime < config.StarvationSeconds) return;
            if (now - _lastShedTime < config.ShedIntervalSeconds) return;
            _lastShedTime = now;

            // shed the member the body needs least - the core decides who (the field core: one of the
            // most-surplus element; the grid core: the hungriest misplaced-surplus member)
            int victim = _core.StarvationVictim();
            if (victim >= 0 && _members[victim] && _core.Alive[victim]) _members[victim].Starve();
        }

        void Extinction()
        {
            if (_core.AliveCount > 0) { _extinctSince = -1f; return; }
            if (_extinctSince < 0f) { _extinctSince = Time.time; return; }
            if (Time.time - _extinctSince < config.ExtinctLingerSeconds) return;
            // The anchor has no body and no heart - removing it pops nothing - and the cell's seeder
            // hatches a fresh swarm in its place (extinction recovery, the seeder's sanctioned job).
            if (DespawnOrDestroy()) return;
            Destroy(gameObject);
        }

        // ───────────────────────────────────────────────────────────────── units

        SVector3 ToSim(Vector3 world) => Sim((world - _centre) / config.UnitScale);
        Vector3 ToWorld(SVector3 sim) => _centre + Uni(sim) * config.UnitScale;
        static SVector3 Sim(Vector3 v) => new(v.x, v.y, v.z);
        static Vector3 Uni(SVector3 v) => new(v.X, v.Y, v.Z);

        Quaternion Face(SVector3 f)
        {
            var fw = Uni(f);
            if (fw.sqrMagnitude < 1e-6f) fw = Uni(_core.BX);
            var up = Uni(_core.BY);
            if (Mathf.Abs(Vector3.Dot(fw.normalized, up)) > 0.98f) up = Uni(_core.BZ);
            return Quaternion.LookRotation(fw, up);
        }
    }
}
