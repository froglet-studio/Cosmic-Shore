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
    /// be preyed on. The lifeforms are its MEMBERS, each with its own heart and body prism, each dying
    /// through the sealed fauna death path.
    ///
    /// ROUND 7 (Docs/SWARM_FAUNA.md §14) - ~1,000 members a swarm at ~zero main-thread cost:
    ///  • the TICK (simulation step + the frame it draws) runs on a worker thread (<see cref="SwarmTickJob"/>);
    ///    the main thread queues kills and food, and swaps buffers once per tick;
    ///  • every LIVING member is DRAWN from one GPU buffer (<see cref="SwarmMemberRenderer"/>), uploaded
    ///    once per tick and interpolated by the shader - no per-member transform, matrix or index write;
    ///  • a member is DATA unless a vessel is near: then (and only then) it gets a PROXY
    ///    (<see cref="SwarmTadpoleFauna"/>) - a real heart and a real body prism with colliders - so every
    ///    weapon, ram, joust and skim finds a lifeform to act on. The swarm keeps drawing it; the proxy's
    ///    own visuals take over only at its death (the released heart, the skeleton). A member the swarm
    ///    sheds to starvation is given a proxy first and withers through the same sealed death.
    ///
    /// Invariants it touches (Docs/SWARM_FAUNA.md §2, §14.4): mass is conserved (every egg is PAID for out
    /// of eaten flora volume, 1:1), there is no imposed death (members only die to vessels, predators or
    /// starvation), one colour (every member wears this anchor's domain), every member drops its crystal
    /// (only through a proxy - a member with no proxy cannot die), and nothing pops (births bloom in the
    /// shader, molts re-form in the shader, a proxy appears and leaves under an unchanged picture, deaths
    /// wither through the platform).
    /// </summary>
    public class SwarmFauna : Fauna
    {
        [Header("Swarm")]
        [SerializeField] SwarmFaunaConfigSO config;

        ISwarmCore _core;
        SwarmTickJob _job;
        SwarmMemberRenderer _render;
        bool _gpu, _inline;
        SwarmPlanData[] _plans;
        Vector3 _centre;                 // the host cell's centre (sim origin)
        float _dt, _acc;
        float _bloomTicks;
        int _cap;

        // proxies: the only members with a GameObject
        SwarmTadpoleFauna[] _proxy;
        float[] _wantedAt;               // last time the tick said "a vessel is near this member"
        bool[] _gone;                    // died; masked until the tick that applies the kill is published
        bool[] _starving;                // given a proxy to be shed - Starve() once the proxy is Ready
        readonly List<int> _proxySlots = new();
        readonly List<int> _goneSlots = new();

        // feeding: a few "mouths" a bitten prism is suctioned into, posed at their biter
        Transform[] _mouth;
        int[] _mouthSlot;
        float[] _mouthUntil;
        int _mouthCursor;

        int _biteCursor;
        float _lastFedTime, _lastShedTime, _extinctSince = -1f;
        Element _startElement = Element.Mass;
        bool _seeded, _warnedError;
        string _eaterName;
        FMODUnity.StudioEventEmitter _loop;
        readonly List<IVesselStatus> _seen = new(8);
        Flora _goalPlant;
        float _atPlantSince = -1f;
        readonly Dictionary<Flora, float> _barren = new();
        static int s_spawnFrame = -1, s_spawnsThisFrame;
        // round 8: proxies materialised because a weapon or predator reached a member (§16.2), budgeted cell-wide
        static int s_hitFrame = -1, s_hitsThisFrame;
        static readonly List<SwarmFauna> s_live = new();
        float _alpha;                    // this frame's display alpha - where every member is DRAWN right now
        Domains[] _slotDomain = { Domains.Blue, Domains.Blue, Domains.Blue };
        readonly List<int> _qScratch = new(64), _qHits = new(64);
        // inline ticks (off-thread disabled, or WebGL) share one per-frame budget - the round-6 rule
        static int s_inlineFrame = -1;
        static double s_inlineMs;

        static readonly ProfilerMarker s_mCollect = new("SwarmFauna.Tick.Collect");
        static readonly ProfilerMarker s_mUpload = new("SwarmFauna.Tick.Upload");
        static readonly ProfilerMarker s_mProxies = new("SwarmFauna.Tick.Proxies");
        static readonly ProfilerMarker s_mSense = new("SwarmFauna.Tick.SenseVessels");
        static readonly ProfilerMarker s_mFeed = new("SwarmFauna.Tick.Feed");
        static readonly ProfilerMarker s_mKick = new("SwarmFauna.Tick.Kick");
        static readonly ProfilerMarker s_mInline = new("SwarmFauna.Tick.InlineStep");
        static readonly ProfilerMarker s_mPose = new("SwarmFauna.Frame.PoseProxies");
        static readonly ProfilerMarker s_mDraw = new("SwarmFauna.Frame.Draw");
        static readonly ProfilerMarker s_mVolume = new("SwarmFauna.Tick.Volume");

        public SwarmFaunaConfigSO Config => config;

        /// <summary>The body plan the swarm is currently growing ("mass", "space", "charge", "time").</summary>
        public string CurrentPlan => _job != null && _plans != null ? _plans[Mathf.Clamp(_job.PlanIx, 0, 3)].Kind : "";

        /// <summary>Live members (eggs included), as of the last published tick.</summary>
        public int MemberCount => _job != null ? _job.AliveCount : 0;

        /// <summary>Members that currently have a proxy (a GameObject, colliders on).</summary>
        public int ProxyCount => _proxySlots.Count;

        public override float CurrentSpeed => config ? config.Cruise * config.UnitScale * config.TickHz : 0f;

        /// <summary>A member's swim speed in world units/s - what a jouster has to outrun.</summary>
        public float MemberSpeed(int i) => _job != null && i >= 0 && i < _cap ? _job.Speed[i] : 0f;

        int Density => config ? Mathf.Max(1, config.PlanDensity) : 1;

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

            _centre = host.transform.position;
            _dt = 1f / config.TickHz;
            _bloomTicks = config.BirthBloomSeconds * config.TickHz;
            _eaterName = "swarm";

            _core = config.Model switch
            {
                SwarmModel.Grid => BuildGridCore(host),
                SwarmModel.Sort => BuildSortCore(host),
                SwarmModel.EvoFate => BuildEvoFateCore(host),
                _ => BuildFieldCore(host),
            };
            _cap = _core.Cap;
            _proxy = new SwarmTadpoleFauna[_cap];
            _wantedAt = new float[_cap];
            _gone = new bool[_cap];
            _starving = new bool[_cap];

            var anchor = ToSim(transform.position);
            var radial = transform.position - _centre;
            var tangent = Vector3.Cross(radial.sqrMagnitude > 1f ? radial.normalized : Vector3.forward, Random.onUnitSphere);
            if (tangent.sqrMagnitude < 1e-4f) tangent = Vector3.right;
            _core.Seed(SwarmFaunaConfigSO.ToIndex(_startElement), config.SeedMembers * Density, anchor, Sim(tangent.normalized));
            _core.SwimTarget = anchor;

            _job = new SwarmTickJob(_core, BuildTickSettings(), config.TickHz) { SwimTarget = anchor };
            _job.Prime();
            _inline = !config.SimulateOffMainThread || Application.platform == RuntimePlatform.WebGLPlayer;

            _slotDomain = BuildSlotDomains();   // before the palette: each slot is drawn in its domain
            _gpu = false;
            if (config.DrawMembersOnGpu)
            {
                _render = new SwarmMemberRenderer(config.MemberShader, config.TadpolePrefab, _cap, gameObject.layer);
                _gpu = _render.Valid;
                if (_gpu) { ApplyPalette(); _render.Upload(_job); }
            }

            BuildMouths();
            s_live.Add(this);
            _lastFedTime = Time.time;
            _acc = Random.value * _dt;   // stagger the cell's swarms across frames from the first tick
            StartLoop();
            CSDebug.LogVerbose(CSLogChannel.Ecology,
                $"[Swarm] {name} ({config.Model}, density {Density}) hatched as {_core.Plan.Kind} with " +
                $"{_job.AliveCount} tadpoles at r={radial.magnitude:F0}; tick {(_inline ? "inline" : "off-thread")}, " +
                $"members {(_gpu ? "GPU-drawn" : "GameObjects")}");
        }

        SwarmTickSettings BuildTickSettings()
        {
            var half = SwarmPlanLibrary.TypicalHalfExtents(_plans);
            var s = new SwarmTickSettings
            {
                Centre = Sim(_centre), UnitScale = config.UnitScale, PrismScale = config.PrismScale,
                HeartPrismGap = config.HeartPrismGap,
                DangerEnter = config.DangerEnter, DangerExit = config.DangerExit,
                EngageRadius = config.EngageRadius, MaxEngaged = config.MaxProxies,
                MultiDomain = config.MultiDomain,
            };
            for (int e = 0; e < 4; e++)
            {
                s.HeartWorldScale[e] = SwarmFaunaConfigSO.Of(config.HeartWorldScale, SwarmFaunaConfigSO.ToElement(e));
                s.DefaultHalf[e] = new SVector3(half[e].x, half[e].y, half[e].z);
            }
            return s;
        }

        readonly Vector4[] _tierDark = new Vector4[9], _tierBright = new Vector4[9];

        /// <summary>Each domain SLOT's colours in the palette's own tier pairs (Docs/PALETTE.md §2) - a one-colour
        /// swarm's three slots are all its domain; a MultiDomain swarm's are the slot table (§16.4).</summary>
        void ApplyPalette()
        {
            var colors = config.Theme ? config.Theme.ColorSet : null;
            Color hd = new(0.1f, 0.2f, 0.5f), hb = new(0.8f, 0.9f, 1.4f);
            if (colors == null)
                CSDebug.LogWarning($"{name}: SwarmFaunaConfigSO.Theme is not assigned - members are drawn in fallback colours.");
            for (int slot = 0; slot < 3; slot++)
            {
                Color bd = new(0.05f, 0.2f, 0.4f), bb = new(0.4f, 0.8f, 1.2f);
                Color dd = bd, db = new(1.5f, 0.3f, 0.1f), sd = bd, sb = bb;
                if (colors != null)
                {
                    var d = _slotDomain[slot];
                    colors.TryGetPrismKindColors(d, PrismKind.Plain, out bb, out bd);
                    colors.TryGetPrismKindColors(d, PrismKind.Danger, out db, out dd);
                    colors.TryGetPrismKindColors(d, PrismKind.Shielded, out sb, out sd);
                }
                _tierDark[0 + slot] = bd; _tierBright[0 + slot] = bb;
                _tierDark[3 + slot] = dd; _tierBright[3 + slot] = db;
                _tierDark[6 + slot] = sd; _tierBright[6 + slot] = sb;
            }
            // a living heart wears the neutral (no-domain) crystal pair - the Crystal's own rule
            if (colors != null && colors.TryGetColorSetByDomain(Domains.Blue, out var neutral) && neutral != null)
            { hd = neutral.DullCrystalColor; hb = neutral.BrightCrystalColor; }
            _render.SetColours(_tierDark, _tierBright, hd, hb, config.HeartWorldScale, 2f);

            // the prism OPENS with distance (BlockGraph's spread) - read each tier's own authored spread off the
            // base materials every live prism is cloned from (ThemeManager), so a member opens exactly as much
            // as any other prism of its tier would
            var set = config.Theme ? config.Theme.BaseMaterialSet : null;
            _render.SetSpread(TierSpread(set ? set.BlockMaterial : null),
                              TierSpread(set ? set.DangerousBlockMaterial : null),
                              TierSpread(set ? set.ShieldedBlockMaterial : null));
        }

        static readonly int SpreadPropId = Shader.PropertyToID("_Spread");
        static readonly int SqrDistancePropId = Shader.PropertyToID("_SqrDistance");

        static Vector4 TierSpread(Material m)
        {
            if (!m || !m.HasProperty(SpreadPropId)) return new Vector4(0f, 0f, 0f, 100000f);
            Vector4 s = m.GetVector(SpreadPropId);
            s.w = m.HasProperty(SqrDistancePropId) ? m.GetFloat(SqrDistancePropId) : 100000f;
            return s;
        }

        void BuildMouths()
        {
            const int n = 8;
            _mouth = new Transform[n]; _mouthSlot = new int[n]; _mouthUntil = new float[n];
            for (int q = 0; q < n; q++)
            {
                var go = new GameObject("SwarmMouth");
                go.transform.SetParent(transform, false);
                _mouth[q] = go.transform;
                _mouthSlot[q] = -1;
            }
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
                FoodDomains = config.MultiDomain,   // a field swarm has no regions: only the colour follows the food
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
                DomainSlots = config.MultiDomain, FoodDomains = config.MultiDomain, HungerKills = false, Funded = true, Oriented = true,
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
                Dwell = config.SortDwell, LayRate = config.SortLayRate, LayMax = config.SortLayMax * Density,
                PCross = config.SortCrossChance, FillTol = config.SortFillTolerance, Over = config.SortBodyFill,
                Molt = true, Transfer = true, MoltRate = config.SortMoltRate, MoltSteps = config.SortMoltSteps, MoltWindow = -1,
                KillLayHoldSteps = Mathf.RoundToInt(config.KillLayHoldSeconds * config.TickHz),
                Periods = new[]
                {
                    Mathf.Max(1, Mathf.RoundToInt(config.SortFramePeriod.x)), Mathf.Max(1, Mathf.RoundToInt(config.SortFramePeriod.y)),
                    Mathf.Max(1, Mathf.RoundToInt(config.SortFramePeriod.z)), Mathf.Max(1, Mathf.RoundToInt(config.SortFramePeriod.w)),
                },
                DomainSlots = config.MultiDomain, FoodDomains = config.MultiDomain, Funded = true, Animate = true, WellLook = true, Oriented = true,
                Cruise = config.Cruise, Turn = config.TurnPerStep,
                Membrane = SimMembrane(host), CrossCost = config.CrossElementCost, Cap = PlanCap,
                // round 6 (Docs/SWARM_FAUNA.md §12): sortfeel's flat wells + wander, the 1-in-k update
                WellDead = config.SortWellDead, WellDeadTime = config.SortWellDeadTime,
                Wander = config.SortWander, WanderTau = config.SortWanderTau,
                Frac = Mathf.Max(1, config.SortUpdateFraction),
                // round 7: a fixed-size ship startles density^(-2/3) of an upsampled body (SwarmSortParams.ThreatGain)
                ThreatGain = 3f * Mathf.Pow(Density, 2f / 3f),
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
                Dwell = config.SortDwell, LayRate = config.SortLayRate, LayMax = config.SortLayMax * Density,
                PCross = config.SortCrossChance, FillTol = config.SortFillTolerance, Over = config.SortBodyFill,
                Molt = true, Transfer = true, MoltRate = config.SortMoltRate, MoltSteps = config.SortMoltSteps,
                KillLayHoldSteps = Mathf.RoundToInt(config.KillLayHoldSeconds * config.TickHz),
                Periods = new[]
                {
                    Mathf.Max(1, Mathf.RoundToInt(config.SortFramePeriod.x)), Mathf.Max(1, Mathf.RoundToInt(config.SortFramePeriod.y)),
                    Mathf.Max(1, Mathf.RoundToInt(config.SortFramePeriod.z)), Mathf.Max(1, Mathf.RoundToInt(config.SortFramePeriod.w)),
                },
                DomainSlots = config.MultiDomain, FoodDomains = config.MultiDomain, Funded = true, KeepEggs = true, Animate = true, Oriented = true,
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

        // ───────────────────────────────────────────────────────────────── the frame

        // Enter Play Mode without a domain reload keeps statics: start every session clean.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            s_spawnFrame = -1; s_spawnsThisFrame = 0;
            s_inlineFrame = -1; s_inlineMs = 0;
            s_hitFrame = -1; s_hitsThisFrame = 0;
            s_live.Clear();
        }

        protected override void OnDestroy()
        {
            // a tick still running on a worker owns the core: it finishes into buffers nobody reads, which is
            // harmless (the job holds no Unity object). The GPU buffers are ours to release.
            _render?.Dispose();
            _render = null;
            s_live.Remove(this);
            var host = HostCell;
            if (host) host.ClearVirtualVolume(this);
            base.OnDestroy();
        }

        void Update()
        {
            if (_job == null) return;
            if (_job.Error != null) { ReportWorkerError(); return; }

            _acc += Time.deltaTime;
            if (_acc >= _dt) AdvanceTick();
            float alpha = Mathf.Clamp01(_acc / _dt);
            _alpha = alpha;

            using (s_mPose.Auto()) PoseProxies(alpha);
            if (_gpu)
                using (s_mDraw.Auto())
                {
                    float r = (_plans[Mathf.Clamp(_job.PlanIx, 0, 3)].Radius * 3f + 20f) * config.UnitScale + 40f;
                    var bounds = new Bounds(Uni(_job.Anchor), Vector3.one * (2f * r));
                    _render.Draw(bounds, alpha, _job.Tick + alpha, _bloomTicks, Uni(_job.BY), Uni(_job.BZ), _cap);
                }
        }

        /// <summary>
        /// The display has reached the end of the published pair. If the next tick (computed in the background
        /// while this pair was on screen) is done, publish it and start the one after; if it is still running,
        /// HOLD at the end of this pair and drop the time - a slow worker slows the swarm, never the frame.
        /// </summary>
        void AdvanceTick()
        {
            var state = _job.State;
            if (state == SwarmJobState.Running) { _acc = _dt; return; }
            if (state == SwarmJobState.Done)
            {
                using (s_mCollect.Auto()) _job.Collect();
                OnTickPublished();
                _acc -= _dt;
                if (_acc > _dt) _acc = _dt;   // never bank more than one tick (the round-6 rule)
            }
            Kick();
        }

        void Kick()
        {
            using (s_mSense.Auto()) SenseVessels();
            _job.SwimTarget = ToSim(Goal);
            if (!_inline)
            {
                using (s_mKick.Auto()) _job.Kick(false);
                return;
            }
            // inline: the cell's ticks share one per-frame budget; over it, this swarm waits a frame
            if (s_inlineFrame != Time.frameCount) { s_inlineFrame = Time.frameCount; s_inlineMs = 0; }
            if (s_inlineMs >= config.SimBudgetMsPerFrame) return;
            long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
            using (s_mInline.Auto()) _job.Kick(true);
            s_inlineMs += (System.Diagnostics.Stopwatch.GetTimestamp() - t0) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
        }

        void ReportWorkerError()
        {
            if (_warnedError) return;
            _warnedError = true;
            CSDebug.LogError($"[Swarm] {name}: the swarm's tick threw on its worker and the swarm has stopped: {_job.Error}");
        }

        /// <summary>Once per tick, on the main thread: everything the published frame drives.</summary>
        void OnTickPublished()
        {
            MaskGone();
            if (_gpu) using (s_mUpload.Auto()) _render.Upload(_job);

            var events = _job.Events;
            for (int q = 0; q < events.Count; q++)
                if (events[q].Kind == SwarmEventKind.Switched) OnMorph(events[q].Index, events[q].Other);

            using (s_mProxies.Auto()) SyncProxies();
            using (s_mVolume.Auto()) StateVirtualVolume();
            using (s_mFeed.Auto()) Feed();
            Starvation();
            Extinction();
            transform.position = Uni(_job.Anchor);
            ReportCost();
        }

        double _tickMsSum; int _tickMsCount; float _nextCostReport;

        /// <summary>The worker's cost is invisible to the Unity Profiler (thread-pool threads are not sampled),
        /// so a swarm states it itself every 10 s on the Ecology channel (off by default) - QA-SWARM-ROUND7.</summary>
        void ReportCost()
        {
            _tickMsSum += _job.LastTickMs; _tickMsCount++;
            if (Time.unscaledTime < _nextCostReport) return;
            _nextCostReport = Time.unscaledTime + 10f;
            if (CSDebug.IsVerbose(CSLogChannel.Ecology))
                CSDebug.LogVerbose(CSLogChannel.Ecology,
                    $"[Swarm] {name}: {_job.AliveCount} tadpoles, {_proxySlots.Count} proxies, worker {_tickMsSum / Mathf.Max(1, _tickMsCount):F2} ms/tick " +
                    $"({(_inline ? "inline" : "off-thread")}, {(_gpu ? "GPU-drawn" : "GameObjects")})");
            _tickMsSum = 0; _tickMsCount = 0;
        }

        // ───────────────────────────────────────────────────────────────── volume (round 8, §16.3)

        readonly double[] _virtualBySlot = new double[4];
        readonly int[] _cellSlotOfDomainSlot = new int[3];
        readonly List<int> _volumeExcluded = new();

        /// <summary>
        /// Once per TICK: tell the host cell how much fauna BODY volume this swarm holds that is not a registered
        /// prism (Docs/SWARM_FAUNA.md §16.3) - "volume is the spine", and a fauna body counts. The worker summed every
        /// drawn body by domain slot; from that the main thread removes (a) members that died since the tick started
        /// (masked, their proxy's skeleton is real mass now) and (b) members whose proxy body has finished creation and
        /// is therefore ALREADY in the cell's own sum. Nothing counts twice and nothing is missed: a member is virtual
        /// volume until its body is real, and real volume after. O(proxies + recent deaths), never O(members).
        /// The value is ABSOLUTE (Cell.SetVirtualVolume), stated only when it changed.
        /// </summary>
        void StateVirtualVolume()
        {
            var host = HostCell;
            if (!host) return;
            for (int ds = 0; ds < 3; ds++) _cellSlotOfDomainSlot[ds] = Cell.VolumeSlotOf(_slotDomain[ds]);
            _volumeExcluded.Clear();
            _volumeExcluded.AddRange(_goneSlots);   // dead in the cell, still in the frame's sum
            for (int q = 0; q < _proxySlots.Count; q++)
            {
                int i = _proxySlots[q];
                var m = _proxy[i];
                if (_gone[i] || !m || !m.Body || !m.Body.IsCreationComplete) continue;
                _volumeExcluded.Add(i);   // its HealthPrism is registered and counts itself
            }
            SwarmVolumeLedger.State(_job.VolumeBySlot, _cellSlotOfDomainSlot, _job.Counted, _job.Instances,
                                    _volumeExcluded, _virtualBySlot);
            host.SetVirtualVolume(this, _virtualBySlot);
        }

        /// <summary>A member that died since the running tick started is still alive in its frame: hide it
        /// until a tick that applied the kill is published (or the slot holds a NEW member).</summary>
        void MaskGone()
        {
            for (int q = _goneSlots.Count - 1; q >= 0; q--)
            {
                int i = _goneSlots[q];
                if (!_job.Instances[i].Alive || _job.BornThisTick(i)) { _gone[i] = false; _goneSlots.RemoveAt(q); continue; }
                _job.Instances[i].Flags = 0u;
            }
        }

        void OnMorph(int fromPlan, int toPlan)
        {
            CSDebug.LogVerbose(CSLogChannel.Ecology,
                $"[Swarm] {name} morphs {_plans[fromPlan].Kind} -> {_plans[toPlan].Kind} ({_job.AliveCount} tadpoles)");
            if (!config.MorphEvent.IsNull && AudioSystem.Instance)
                AudioSystem.Instance.PlaySFXEvent(config.MorphEvent, transform.position);
        }

        // ───────────────────────────────────────────────────────────────── proxies

        /// <summary>
        /// Members a vessel is near get a proxy (budgeted cell-wide per frame); a proxy whose vessel has been
        /// gone for ProxyLingerSeconds is retired. Without GPU drawing EVERY member needs one (the fallback).
        /// Each kept proxy takes its member's shape, tier and heart element from the published frame.
        /// </summary>
        void SyncProxies()
        {
            float now = Time.time;
            var inst = _job.Instances;
            if (_gpu)
            {
                for (int q = 0; q < _job.EngagedCount; q++)
                {
                    int i = _job.Engaged[q];
                    if (_gone[i]) continue;
                    _wantedAt[i] = now;
                    if (!_proxy[i]) TrySpawnProxy(i);
                }
            }
            else
            {
                for (int i = 0; i < _cap; i++)
                {
                    if (!inst[i].Alive || _gone[i]) continue;
                    _wantedAt[i] = now;
                    if (!_proxy[i]) TrySpawnProxy(i);
                }
            }

            for (int q = _proxySlots.Count - 1; q >= 0; q--)
            {
                int i = _proxySlots[q];
                var m = _proxy[i];
                if (!m) { _proxySlots.RemoveAt(q); _proxy[i] = null; continue; }
                if (!inst[i].Alive && !_gone[i])
                {
                    // the core dropped the member without a death through us (a research-mode core); the
                    // proxy dies the platform's way so the crystal is never lost
                    m.Starve();
                    continue;
                }
                if (_gpu && !_starving[i] && now - _wantedAt[i] > config.ProxyLingerSeconds)
                {
                    m.Retire();
                    _proxy[i] = null;
                    _proxySlots.RemoveAt(q);
                    continue;
                }
                ref var s = ref inst[i];
                var e = SwarmFaunaConfigSO.ToElement(s.CurMolt >= 0.5f && s.CurMolt < 1f ? s.HeartTo : s.HeartFrom);
                if (m.HeartElement != e) m.ReformHeart(e, SwarmFaunaConfigSO.Of(config.HeartWorldScale, e));
                m.SetShape(new Vector3(s.Scale.X, s.Scale.Y, s.Scale.Z), s.PrismZ);
                m.SetTier(s.Tier == 1, s.Tier == 2);
            }
        }

        bool TrySpawnProxy(int i, bool force = false)
        {
            int frame = Time.frameCount;
            if (s_spawnFrame != frame) { s_spawnFrame = frame; s_spawnsThisFrame = 0; }
            if (!force && s_spawnsThisFrame >= config.MaxSpawnsPerFrame) return false;
            s_spawnsThisFrame++;

            ref var s = ref _job.Instances[i];
            var e = SwarmFaunaConfigSO.ToElement(s.CurMolt >= 0.5f && s.CurMolt < 1f ? s.HeartTo : s.HeartFrom);
            var member = Instantiate(config.TadpolePrefab, Uni(s.CurPos), Face(s.CurFace));
            member.Bind(HostCell, this, i, e, SwarmFaunaConfigSO.Of(config.HeartWorldScale, e),
                        new Vector3(s.Scale.X, s.Scale.Y, s.Scale.Z), s.PrismZ, hideLive: _gpu,
                        memberDomain: MemberDomain(i),
                        memberAgeSeconds: Mathf.Max(0f, (_job.Tick + _alpha - s.BirthTick) / config.TickHz));
            HostCell.RegisterSpawnedObject(member.gameObject);
            // The replication seam every fauna producer reaches. A swarm member carries no NetworkObject
            // (members are client-local, like every freestyle creature), so this only neutralizes; it is
            // here so the seam gate holds for this producer too.
            FaunaNetworkSync.ServerSpawn(member);
            // without GPU drawing the proxy IS the member's picture, so it grows in (continuity of existence)
            if (!_gpu && _job.BornThisTick(i)) member.transform.localScale = Vector3.one * 0.001f;
            _proxy[i] = member;
            _proxySlots.Add(i);
            return true;
        }

        /// <summary>A member died (any path, through its proxy). The core hears of it on the next tick.</summary>
        public void HandleMemberDeath(SwarmTadpoleFauna member)
        {
            if (_job == null || !member) return;
            int i = member.Index;
            if (i < 0 || i >= _cap || _proxy[i] != member) return;
            _job.QueueKill(i);
            _proxy[i] = null;
            _proxySlots.Remove(i);
            _starving[i] = false;
            if (!_gone[i]) { _gone[i] = true; _goneSlots.Add(i); }
            if (_gpu) _render.HideSlot(_job, i);   // the proxy's own death visuals take over this frame
            else _job.Instances[i].Flags = 0u;
        }

        /// <summary>Proxies follow their member every frame (at most MaxProxies; the mover contract keeps
        /// the spatial index and colliders honest). The fallback also grows newborns in here.</summary>
        void PoseProxies(float alpha)
        {
            var up = Uni(_job.BY);
            for (int q = 0; q < _proxySlots.Count; q++)
            {
                int i = _proxySlots[q];
                var m = _proxy[i];
                if (!m) continue;
                m.transform.SetPositionAndRotation(Uni(_job.PoseAt(i, alpha)), Face(_job.FaceAt(i, alpha), up));
                if (!_gpu)
                {
                    float age = (_job.Tick + alpha - _job.Instances[i].BirthTick) / Mathf.Max(_bloomTicks, 1e-3f);
                    float a = Mathf.Clamp01(age);
                    m.transform.localScale = Vector3.one * Mathf.Max(0.001f, a * a * (3f - 2f * a));
                }
                m.SyncBodyToIndex();
                if (_starving[i] && m.Ready) { _starving[i] = false; m.Starve(); }
            }
            for (int q = 0; q < _mouth.Length; q++)
            {
                int i = _mouthSlot[q];
                if (i < 0) continue;
                if (Time.time > _mouthUntil[q] || !_job.Instances[i].Alive) { _mouthSlot[q] = -1; continue; }
                _mouth[q].position = Uni(_job.PoseAt(i, alpha));
            }
        }

        // ───────────────────────────────────────────────────────────────── members as targets (round 8)

        /// <summary>
        /// One living member a query found (Docs/SWARM_FAUNA.md §16.1): which swarm, which slot, the point the
        /// volume tested (its body prism's centre, or its heart) and the domain it wears. A member that already has
        /// a proxy is never returned - its real body and heart are in PrismSpatialIndex and PhysX, where the
        /// weapon's own path already finds them, so returning it here too would hit it twice.
        /// </summary>
        public readonly struct MemberHit
        {
            public readonly SwarmFauna Swarm;
            public readonly int Slot;
            public readonly Vector3 Point;
            public readonly Domains Domain;
            /// <summary>Half the body prism's scale diagonal - the bounding radius a projectile sweep allows a prism
            /// (<c>0.5 * lossyScale.magnitude</c>), so a member's contact test is a prism's.</summary>
            public readonly float BodyRadius;
            public MemberHit(SwarmFauna swarm, int slot, Vector3 point, Domains domain, float bodyRadius)
            { Swarm = swarm; Slot = slot; Point = point; Domain = domain; BodyRadius = bodyRadius; }
            /// <summary>A key unique per (swarm, slot) - a blast's once-per-member ledger.</summary>
            public long Key => ((long)Swarm.GetInstanceID() << 32) | (uint)Slot;
        }

        /// <summary>Every live GPU-drawn swarm.</summary>
        public static IReadOnlyList<SwarmFauna> Live => s_live;

        /// <summary>
        /// Every proxy-less living member, across every swarm, whose BODY centre (or heart, with
        /// <paramref name="heart"/>) lies in <paramref name="v"/> where it is drawn THIS frame. The same centre test
        /// PrismSpatialIndex runs on a prism (SwarmVolume is that test, proven against the shipped Burst code), over
        /// a grid the tick worker built - O(members near the volume), and a swarm whose box misses the volume costs
        /// one compare. Appends; returns the count appended. Main thread.
        /// </summary>
        public static int CollectMembers(in SwarmVolume v, bool heart, List<MemberHit> results)
        {
            int before = results.Count;
            for (int k = 0; k < s_live.Count; k++)
            {
                var sw = s_live[k];
                if (sw) sw.CollectOwn(v, heart, results);
            }
            return results.Count - before;
        }

        void CollectOwn(in SwarmVolume v, bool heart, List<MemberHit> results)
        {
            // without GPU drawing every living member already HAS a proxy (the fallback), so nothing is virtual
            if (_job == null || !_gpu) return;
            _qHits.Clear();
            _job.QueryMembers(v, _alpha, heart, _qScratch, _qHits);
            for (int q = 0; q < _qHits.Count; q++)
            {
                int i = _qHits[q];
                if (_proxy[i] || _gone[i]) continue;
                var p = heart ? _job.PoseAt(i, _alpha) : _job.BodyAt(i, _alpha);
                results.Add(new MemberHit(this, i, Uni(p), MemberDomain(i), 0.5f * _job.Instances[i].Scale.Length()));
            }
        }

        /// <summary>The domain member <paramref name="i"/> wears (its slot in this swarm's slot table, §16.4).</summary>
        public Domains MemberDomain(int i) =>
            _job != null && i >= 0 && i < _cap ? _slotDomain[Mathf.Clamp(_job.Instances[i].DomainSlot, 0, 2)] : domain;

        /// <summary>Room left in this frame's cell-wide budget of hit materialisations (§16.2).</summary>
        public static bool HitBudgetLeft(SwarmFaunaConfigSO cfg)
        {
            int frame = Time.frameCount;
            if (s_hitFrame != frame) { s_hitFrame = frame; s_hitsThisFrame = 0; }
            return s_hitsThisFrame < (cfg ? cfg.MaxHitMaterialisationsPerFrame : 48);
        }

        /// <summary>
        /// THE seam a weapon or a predator reaches a virtual member through (Docs/SWARM_FAUNA.md §16.2). The member
        /// becomes its proxy NOW, posed exactly where it is drawn, wearing its tier, with its body prism's creation
        /// finished in this frame (<see cref="Prism.CompleteCreationImmediately"/>) - so the caller then runs its OWN
        /// code on a real body prism and a real heart, and every consequence (the domain test, shields, the sealed
        /// <see cref="Fauna.Die"/>, the crystal, the wither or the suction, the kill credit) is the platform's,
        /// unchanged. Null when the member is gone, or (unless <paramref name="force"/>) the cell-wide per-frame
        /// budget is spent - the caller then keeps the hit and asks again next frame.
        /// </summary>
        public SwarmTadpoleFauna MaterialiseForHit(int i, bool force = false)
        {
            if (_job == null || i < 0 || i >= _cap) return null;
            var existing = _proxy[i];
            if (existing)
            {
                _wantedAt[i] = Time.time;
                return existing.IsDead || !existing.MaterialiseNow() ? null : existing;
            }
            if (_gone[i] || !_job.Instances[i].Alive) return null;
            if (!force && !HitBudgetLeft(config)) return null;
            if (!TrySpawnProxy(i, force: true)) return null;
            s_hitsThisFrame++;

            var m = _proxy[i];
            _wantedAt[i] = Time.time;
            m.transform.SetPositionAndRotation(Uni(_job.PoseAt(i, _alpha)), Face(_job.FaceAt(i, _alpha), Uni(_job.BY)));
            if (!m.MaterialiseNow()) return null;
            ref var s = ref _job.Instances[i];
            m.SetTier(s.Tier == 1, s.Tier == 2);
            m.SyncBodyToIndex();
            return m;
        }

        // ───────────────────────────────────────────────────────────────── members as PREY (round 8, §16.3)

        static readonly List<MemberHit> s_preyScratch = new(32);

        /// <summary>
        /// Can <paramref name="predator"/> eat member <paramref name="i"/>? The rules a predator applies to a registry
        /// creature, asked of the member's own state: a member is a HERBIVORE (the tadpole prefab's diet), so a
        /// herbivore-only predator may take it; it is still in its post-spawn grace for the first
        /// <c>predationImmunitySeconds</c> of its SIMULATION life (not its proxy's); and a predator penned to a band
        /// is never led out of it ("a creature must never be led to mass it cannot reach or eat").
        /// </summary>
        bool IsPreyFor(int i, Vector3 at, Fauna predator, bool herbivoresOnly)
        {
            if (!predator || predator == this) return false;
            if (herbivoresOnly && config.TadpolePrefab && config.TadpolePrefab.Diet != FaunaDiet.Herbivore) return false;
            float grace = config.TadpolePrefab ? config.TadpolePrefab.PredationImmunitySeconds : 0f;
            if (grace > 0f && (_job.Tick + _alpha - _job.Instances[i].BirthTick) / config.TickHz < grace) return false;
            return predator.IsInsideBand(at);
        }

        /// <summary>
        /// The nearest swarm member <paramref name="predator"/> can eat within <paramref name="maxSqr"/> of
        /// <paramref name="origin"/>, as a real creature: a member that already has a proxy is returned as that proxy,
        /// a member that is only data is materialised (budgeted, §16.2) - and kept alive while it is hunted
        /// (<see cref="SwarmTadpoleFauna.NotifyHunted"/>). The search widens a sphere from the swarm's own box, so it
        /// walks only the members near the answer. Null when nothing qualifies (or the frame's budget is spent).
        /// </summary>
        public static Fauna NearestPrey(Vector3 origin, float maxSqr, Fauna predator, bool herbivoresOnly, out float sqr)
        {
            sqr = float.PositiveInfinity;
            SwarmFauna bestSwarm = null; int bestSlot = -1;
            Fauna bestProxy = null;
            for (int k = 0; k < s_live.Count; k++)
            {
                var sw = s_live[k];
                if (!sw || sw._job == null || !sw._gpu) continue;
                // proxies first: they are real creatures the registry does not hold
                for (int q = 0; q < sw._proxySlots.Count; q++)
                {
                    var m = sw._proxy[sw._proxySlots[q]];
                    if (!m || m.IsDead || !m.IsAlivePrey || m.IsPredationImmune || m == predator) continue;
                    if (herbivoresOnly && m.Diet != FaunaDiet.Herbivore) continue;
                    var at = m.transform.position;
                    float d = (at - origin).sqrMagnitude;
                    if (d < sqr && d <= maxSqr && predator.IsInsideBand(at)) { sqr = d; bestProxy = m; bestSwarm = null; }
                }
                // members that are only data: grow a sphere from the box's nearest face until something qualifies
                var g = sw._job.Grid;
                if (g.Count == 0) continue;
                var lo = new Vector3(g.Lo.X, g.Lo.Y, g.Lo.Z); var hi = new Vector3(g.Hi.X, g.Hi.Y, g.Hi.Z);
                var nearest = Vector3.Max(lo, Vector3.Min(hi, origin));
                float r = Mathf.Max(64f, (nearest - origin).magnitude + 32f);
                float rMax = Mathf.Min(Mathf.Sqrt(Mathf.Min(maxSqr, sqr)), (origin - (lo + hi) * 0.5f).magnitude + (hi - lo).magnitude);
                for (; ; r *= 2f)
                {
                    float rr = Mathf.Min(r, rMax);
                    s_preyScratch.Clear();
                    sw.CollectOwn(SwarmTargets.Sphere(origin, rr), true, s_preyScratch);
                    bool found = false;
                    for (int q = 0; q < s_preyScratch.Count; q++)
                    {
                        var h = s_preyScratch[q];
                        float d = (h.Point - origin).sqrMagnitude;
                        if (d >= sqr || d > maxSqr || !sw.IsPreyFor(h.Slot, h.Point, predator, herbivoresOnly)) continue;
                        sqr = d; bestSwarm = sw; bestSlot = h.Slot; bestProxy = null; found = true;
                    }
                    if (found || rr >= rMax) break;
                }
            }
            if (bestProxy) return bestProxy;
            if (!bestSwarm) return null;
            var proxy = bestSwarm.MaterialiseForHit(bestSlot);
            return proxy && !proxy.IsPredationImmune ? proxy : null;
        }

        /// <summary>
        /// Every swarm member within <paramref name="range"/> of a predator's MOUTH that it can eat, as real creatures
        /// (existing proxies, plus members materialised under the per-frame budget) - appended to
        /// <paramref name="results"/>. The predator then calls <see cref="Fauna.Predated"/> on each exactly as it does on
        /// a registry creature, so the suction into the mouth, the mass transfer and the crystal are the platform's.
        /// </summary>
        public static void PreyAtMouth(Vector3 mouth, float range, Fauna predator, bool herbivoresOnly, List<Fauna> results)
        {
            float r2 = range * range;
            for (int k = 0; k < s_live.Count; k++)
            {
                var sw = s_live[k];
                if (!sw || sw._job == null || !sw._gpu) continue;
                for (int q = 0; q < sw._proxySlots.Count; q++)
                {
                    var m = sw._proxy[sw._proxySlots[q]];
                    if (!m || m.IsDead || m == predator) continue;
                    if (herbivoresOnly && m.Diet != FaunaDiet.Herbivore) continue;
                    if ((m.transform.position - mouth).sqrMagnitude <= r2) results.Add(m);
                }
                s_preyScratch.Clear();
                sw.CollectOwn(SwarmTargets.Sphere(mouth, range), true, s_preyScratch);
                for (int q = 0; q < s_preyScratch.Count; q++)
                {
                    var h = s_preyScratch[q];
                    if (!sw.IsPreyFor(h.Slot, h.Point, predator, herbivoresOnly)) continue;
                    var proxy = sw.MaterialiseForHit(h.Slot);
                    if (proxy) results.Add(proxy);
                    else break;   // the frame's budget is spent - the rest are still there next frame
                }
            }
        }

        /// <summary>Keep member <paramref name="i"/>'s proxy alive this frame (a predator is hunting it, §16.3).</summary>
        public void KeepProxy(int i)
        {
            if (_wantedAt != null && i >= 0 && i < _cap) _wantedAt[i] = Time.time;
        }

        /// <summary>
        /// The swarm's domain SLOT table (§16.4): slot 0 is the anchor's - the cell's controlling domain when the
        /// swarm was seeded, which every seed wears. A one-colour swarm maps every slot to it; a MultiDomain swarm
        /// gives slots 1 and 2 the other two playable domains in Jade, Ruby, Gold order. Fixed for the swarm's
        /// life, so a member's slot always names the same domain.
        /// </summary>
        Domains[] BuildSlotDomains()
        {
            var t = new[] { domain, domain, domain };
            if (!config.MultiDomain) return t;
            int k = 1;
            foreach (var d in PlayableDomains)
                if (d != domain && k < 3) t[k++] = d;
            return t;
        }

        static readonly Domains[] PlayableDomains = { Domains.Jade, Domains.Ruby, Domains.Gold };

        /// <summary>The slot a domain's mass funds (§16.4). Mass of a domain the table does not hold - neutral
        /// (Blue) environment, or anything in a one-colour swarm - funds slot 0, the anchor's.</summary>
        int SlotOfDomain(Domains d)
        {
            for (int s = 1; s < 3; s++) if (_slotDomain[s] == d && d != _slotDomain[0]) return s;
            return 0;
        }

        protected override bool AcceptsTeamRecolour => !(config && config.MultiDomain);

        /// <summary>A one-colour swarm re-coloured by its cell (Cell.SetModeControlOverride): every slot, the
        /// palette and every live proxy take the new domain at once, so the cell never holds two fauna colours.</summary>
        protected override void OnTeamChanged()
        {
            if (!_seeded) return;
            _slotDomain = BuildSlotDomains();
            if (_gpu) ApplyPalette();
            for (int q = 0; q < _proxySlots.Count; q++)
            {
                var m = _proxy[_proxySlots[q]];
                if (m) m.SetTeam(domain);
            }
        }

        // ───────────────────────────────────────────────────────────────── vessels

        void SenseVessels()
        {
            _job.PredCount = 0;
            _seen.Clear();
            float radius = _plans[Mathf.Clamp(_job.PlanIx, 0, 3)].Radius * config.UnitScale * 1.6f
                           + Mathf.Max(config.SenseMargin, config.EngageRadius);
            int hits = Physics.OverlapSphereNonAlloc(transform.position, radius, OverlapScratch, NonPrismOverlapMask);
            float toSimVel = 1f / (config.UnitScale * config.TickHz);
            var preds = _job.Preds;
            for (int h = 0; h < hits && _job.PredCount < preds.Length; h++)
            {
                var col = OverlapScratch[h];
                if (!col) continue;
                if (!col.TryGetComponent(out IVesselStatus status))
                    status = col.GetComponentInParent<IVesselStatus>();
                if (status == null || _seen.Contains(status) || status is not Component c || !c) continue;
                _seen.Add(status);
                var v = status.Course * status.Speed;
                preds[_job.PredCount++] = new SwarmPredator
                {
                    C = ToSim(c.transform.position),
                    V = Sim(v) * toSimVel,
                    R = config.VesselRadius / config.UnitScale,
                };
            }
        }

        // ───────────────────────────────────────────────────────────────── food

        /// <summary>
        /// Grazing. A few members per tick take one bite each of the nearest edible FLORA prism within
        /// reach of where the published frame has them; the prism is consumed (suctioned into a mouth
        /// posed at the biter - the food web's sanctioned down-force on mass) and its volume is QUEUED
        /// into the stomach under the PLANT's element, which is what later pays for eggs. A full stomach
        /// stops grazing: a grown body does not strip its feeding ground for nothing.
        /// </summary>
        void Feed()
        {
            float capacity = config.StomachEggs * Density * (config.EggVolume.x + config.EggVolume.y + config.EggVolume.z + config.EggVolume.w) * 0.25f;
            var st = _job.Stomach;
            if (st[0] + st[1] + st[2] + st[3] >= capacity) { _lastFedTime = Time.time; return; }

            var index = PrismSpatialIndex.EnsureInstance();
            if (index == null || !index.IsAvailable) return;

            var inst = _job.Instances;
            int bitten = 0, biters = config.BitersPerStep;   // round 7: NOT x Density - bites are the one main-thread cost that scales with appetite
            for (int tries = 0; tries < _cap && bitten < biters; tries++)
            {
                _biteCursor = (_biteCursor + 1) % _cap;
                int i = _biteCursor;
                if (!inst[i].Alive || _gone[i]) continue;
                bitten++;
                Vector3 at = Uni(inst[i].CurPos);
                int found = index.QuerySphere(at, config.BiteRadius, FeedScratch);
                for (int q = 0; q < found; q++)
                {
                    var prism = FeedScratch[q];
                    var eater = MemberDomain(i);   // a MultiDomain member eats as its own domain (§16.4)
                    if (!IsFood(prism, eater, out int e)) continue;
                    float volume = Mathf.Max(0.001f, prism.Volume);
                    var paid = prism.Domain;   // read before Consume - the domain of the mass that will fund an egg
                    prism.Consume(MouthFor(i, at), eater, _eaterName, false, true);
                    _job.QueueDeposit(e, volume, SlotOfDomain(paid));
                    _lastFedTime = Time.time;
                    _atPlantSince = -1f;
                    break;
                }
            }
        }

        Transform MouthFor(int i, Vector3 at)
        {
            _mouthCursor = (_mouthCursor + 1) % _mouth.Length;
            _mouthSlot[_mouthCursor] = i;
            _mouthUntil[_mouthCursor] = Time.time + 2f;   // longer than a consume suction
            _mouth[_mouthCursor].position = at;
            return _mouth[_mouthCursor];
        }

        /// <summary>
        /// The swarm's diet: living FLORA mass only, through the platform's one edibility rule
        /// (<see cref="Fauna.IsPreyForMe"/> = the cell's spatial diet + this species' band) and the
        /// shielded-mass rule (<see cref="Fauna.IsShieldedMass"/> - which is why a Charge plant, whose
        /// leaves are armoured, is no food at all). Returns the plant's element as a research index.
        /// </summary>
        bool IsFood(Prism prism, Domains eater, out int element)
        {
            element = -1;
            if (!prism || prism.destroyed || prism is not HealthPrism hp) return false;
            if (hp.ResolveOwnerFauna() != null) return false;          // another creature's body
            if (hp.LifeForm is not Flora plant || !plant) return false;  // flora only
            if (IsShieldedMass(prism)) return false;
            if (!IsPreyForMe(prism.transform.position, prism.Domain, eater)) return false;
            element = SwarmFaunaConfigSO.ToIndex(plant.Element);
            return element >= 0;
        }

        /// <summary>
        /// Where to swim: the nearest living plant inside this swarm's band that it has not just
        /// found grazed bare; else a fresh point in the band. Runs on the base class's goal clock.
        /// </summary>
        protected override Vector3 ResolveGoal()
        {
            if (_job == null || !HostCell) return Goal;
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
            // the victim the tick picked (asked for last tick): give it a proxy, shed it once that is Ready
            int v = _job.StarvationVictim;
            if (v >= 0 && v < _cap && _job.Instances[v].Alive && !_gone[v] && !_starving[v])
            {
                if (_proxy[v] || TrySpawnProxy(v, force: true)) { _starving[v] = true; _wantedAt[v] = Time.time; }
            }

            float now = Time.time;
            if (now - _lastFedTime < config.StarvationSeconds) return;
            if (now - _lastShedTime < config.ShedIntervalSeconds) return;
            _lastShedTime = now;
            // shed the member the body needs least - the core decides who, on the worker, next tick
            _job.WantStarvationVictim = true;
        }

        void Extinction()
        {
            if (_job.AliveCount > 0 || _proxySlots.Count > 0) { _extinctSince = -1f; return; }
            if (_extinctSince < 0f) { _extinctSince = Time.time; return; }
            if (Time.time - _extinctSince < config.ExtinctLingerSeconds) return;
            // The anchor has no body and no heart - removing it pops nothing - and the cell's seeder
            // hatches a fresh swarm in its place (extinction recovery, the seeder's sanctioned job).
            if (DespawnOrDestroy()) return;
            Destroy(gameObject);
        }

        // ───────────────────────────────────────────────────────────────── units

        SVector3 ToSim(Vector3 world) => Sim((world - _centre) / config.UnitScale);
        static SVector3 Sim(Vector3 v) => new(v.x, v.y, v.z);
        static Vector3 Uni(SVector3 v) => new(v.X, v.Y, v.Z);

        Quaternion Face(SVector3 f) => Face(f, Uni(_job.BY));

        Quaternion Face(SVector3 f, Vector3 up)
        {
            var fw = Uni(f);
            if (fw.sqrMagnitude < 1e-6f) fw = Uni(_job.BX);
            if (Mathf.Abs(Vector3.Dot(fw.normalized, up)) > 0.98f) up = Uni(_job.BZ);
            return Quaternion.LookRotation(fw, up);
        }
    }
}
