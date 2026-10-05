using System.Collections.Generic;
using CosmicShore.Core;
using CosmicShore.Data;
using CosmicShore.ECS;
using CosmicShore.Utility;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
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
    /// ROUND 11a (Docs/SWARM_FAUNA.md §19) - ONE prism system: every living member's body is an ordinary
    /// <see cref="PrismSpatialIndex"/> VIRTUAL entry (this swarm is its <see cref="IVirtualPrismOwner"/>; slot = member
    /// index), pushed in bulk once per tick, suspended while the member's proxy body is a registered prism, released on
    /// death - so weapons, predators, AOE, the cell's volume and the phase ladder find a member exactly as they find any
    /// prism - and an ordinary <see cref="PrismRenderService"/> entity, posed by one Burst transform write per frame and
    /// wearing the theme's per-domain tier materials. Hearts stay on the instanced crystal draw.
    ///
    /// Invariants it touches (Docs/SWARM_FAUNA.md §2, §14.4): mass is conserved (every egg is PAID for out
    /// of eaten flora volume, 1:1), there is no imposed death (members only die to vessels, predators or
    /// starvation), one colour (every member wears this anchor's domain), every member drops its crystal
    /// (only through a proxy - a member with no proxy cannot die), and nothing pops (births bloom in the
    /// shader, molts re-form in the shader, a proxy appears and leaves under an unchanged picture, deaths
    /// wither through the platform).
    /// </summary>
    public class SwarmFauna : Fauna, IVirtualFaunaOwner, IVirtualPrismBudget, ISwarmEntrySink, IMacroPopulation
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
        float _atPlantSince = -1f, _lastGoalBite = -1f, _goalSince;
        bool _foraging = true;
        readonly Dictionary<Flora, float> _rested = new();
        static int s_spawnFrame = -1, s_spawnsThisFrame;
        // round 8: proxies materialised because a weapon or predator reached a member (§16.2), budgeted cell-wide
        static int s_hitFrame = -1, s_hitsThisFrame;
        float _alpha;                    // this frame's display alpha - where every member is DRAWN right now
        Domains[] _slotDomain = { Domains.Blue, Domains.Blue, Domains.Blue };

        // round 11a (§19.1): every living member is a PrismSpatialIndex virtual entry
        SwarmEntryLedger _entries;
        bool[] _realBody;                // per slot: the member's proxy body is a registered prism (its entry is suspended)
        NativeArray<int> _entryIdsNative;
        NativeArray<float3> _pointsNative;
        PrismSpatialIndex _index;
        // round 11a (§19.2): every living member's BODY is a PrismRenderService entity (unless the fallback draws it)
        bool _unified;
        SwarmEntityLedger _entities;
        NativeArray<PrismRenderHandle> _handles;       // per slot
        NativeArray<PrismRenderHandle> _shownHandles;  // per frame, in the ledger's Shown order
        NativeArray<float4x4> _matrices;
        NativeArray<byte> _lookScratch;
        float[] _matrixScratch;                        // one matrix: entity creation's first pose
        // round 11a-2 (§19.4): the per-frame pose runs as a Burst job (SwarmPoseJob) over native copies of the published
        // frame and the shown-slot list, refreshed once per tick; its handle is the transform write's dependency
        NativeArray<SwarmInstance> _instNative;
        NativeArray<int> _shownNative;
        JobHandle _poseHandle;
        int _poseCount;
        byte[] _look;                    // per slot: the look (tier * 3 + domain slot) its entity wears
        readonly Material[] _looks = new Material[9];
        Mesh _bodyMesh;
        // round 11f (Docs/ECOLOGY_LOD.md §5): the macro LOD - a collapsed swarm's 10 Hz tick stops, its formation drifts
        SwarmMacroBody _macro;
        CellEcologyLod _lod;
        bool _wantCollapse;
        static readonly ProfilerMarker s_mMacro = new("SwarmFauna.MacroTick");
        static readonly ProfilerMarker s_mIndex = new("SwarmFauna.Tick.Index");
        static readonly ProfilerMarker s_mEntities = new("SwarmFauna.Tick.Entities");
        static readonly ProfilerMarker s_mBodies = new("SwarmFauna.Frame.Bodies");
        static readonly ProfilerMarker s_mBodiesWrite = new("SwarmFauna.Frame.BodiesWrite");
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
            if (_gpu)
            {
                // round 11a (§19): the members are prisms of the ONE prism system - index entries always, render
                // entities unless the ECS path is unavailable or switched off (then the instanced body draw stays)
                BindIndexEntries();
                _unified = config.UnifiedPrismBodies && BindBodyEntities();
                _render.DrawBodies = !_unified;
                SyncIndex();
                if (_unified) SyncEntities();
            }

            BuildMouths();
            VirtualFauna.Register(this);
            _lastFedTime = Time.time;
            if (config.MacroLod && _gpu)
            {
                _macro = new SwarmMacroBody(_job);
                _lod = CellEcologyLod.For(host);
                _lod?.Register(this);
            }
            _acc = Random.value * _dt;   // stagger the cell's swarms across frames from the first tick
            StartLoop();
            CSDebug.LogVerbose(CSLogChannel.Ecology,
                $"[Swarm] {name} ({config.Model}, density {Density}) hatched as {_core.Plan.Kind} with " +
                $"{_job.AliveCount} tadpoles at r={radial.magnitude:F0}; tick {(_inline ? "inline" : "off-thread")}, " +
                $"members {(_gpu ? (_unified ? "prism entities" : "GPU-drawn") : "GameObjects")}");
        }

        SwarmTickSettings BuildTickSettings()
        {
            var half = SwarmPlanLibrary.TypicalHalfExtents(_plans);
            var s = new SwarmTickSettings
            {
                Centre = Sim(_centre), UnitScale = config.UnitScale, PrismScale = config.PrismScale,
                HeartPrismGap = config.HeartPrismGap,
                DangerEnter = config.DangerEnter, DangerExit = config.DangerExit,
                Bestiary = config.Bestiary, HuntEnter = config.HuntEnter, LurkCalm = config.LurkCalm,
                LocustPhaseTicks = Mathf.Max(1, Mathf.RoundToInt(config.LocustPhaseSeconds * config.TickHz)),
                EngageRadius = config.EngageRadius, MaxEngaged = config.MaxProxies,
                MultiDomain = Lineages,
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
                HungerKills = false, Funded = true, Oriented = true,   // one colour: lineages are the sort model's (round 9)
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
                DomainSlots = Lineages, Lineages = Lineages, Drift = config.LineageDrift, Funded = true, Animate = true, WellLook = true, Oriented = true,
                Cruise = config.Cruise, Turn = config.TurnPerStep,
                Membrane = SimMembrane(host), CrossCost = config.CrossElementCost, Cap = PlanCap,
                // round 6 (Docs/SWARM_FAUNA.md §12): sortfeel's flat wells + wander, the 1-in-k update
                WellDead = config.SortWellDead, WellDeadTime = config.SortWellDeadTime,
                Wander = config.SortWander, WanderTau = config.SortWanderTau,
                Frac = Mathf.Max(1, config.SortUpdateFraction),
                // round 11d (§22): regrow from the wound, at a pace that eases back in
                BudAtWound = config.SortBudAtWound, FateNear = config.SortFateNear,
                LayRamp = Mathf.RoundToInt(config.SortLayRampSeconds * config.TickHz),
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
                Funded = true, KeepEggs = true, Animate = true, Oriented = true,   // one colour: lineages are the sort model's (round 9)
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
            s_warnedLooks = false;
        }

        protected override void OnDestroy()
        {
            // a tick still running on a worker owns the core: it finishes into buffers nobody reads, which is
            // harmless (the job holds no Unity object). The GPU buffers are ours to release.
            _render?.Dispose();
            _render = null;
            VirtualFauna.Unregister(this);
            _lod?.Unregister(this);
            _lod = null;
            ReleaseIndexEntries();
            ReleaseBodyEntities();
            base.OnDestroy();
        }

        void Update()
        {
            if (_job == null) return;
            if (_job.Error != null) { ReportWorkerError(); return; }
            _lod?.Advance();   // the cell's LOD: may expand this swarm (a pilot approaches) or tick it (collapsed)
            if (_macro is { Collapsed: true })
            {
                // collapsed: no tick, no pose - the hearts are drawn where the last macro tick put them (the bodies are
                // prism entities, posed by that tick), so whatever a distant camera shows does not move between ticks
                if (_gpu) DrawMembers(_alpha);
                return;
            }

            _acc += Time.deltaTime;
            if (_acc >= _dt) AdvanceTick();
            float alpha = Mathf.Clamp01(_acc / _dt);
            _alpha = alpha;

            // the body pose job goes first, so it runs on the workers while this frame's proxies and hearts are posed
            if (_unified) using (s_mBodies.Auto()) SchedulePoseBodies(alpha);
            using (s_mPose.Auto()) PoseProxies(alpha);
            if (_gpu) DrawMembers(alpha);
            if (_unified) using (s_mBodiesWrite.Auto()) WritePosedBodies();
        }

        /// <summary>The swarm's world radius as drawn (the draw bounds' half extent, less the margin).</summary>
        float BodyRadius => (_plans[Mathf.Clamp(_job.PlanIx, 0, 3)].Radius * 3f + 20f) * config.UnitScale;

        void DrawMembers(float alpha)
        {
            using (s_mDraw.Auto())
            {
                var bounds = new Bounds(Uni(_job.Anchor), Vector3.one * (2f * (BodyRadius + 40f)));
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
            // round 11f: the director asked for a collapse while a tick was in flight - the published tick is the
            // formation the swarm freezes into, and no next tick is started
            if (_wantCollapse)
            {
                _wantCollapse = false;
                if (CollapseReady && _macro.TryCollapse()) { _alpha = Mathf.Clamp01(_acc / _dt); return; }
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
            if (_gpu) using (s_mIndex.Auto()) SyncIndex();
            if (_unified) using (s_mEntities.Auto()) SyncEntities();
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
                    $"({(_inline ? "inline" : "off-thread")}, {(_gpu ? (_unified ? "prism entities" : "GPU-drawn") : "GameObjects")}, " +
                    $"{(_entries != null ? _entries.Registered - _entries.Suspended : 0)} index entries live)");
            _tickMsSum = 0; _tickMsCount = 0;
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
                    ResumeEntry(i);   // its body prism leaves the index with it: the member's virtual entry is its one entry again
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
            // round 11a: the member leaves the index (its skeleton or its eater holds the mass now) and its render
            // entity hides in this frame's visibility flush - the proxy's own death visuals are what is drawn
            if (_entries != null && _index != null) _entries.Release(i, this);
            if (_realBody != null) _realBody[i] = false;
            if (_unified && _entities.HideNow(i)) PrismRenderService.QueueVisible(_handles[i], false);
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
                // the frame the proxy's body becomes a registered prism, the member's virtual entry steps aside, so the
                // index never holds the member twice (not even until the next tick)
                if (_realBody != null && !_realBody[i] && m.Body && !m.Body.destroyed && m.Body.IsCreationComplete) SuspendEntry(i);
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

        // ───────────────────────────────────────────────────────────────── members as prisms (round 11a, §19)

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
        /// The one place a virtual member becomes real (Docs/SWARM_FAUNA.md §16.2, reached since round 11a only through
        /// the platform: <see cref="IVirtualPrismOwner.MaterialiseVirtualPrism"/>, i.e. the AOE resolve and
        /// <see cref="PrismSpatialIndex.ResolvePrism"/>). The member
        /// becomes its proxy NOW, posed exactly where it is drawn, wearing its tier, with its body prism's creation
        /// finished in this frame (<see cref="Prism.CompleteCreationImmediately"/>) - so the caller then runs its OWN
        /// code on a real body prism and a real heart, and every consequence (the domain test, shields, the sealed
        /// <see cref="Fauna.Die"/>, the crystal, the wither or the suction, the kill credit) is the platform's,
        /// unchanged. Null when the member is gone, or (unless <paramref name="force"/>) the cell-wide per-frame
        /// budget is spent - the caller then keeps the hit and asks again next frame.
        /// </summary>
        SwarmTadpoleFauna MaterialiseForHit(int i, bool force = false)
        {
            if (_job == null || i < 0 || i >= _cap) return null;
            if (_macro is { Collapsed: true }) ExpandMacro();   // a hit is resolved by individuals (ECOLOGY_LOD §4 clause 3)
            var existing = _proxy[i];
            if (existing)
            {
                _wantedAt[i] = Time.time;
                return existing.IsDead || !existing.MaterialiseNow() ? null : existing;
            }
            if (_gone[i] || !_job.Instances[i].Alive) return null;
            if (!force && !HitBudgetLeft(config)) return null;
            if (!TrySpawnProxy(i, force: true)) return null;
            HitBudgetLeft(config);   // rolls the counter over on a new frame
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
            if (_job.Instances[i].Tier == 2) return false;   // shielded mass is never food (ECOSYSTEM_DESIGN_PRINCIPLES)
            if (herbivoresOnly && config.TadpolePrefab && config.TadpolePrefab.Diet != FaunaDiet.Herbivore) return false;
            float grace = config.TadpolePrefab ? config.TadpolePrefab.PredationImmunitySeconds : 0f;
            if (grace > 0f && (_job.Tick + _alpha - _job.Instances[i].BirthTick) / config.TickHz < grace) return false;
            return predator.IsInsideBand(at);
        }

        /// <summary>Keep member <paramref name="i"/>'s proxy alive this frame (a predator is hunting it, §16.3).</summary>
        public void KeepProxy(int i)
        {
            if (_wantedAt != null && i >= 0 && i < _cap) _wantedAt[i] = Time.time;
        }

        // ───────────────────────────────────────────────────────────────── the spatial index (round 11a, §19.1)

        /// <summary>Allocates the per-slot entry ledger and the two native arrays its bulk position push reads.</summary>
        void BindIndexEntries()
        {
            _index = PrismSpatialIndex.EnsureInstance();
            _entries = new SwarmEntryLedger(_cap);
            _realBody = new bool[_cap];
            _entryIdsNative = new NativeArray<int>(_cap, Allocator.Persistent);
            _pointsNative = new NativeArray<float3>(_cap, Allocator.Persistent);
        }

        void ReleaseIndexEntries()
        {
            if (_entries != null && _index != null && _index.IsAvailable) _entries.ReleaseAll(this);
            _entries = null;
            if (_entryIdsNative.IsCreated) _entryIdsNative.Dispose();
            if (_pointsNative.IsCreated) _pointsNative.Dispose();
        }

        /// <summary>
        /// Once per TICK (Docs/SWARM_FAUNA.md §19.1): every existing entry's stored point is pushed in ONE bulk call
        /// (<see cref="PrismSpatialIndex.UpdatePositionsBatch"/>, the points the worker built), then the ledger
        /// registers the newborns, re-registers reused slots, releases the dead, and suspends a member whose proxy
        /// body is a registered prism. O(members) of array work and O(births + deaths + changes) of index calls.
        /// </summary>
        float _heartReach;

        void SyncIndex()
        {
            if (_entries == null || _index == null || !_index.IsAvailable) return;
            float reach = 0f;
            var inst = _job.Instances;
            for (int i = 0; i < _cap; i++)
            {
                var m = _proxy[i];
                _realBody[i] = m && !m.IsDead && !_gone[i] && m.Body && !m.Body.destroyed && m.Body.IsCreationComplete;
                if (!inst[i].Alive) continue;
                // how far this member's heart can be drawn from its stored body point: the seat, plus the
                // half step the stored (alpha 0.5) point lags or leads the drawn one
                float r = Mathf.Abs(inst[i].PrismZ) + 0.5f * SVector3.Distance(inst[i].PrevPos, inst[i].CurPos);
                if (r > reach) reach = r;
            }
            _heartReach = reach;
            _entryIdsNative.CopyFrom(_entries.Ids);
            _pointsNative.Reinterpret<SVector3>().CopyFrom(_job.IndexPoint);
            _index.UpdatePositionsBatch(_entryIdsNative, _pointsNative, _cap);
            _entries.Sync(_job.Instances, _realBody, _job.IndexPoint, this);
        }

        void SuspendEntry(int i)
        {
            if (_entries == null || _index == null) return;
            _realBody[i] = true;
            int id = _entries.Ids[i];
            if (id < 0 || _entries.IsSuspended(i)) return;
            ((ISwarmEntrySink)this).SetSuspended(id, true);
            _entries.NoteSuspendedByIndex(i);
        }

        /// <summary>Member i's proxy retired without dying: re-file its entry where it is drawn now, live again.</summary>
        void ResumeEntry(int i)
        {
            if (_entries == null || _index == null) return;
            _realBody[i] = false;
            int id = _entries.Ids[i];
            if (id < 0) return;
            _index.UpdatePosition(id, Uni(_job.BodyAt(i, _alpha)));
            _entries.Resume(i, this);
        }

        // ISwarmEntrySink - the ledger's calls, onto PrismSpatialIndex's virtual-entry API and the host cell's volume
        // binding. Explicit, so nothing outside the swarm can drive them.
        int ISwarmEntrySink.Register(int slot, SVector3 point, int domainSlot, float volume, bool shielded, float radius)
        {
            var d = _slotDomain[Mathf.Clamp(domainSlot, 0, 2)];
            int id = _index.RegisterVirtual(this, slot, new float3(point.X, point.Y, point.Z), (int)d, volume, shielded,
                                            false, radius);
            var host = HostCell;
            if (id >= 0 && host) host.BindVirtualMass(id, d);   // volume-only fauna body mass (§16.3, now the cell's own sum)
            return id;
        }

        void ISwarmEntrySink.Release(int id) => _index.Unregister(id);

        void ISwarmEntrySink.SetSuspended(int id, bool suspended) => _index.SetVirtualSuspended(id, suspended);

        void ISwarmEntrySink.SetShape(int id, float volume, float radius)
        {
            _index.UpdateCellVolume(id, volume);
            _index.UpdateVolume(id, Mathf.Max(volume, 1f));
            _index.SetVirtualRadius(id, radius);
        }

        void ISwarmEntrySink.SetShielded(int id, bool shielded) => _index.UpdateShieldState(id, shielded, false);

        void ISwarmEntrySink.SetDomainSlot(int id, int domainSlot)
        {
            var d = _slotDomain[Mathf.Clamp(domainSlot, 0, 2)];
            _index.UpdateDomain(id, (int)d);
            var host = HostCell;
            if (host) host.BindVirtualMass(id, d);
        }

        // IVirtualPrismOwner / IVirtualPrismBudget - how the platform makes a member real

        /// <summary>The index needs member <paramref name="slot"/> as a real prism NOW (an AOE hit, a hitscan round, a
        /// projectile, a predator): its proxy, posed where it is drawn, creation complete. The index suspends the entry.</summary>
        Prism IVirtualPrismOwner.MaterialiseVirtualPrism(int slot)
        {
            var m = MaterialiseForHit(slot, force: true);
            if (!m || !m.Body) return null;
            if (_realBody != null) _realBody[slot] = true;
            _entries?.NoteSuspendedByIndex(slot);
            return m.Body;
        }

        bool IVirtualPrismBudget.HasMaterialiseBudget(int slot) => HitBudgetLeft(config);

        // IVirtualFaunaOwner - what only a creature can answer

        bool IVirtualFaunaOwner.IsVirtualPrey(int slot, Vector3 at, Fauna predator, bool herbivoresOnly) =>
            _job != null && slot >= 0 && slot < _cap && _job.Instances[slot].Alive && !_gone[slot] && !_proxy[slot]
            && IsPreyFor(slot, at, predator, herbivoresOnly);

        float IVirtualFaunaOwner.HeartReach => _heartReach;

        bool IVirtualFaunaOwner.TryGetVirtualHeart(int slot, out Vector3 heart)
        {
            heart = default;
            if (_job == null || slot < 0 || slot >= _cap || !_job.Instances[slot].Alive || _gone[slot]) return false;
            heart = Uni(_job.PoseAt(slot, _alpha));
            return true;
        }

        void IVirtualFaunaOwner.CollectMaterialisedFauna(Vector3 centre, float radius, List<Fauna> results)
        {
            float r2 = radius * radius;
            for (int q = 0; q < _proxySlots.Count; q++)
            {
                var m = _proxy[_proxySlots[q]];
                if (!m || m.IsDead) continue;
                if ((m.transform.position - centre).sqrMagnitude <= r2) results.Add(m);
            }
        }

        // ───────────────────────────────────────────────────────────────── the macro LOD (round 11f, Docs/ECOLOGY_LOD.md §5)

        /// <summary>Due to shed a starving member: only individuals can die (a body withers, a crystal drops).</summary>
        bool StarvationDue
        {
            get
            {
                float now = Time.time;
                return now - _lastFedTime >= config.StarvationSeconds && now - _lastShedTime >= config.ShedIntervalSeconds;
            }
        }

        /// <summary>Nothing pending that only individuals resolve: no proxy (a hit, a shed, a predator's bite), no kill
        /// waiting for its tick, no starvation shed due.</summary>
        bool CollapseReady =>
            _macro != null && !_macro.Collapsed && _gpu && _job.Error == null && _proxySlots.Count == 0 && _goneSlots.Count == 0
            && !StarvationDue;

        SVector3 IMacroPopulation.MacroCentre => _job != null ? _job.Anchor : Sim(transform.position);
        float IMacroPopulation.MacroExtent => _job != null && _plans != null ? BodyRadius : 0f;
        bool IMacroPopulation.IsCollapsed => _macro is { Collapsed: true };
        bool IMacroPopulation.CanCollapse => CollapseReady;
        bool IMacroPopulation.NeedsIndividuals =>
            _macro is { Collapsed: true } && (_proxySlots.Count > 0 || _goneSlots.Count > 0 || StarvationDue);
        MacroPopulationTotals IMacroPopulation.Totals => _macro != null ? _macro.Totals : default;

        /// <summary>Collapses now if no tick is in flight; otherwise asks for it at the next published tick (the formation
        /// it freezes into) and returns false - the director sees it collapsed on its next tick.</summary>
        bool IMacroPopulation.Collapse()
        {
            if (!CollapseReady) return false;
            if (_job.State == SwarmJobState.Idle && _macro.TryCollapse()) return true;
            _wantCollapse = true;
            return false;
        }

        void IMacroPopulation.Expand() => ExpandMacro();

        /// <summary>Resumes the 10 Hz tick from the frozen formation, at the display alpha it froze at - nothing jumps.</summary>
        void ExpandMacro()
        {
            _wantCollapse = false;
            if (_macro is not { Collapsed: true }) return;
            _macro.Expand();
            _acc = _alpha * _dt;
        }

        /// <summary>
        /// One macro tick (1 Hz, ECOLOGY_LOD §5): the formation drifts rigidly toward the goal at cruise (core, published
        /// frames, index points and anchor together), the index entries and body entities follow once, and the swarm
        /// grazes what is under it at the macro cadence (BitersPerStep x TickHz x dt bites, banked into the same stomach).
        /// Counts never change here: births are laid on expansion from the banked stomach, and a starvation shed expands it.
        /// </summary>
        void IMacroPopulation.MacroTick(float dt)
        {
            if (_macro is not { Collapsed: true }) return;
            using (s_mMacro.Auto())
            {
                _macro.MacroTick(dt, Sim(Goal), CurrentSpeed, config.UnitScale);
                transform.position = Uni(_job.Anchor);
                using (s_mUpload.Auto()) _render.Upload(_job);
                using (s_mIndex.Auto()) SyncIndex();
                if (_unified)
                {
                    using (s_mEntities.Auto()) SyncEntities();
                    SchedulePoseBodies(_alpha);
                    WritePosedBodies();
                }
                using (s_mFeed.Auto()) Feed(Mathf.Max(1, Mathf.RoundToInt(config.BitersPerStep * config.TickHz * dt)));
            }
        }

        // ───────────────────────────────────────────────────────────────── bodies as prism entities (round 11a, §19.2)

        static bool s_warnedLooks;

        /// <summary>
        /// Resolves the body mesh and the nine looks (tier x domain slot: the theme's per-domain Block / Dangerous /
        /// Shielded prism materials - the very materials every live prism of that tier and domain wears, so a member
        /// opens with distance, colours and animates exactly like one) and allocates the native arrays. False - the
        /// round-7 instanced body draw stays - when the render service is off or a material is missing.
        /// </summary>
        bool BindBodyEntities()
        {
            if (!PrismRenderService.Enabled) return false;
            var bodyPrism = config.TadpolePrefab ? config.TadpolePrefab.GetComponentInChildren<HealthPrism>(true) : null;
            _bodyMesh = bodyPrism && bodyPrism.TryGetComponent(out MeshFilter mf) ? mf.sharedMesh : null;
            if (!_bodyMesh || !BuildLooks()) return false;
            _entities = new SwarmEntityLedger(_cap);
            _look = new byte[_cap];
            _handles = new NativeArray<PrismRenderHandle>(_cap, Allocator.Persistent);
            _shownHandles = new NativeArray<PrismRenderHandle>(_cap, Allocator.Persistent);
            _restyleHandles = new NativeArray<PrismRenderHandle>(_cap, Allocator.Persistent);
            _lookScratch = new NativeArray<byte>(_cap, Allocator.Persistent);
            _matrices = new NativeArray<float4x4>(_cap, Allocator.Persistent);
            _instNative = new NativeArray<SwarmInstance>(_cap, Allocator.Persistent);
            _shownNative = new NativeArray<int>(_cap, Allocator.Persistent);
            _matrixScratch = new float[16];
            return true;
        }

        NativeArray<PrismRenderHandle> _restyleHandles;

        /// <summary>[tier * 3 + domain slot] = that tier's prism material in that slot's domain (ThemeManager paints one
        /// set per domain at runtime). A domain the theme has not painted wears the unpainted base set, warned once.</summary>
        bool BuildLooks()
        {
            var theme = config.Theme;
            if (!theme) return false;
            var sets = theme.TeamMaterialSets;
            var baseSet = theme.BaseMaterialSet;
            for (int slot = 0; slot < 3; slot++)
            {
                var d = _slotDomain[slot];
                CosmicShore.ScriptableObjects.SO_MaterialSet set = sets != null && sets.TryGetValue(d, out var painted) && painted ? painted : null;
                if (!set)
                {
                    set = baseSet;
                    if (!s_warnedLooks)
                    {
                        s_warnedLooks = true;
                        CSDebug.LogWarning($"[Swarm] {name}: the theme has no painted prism materials for {d}; members of that " +
                                           "domain wear the unpainted base set (Docs/SWARM_FAUNA.md §19.2).");
                    }
                }
                if (!set) return false;
                _looks[0 + slot] = set.BlockMaterial;
                _looks[3 + slot] = set.DangerousBlockMaterial;
                _looks[6 + slot] = set.ShieldedBlockMaterial;
            }
            for (int l = 0; l < _looks.Length; l++) if (!_looks[l]) return false;
            return true;
        }

        void ReleaseBodyEntities()
        {
            if (_handles.IsCreated)
            {
                for (int i = 0; i < _cap; i++)
                {
                    var h = _handles[i];
                    PrismRenderService.Destroy(ref h);
                }
                _handles.Dispose();
            }
            if (_shownHandles.IsCreated) _shownHandles.Dispose();
            if (_restyleHandles.IsCreated) _restyleHandles.Dispose();
            if (_lookScratch.IsCreated) _lookScratch.Dispose();
            CompletePose();
            if (_matrices.IsCreated) _matrices.Dispose();
            if (_instNative.IsCreated) _instNative.Dispose();
            if (_shownNative.IsCreated) _shownNative.Dispose();
            _entities = null;
            _unified = false;
        }

        /// <summary>The ECS path went away under a live swarm (a world teardown): the instanced body draw takes over.</summary>
        void FallBackToInstancedBodies(string why)
        {
            CSDebug.LogWarning($"[Swarm] {name}: member bodies fall back to the instanced draw - {why} (Docs/SWARM_FAUNA.md §19.2).");
            ReleaseBodyEntities();
            if (_render != null) _render.DrawBodies = true;
        }

        /// <summary>
        /// Once per TICK (§19.2): entities for slots holding a member for the first time (ONE CreateBatch), a restyle
        /// for every shown member whose tier or domain changed (ONE SetLooksBatch), shows for newborns, hides for the
        /// dead (queued - the service flushes them as one structural change per direction), and the compact handle list
        /// the per-frame transform write walks.
        /// </summary>
        void SyncEntities()
        {
            if (!_unified || _entities == null) return;
            CompletePose();   // never in flight here (Update finishes it), but the native inputs are about to change
            var inst = _job.Instances;
            _entities.Sync(inst);

            var create = _entities.Create;
            if (create.Count > 0)
            {
                int n = create.Count;
                var mats = new NativeArray<float4x4>(n, Allocator.TempJob);
                var outHandles = new NativeArray<PrismRenderHandle>(n, Allocator.TempJob);
                var up = _job.BY; var upAlt = _job.BZ;
                for (int k = 0; k < n; k++)
                {
                    SwarmBodyPose.Matrix(inst[create[k]], 0f, _job.Tick, _bloomTicks, up, upAlt, _matrixScratch, 0);
                    mats[k] = ToFloat4x4(_matrixScratch, 0);
                }
                bool ok = PrismRenderService.CreateBatch(_bodyMesh, _looks[0], gameObject.layer, mats, outHandles);
                if (ok)
                    for (int k = 0; k < n; k++) { _handles[create[k]] = outHandles[k]; _look[create[k]] = byte.MaxValue; }
                mats.Dispose();
                outHandles.Dispose();
                _entities.Created(ok);
                if (!ok) { FallBackToInstancedBodies("PrismRenderService.CreateBatch declined"); return; }
            }

            // restyle: tier (plain / danger / shield) x domain slot, only where it changed
            int r = 0;
            for (int k = 0; k < _entities.ShownCount; k++)
            {
                int i = _entities.Shown[k];
                ref var s = ref inst[i];
                byte look = (byte)(Mathf.Clamp(s.Tier, 0, 2) * 3 + Mathf.Clamp(s.DomainSlot, 0, 2));
                if (_look[i] == look) continue;
                _look[i] = look;
                _restyleHandles[r] = _handles[i];
                _lookScratch[r] = look;
                r++;
            }
            if (r > 0) PrismRenderService.SetLooksBatch(_restyleHandles, _lookScratch, r, _looks);

            for (int k = 0; k < _entities.Show.Count; k++) PrismRenderService.QueueVisible(_handles[_entities.Show[k]], true);
            for (int k = 0; k < _entities.Hide.Count; k++) PrismRenderService.QueueVisible(_handles[_entities.Hide[k]], false);
            for (int k = 0; k < _entities.ShownCount; k++) _shownHandles[k] = _handles[_entities.Shown[k]];

            // the pose job's inputs for the frames until the next tick: two memcpys (the published frame, the shown list)
            _instNative.CopyFrom(inst);
            _shownNative.CopyFrom(_entities.Shown);
        }

        /// <summary>
        /// Once per FRAME, first thing after the display alpha is known (§19.2, §19.4): schedules <see cref="SwarmPoseJob"/> -
        /// every shown member's body matrix at this alpha, Burst-compiled, on the job workers - and kicks the workers. The
        /// pose is <see cref="SwarmBodyPose.PoseMatrix"/>, the function the harness proves (R11d).
        /// </summary>
        void SchedulePoseBodies(float alpha)
        {
            CompletePose();
            int n = _entities != null ? _entities.ShownCount : 0;
            _poseCount = n;
            if (n == 0) return;
            _poseHandle = new SwarmPoseJob
            {
                Instances = _instNative, Shown = _shownNative, Matrices = _matrices,
                Alpha = alpha, Clock = _job.Tick + alpha, BloomTicks = _bloomTicks, Up = _job.BY, UpAlt = _job.BZ,
            }.Schedule(n, SwarmPoseJob.BatchSize);
            JobHandle.ScheduleBatchedJobs();
        }

        /// <summary>
        /// Once per FRAME, last thing in Update: ONE Burst transform write for the swarm, chained on the pose job
        /// (<see cref="PrismRenderService.SetTransformsBatch(NativeArray{PrismRenderHandle}, NativeArray{float4x4}, int, JobHandle)"/>
        /// resolves the handles in a job too). Nothing per member runs on the main thread.
        /// </summary>
        void WritePosedBodies()
        {
            int n = _poseCount;
            _poseCount = 0;
            if (n == 0 || !_unified) { CompletePose(); return; }
            var dep = _poseHandle;
            _poseHandle = default;
            PrismRenderService.SetTransformsBatch(_shownHandles, _matrices, n, dep);
        }

        void CompletePose()
        {
            _poseHandle.Complete();
            _poseHandle = default;
        }

        static float4x4 ToFloat4x4(float[] m, int o) => new float4x4(
            new float4(m[o + 0], m[o + 1], m[o + 2], m[o + 3]), new float4(m[o + 4], m[o + 5], m[o + 6], m[o + 7]),
            new float4(m[o + 8], m[o + 9], m[o + 10], m[o + 11]), new float4(m[o + 12], m[o + 13], m[o + 14], m[o + 15]));

        /// <summary>
        /// The swarm's domain SLOT table (§16.4): slot 0 is the anchor's - the cell's controlling domain when the
        /// swarm was seeded, which every seed wears. A one-colour swarm maps every slot to it; a MultiDomain swarm
        /// gives slots 1 and 2 the other two playable domains in Jade, Ruby, Gold order. Fixed for the swarm's
        /// life, so a member's slot always names the same domain.
        /// </summary>
        Domains[] BuildSlotDomains()
        {
            var t = new[] { domain, domain, domain };
            if (!Lineages) return t;
            int k = 1;
            foreach (var d in PlayableDomains)
                if (d != domain && k < 3) t[k++] = d;
            return t;
        }

        static readonly Domains[] PlayableDomains = { Domains.Jade, Domains.Ruby, Domains.Gold };

        protected override bool AcceptsTeamRecolour => !Lineages;

        /// <summary>Round 9 (§17): this swarm grows regional lineages - MultiDomain, on the one model that has them.
        /// Any other model is one colour whatever the flag says.</summary>
        bool Lineages => config && config.MultiDomain && config.Model == SwarmModel.Sort;

        /// <summary>A one-colour swarm re-coloured by its cell (Cell.SetModeControlOverride): every slot, the
        /// palette and every live proxy take the new domain at once, so the cell never holds two fauna colours.</summary>
        protected override void OnTeamChanged()
        {
            if (!_seeded) return;
            _slotDomain = BuildSlotDomains();
            if (_gpu) ApplyPalette();
            // round 11a: the index entries re-state their domain, and every body entity re-takes its look next tick
            if (_entries != null && _index != null) _entries.RestateDomains(this);
            if (_unified)
            {
                if (!BuildLooks()) FallBackToInstancedBodies("the theme lost a prism material on recolour");
                else for (int i = 0; i < _cap; i++) _look[i] = byte.MaxValue;
            }
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

        float StomachCapacity => config.StomachEggs * Density * (config.EggVolume.x + config.EggVolume.y + config.EggVolume.z + config.EggVolume.w) * 0.25f;

        /// <summary>How full the stomach is, 0..1 (the published tick's banked volume over its capacity).</summary>
        float StomachFill
        {
            get
            {
                var st = _job.Stomach;
                float held = _macro is { Collapsed: true } ? (float)_macro.State.StomachTotal : st[0] + st[1] + st[2] + st[3];
                return held / Mathf.Max(1e-3f, StomachCapacity);
            }
        }

        /// <summary>
        /// Grazing. A few members per tick take one bite each of the nearest edible FLORA prism within
        /// reach of where the published frame has them; the prism is consumed (suctioned into a mouth
        /// posed at the biter - the food web's sanctioned down-force on mass) and its volume is QUEUED
        /// into the stomach under the PLANT's element, which is what later pays for eggs. A full stomach
        /// stops grazing: a grown body does not strip its feeding ground for nothing.
        /// </summary>
        void Feed() => Feed(config.BitersPerStep);

        void Feed(int biters)
        {
            if (StomachFill >= 1f) { _lastFedTime = Time.time; return; }

            var index = PrismSpatialIndex.EnsureInstance();
            if (index == null || !index.IsAvailable) return;

            var inst = _job.Instances;
            int bitten = 0;   // round 7: biters NOT x Density - bites are the one main-thread cost that scales with appetite
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
                    var eater = MemberDomain(i);   // a member eats as its own domain (§16.4, the platform's one predicate)
                    if (!IsFood(prism, eater, out int e)) continue;
                    float volume = Mathf.Max(0.001f, prism.Volume);
                    bool fromGoal = _goalPlant && (prism as HealthPrism).LifeForm == _goalPlant;
                    prism.Consume(MouthFor(i, at), eater, _eaterName, false, true);
                    // round 9: food pays for eggs; it never decides a colour. Collapsed, the macro body banks it (same queue)
                    if (_macro is { Collapsed: true }) _macro.Bank(e, volume);
                    else _job.QueueDeposit(e, volume);
                    _lastFedTime = Time.time;
                    if (fromGoal) _lastGoalBite = Time.time;
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
        /// Where to swim (round 9, Docs/SWARM_FAUNA.md §17.1) - an ordinary grazer's day, not a vigil at one crystal.
        /// HUNGRY (stomach below <see cref="SwarmFaunaConfigSO.ForageBelow"/>) the swarm heads for the nearest plant in
        /// its band it can EAT and has not just left; it grazes there until SATED
        /// (<see cref="SwarmFaunaConfigSO.SatedAbove"/>) or until <see cref="SwarmFaunaConfigSO.GiveUpSeconds"/> pass
        /// with no bite taken from THAT plant (grazed bare). Either way it leaves and the plant is rested for
        /// <see cref="SwarmFaunaConfigSO.PlantRestSeconds"/>, so the next meal is somewhere else. Sated, it roams its
        /// band. Round 8 parked a swarm on the nearest plant's heart crystal forever: the goal was always the nearest
        /// plant, edible or not, and any bite anywhere (a regrowing membrane always offers one) reset its only exit.
        /// </summary>
        protected override Vector3 ResolveGoal()
        {
            if (_job == null || !HostCell) return Goal;
            Vector3 here = transform.position;
            float now = Time.time, fill = StomachFill;

            if (!_foraging && fill < config.ForageBelow) _foraging = true;
            else if (_foraging && fill >= config.SatedAbove) _foraging = false;

            if (_goalPlant)
            {
                bool bare = _atPlantSince >= 0f && now - Mathf.Max(_atPlantSince, _lastGoalBite) > config.GiveUpSeconds;
                bool unreached = _atPlantSince < 0f && now - _goalSince > 6f * config.GiveUpSeconds;   // never got there
                if (!_foraging || bare || unreached || _goalPlant.IsDying)
                {
                    _rested[_goalPlant] = now;   // leave it to regrow; the next meal is elsewhere
                    _goalPlant = null;
                    _atPlantSince = -1f;
                }
            }

            if (_foraging)
            {
                var plant = _goalPlant ? _goalPlant : FloraHeartRegistry.NearestToPoint(here, f =>
                    f.IsDying || !IsInsideBand(f.HeartTransform.position) ||
                    HostCell.IsInsideNucleus(f.HeartTransform.position) ||
                    !IsPreyForMe(f.HeartTransform.position, f.Domain) ||       // never led to food it cannot eat
                    SwarmFaunaConfigSO.ToIndex(f.Element) < 0 ||
                    (_rested.TryGetValue(f, out float t) && now - t < config.PlantRestSeconds));
                if (plant)
                {
                    if (plant != _goalPlant) { _goalPlant = plant; _goalSince = now; _atPlantSince = -1f; _lastGoalBite = -1f; }
                    Vector3 target = plant.HeartTransform.position;
                    if (_atPlantSince < 0f && (target - here).sqrMagnitude < 60f * 60f) _atPlantSince = now;
                    return target;
                }
            }

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
