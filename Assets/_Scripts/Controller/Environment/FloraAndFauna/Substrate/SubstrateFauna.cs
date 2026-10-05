using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.Utility;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Round 11b (Docs/SUBSTRATE_FAUNA.md): one POPULATION of a Living Ecology species - a pack of hunters, a locust
    /// cloud, a scatter of lurkers - living in the cell's shared <see cref="SubstrateCore"/>. This is the population's
    /// ANCHOR in the worm colony's shape (Docs/ECOSYSTEM.md §23.3, as <see cref="SwarmFauna"/>): spawned and
    /// lineage-registered by the cell's ordinary spawner in the cell's CONTROLLING domain, heartless and bodiless, never
    /// prey. The lifeforms are its AGENTS - data in the core, drawn from one GPU buffer, and given a real proxy
    /// (<see cref="SubstrateAgentFauna"/>: a heart and a body prism) only when a vessel is near, a weapon or predator
    /// reaches one, or one starves.
    ///
    /// Once per substrate tick (<see cref="OnTickPublished"/>, main thread, called by the cell's
    /// <see cref="SubstrateCellHost"/>) it: uploads its slice of the frame to the GPU; keeps a VIRTUAL entry per agent
    /// in <see cref="PrismSpatialIndex"/> (so AOE finds an agent with no GameObject, and materialises it through
    /// <see cref="IVirtualPrismOwner"/>); gives engaged agents proxies and keeps their tier (a striking agent is a DANGER
    /// prism - contact burns an opposing pilot's petals); lands the bites the core asked for (flora prisms consumed into
    /// a mouth, the volume paid into the agent's body 1:1); resolves its predators' catches (the prey dies through its
    /// own proxy, crystal and all, and its body becomes the predator's); sheds the agents whose stomach ran out (through
    /// a proxy, leaving the body as a skeleton); and states its body volume to the cell.
    ///
    /// Laws (Docs/claude/ECOSYSTEM_DESIGN_PRINCIPLES.md): mass is conserved (an agent's body volume IS its stock; eating
    /// adds, a birth splits, a death leaves it); no imposed death and no timers (starvation is a stomach); nothing pops
    /// (births bloom in the shader, proxies appear and leave under an unchanged picture, deaths wither through the
    /// platform); every agent drops one crystal (only through a proxy - an agent with no proxy cannot die).
    /// </summary>
    public class SubstrateFauna : Fauna, IVirtualPrismOwner
    {
        [Header("Substrate")]
        [Tooltip("The species this population is (its parameters, drawing, proxy budget and feeding).")]
        [SerializeField] SubstrateSpeciesSO species;

        const float BloomTicks = 8f;
        const int HitBudgetPerFrame = 48;
        const int PreyPerTick = 4;

        SubstrateCellHost _host;
        SubstrateMemberRenderer _render;
        bool _gpu, _refused;
        int _element = 1;            // research index; Mass unless the species config says otherwise
        int _pop = -1, _start, _cap;
        string _eaterName;

        SubstrateAgentFauna[] _proxy;
        float[] _wantedAt;
        bool[] _gone, _starving;
        readonly List<int> _proxySlots = new(), _goneSlots = new();

        int[] _vid;
        NativeArray<int> _vIds;
        NativeArray<float3> _vPos;
        bool _reregister;

        Transform[] _mouth;
        int[] _mouthSlot;
        float[] _mouthUntil;
        int _mouthCursor, _biteCursor;
        float _extinctSince = -1f;

        readonly double[] _virtualBySlot = new double[4];
        static int s_spawnFrame = -1, s_spawnsThisFrame, s_hitFrame = -1, s_hitsThisFrame;

        static readonly ProfilerMarker s_mVirtual = new("SubstrateFauna.Tick.Virtual");
        static readonly ProfilerMarker s_mProxies = new("SubstrateFauna.Tick.Proxies");
        static readonly ProfilerMarker s_mFeed = new("SubstrateFauna.Tick.Feed");
        static readonly ProfilerMarker s_mPose = new("SubstrateFauna.Frame.Pose");
        static readonly ProfilerMarker s_mDraw = new("SubstrateFauna.Frame.Draw");

        public SubstrateSpeciesSO Species => species;
        /// <summary>This population's index in the cell's core (-1 until it has joined).</summary>
        public int Pop => _pop;
        /// <summary>Living agents as of the last published tick.</summary>
        public int AgentCount => _host != null && _pop >= 0 ? _host.Job.PopAlive[_pop] : 0;
        public int ProxyCount => _proxySlots.Count;
        public float VesselRadius => species ? species.VesselRadius : 6f;
        /// <summary>The overlap mask a cell-wide vessel sense uses (everything but prism layers).</summary>
        public static int VesselOverlapMask => NonPrismOverlapMask;

        /// <summary>The anchor is a population, not an animal: nothing eats it whole.</summary>
        public override bool Predated(string predatorName, Transform devourTarget) => false;

        /// <summary>A heartless population: the species config's element names the heart every agent carries.</summary>
        protected override void ProvisionHeart(Element element)
        {
            int e = SubstrateSpeciesSO.ToIndex(element);
            if (e >= 0) _element = e;
        }

        /// <summary>The substrate steers the agents; the anchor itself never travels.</summary>
        protected override Vector3 ResolveGoal() => transform.position;

        protected override void Start()
        {
            base.Start();
            if (!species || !species.AgentPrefab || !HostCell)
            {
                CSDebug.LogWarning($"{name}: substrate population has no species, agent prefab or host cell - it will not hatch.");
                return;
            }
            _eaterName = species.SpeciesName;
            _host = SubstrateCellHost.For(HostCell, species);
            _host?.Join(this);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            s_spawnFrame = -1; s_spawnsThisFrame = 0;
            s_hitFrame = -1; s_hitsThisFrame = 0;
        }

        // ───────────────────────────────────────────────────────────────── joining the cell's substrate

        /// <summary>
        /// Called by the host between ticks: take a block of the core, seed it, and bring up drawing. Returns the core
        /// population index, or -1 (the cell's substrate is full).
        /// </summary>
        public int ClaimBlock(SubstrateCellHost host)
        {
            var P = species.ToParams();
            float inner = 0f, outer = 0f;
            var cfg = SourceConfig;
            if (cfg && cfg.BandOuterRadius > 0f)
            {
                inner = Mathf.Min(cfg.BandInnerRadius, cfg.BandOuterRadius);
                outer = Mathf.Max(cfg.BandInnerRadius, cfg.BandOuterRadius);
            }
            int pop = host.Core.AddPopulation(P, _element, inner, outer);
            if (pop < 0) return -1;
            var block = host.Core.Pops[pop];
            _pop = pop; _start = block.Start; _cap = block.Cap;
            _proxy = new SubstrateAgentFauna[_cap];
            _wantedAt = new float[_cap];
            _gone = new bool[_cap];
            _starving = new bool[_cap];
            _vid = new int[_cap];
            for (int k = 0; k < _cap; k++) _vid[k] = -1;
            _vIds = new NativeArray<int>(_cap, Allocator.Persistent);
            _vPos = new NativeArray<float3>(_cap, Allocator.Persistent);
            Seed(host, pop);

            _render = new SubstrateMemberRenderer(species.MemberShader, species.AgentPrefab, _cap, _element, gameObject.layer);
            _gpu = _render.Valid;
            if (_gpu) _render.SetPalette(species.Theme, domain, species.HeartWorldScaleByElement);
            BuildMouths();
            CSDebug.LogVerbose(CSLogChannel.Ecology,
                $"[Substrate] {name}: {species.SpeciesName} population {pop} seeded ({host.Core.Pops[pop].Alive} agents, slots " +
                $"{_start}..{_start + _cap - 1}, band {inner:F0}-{outer:F0}, {(_gpu ? "GPU-drawn" : "proxy-drawn")})");
            return pop;
        }

        void Seed(SubstrateCellHost host, int pop)
        {
            int n = Mathf.Min(species.SeedCount, _cap);
            if (species.SeedAtFlora)
            {
                // an ambusher is seeded AMONG the crystals it mimics: at living flora hearts in its band
                var at = new List<System.Numerics.Vector3>(n);
                var live = FloraHeartRegistry.Live;
                for (int k = 0; k < live.Count && at.Count < n; k++)
                {
                    var f = live[k];
                    if (!f || f.IsDying) continue;
                    var p = f.HeartTransform.position;
                    if (!IsInsideBand(p) || (p - host.Centre).magnitude > host.Core.R) continue;
                    at.Add(ToSim(p));
                }
                for (int k = 0; at.Count > 0 && at.Count < n; k++) at.Add(at[k % at.Count]);
                if (at.Count > 0) { host.Core.SeedAt(pop, at.ToArray(), 12f); return; }
            }
            host.Core.Seed(pop, n, ToSim(transform.position), species.SeedSpread);
        }

        /// <summary>The cell's substrate had no room: this anchor leaves (it holds no agent, so nothing pops).</summary>
        public void OnRefused()
        {
            if (_refused) return;
            _refused = true;
            CSDebug.LogWarning($"[Substrate] {name}: the cell's substrate has no room for another {species.SpeciesName} " +
                               $"population (raise SubstrateSpeciesSO.CellCapacity). The anchor despawns.");
            if (!DespawnOrDestroy()) Destroy(gameObject);
        }

        void BuildMouths()
        {
            const int n = 6;
            _mouth = new Transform[n]; _mouthSlot = new int[n]; _mouthUntil = new float[n];
            for (int q = 0; q < n; q++)
            {
                var go = new GameObject("SubstrateMouth");
                go.transform.SetParent(transform, false);
                _mouth[q] = go.transform;
                _mouthSlot[q] = -1;
            }
        }

        protected override void OnDestroy()
        {
            _host?.Leave(this);
            UnregisterAllVirtual();
            if (_vIds.IsCreated) _vIds.Dispose();
            if (_vPos.IsCreated) _vPos.Dispose();
            _render?.Dispose();
            _render = null;
            var host = HostCell;
            if (host) host.ClearVirtualVolume(this);
            base.OnDestroy();
        }

        // ───────────────────────────────────────────────────────────────── the frame

        void Update()
        {
            if (_host == null) return;
            _host.Advance();
            if (_pop < 0) return;
            float alpha = _host.Alpha;
            using (s_mPose.Auto()) PoseProxies(alpha);
            if (_gpu)
                using (s_mDraw.Auto())
                {
                    float r = _host.Core.R + 200f;
                    _render.Draw(new Bounds(_host.Centre, Vector3.one * (2f * r)), alpha, _host.Job.Tick + alpha, BloomTicks);
                }
        }

        /// <summary>Once per substrate tick, on the main thread: everything this population's slice of the frame drives.</summary>
        public void OnTickPublished(SubstrateCellHost host)
        {
            if (_pop < 0) return;
            var job = host.Job;
            MaskGone(job);
            if (_gpu) _render.Upload(job, _start, _pop);
            ReadEvents(job);
            using (s_mVirtual.Auto()) SyncVirtual(job);
            using (s_mProxies.Auto()) { SyncProxies(job); ShedStarving(job); }
            using (s_mFeed.Auto()) { Feed(job); Hunt(host); }
            StateVirtualVolume(job);
            Extinction(job);
        }

        bool Mine(int i) => i >= _start && i < _start + _cap;

        void MaskGone(SubstrateTickJob job)
        {
            for (int q = _goneSlots.Count - 1; q >= 0; q--)
            {
                int k = _goneSlots[q], i = _start + k;
                if (!job.Instances[i].Alive || job.BornThisTick(i)) { _gone[k] = false; _goneSlots.RemoveAt(q); continue; }
                job.Instances[i].Flags = 0u;
            }
        }

        void ReadEvents(SubstrateTickJob job)
        {
            var ev = job.Events;
            for (int q = 0; q < ev.Count; q++)
            {
                var e = ev[q];
                if (!Mine(e.Index)) continue;
                if (e.Kind == SubstrateEventKind.Starving) _starving[e.Index - _start] = true;
                else if (e.Kind == SubstrateEventKind.Born) _starving[e.Index - _start] = false;
            }
        }

        // ───────────────────────────────────────────────────────────────── virtual entries (PrismSpatialIndex)

        /// <summary>
        /// One virtual entry per living agent at its BODY centre, so every position query (AOE, occupancy) sees an agent
        /// that has no GameObject, and a hit materialises it through <see cref="MaterialiseVirtualPrism"/>. An agent with
        /// a proxy has its entry suspended (its real body prism is registered itself - counted once). The entries are
        /// unbound (no cell volume filing): the population states its body volume through Cell.SetVirtualVolume.
        /// </summary>
        void SyncVirtual(SubstrateTickJob job)
        {
            var index = PrismSpatialIndex.EnsureInstance();
            if (index == null || !index.IsAvailable) return;
            if (_reregister) { UnregisterAllVirtual(); _reregister = false; }
            int n = 0;
            for (int k = 0; k < _cap; k++)
            {
                int i = _start + k;
                bool alive = job.Instances[i].Alive && !_gone[k];
                if (!alive)
                {
                    if (_vid[k] >= 0) { index.Unregister(_vid[k]); _vid[k] = -1; }
                    continue;
                }
                var b = job.BodyAt(i, 1f);
                var p = new float3(b.X, b.Y, b.Z);
                if (_vid[k] < 0)
                {
                    var body = job.Body[i];
                    _vid[k] = index.RegisterVirtual(this, i, p, (int)domain, body.X * body.Y * body.Z);
                    if (_vid[k] >= 0 && _proxy[k]) index.SetVirtualSuspended(_vid[k], true);
                    continue;
                }
                _vIds[n] = _vid[k]; _vPos[n] = p; n++;
            }
            if (n > 0) index.UpdatePositionsBatch(_vIds, _vPos, n);
        }

        void UnregisterAllVirtual()
        {
            if (_vid == null) return;
            var index = PrismSpatialIndex.Instance;
            for (int k = 0; k < _vid.Length; k++)
            {
                if (_vid[k] < 0) continue;
                if (index != null && index.IsAvailable) index.Unregister(_vid[k]);
                _vid[k] = -1;
            }
        }

        /// <summary>The index reached an agent that has no GameObject (AOE damage): make it real now.</summary>
        public Prism MaterialiseVirtualPrism(int slot)
        {
            var m = MaterialiseForHit(slot);
            return m ? m.Body : null;
        }

        void SuspendVirtual(int k, bool suspended)
        {
            if (_vid == null || _vid[k] < 0) return;
            var index = PrismSpatialIndex.Instance;
            if (index == null || !index.IsAvailable) return;
            if (!suspended && _host != null)
            {
                // resume at where the agent is now (the proxy moved it)
                var b = _host.Job.BodyAt(_start + k, _host.Alpha);
                index.UpdatePosition(_vid[k], new Vector3(b.X, b.Y, b.Z));
            }
            index.SetVirtualSuspended(_vid[k], suspended);
        }

        // ───────────────────────────────────────────────────────────────── proxies

        void SyncProxies(SubstrateTickJob job)
        {
            float now = Time.time;
            int engaged = job.EngagedCount[_pop];
            for (int q = 0; q < engaged; q++)
            {
                int k = job.Engaged[_start + q] - _start;
                if (k < 0 || k >= _cap || _gone[k]) continue;
                _wantedAt[k] = now;
                if (!_proxy[k]) TrySpawnProxy(k);
            }
            for (int q = _proxySlots.Count - 1; q >= 0; q--)
            {
                int k = _proxySlots[q], i = _start + k;
                var m = _proxy[k];
                if (!m) { _proxySlots.RemoveAt(q); _proxy[k] = null; continue; }
                if (!job.Instances[i].Alive && !_gone[k])
                {
                    // the core dropped the agent without a death through us: it dies the platform's way
                    m.Starve();
                    continue;
                }
                if (!_starving[k] && now - _wantedAt[k] > species.ProxyLingerSeconds)
                {
                    m.Retire();
                    _proxy[k] = null;
                    _proxySlots.RemoveAt(q);
                    SuspendVirtual(k, false);
                    continue;
                }
                m.SetShape(BodyScale(job, i), BodyZ(job, i));
                m.SetDanger(job.Instances[i].Tier == 1);
            }
        }

        UnityEngine.Vector3 BodyScale(SubstrateTickJob job, int i)
        {
            var b = job.Body[i];
            return new UnityEngine.Vector3(b.X, b.Y, b.Z);
        }

        float BodyZ(SubstrateTickJob job, int i) =>
            -(species.HeartWorldScale(SubstrateSpeciesSO.ToElement(_element)) + species.HeartPrismGap + 0.5f * job.Body[i].Z);

        bool TrySpawnProxy(int k, bool force = false)
        {
            int frame = Time.frameCount;
            if (s_spawnFrame != frame) { s_spawnFrame = frame; s_spawnsThisFrame = 0; }
            if (!force && (s_spawnsThisFrame >= species.MaxSpawnsPerFrame || _proxySlots.Count >= species.MaxProxies)) return false;
            s_spawnsThisFrame++;

            var job = _host.Job;
            int i = _start + k;
            var e = SubstrateSpeciesSO.ToElement(_element);
            var at = job.PoseAt(i, _host.Alpha);
            var member = Instantiate(species.AgentPrefab, new Vector3(at.X, at.Y, at.Z), Face(job.FaceAt(i, _host.Alpha)));
            float age = Mathf.Max(0f, (job.Tick + _host.Alpha - job.Instances[i].BirthTick) * _host.Dt);
            member.Bind(HostCell, this, i, e, species.HeartWorldScale(e), BodyScale(job, i), BodyZ(job, i), domain, age, _gpu);
            HostCell.RegisterSpawnedObject(member.gameObject);
            // The replication seam every fauna producer reaches. An agent carries no NetworkObject (agents are
            // client-local, like every swarm member), so this only neutralizes; it keeps the seam gate whole.
            FaunaNetworkSync.ServerSpawn(member);
            _proxy[k] = member;
            _proxySlots.Add(k);
            SuspendVirtual(k, true);
            return true;
        }

        /// <summary>An agent died (any path, through its proxy). The core hears of it on the next tick.</summary>
        public void HandleAgentDeath(SubstrateAgentFauna agent)
        {
            if (_host == null || !agent) return;
            int k = agent.Index - _start;
            if (k < 0 || k >= _cap || _proxy[k] != agent) return;
            _host.Job.QueueKill(agent.Index);
            _proxy[k] = null;
            _proxySlots.Remove(k);
            _starving[k] = false;
            if (!_gone[k]) { _gone[k] = true; _goneSlots.Add(k); }
            if (_vid[k] >= 0)
            {
                var index = PrismSpatialIndex.Instance;
                if (index != null && index.IsAvailable) index.Unregister(_vid[k]);
                _vid[k] = -1;
            }
            if (_gpu) _render.HideSlot(_host.Job, _start, k);
            else _host.Job.Instances[agent.Index].Flags = 0u;
        }

        void PoseProxies(float alpha)
        {
            var job = _host.Job;
            for (int q = 0; q < _proxySlots.Count; q++)
            {
                int k = _proxySlots[q], i = _start + k;
                var m = _proxy[k];
                if (!m) continue;
                var p = job.PoseAt(i, alpha);
                m.transform.SetPositionAndRotation(new Vector3(p.X, p.Y, p.Z), Face(job.FaceAt(i, alpha)));
                m.SyncBodyToIndex();
                if (_starving[k] && m.Ready) { _starving[k] = false; m.Starve(); }
            }
            for (int q = 0; q < _mouth.Length; q++)
            {
                int k = _mouthSlot[q];
                if (k < 0) continue;
                if (Time.time > _mouthUntil[q] || !job.Instances[_start + k].Alive) { _mouthSlot[q] = -1; continue; }
                var p = job.PoseAt(_start + k, alpha);
                _mouth[q].position = new Vector3(p.X, p.Y, p.Z);
            }
        }

        static Quaternion Face(System.Numerics.Vector3 f)
        {
            var fw = new Vector3(f.X, f.Y, f.Z);
            if (fw.sqrMagnitude < 1e-6f) fw = Vector3.forward;
            var up = Mathf.Abs(Vector3.Dot(fw.normalized, Vector3.up)) > 0.98f ? Vector3.forward : Vector3.up;
            return Quaternion.LookRotation(fw, up);
        }

        /// <summary>
        /// THE seam a weapon, a predator or the index reaches a data-only agent through: the agent becomes its proxy NOW,
        /// posed where it is drawn, wearing its tier, its body prism's creation finished this frame - so the caller runs
        /// its own code on a real body and heart. Null when the agent is gone or (unless forced) the frame's budget is spent.
        /// </summary>
        public SubstrateAgentFauna MaterialiseForHit(int i, bool force = false)
        {
            if (_host == null || !Mine(i)) return null;
            int k = i - _start;
            var job = _host.Job;
            var existing = _proxy[k];
            if (existing)
            {
                _wantedAt[k] = Time.time;
                return existing.IsDead || !existing.MaterialiseNow() ? null : existing;
            }
            if (_gone[k] || !job.Instances[i].Alive) return null;
            int frame = Time.frameCount;
            if (s_hitFrame != frame) { s_hitFrame = frame; s_hitsThisFrame = 0; }
            if (!force && s_hitsThisFrame >= HitBudgetPerFrame) return null;
            if (!TrySpawnProxy(k, force: true)) return null;
            s_hitsThisFrame++;
            var m = _proxy[k];
            _wantedAt[k] = Time.time;
            if (!m.MaterialiseNow()) return null;
            m.SetDanger(job.Instances[i].Tier == 1);
            m.SyncBodyToIndex();
            return m;
        }

        /// <summary>Keep agent <paramref name="i"/>'s proxy alive this frame (a predator is hunting it).</summary>
        public void KeepProxy(int i)
        {
            if (_wantedAt != null && Mine(i)) _wantedAt[i - _start] = Time.time;
        }

        /// <summary>Agent <paramref name="i"/>'s speed in world units/s.</summary>
        public float AgentSpeed(int i) => _host != null && Mine(i) ? _host.Job.Speed[i] : 0f;

        // ───────────────────────────────────────────────────────────────── starvation (a stomach, not a timer)

        /// <summary>
        /// The core flags an agent STARVING when its stomach and reserve are empty (hunger past 1 + StarveS * metabolism -
        /// food pushes it back exactly; nothing here counts time). It never kills: the owner gives the agent a proxy and,
        /// once that body is real, it withers through the sealed death, dropping its crystal and leaving its stock as a
        /// skeleton.
        /// </summary>
        void ShedStarving(SubstrateTickJob job)
        {
            for (int k = 0; k < _cap; k++)
            {
                if (!_starving[k]) continue;
                int i = _start + k;
                if (!job.Instances[i].Alive || _gone[k]) { _starving[k] = false; continue; }
                _wantedAt[k] = Time.time;
                if (!_proxy[k]) TrySpawnProxy(k, force: true);
            }
        }

        // ───────────────────────────────────────────────────────────────── food and the food web

        /// <summary>
        /// The bites the core asked for (hungry agents in this tick's slice): the nearest edible FLORA prism within
        /// BiteRadius is consumed into a mouth posed at the agent and its volume is paid into the agent's body 1:1.
        /// </summary>
        void Feed(SubstrateTickJob job)
        {
            var reqs = job.EatRequests;
            if (reqs.Count == 0) return;
            var index = PrismSpatialIndex.EnsureInstance();
            if (index == null || !index.IsAvailable) return;
            int bitten = 0;
            for (int t = 0; t < reqs.Count && bitten < species.MaxBitesPerTick; t++)
            {
                _biteCursor = (_biteCursor + 1) % reqs.Count;
                int i = reqs[_biteCursor];
                if (!Mine(i) || _gone[i - _start] || !job.Instances[i].Alive) continue;
                bitten++;
                var b = job.PoseAt(i, 1f);
                var at = new Vector3(b.X, b.Y, b.Z);
                int found = index.QuerySphere(at, species.BiteRadius, FeedScratch);
                for (int q = 0; q < found; q++)
                {
                    var prism = FeedScratch[q];
                    if (!IsFood(prism)) continue;
                    float volume = Mathf.Max(0.001f, prism.Volume);
                    prism.Consume(MouthFor(i - _start, at), domain, _eaterName, false, true);
                    job.QueueFeed(i, volume);
                    break;
                }
            }
        }

        /// <summary>The diet: living flora mass only, through the platform's one edibility rule and the shielded-mass rule.</summary>
        bool IsFood(Prism prism)
        {
            if (!prism || prism.destroyed || prism is not HealthPrism hp) return false;
            if (hp.ResolveOwnerFauna() != null) return false;
            if (hp.LifeForm is not Flora plant || !plant) return false;
            if (IsShieldedMass(prism)) return false;
            return IsPreyForMe(prism.transform.position, prism.Domain, domain);
        }

        /// <summary>
        /// The FOOD WEB: a hunter of this population caught a prey agent (the core found it within reach). The prey dies
        /// through its own proxy - <see cref="Fauna.Predated"/>, so its crystal drops and its body is suctioned into the
        /// hunter's mouth - and its stock becomes the hunter's body. Mass moves; it never vanishes.
        /// </summary>
        void Hunt(SubstrateCellHost host)
        {
            var reqs = host.Job.PreyRequests;
            int done = 0;
            for (int q = 0; q < reqs.Count && done < PreyPerTick; q++)
            {
                var r = reqs[q];
                if (!Mine(r.Predator) || _gone[r.Predator - _start] || !host.Job.Instances[r.Predator].Alive) continue;
                var owner = host.OwnerOfSlot(r.Prey);
                if (!owner) continue;
                var preyBody = host.Job.Body[r.Prey];
                float stock = preyBody.X * preyBody.Y * preyBody.Z;
                var prey = owner.MaterialiseForHit(r.Prey, force: true);
                if (!prey) continue;
                var p = host.Job.PoseAt(r.Predator, host.Alpha);
                if (!prey.Predated(_eaterName, MouthFor(r.Predator - _start, new Vector3(p.X, p.Y, p.Z)))) continue;
                host.Job.QueueFeed(r.Predator, stock);
                done++;
            }
        }

        Transform MouthFor(int k, Vector3 at)
        {
            _mouthCursor = (_mouthCursor + 1) % _mouth.Length;
            _mouthSlot[_mouthCursor] = k;
            _mouthUntil[_mouthCursor] = Time.time + 2f;   // longer than a consume suction
            _mouth[_mouthCursor].position = at;
            return _mouth[_mouthCursor];
        }

        // ───────────────────────────────────────────────────────────────── volume, extinction, colour

        /// <summary>
        /// The population's fauna BODY volume that has no registered prism (Docs/SWARM_FAUNA.md §16.3's ledger): the
        /// worker summed every living agent's stock; the main thread removes agents that died since the tick started
        /// (their skeleton is real mass now) and agents whose proxy body has finished creation (already in the cell's
        /// own sum). Nothing counts twice and nothing is missed.
        /// </summary>
        void StateVirtualVolume(SubstrateTickJob job)
        {
            var host = HostCell;
            if (!host) return;
            double v = job.PopVolume[_pop];
            for (int q = 0; q < _goneSlots.Count; q++)
            {
                int i = _start + _goneSlots[q];
                if (job.Instances[i].Alive) { var b = job.Body[i]; v -= b.X * b.Y * b.Z; }
            }
            for (int q = 0; q < _proxySlots.Count; q++)
            {
                int k = _proxySlots[q], i = _start + k;
                var m = _proxy[k];
                if (_gone[k] || !m || !m.Body || !m.Body.IsCreationComplete || !job.Instances[i].Alive) continue;
                var b = job.Body[i];
                v -= b.X * b.Y * b.Z;
            }
            System.Array.Clear(_virtualBySlot, 0, 4);
            _virtualBySlot[Cell.VolumeSlotOf(domain)] = System.Math.Max(0.0, v);
            host.SetVirtualVolume(this, _virtualBySlot);
        }

        void Extinction(SubstrateTickJob job)
        {
            if (job.PopAlive[_pop] > 0 || _proxySlots.Count > 0) { _extinctSince = -1f; return; }
            if (_extinctSince < 0f) { _extinctSince = Time.time; return; }
            if (Time.time - _extinctSince < species.ExtinctLingerSeconds) return;
            // heartless and bodiless: removing the anchor pops nothing, and the seeder may hatch a new population
            if (DespawnOrDestroy()) return;
            Destroy(gameObject);
        }

        /// <summary>The cell re-coloured its fauna (Cell.SetModeControlOverride): the palette, every proxy and every
        /// virtual entry take the new domain, and every agent born from now on wears it.</summary>
        protected override void OnTeamChanged()
        {
            if (_pop < 0) return;
            if (_gpu) _render.SetPalette(species.Theme, domain, species.HeartWorldScaleByElement);
            for (int q = 0; q < _proxySlots.Count; q++)
            {
                var m = _proxy[_proxySlots[q]];
                if (m) m.SetTeam(domain);
            }
            _reregister = true;
        }

        System.Numerics.Vector3 ToSim(Vector3 world)
        {
            var d = world - (_host != null ? _host.Centre : (HostCell ? HostCell.transform.position : Vector3.zero));
            return new System.Numerics.Vector3(d.x, d.y, d.z);
        }
    }
}
