using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.ECS;
using CosmicShore.Utility;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;
using SVector3 = System.Numerics.Vector3;

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
    /// ONE prism system (Docs/SWARM_FAUNA.md §19, round 11a): an agent's BODY is an ordinary
    /// <see cref="PrismSpatialIndex"/> VIRTUAL entry - kept by the swarm's own ledger (<see cref="SwarmEntryLedger"/>) at
    /// a volume that IS the agent's stock and bound to the cell's volume sum (<see cref="Cell.BindVirtualMass"/>) - so every
    /// prism query, weapon, AOE pass, predator (<see cref="VirtualFauna"/>, through <see cref="IVirtualFaunaOwner"/>) and the
    /// phase ladder see it exactly as they see any fauna body; and, when <see cref="PrismRenderService"/> is on, the body is
    /// drawn as an ordinary prism entity in its tier's material (the member shader then draws only the hearts).
    ///
    /// Once per substrate tick (<see cref="OnTickPublished"/>, main thread, called by the cell's
    /// <see cref="SubstrateCellHost"/>) it: takes its slice of the frame; keeps its index entries (registered at birth,
    /// suspended while a proxy's real body stands in, released at death); gives engaged agents proxies and keeps their
    /// tier (a striking agent is a DANGER prism - contact burns an opposing pilot's petals); lands the bites the core
    /// asked for (flora prisms consumed into a mouth, the volume paid into the agent's body 1:1); resolves its predators'
    /// catches (the prey dies through its own proxy, crystal and all, and its body becomes the predator's); and sheds the
    /// agents whose stomach ran out (through a proxy, leaving the body as a skeleton).
    ///
    /// Laws (Docs/claude/ECOSYSTEM_DESIGN_PRINCIPLES.md): mass is conserved (an agent's body volume IS its stock; eating
    /// adds, a birth splits, a death leaves it); no imposed death and no timers (starvation is a stomach); nothing pops
    /// (births bloom in the shader, proxies appear and leave under an unchanged picture, deaths wither through the
    /// platform); every agent drops one crystal (only through a proxy - an agent with no proxy cannot die).
    /// </summary>
    public class SubstrateFauna : Fauna, IVirtualFaunaOwner, IVirtualPrismBudget, ISwarmEntrySink, IMacroPopulation
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

        // the population's slice of the frame, and its spatial-index entries (round 11a: one prism system)
        SwarmInstance[] _local, _ledgerInst;
        SVector3[] _points;
        bool[] _realBody;
        SwarmEntryLedger _entries;
        PrismSpatialIndex _index;
        NativeArray<int> _entryIds;
        NativeArray<float3> _pointsNative;
        float _heartReach;

        // bodies as PrismRenderService entities (when the service is on) - the swarm's §19.2 path over this slice
        bool _unified;
        SwarmEntityLedger _entities;
        Mesh _bodyMesh;
        readonly Material[] _looks = new Material[9];
        byte[] _look;
        NativeArray<PrismRenderHandle> _handles, _shownHandles, _restyleHandles;
        NativeArray<byte> _lookScratch;
        NativeArray<float4x4> _matrices;
        float[] _matrixScratch;
        static bool s_warnedLooks;

        Transform[] _mouth;
        int[] _mouthSlot;
        float[] _mouthUntil;
        int _mouthCursor, _biteCursor;
        float _extinctSince = -1f;

        // round 11f-2 (Docs/ECOLOGY_LOD.md §6.1): the population as an IMacroPopulation - frozen in the shared core
        CellEcologyLod _lod;
        bool _collapsed;
        SVector3 _macroCentre;
        float _macroExtent, _reserveS = float.PositiveInfinity;
        double _reserveVol;

        static int s_spawnFrame = -1, s_spawnsThisFrame, s_hitFrame = -1, s_hitsThisFrame;

        static readonly ProfilerMarker s_mVirtual = new("SubstrateFauna.Tick.Index");
        static readonly ProfilerMarker s_mEntities = new("SubstrateFauna.Tick.Entities");
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
            // this species' own engagement: the host's tick settings are the FIRST-joined species' (QA-SWARM-ROUND11-9)
            block.EngageRadius = species.EngageRadius;
            block.MaxEngaged = species.MaxProxies;
            _pop = pop; _start = block.Start; _cap = block.Cap;
            _proxy = new SubstrateAgentFauna[_cap];
            _wantedAt = new float[_cap];
            _gone = new bool[_cap];
            _starving = new bool[_cap];
            _local = new SwarmInstance[_cap];
            _ledgerInst = new SwarmInstance[_cap];
            _points = new SVector3[_cap];
            _realBody = new bool[_cap];
            _entries = new SwarmEntryLedger(_cap);
            _index = PrismSpatialIndex.EnsureInstance();
            _entryIds = new NativeArray<int>(_cap, Allocator.Persistent);
            _pointsNative = new NativeArray<float3>(_cap, Allocator.Persistent);
            Seed(host, pop);

            _render = new SubstrateMemberRenderer(species.MemberShader, species.AgentPrefab, _cap, _element, gameObject.layer);
            _gpu = _render.Valid;
            if (_gpu) _render.SetPalette(species.Theme, domain, species.HeartWorldScaleByElement);
            _unified = BindBodyEntities();
            if (_render != null) _render.DrawBodies = !_unified;
            BuildMouths();
            VirtualFauna.Register(this);
            ReadMacroState(host);
            if (species.MacroLod)
            {
                _lod = CellEcologyLod.For(HostCell);
                _lod?.Register(this);
            }
            CSDebug.LogVerbose(CSLogChannel.Ecology,
                $"[Substrate] {name}: {species.SpeciesName} population {pop} seeded ({host.Core.Pops[pop].Alive} agents, slots " +
                $"{_start}..{_start + _cap - 1}, band {inner:F0}-{outer:F0}, bodies {(_unified ? "prism entities" : _gpu ? "instanced" : "proxy-only")})");
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
            _lod?.Unregister(this);
            _lod = null;
            VirtualFauna.Unregister(this);
            if (_entries != null && _index != null && _index.IsAvailable) _entries.ReleaseAll(this);
            if (_entryIds.IsCreated) _entryIds.Dispose();
            if (_pointsNative.IsCreated) _pointsNative.Dispose();
            ReleaseBodyEntities();
            _render?.Dispose();
            _render = null;
            base.OnDestroy();
        }

        // ───────────────────────────────────────────────────────────────── the frame

        void Update()
        {
            if (_host == null) return;
            _lod?.Advance();   // the cell's ecology LOD: may thaw (a pilot approaches) or freeze this population
            _host.Advance();
            if (_pop < 0) return;
            float alpha = _host.Alpha;
            using (s_mPose.Auto()) { PoseProxies(alpha); if (_unified) PoseBodies(alpha); }
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
            job.Slice(_start, _cap, _local, _ledgerInst, _points);
            if (_gpu) _render.Upload(job, _start, _pop);
            ReadEvents(job);
            using (s_mProxies.Auto()) { SyncProxies(job); ShedStarving(job); }
            using (s_mVirtual.Auto()) SyncIndex(job);
            using (s_mEntities.Auto()) SyncEntities(job);
            using (s_mFeed.Auto()) { Feed(job); Hunt(host); }
            Extinction(job);
            ReadMacroState(host);
        }

        // ───────────────────────────────────────────────────────────────── the ecology LOD (round 11f-2, ECOLOGY_LOD §6.1)

        /// <summary>Between ticks (the core is the main thread's): where the population is, how far it spreads, and how
        /// long its hungriest agent can wait - what the director and the thaw rule read until the next tick.</summary>
        void ReadMacroState(SubstrateCellHost host)
        {
            var c = SVector3.Zero;
            int n = 0;
            for (int k = 0; k < _cap; k++)
                if (_local[k].Alive) { c += _local[k].CurPos; n++; }
            c = n > 0 ? c / n : new SVector3(transform.position.x, transform.position.y, transform.position.z);
            float ext = 0f;
            for (int k = 0; k < _cap; k++)
                if (_local[k].Alive) ext = Mathf.Max(ext, SVector3.Distance(c, _local[k].CurPos));
            _macroCentre = c;
            _macroExtent = ext + Mathf.Max(0f, species.EngageRadius * 0.25f);
            _reserveS = host.Core.ReserveSeconds(_pop);
            _reserveVol = host.Core.ReserveVolume(_pop);
        }

        bool AnyStarving()
        {
            for (int k = 0; k < _cap; k++) if (_starving[k]) return true;
            return false;
        }

        SVector3 IMacroPopulation.MacroCentre => _macroCentre;
        float IMacroPopulation.MacroExtent => _macroExtent;
        bool IMacroPopulation.IsCollapsed => _collapsed;

        /// <summary>Nothing only individuals can resolve: no proxy, no pending kill, no starving agent, nobody engaged,
        /// and twice the thaw margin of reserve (the hysteresis that keeps it from freezing just to thaw).</summary>
        bool IMacroPopulation.CanCollapse =>
            _host != null && _pop >= 0 && !_collapsed && _host.Job.Error == null && _proxySlots.Count == 0 && _goneSlots.Count == 0
            && _host.Job.EngagedCount[_pop] == 0 && !AnyStarving() && _reserveS > 2f * species.ThawReserveSeconds;

        bool IMacroPopulation.NeedsIndividuals =>
            _collapsed && (_proxySlots.Count > 0 || _goneSlots.Count > 0 || AnyStarving() || _reserveS < species.ThawReserveSeconds);

        MacroPopulationTotals IMacroPopulation.Totals => _host == null || _pop < 0 ? default : new MacroPopulationTotals
        {
            Individuals = _host.Job.PopAlive[_pop], BodyVolume = _host.Job.PopVolume[_pop], Stomach = _reserveVol,
        };

        /// <summary>Freezes the population at the next tick boundary (its agents hold still where they are drawn).</summary>
        bool IMacroPopulation.Collapse()
        {
            if (!((IMacroPopulation)this).CanCollapse) return false;
            _collapsed = true;
            _host.Freeze(_pop, true);
            return true;
        }

        void IMacroPopulation.Expand() => Thaw();

        void Thaw()
        {
            if (!_collapsed) return;
            _collapsed = false;
            _host?.Freeze(_pop, false);
        }

        /// <summary>Nothing to do at the macro cadence: the frozen core still burns each agent's metabolism every tick, and
        /// a band population has no goal to drift toward (the anchor does not move), so it holds where it is.</summary>
        void IMacroPopulation.MacroTick(float dt) { }

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

        // ───────────────────────────────────────────────────────────────── the index entries (round 11a, one prism system)

        /// <summary>
        /// Once per tick: every living agent is seen by <see cref="PrismSpatialIndex"/> EXACTLY ONCE - by its virtual
        /// entry (at its body centre, volume = its stock, bound to the cell's volume sum in this population's domain), or,
        /// while its proxy's body prism has finished creation, by that real prism with the entry suspended - and a dead
        /// agent not at all. The swarm's ledger does the bookkeeping (count-once: swarm harness R11b, substrate harness X).
        /// </summary>
        void SyncIndex(SubstrateTickJob job)
        {
            if (_index == null || !_index.IsAvailable) _index = PrismSpatialIndex.EnsureInstance();
            if (_index == null || !_index.IsAvailable) return;
            float reach = 0f;
            for (int k = 0; k < _cap; k++)
            {
                var m = _proxy[k];
                _realBody[k] = m && !m.IsDead && !_gone[k] && m.Body && !m.Body.destroyed && m.Body.IsCreationComplete;
                if (!_local[k].Alive) continue;
                // how far the heart can be drawn from its stored body point: the seat plus half a step of travel
                float r = Mathf.Abs(_local[k].PrismZ) + 0.5f * SVector3.Distance(_local[k].PrevPos, _local[k].CurPos);
                if (r > reach) reach = r;
            }
            _heartReach = reach;
            _entryIds.CopyFrom(_entries.Ids);
            _pointsNative.Reinterpret<SVector3>().CopyFrom(_points);
            _index.UpdatePositionsBatch(_entryIds, _pointsNative, _cap);
            _entries.Sync(_ledgerInst, _realBody, _points, this);
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

        /// <summary>Agent k's proxy retired without dying: re-file its entry where it is drawn now, live again.</summary>
        void ResumeEntry(int k)
        {
            if (_entries == null || _index == null || _host == null) return;
            _realBody[k] = false;
            int id = _entries.Ids[k];
            if (id < 0) return;
            var b = _host.Job.BodyAt(_start + k, _host.Alpha);
            _index.UpdatePosition(id, new Vector3(b.X, b.Y, b.Z));
            _entries.Resume(k, this);
        }

        // ISwarmEntrySink - the ledger's calls onto the index's virtual-entry API and the cell's volume binding. The
        // ledger's slot is the agent's index in THIS population's slice; the index is told the core slot.
        int ISwarmEntrySink.Register(int slot, SVector3 point, int domainSlot, float volume, bool shielded, float radius)
        {
            int id = _index.RegisterVirtual(this, _start + slot, new float3(point.X, point.Y, point.Z), (int)domain, volume,
                                            shielded, false, radius);
            var host = HostCell;
            if (id >= 0 && host) host.BindVirtualMass(id, domain);   // volume-only fauna body mass, the cell's own sum
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

        // IVirtualPrismOwner / IVirtualPrismBudget - how the platform makes an agent real

        /// <summary>The index needs agent <paramref name="slot"/> (a core slot) as a real prism NOW - an AOE hit, a round, a
        /// projectile, a predator: its proxy, posed where it is drawn, creation complete. The index suspends the entry.</summary>
        Prism IVirtualPrismOwner.MaterialiseVirtualPrism(int slot)
        {
            var m = MaterialiseForHit(slot, force: true);
            return m ? m.Body : null;
        }

        bool IVirtualPrismBudget.HasMaterialiseBudget(int slot) => HitBudgetLeft();

        static bool HitBudgetLeft()
        {
            int frame = Time.frameCount;
            if (s_hitFrame != frame) { s_hitFrame = frame; s_hitsThisFrame = 0; }
            return s_hitsThisFrame < HitBudgetPerFrame;
        }

        // IVirtualFaunaOwner - what only a creature can answer (VirtualFauna's predators and heart-seeking blasts)

        bool IVirtualFaunaOwner.IsVirtualPrey(int slot, Vector3 at, Fauna predator, bool herbivoresOnly)
        {
            if (_host == null || !Mine(slot) || !predator || predator == this) return false;
            int k = slot - _start;
            if (!_local[k].Alive || _gone[k] || _proxy[k]) return false;
            var prefab = species.AgentPrefab;
            if (herbivoresOnly && prefab && prefab.Diet != FaunaDiet.Herbivore) return false;
            float grace = prefab ? prefab.PredationImmunitySeconds : 0f;
            if (grace > 0f && (_host.Job.Tick + _host.Alpha - _local[k].BirthTick) * _host.Dt < grace) return false;
            return predator.IsInsideBand(at);
        }

        float IVirtualFaunaOwner.HeartReach => _heartReach;

        bool IVirtualFaunaOwner.TryGetVirtualHeart(int slot, out Vector3 heart)
        {
            heart = default;
            if (_host == null || !Mine(slot) || !_local[slot - _start].Alive || _gone[slot - _start]) return false;
            var p = _host.Job.PoseAt(slot, _host.Alpha);
            heart = new Vector3(p.X, p.Y, p.Z);
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

        // ───────────────────────────────────────────────────────────────── bodies as prism entities (§19.2's path)

        /// <summary>
        /// When <see cref="PrismRenderService"/> is on, every living agent's BODY is an ordinary prism entity wearing its
        /// tier's material in the population's domain - the very materials every live prism of that tier wears (Garrett:
        /// one unified prism system). The pose is the member shader's, as arithmetic (<see cref="SwarmBodyPose"/>), and
        /// one Burst transform write a frame moves them all. False (the instanced body draw stays) when the service is off
        /// or a material is missing.
        /// </summary>
        bool BindBodyEntities()
        {
            if (!PrismRenderService.Enabled) return false;
            var bodyPrism = species.AgentPrefab ? species.AgentPrefab.GetComponentInChildren<HealthPrism>(true) : null;
            _bodyMesh = bodyPrism && bodyPrism.TryGetComponent(out MeshFilter mf) ? mf.sharedMesh : null;
            if (!_bodyMesh || !BuildLooks()) return false;
            _entities = new SwarmEntityLedger(_cap);
            _look = new byte[_cap];
            _handles = new NativeArray<PrismRenderHandle>(_cap, Allocator.Persistent);
            _shownHandles = new NativeArray<PrismRenderHandle>(_cap, Allocator.Persistent);
            _restyleHandles = new NativeArray<PrismRenderHandle>(_cap, Allocator.Persistent);
            _lookScratch = new NativeArray<byte>(_cap, Allocator.Persistent);
            _matrices = new NativeArray<float4x4>(_cap, Allocator.Persistent);
            _matrixScratch = new float[16 * _cap];
            return true;
        }

        /// <summary>[tier * 3 + domain slot] = that tier's prism material in this population's domain (one population is
        /// one colour, so the three slots are the same set).</summary>
        bool BuildLooks()
        {
            var theme = species.Theme;
            if (!theme) return false;
            var sets = theme.TeamMaterialSets;
            CosmicShore.ScriptableObjects.SO_MaterialSet set =
                sets != null && sets.TryGetValue(domain, out var painted) && painted ? painted : null;
            if (!set)
            {
                set = theme.BaseMaterialSet;
                if (!s_warnedLooks)
                {
                    s_warnedLooks = true;
                    CSDebug.LogWarning($"[Substrate] {name}: the theme has no painted prism materials for {domain}; agents " +
                                       "wear the unpainted base set (Docs/SUBSTRATE_FAUNA.md §5).");
                }
            }
            if (!set) return false;
            for (int slot = 0; slot < 3; slot++)
            {
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
                for (int k = 0; k < _cap; k++)
                {
                    var h = _handles[k];
                    PrismRenderService.Destroy(ref h);
                }
                _handles.Dispose();
            }
            if (_shownHandles.IsCreated) _shownHandles.Dispose();
            if (_restyleHandles.IsCreated) _restyleHandles.Dispose();
            if (_lookScratch.IsCreated) _lookScratch.Dispose();
            if (_matrices.IsCreated) _matrices.Dispose();
            _entities = null;
            _unified = false;
        }

        void FallBackToInstancedBodies(string why)
        {
            CSDebug.LogWarning($"[Substrate] {name}: agent bodies fall back to the instanced draw - {why}.");
            ReleaseBodyEntities();
            if (_render != null) _render.DrawBodies = true;
        }

        static readonly SVector3 s_up = SVector3.UnitY, s_upAlt = SVector3.UnitZ;

        /// <summary>Once per TICK: entities for slots holding an agent for the first time (one CreateBatch), a restyle where
        /// the tier changed (one SetLooksBatch - a strike turns the body into a danger prism), shows and hides, and the
        /// compact handle list the per-frame transform write walks.</summary>
        void SyncEntities(SubstrateTickJob job)
        {
            if (!_unified || _entities == null) return;
            _entities.Sync(_local);
            var create = _entities.Create;
            if (create.Count > 0)
            {
                int n = create.Count;
                var mats = new NativeArray<float4x4>(n, Allocator.TempJob);
                var outHandles = new NativeArray<PrismRenderHandle>(n, Allocator.TempJob);
                for (int q = 0; q < n; q++)
                {
                    SwarmBodyPose.Matrix(_local[create[q]], 0f, job.Tick, BloomTicks, s_up, s_upAlt, _matrixScratch, 0);
                    mats[q] = ToFloat4x4(_matrixScratch, 0);
                }
                bool ok = PrismRenderService.CreateBatch(_bodyMesh, _looks[0], gameObject.layer, mats, outHandles);
                if (ok)
                    for (int q = 0; q < n; q++) { _handles[create[q]] = outHandles[q]; _look[create[q]] = byte.MaxValue; }
                mats.Dispose();
                outHandles.Dispose();
                _entities.Created(ok);
                if (!ok) { FallBackToInstancedBodies("PrismRenderService.CreateBatch declined"); return; }
            }
            int r = 0;
            for (int q = 0; q < _entities.ShownCount; q++)
            {
                int k = _entities.Shown[q];
                byte look = (byte)(Mathf.Clamp(_local[k].Tier, 0, 2) * 3);
                if (_look[k] == look) continue;
                _look[k] = look;
                _restyleHandles[r] = _handles[k];
                _lookScratch[r] = look;
                r++;
            }
            if (r > 0) PrismRenderService.SetLooksBatch(_restyleHandles, _lookScratch, r, _looks);
            for (int q = 0; q < _entities.Show.Count; q++) PrismRenderService.QueueVisible(_handles[_entities.Show[q]], true);
            for (int q = 0; q < _entities.Hide.Count; q++) PrismRenderService.QueueVisible(_handles[_entities.Hide[q]], false);
            for (int q = 0; q < _entities.ShownCount; q++) _shownHandles[q] = _handles[_entities.Shown[q]];
        }

        /// <summary>Once per FRAME: every shown agent's body matrix at this frame's alpha, one memcpy, one Burst write.</summary>
        void PoseBodies(float alpha)
        {
            int n = _entities != null ? _entities.ShownCount : 0;
            if (n == 0) return;
            SwarmBodyPose.Matrices(_local, _entities.Shown, n, alpha, _host.Job.Tick + alpha, BloomTicks, s_up, s_upAlt,
                                   _matrixScratch);
            NativeArray<float>.Copy(_matrixScratch, 0, _matrices.Reinterpret<float>(64), 0, 16 * n);
            PrismRenderService.SetTransformsBatch(_shownHandles, _matrices, n);
        }

        static float4x4 ToFloat4x4(float[] m, int o) => new float4x4(
            new float4(m[o + 0], m[o + 1], m[o + 2], m[o + 3]), new float4(m[o + 4], m[o + 5], m[o + 6], m[o + 7]),
            new float4(m[o + 8], m[o + 9], m[o + 10], m[o + 11]), new float4(m[o + 12], m[o + 13], m[o + 14], m[o + 15]));

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
                    ResumeEntry(k);
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
            // its entry goes now - the skeleton or the eater holds its mass
            if (_index != null && _index.IsAvailable) _entries.Release(k, this);
            _realBody[k] = false;
            _local[k].Flags = 0u;
            if (_unified && _entities.HideNow(k)) PrismRenderService.QueueVisible(_handles[k], false);
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
            Thaw();   // a hit or a hunt is resolved by individuals (ECOLOGY_LOD §4 clause 3)
            int k = i - _start;
            var job = _host.Job;
            var existing = _proxy[k];
            if (existing)
            {
                _wantedAt[k] = Time.time;
                if (existing.IsDead || !existing.MaterialiseNow()) return null;
                SuspendEntry(k);
                return existing;
            }
            if (_gone[k] || !job.Instances[i].Alive) return null;
            if (!HitBudgetLeft() && !force) return null;
            if (!TrySpawnProxy(k, force: true)) return null;
            s_hitsThisFrame++;
            var m = _proxy[k];
            _wantedAt[k] = Time.time;
            var p = job.PoseAt(i, _host.Alpha);
            m.transform.SetPositionAndRotation(new Vector3(p.X, p.Y, p.Z), Face(job.FaceAt(i, _host.Alpha)));
            if (!m.MaterialiseNow()) return null;
            m.SetDanger(job.Instances[i].Tier == 1);
            m.SyncBodyToIndex();
            SuspendEntry(k);   // its real body is the agent's one entry now
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
        /// index entry (and its cell volume binding) take the new domain, and every agent born from now on wears it.</summary>
        protected override void OnTeamChanged()
        {
            if (_pop < 0) return;
            if (_gpu) _render.SetPalette(species.Theme, domain, species.HeartWorldScaleByElement);
            if (_unified && BuildLooks()) for (int k = 0; k < _cap; k++) _look[k] = byte.MaxValue;   // restyle all next tick
            for (int q = 0; q < _proxySlots.Count; q++)
            {
                var m = _proxy[_proxySlots[q]];
                if (m) m.SetTeam(domain);
            }
            if (_entries != null && _index != null && _index.IsAvailable) _entries.RestateDomains(this);
        }

        System.Numerics.Vector3 ToSim(Vector3 world)
        {
            var d = world - (_host != null ? _host.Centre : (HostCell ? HostCell.transform.position : Vector3.zero));
            return new System.Numerics.Vector3(d.x, d.y, d.z);
        }
    }
}
