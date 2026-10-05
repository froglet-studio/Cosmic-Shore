using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.Utility;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;
using SVector3 = System.Numerics.Vector3;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The anchor of a colony that STEALS prisms (Docs/BUILDERS_AND_THIEVES.md): a FORTRESS that walls its core in with
    /// stolen mass and knits every cut shut, or a THIEF NEST on a plant whose magpies tail a ship and hoard its warm
    /// wake. Thin Unity glue over the pure cores (<see cref="BuilderColonyCore"/>, <see cref="ThiefNestCore"/>), in the
    /// swarm's round-7 shape (Docs/SWARM_FAUNA.md §14):
    ///  • members are DATA, drawn from one GPU buffer (<see cref="SwarmMemberRenderer"/>, the swarm's member draw);
    ///  • a member within <see cref="BuilderColonyConfigSO.EngageRadius"/> of a vessel is given a real proxy
    ///    (<see cref="SwarmTadpoleFauna"/>: a heart + one body prism), so every weapon, ram and joust finds a body;
    ///  • every death - a ram, a weapon, an empty stomach - goes through a proxy's sealed Die, so each member drops
    ///    exactly one crystal (its own heart) and nothing pops;
    ///  • the prisms it steals are the platform's own (<see cref="BuilderPrismWorld"/>): no structure adds a collider;
    ///  • a member's BODY is an ordinary PrismSpatialIndex VIRTUAL entry while it is only data (round 11a's one prism
    ///    system, Docs/SWARM_FAUNA.md §19.1, through the swarm's own <see cref="SwarmEntryLedger"/>), so every prism
    ///    query, AOE pass, predator (<see cref="VirtualFauna"/>) and the cell's volume sum sees it exactly once.
    ///
    /// The anchor is heartless (a population, like the swarm): the species config's element names the members' hearts.
    /// Its colour is the domain the cell spawned it in (the controlling domain) and stays its own - the colony's
    /// stolen walls and hoard are its history, so a cell's one-colour re-colour does not repaint it.
    /// </summary>
    public class BuilderColonyFauna : Fauna, IVirtualFaunaOwner, IVirtualPrismBudget, ISwarmEntrySink
    {
        [Header("Builder Colony")]
        [Tooltip("Species, numbers and look (Docs/BUILDERS_AND_THIEVES.md). Authored by Tools/Build/author_builders.py.")]
        [SerializeField] BuilderColonyConfigSO config;

        static readonly ProfilerMarker s_mTick = new("BuilderColonyFauna.Tick");
        static readonly ProfilerMarker s_mSense = new("BuilderColonyFauna.Tick.SenseVessels");
        static readonly ProfilerMarker s_mStep = new("BuilderColonyFauna.Tick.Step");
        static readonly ProfilerMarker s_mProxies = new("BuilderColonyFauna.Tick.Proxies");
        static readonly ProfilerMarker s_mUpload = new("BuilderColonyFauna.Tick.Upload");
        static readonly ProfilerMarker s_mFrame = new("BuilderColonyFauna.Frame");

        const int MaxVessels = 8;
        const int MaxSpawnsPerFrame = 6;

        BuilderColonyCore _fort;
        ThiefNestCore _thief;
        BuilderPrismWorld _world;
        int _colonyId, _cap, _tick;
        float _dt, _acc, _bloomTicks;
        Element _element = Element.Mass;
        bool _seeded, _gpu;
        Vector3 _nest;

        // the cores' read surface (the same shape on both species)
        SVector3[] _pos, _vel;
        bool[] _alive, _platformBody;
        float[] _bornAt;
        List<BuilderDeath> _deaths;
        List<int> _born;

        // the frame: last tick's and this tick's poses (the GPU and the proxies interpolate between them)
        SVector3[] _prev, _prevFace, _face;
        float[] _birthTick;
        SwarmInstance[] _inst;
        uint[] _heartIdx;
        readonly int[] _heartStart = new int[4], _heartCount = new int[4];
        SwarmMemberRenderer _render;
        Bounds _bounds;
        readonly Vector4[] _tierDark = new Vector4[9], _tierBright = new Vector4[9];

        // proxies
        SwarmTadpoleFauna[] _proxy;
        float[] _wantedAt;
        readonly List<int> _proxySlots = new();
        readonly List<SwarmTadpoleFauna> _dying = new();
        int _spawnFrame = -1, _spawnsThisFrame;

        // vessels
        readonly BuilderVessel[] _vessels = new BuilderVessel[MaxVessels];
        readonly List<IVesselStatus> _seen = new(MaxVessels);
        readonly List<Vector3> _vesselPos = new(MaxVessels);
        int _vesselCount;

        Transform[] _mouths;
        float _alpha;

        // the spatial index (round 11a): one virtual entry per data-only member's body
        PrismSpatialIndex _index;
        SwarmEntryLedger _entries;
        bool[] _realBody;
        SVector3[] _points;
        NativeArray<int> _entryIdsNative;
        NativeArray<float3> _pointsNative;
        float _heartReach;
        // a weapon or predator reaching a data-only member makes it real - budgeted per frame across every colony
        const int MaxHitMaterialisationsPerFrame = 16;
        static int s_hitFrame = -1, s_hitsThisFrame;

        // Enter Play Mode without a domain reload keeps statics: start every session clean.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { s_hitFrame = -1; s_hitsThisFrame = 0; }

        public BuilderColonyConfigSO Config => config;
        public BuilderSpecies Species => config ? config.Species : BuilderSpecies.Fortress;
        public int MemberCount => _fort != null ? _fort.AliveCount : _thief != null ? _thief.AliveCount : 0;
        public int ProxyCount => _proxySlots.Count;
        /// <summary>Prisms in this colony's structure (a fortress's wall, a nest's hoard).</summary>
        public int StructureCount => _fort != null ? _fort.Built : _thief != null ? _thief.HoardCount : 0;
        public int OpenWounds => _fort != null ? _fort.OpenWounds : 0;
        public Vector3 NestPosition => _nest;

        /// <summary>A member's speed in world units/s (what a jouster has to outrun).</summary>
        public override float CurrentSpeed =>
            !config ? 0f : config.Species == BuilderSpecies.Thieves ? config.ThiefSpeed : config.WorkerSpeed;

        /// <summary>The anchor is a population, not an animal: nothing eats a colony whole.</summary>
        public override bool Predated(string predatorName, Transform devourTarget) => false;

        /// <summary>The colony's colour is its own (its walls and hoard are stolen history), not the cell's re-colour.</summary>
        protected override bool AcceptsTeamRecolour => false;

        /// <summary>Element-as-data for a POPULATION: the anchor is heartless; the element names the members' hearts.</summary>
        protected override void ProvisionHeart(Element element)
        {
            if (element is >= Element.Charge and <= Element.Time) _element = element;
        }

        /// <summary>No goal coroutine: the core steers every member.</summary>
        protected override void Start() => Seed();

        // ───────────────────────────────────────────────────────────────── seeding

        void Seed()
        {
            if (_seeded) return;
            var host = HostCell;
            if (!config || !config.MemberPrefab || !host)
            {
                CSDebug.LogWarning($"{name}: builder colony has no config, member prefab or host cell - it will not found.");
                return;
            }
            _seeded = true;
            _dt = 1f / Mathf.Max(1f, config.TickHz);
            _bloomTicks = config.BirthBloomSeconds * config.TickHz;
            _colonyId = BuilderRegistry.NewColonyId();
            var centre = host.transform.position;
            float membrane = host.MembraneRadius > 1f ? host.MembraneRadius : 1200f;
            float bandInner = 0f, bandOuter = 0f;
            var cfg = SourceConfig;
            if (cfg && cfg.BandOuterRadius > 0f)
            {
                // the band is where the colony LIVES; it forages a worker's sense beyond it either side (a creature is
                // never led to mass it cannot reach - the workers fly the whole reach, the containment is the membrane)
                bandInner = Mathf.Max(0f, Mathf.Min(cfg.BandInnerRadius, cfg.BandOuterRadius) - config.Sense);
                bandOuter = Mathf.Max(cfg.BandInnerRadius, cfg.BandOuterRadius) + config.Sense;
            }

            const int mouthCount = 4;
            _mouths = new Transform[mouthCount];
            for (int q = 0; q < mouthCount; q++)
            {
                var go = new GameObject("BuilderMouth");
                go.transform.SetParent(transform, false);
                _mouths[q] = go.transform;
            }
            string colonyName = config.Species == BuilderSpecies.Fortress ? "fortress colony" : "thief nest";
            _world = new BuilderPrismWorld(colonyName, _colonyId, domain, _mouths);
            int seed = Random.Range(1, int.MaxValue);

            if (config.Species == BuilderSpecies.Fortress)
            {
                _nest = transform.position;
                _fort = new BuilderColonyCore(_world, config.ToColonyParams(centre, membrane, bandInner, bandOuter),
                                              S(_nest), (int)domain, _colonyId, seed);
                _world.SiteAddress = site => { _fort.SiteCoords(site, out int x, out int y, out int z); return new Vector3Int(x, y, z); };
                _cap = _fort.Cap; _pos = _fort.Pos; _vel = _fort.Vel; _alive = _fort.Alive; _bornAt = _fort.BornAt;
                _deaths = _fort.Deaths; _born = _fort.Born; _platformBody = _fort.PlatformBody;
            }
            else
            {
                _nest = FindNestPlant(centre, membrane);
                _thief = new ThiefNestCore(_world, config.ToThiefParams(centre, membrane), S(_nest), (int)domain, _colonyId, seed);
                _cap = _thief.Cap; _pos = _thief.Pos; _vel = _thief.Vel; _alive = _thief.Alive; _bornAt = _thief.BornAt;
                _deaths = _thief.Deaths; _born = _thief.Born; _platformBody = _thief.PlatformBody;
            }

            _prev = new SVector3[_cap]; _prevFace = new SVector3[_cap]; _face = new SVector3[_cap];
            _birthTick = new float[_cap];
            _inst = new SwarmInstance[_cap];
            _heartIdx = new uint[Mathf.Max(1, 2 * _cap)];
            _proxy = new SwarmTadpoleFauna[_cap];
            _wantedAt = new float[_cap];
            for (int k = 0; k < _cap; k++)
            {
                _prev[k] = _pos[k];
                _face[k] = _prevFace[k] = new SVector3(0f, 0f, 1f);
                _birthTick[k] = -1000f;
            }

            _gpu = false;
            if (config.DrawMembersOnGpu)
            {
                _render = new SwarmMemberRenderer(config.MemberShader, config.MemberPrefab, _cap, gameObject.layer);
                _gpu = _render.Valid;
                if (_gpu) ApplyPalette();
            }
            BuildFrame();
            if (_gpu) Upload();
            BindIndexEntries();
            SyncIndex();
            VirtualFauna.Register(this);
            _acc = Random.value * _dt;   // stagger colonies across frames from the first tick

            CSDebug.LogVerbose(CSLogChannel.Ecology,
                $"[Builders] {name} founded a {config.Species} colony of {MemberCount} ({_element}, {domain}) at " +
                $"r={(_nest - centre).magnitude:F0}; members {(_gpu ? "GPU-drawn" : "GameObjects")}");
        }

        /// <summary>A thief nest sits ON a plant: the nearest living flora heart within a territory's reach of where the cell
        /// spawned the anchor (else that spawn point). Perched a little outside the heart so the hoard wraps the crown.</summary>
        Vector3 FindNestPlant(Vector3 centre, float membrane)
        {
            var from = transform.position;
            float reach2 = config.ScoutRange * config.ScoutRange;
            var plant = FloraHeartRegistry.NearestToPoint(from,
                f => !f || f.IsDying || !f.HeartTransform || (f.HeartTransform.position - from).sqrMagnitude > reach2
                     || (f.HeartTransform.position - centre).magnitude > membrane * 0.9f);
            if (!plant || !plant.HeartTransform) return transform.position;
            var at = plant.HeartTransform.position;
            var outward = at - centre;
            outward = outward.sqrMagnitude > 1f ? outward.normalized : Vector3.up;
            return at + outward * 6f;
        }

        void ApplyPalette()
        {
            var colors = config.Theme ? config.Theme.ColorSet : null;
            Color hd = new(0.1f, 0.2f, 0.5f), hb = new(0.8f, 0.9f, 1.4f);
            if (colors == null)
                CSDebug.LogWarning($"{name}: BuilderColonyConfigSO.Theme is not assigned - members are drawn in fallback colours.");
            Color bd = new(0.05f, 0.2f, 0.4f), bb = new(0.4f, 0.8f, 1.2f);
            Color dd = bd, db = new(1.5f, 0.3f, 0.1f), sd = bd, sb = bb;
            if (colors != null)
            {
                colors.TryGetPrismKindColors(domain, PrismKind.Plain, out bb, out bd);
                colors.TryGetPrismKindColors(domain, PrismKind.Danger, out db, out dd);
                colors.TryGetPrismKindColors(domain, PrismKind.Shielded, out sb, out sd);
                if (colors.TryGetColorSetByDomain(Domains.Blue, out var neutral) && neutral != null)
                { hd = neutral.DullCrystalColor; hb = neutral.BrightCrystalColor; }
            }
            for (int slot = 0; slot < 3; slot++)
            {
                _tierDark[0 + slot] = bd; _tierBright[0 + slot] = bb;
                _tierDark[3 + slot] = dd; _tierBright[3 + slot] = db;
                _tierDark[6 + slot] = sd; _tierBright[6 + slot] = sb;
            }
            _render.SetColours(_tierDark, _tierBright, hd, hb, config.HeartWorldScale, 2f);
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

        // ───────────────────────────────────────────────────────────────── the frame

        void Update()
        {
            if (!_seeded) return;
            using (s_mFrame.Auto())
            {
                _acc += Time.deltaTime;
                if (_acc >= _dt)
                {
                    _acc -= _dt;
                    if (_acc > _dt) _acc = _dt;   // never bank more than one tick
                    using (s_mTick.Auto()) Tick();
                }
                float alpha = Mathf.Clamp01(_acc / _dt);
                _alpha = alpha;
                PollProxyDeaths();   // per frame: a member the platform killed stops being drawn in the same frame
                _world.Animate(alpha);
                PoseProxies(alpha);
                ShedDying();
                if (_gpu)
                    _render.Draw(_bounds, alpha, _tick + alpha, _bloomTicks, Vector3.up, Vector3.forward, _cap);
            }
        }

        void Tick()
        {
            using (s_mSense.Auto()) SenseVessels();
            for (int k = 0; k < _cap; k++)
            {
                _prev[k] = _pos[k];
                _prevFace[k] = _face[k];
                var m = _proxy[k];
                _platformBody[k] = m && !m.IsDead && m.Ready;   // its real body takes the vessel's contact (plate, ram)
            }
            using (s_mStep.Auto())
            {
                if (_fort != null) _fort.Step(_dt, _vessels, _vesselCount);
                else _thief.Step(_dt, _vessels, _vesselCount);
            }
            _tick++;
            for (int k = 0; k < _cap; k++)
            {
                if (!_alive[k]) continue;
                var v = _vel[k];
                if (v.LengthSquared() > 1e-4f) _face[k] = SVector3.Normalize(v);
            }
            for (int q = 0; q < _born.Count; q++)
            {
                int k = _born[q];
                _birthTick[k] = _tick;
                _prev[k] = _pos[k];
            }
            for (int q = 0; q < _deaths.Count; q++) HandleCoreDeath(_deaths[q]);
            BuildFrame();
            using (s_mUpload.Auto()) if (_gpu) Upload();
            using (s_mProxies.Auto()) UpdateProxies();
            SyncIndex();
        }

        void BuildFrame()
        {
            int e = SwarmFaunaConfigSO.ToIndex(_element);
            float heart = config.HeartScale(_element);
            var scale = new SVector3(config.BodyScale.x, config.BodyScale.y, config.BodyScale.z);
            float prismZ = -(heart + config.HeartPrismGap + 0.5f * scale.Z);
            int n = 0;
            var min = new SVector3(float.MaxValue);
            var max = new SVector3(float.MinValue);
            for (int k = 0; k < _cap; k++)
            {
                ref var s = ref _inst[k];
                if (!_alive[k]) { s.Flags = 0u; continue; }
                int tier = _fort != null && _fort.Striking(k) ? 1 : 0;
                s.PrevPos = _prev[k]; s.CurPos = _pos[k];
                s.PrevFace = _prevFace[k]; s.CurFace = _face[k];
                s.PrevMolt = 0f; s.CurMolt = 0f;
                s.Scale = scale; s.PrismZ = prismZ;
                s.BirthTick = _birthTick[k];
                s.Flags = SwarmInstance.Pack(true, tier, e, e, 0);
                _heartIdx[n++] = (uint)k;
                min = SVector3.Min(min, SVector3.Min(_prev[k], _pos[k]));
                max = SVector3.Max(max, SVector3.Max(_prev[k], _pos[k]));
            }
            for (int q = 0; q < 4; q++) { _heartStart[q] = 0; _heartCount[q] = 0; }
            _heartCount[e] = n;
            if (n == 0) { _bounds = new Bounds(_nest, Vector3.one); return; }
            float pad = 20f + config.BodyScale.z + heart;
            _bounds = new Bounds();
            _bounds.SetMinMax(U(min) - Vector3.one * pad, U(max) + Vector3.one * pad);
        }

        void Upload() => _render.Upload(_inst, _heartIdx, _heartStart, _heartCount);

        // ───────────────────────────────────────────────────────────────── vessels

        void SenseVessels()
        {
            _vesselCount = 0;
            _seen.Clear();
            _vesselPos.Clear();
            _world.Vessels.Clear();
            float radius = _fort != null
                ? config.ShellRadius * 2f + Mathf.Max(config.AlarmRadius, config.EngageRadius) + config.Sense
                : config.Territory + config.ScoutRange;
            int hits = Physics.OverlapSphereNonAlloc(_nest, radius, OverlapScratch, NonPrismOverlapMask);
            for (int h = 0; h < hits && _vesselCount < MaxVessels; h++)
            {
                var col = OverlapScratch[h];
                if (!col) continue;
                if (!col.TryGetComponent(out IVesselStatus status))
                    status = col.GetComponentInParent<IVesselStatus>();
                if (status == null || _seen.Contains(status) || status is not Component c || !c) continue;
                _seen.Add(status);
                var p = c.transform.position;
                var v = status.Course * status.Speed;
                _vessels[_vesselCount] = new BuilderVessel
                {
                    Pos = S(p), Vel = S(v), Radius = config.VesselRadius, Id = _vesselCount,
                    Domain = (int)status.Domain, Rams = true,
                };
                _world.Vessels.Add((status.PlayerName, status.Domain));
                _vesselPos.Add(p);
                _vesselCount++;
            }
        }

        // ───────────────────────────────────────────────────────────────── deaths

        /// <summary>The core killed a member (a ram, a knock-down, an empty stomach): it dies through a proxy's sealed Die,
        /// so it drops its one crystal where it was drawn.</summary>
        void HandleCoreDeath(BuilderDeath d)
        {
            int k = d.Agent;
            if (k < 0 || k >= _cap) return;
            if (d.Vessel == BuilderDeath.PlatformKill) return;   // the platform already ran this death on the proxy
            HideSlot(k);
            _entries?.Release(k, this);   // the proxy's real body (then its skeleton or its eater) holds the mass now
            var m = _proxy[k];
            if (!m || m.IsDead) m = SpawnProxy(k, force: true);
            DetachProxy(k);
            if (!m) return;
            m.MaterialiseNow();
            if (d.Vessel == BuilderDeath.StarvedBy) { _dying.Add(m); return; }
            string killer = d.Vessel >= 0 && d.Vessel < _world.Vessels.Count ? _world.Vessels[d.Vessel].name : "vessel";
            // a newborn's predation immunity refuses a joust; the core already counted the death, so it withers instead
            if (!m.Jousted(killer)) _dying.Add(m);
        }

        /// <summary>A proxy the core killed by starvation (or whose joust was refused) withers once its body is real.</summary>
        void ShedDying()
        {
            for (int i = _dying.Count - 1; i >= 0; i--)
            {
                var m = _dying[i];
                if (!m || m.IsDead) { _dying.RemoveAt(i); continue; }
                if (!m.Ready && !m.MaterialiseNow()) continue;
                m.Starve();
                _dying.RemoveAt(i);
            }
        }

        /// <summary>The platform killed a proxy (a weapon, a ram on its body prism, a joust, a predator): the core
        /// learns it, drops what the member carried - a knocked-down thief's prism goes back to its pilot.</summary>
        void PollProxyDeaths()
        {
            for (int q = _proxySlots.Count - 1; q >= 0; q--)
            {
                int k = _proxySlots[q];
                var m = _proxy[k];
                if (m && !m.IsDead) continue;
                DetachProxy(k);
                if (!_alive[k]) continue;
                _entries?.Release(k, this);
                if (_fort != null) _fort.Kill(k, BuilderDeath.PlatformKill);
                else _thief.Kill(k, BuilderDeath.PlatformKill);
                HideSlot(k);
            }
        }

        void HideSlot(int k)
        {
            _inst[k].Flags = 0u;
            if (_gpu) _render.HideSlot(_inst, k);
        }

        void DetachProxy(int k)
        {
            _proxy[k] = null;
            _proxySlots.Remove(k);
        }

        // ───────────────────────────────────────────────────────────────── proxies

        void UpdateProxies()
        {
            float now = Time.time;
            float r2 = config.EngageRadius * config.EngageRadius;
            for (int k = 0; k < _cap; k++)
            {
                if (!_alive[k]) continue;
                var p = U(_pos[k]);
                for (int v = 0; v < _vesselPos.Count; v++)
                    if ((_vesselPos[v] - p).sqrMagnitude < r2) { _wantedAt[k] = now; break; }
            }
            // retire the ones nobody is near any more (not a death: the member lives on in the core)
            for (int q = _proxySlots.Count - 1; q >= 0; q--)
            {
                int k = _proxySlots[q];
                if (now - _wantedAt[k] <= config.ProxyLingerSeconds) continue;
                var m = _proxy[k];
                DetachProxy(k);
                if (m) m.Retire();
                ResumeEntry(k);
            }
            // nearest first is not needed for correctness: a member a vessel is about to reach was wanted last tick too
            for (int k = 0; k < _cap && _proxySlots.Count < config.MaxProxies; k++)
            {
                if (!_alive[k] || _proxy[k] || now - _wantedAt[k] > 0.01f) continue;
                if (!SpawnProxy(k, force: false)) break;
            }
            for (int q = 0; q < _proxySlots.Count; q++)
            {
                int k = _proxySlots[q];
                var m = _proxy[k];
                if (m && !m.IsDead) m.SetTier(_fort != null && _fort.Striking(k), false);
            }
        }

        SwarmTadpoleFauna SpawnProxy(int k, bool force)
        {
            int frame = Time.frameCount;
            if (_spawnFrame != frame) { _spawnFrame = frame; _spawnsThisFrame = 0; }
            if (!force && _spawnsThisFrame >= MaxSpawnsPerFrame) return null;
            _spawnsThisFrame++;

            var host = HostCell;
            if (!host) return null;
            var member = Instantiate(config.MemberPrefab, U(_pos[k]), Face(_face[k]));
            float heart = config.HeartScale(_element);
            member.Bind(host, null, k, _element, heart, config.BodyScale,
                        -(heart + config.HeartPrismGap + 0.5f * config.BodyScale.z), hideLive: _gpu,
                        memberDomain: domain,
                        memberAgeSeconds: Mathf.Max(0f, (_tick - _birthTick[k]) / config.TickHz));
            host.RegisterSpawnedObject(member.gameObject);
            FaunaNetworkSync.ServerSpawn(member);
            member.MaterialiseNow();
            _proxy[k] = member;
            _proxySlots.Add(k);
            SuspendEntry(k);   // its body prism is registered now - the virtual entry steps aside (count once)
            return member;
        }

        void PoseProxies(float alpha)
        {
            for (int q = 0; q < _proxySlots.Count; q++)
            {
                int k = _proxySlots[q];
                var m = _proxy[k];
                if (!m || m.IsDead) continue;
                var at = SVector3.Lerp(_prev[k], _pos[k], alpha);
                var f = SVector3.Lerp(_prevFace[k], _face[k], alpha);
                m.transform.SetPositionAndRotation(U(at), Face(f));
                m.SyncBodyToIndex();
            }
        }

        // ───────────────────────────────────────────────────────────────── the spatial index (round 11a)

        void BindIndexEntries()
        {
            _index = PrismSpatialIndex.EnsureInstance();
            _entries = new SwarmEntryLedger(_cap);
            _realBody = new bool[_cap];
            _points = new SVector3[_cap];
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

        /// <summary>Once per TICK: every existing entry's stored point in one bulk push (the body centre at mid-step, as the
        /// swarm stores it), then the ledger registers newborns, releases the dead and suspends members with a real body.</summary>
        void SyncIndex()
        {
            if (_entries == null || _index == null || !_index.IsAvailable) return;
            float reach = 0f;
            for (int k = 0; k < _cap; k++)
            {
                var m = _proxy[k];
                _realBody[k] = m && !m.IsDead && m.Body && !m.Body.destroyed && m.Body.IsCreationComplete;
                if (!_inst[k].Alive) continue;
                _points[k] = SwarmBodyPose.Body(_inst[k], SwarmBodyPose.IndexAlpha);
                float r = Mathf.Abs(_inst[k].PrismZ) + 0.5f * SVector3.Distance(_inst[k].PrevPos, _inst[k].CurPos);
                if (r > reach) reach = r;
            }
            _heartReach = reach;
            _entryIdsNative.CopyFrom(_entries.Ids);
            _pointsNative.Reinterpret<SVector3>().CopyFrom(_points);
            _index.UpdatePositionsBatch(_entryIdsNative, _pointsNative, _cap);
            _entries.Sync(_inst, _realBody, _points, this);
        }

        void SuspendEntry(int k)
        {
            if (_entries == null || _index == null) return;
            _realBody[k] = true;
            int id = _entries.Ids[k];
            if (id < 0 || _entries.IsSuspended(k)) return;
            _index.SetVirtualSuspended(id, true);
            _entries.NoteSuspendedByIndex(k);
        }

        /// <summary>Member k's proxy retired without dying: its entry is re-filed where it is drawn now, live again.</summary>
        void ResumeEntry(int k)
        {
            if (_entries == null || _index == null) return;
            _realBody[k] = false;
            int id = _entries.Ids[k];
            if (id < 0) return;
            _index.UpdatePosition(id, U(SwarmBodyPose.Body(_inst[k], _alpha)));
            _entries.Resume(k, this);
        }

        static bool HitBudgetLeft()
        {
            int frame = Time.frameCount;
            if (s_hitFrame != frame) { s_hitFrame = frame; s_hitsThisFrame = 0; }
            return s_hitsThisFrame < MaxHitMaterialisationsPerFrame;
        }

        // ISwarmEntrySink - the ledger's calls onto the index's virtual-entry API and the host cell's volume binding
        int ISwarmEntrySink.Register(int slot, SVector3 point, int domainSlot, float volume, bool shielded, float radius)
        {
            int id = _index.RegisterVirtual(this, slot, new float3(point.X, point.Y, point.Z), (int)domain, volume, shielded,
                                            false, radius);
            var host = HostCell;
            if (id >= 0 && host) host.BindVirtualMass(id, domain);
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
            _index.UpdateDomain(id, (int)domain);
            var host = HostCell;
            if (host) host.BindVirtualMass(id, domain);
        }

        // IVirtualPrismOwner / IVirtualPrismBudget - how the platform makes a data-only member real (an AOE hit, a round,
        // a predator): its proxy, posed where it is drawn, creation complete. The index suspends the entry itself.
        Prism IVirtualPrismOwner.MaterialiseVirtualPrism(int slot)
        {
            if (!_seeded || slot < 0 || slot >= _cap || !_alive[slot]) return null;
            var m = _proxy[slot];
            if (!m || m.IsDead)
            {
                HitBudgetLeft();
                s_hitsThisFrame++;
                m = SpawnProxy(slot, force: true);
            }
            if (!m || !m.MaterialiseNow()) return null;
            _wantedAt[slot] = Time.time;
            m.transform.SetPositionAndRotation(U(SVector3.Lerp(_prev[slot], _pos[slot], _alpha)),
                                               Face(SVector3.Lerp(_prevFace[slot], _face[slot], _alpha)));
            m.SetTier(_fort != null && _fort.Striking(slot), false);
            m.SyncBodyToIndex();
            _realBody[slot] = true;
            _entries?.NoteSuspendedByIndex(slot);
            return m.Body;
        }

        bool IVirtualPrismBudget.HasMaterialiseBudget(int slot) => HitBudgetLeft();

        // IVirtualFaunaOwner - what only a creature can answer

        bool IVirtualFaunaOwner.IsVirtualPrey(int slot, Vector3 at, Fauna predator, bool herbivoresOnly)
        {
            if (!_seeded || slot < 0 || slot >= _cap || !_alive[slot] || _proxy[slot]) return false;
            if (!predator || predator == this) return false;
            var prefab = config.MemberPrefab;
            if (herbivoresOnly && prefab && prefab.Diet != FaunaDiet.Herbivore) return false;
            float grace = prefab ? prefab.PredationImmunitySeconds : 0f;
            if (grace > 0f && (_tick + _alpha - _birthTick[slot]) / config.TickHz < grace) return false;
            return predator.IsInsideBand(at);
        }

        float IVirtualFaunaOwner.HeartReach => _heartReach;

        bool IVirtualFaunaOwner.TryGetVirtualHeart(int slot, out Vector3 heart)
        {
            heart = default;
            if (!_seeded || slot < 0 || slot >= _cap || !_alive[slot]) return false;
            heart = U(SVector3.Lerp(_prev[slot], _pos[slot], _alpha));
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

        static Quaternion Face(SVector3 f)
        {
            var fw = U(f);
            if (fw.sqrMagnitude < 1e-6f) fw = Vector3.forward;
            var up = Mathf.Abs(Vector3.Dot(fw.normalized, Vector3.up)) > 0.98f ? Vector3.forward : Vector3.up;
            return Quaternion.LookRotation(fw, up);
        }

        static Vector3 U(SVector3 v) => new(v.X, v.Y, v.Z);
        static SVector3 S(Vector3 v) => new(v.x, v.y, v.z);

        // ───────────────────────────────────────────────────────────────── teardown

        protected override void OnDestroy()
        {
            _render?.Dispose();
            _render = null;
            _world?.ReleaseAll();
            if (_seeded) BuilderRegistry.ReleaseColony(_colonyId);
            VirtualFauna.Unregister(this);
            ReleaseIndexEntries();
            base.OnDestroy();
        }
    }
}
