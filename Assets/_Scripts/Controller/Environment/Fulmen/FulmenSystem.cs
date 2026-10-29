using System;
using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.Utility;
using Unity.Profiling;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// FULMEN, everyone's (the Charge fundamental, <c>Fulmen/README.md</c>): grows thunderbolts (<see cref="FulmenMath"/>),
    /// burns the prisms in their path (<see cref="PrismBurnManager"/>, on the authority only) and chains a smaller bolt
    /// from every omni crystal a branch strikes. Vessel-agnostic: a caller hands it a <see cref="FulmenConfigSO"/>, a
    /// centre, a range and a seed (<see cref="Strike"/>); nothing here knows which vessel asked.
    ///
    /// <para><b>Perf contract.</b> Bolts, chains and the child queue are fixed pools made once; each bolt keeps its own
    /// segment buffer, grown only when a config asks for more. A bolt's segments are walked in order of
    /// <see cref="FulmenMath.Segment.EndDistance"/> as its front passes them, so each segment is queried against
    /// <see cref="PrismSpatialIndex.QuerySegment"/> exactly once. Per frame: at most
    /// <see cref="FulmenConfigSO.SegmentQueriesPerFrame"/> segment queries, <see cref="FulmenConfigSO.IgnitesPerFrame"/>
    /// ignites and <see cref="FulmenConfigSO.MaxChildSpawnsPerFrame"/> child bolts; past a budget the work waits a frame.
    /// A chain ignites a prism at most once and strikes a crystal at most once; crystals are never consumed.</para>
    ///
    /// <para><b>Network.</b> Every peer calls <see cref="Strike"/> with the same seed (the crystal effect is broadcast),
    /// so every peer grows the same root bolt. Only a chain struck with <c>authority</c> ignites prisms; the burn
    /// manager's damage replicates as any burn does.</para>
    /// </summary>
    public sealed class FulmenSystem : Singleton<FulmenSystem>
    {
        public const int MaxBolts = 32;
        const int MaxChains = 16;
        const int MaxPending = 32;
        const int MaxCandidates = 64;

        static readonly ProfilerMarker s_mStrike = new("Fulmen.Strike");
        static readonly ProfilerMarker s_mTick = new("Fulmen.Tick");
        static readonly ProfilerMarker s_mBurn = new("Fulmen.Burn");

        /// <summary>One chain reaction: a root bolt and every child it set off.</summary>
        public sealed class Chain
        {
            public bool Active, Authority;
            public string Igniter;
            public Domains Domain;
            public int Children, LiveBolts, Pending;
            public readonly HashSet<int> Struck = new();
            public readonly HashSet<Prism> Burned = new();
        }

        /// <summary>One bolt (pooled). Read by <c>FulmenView</c>.</summary>
        public sealed class Bolt
        {
            public bool Active;
            public FulmenConfigSO Config;
            public Chain Chain;
            public FulmenMath.Segment[] Segments = Array.Empty<FulmenMath.Segment>();
            public float[] Keys = Array.Empty<float>();
            public int[] Order = Array.Empty<int>();
            public int Count, Next, Seed, Generation;
            public Vector3 Origin;
            public float Range, Front, DoneAt, Width;
            public readonly Crystal[] Candidates = new Crystal[MaxCandidates];
            public int CandidateCount;

            /// <summary>1 while growing and holding, falling to 0 over the fade.</summary>
            public float Alpha(float now) => DoneAt < 0f || Config == null
                ? 1f
                : 1f - Mathf.Clamp01((now - DoneAt - Config.HoldSeconds) / Config.FadeSeconds);
        }

        struct PendingChild
        {
            public Chain Chain;
            public FulmenConfigSO Config;
            public Vector3 At;
            public float Range, Width;
            public int Seed, Generation;
        }

        readonly Bolt[] _bolts = new Bolt[MaxBolts];
        readonly Chain[] _chains = new Chain[MaxChains];
        readonly PendingChild[] _pending = new PendingChild[MaxPending];
        int _pendingHead, _pendingCount;
        readonly List<Prism> _hits = new();

        /// <summary>The bolt pool; check <see cref="Bolt.Active"/>.</summary>
        public Bolt[] Bolts => _bolts;
        public int LiveBolts { get; private set; }

        public static FulmenSystem EnsureInstance()
        {
            if (Instance != null) return Instance;
            var go = new GameObject(nameof(FulmenSystem));
            return go.AddComponent<FulmenSystem>();
        }

        public override void Awake()
        {
            base.Awake();
            for (int i = 0; i < MaxBolts; i++) _bolts[i] = new Bolt();
            for (int i = 0; i < MaxChains; i++) _chains[i] = new Chain();
            if (!GetComponent<FulmenView>()) gameObject.AddComponent<FulmenView>();   // RequireComponent adds its mesh parts
        }

        /// <summary>
        /// Sets off a chain: a root bolt of <paramref name="range"/> at <paramref name="centre"/>, grown from
        /// <paramref name="seed"/>. <paramref name="authority"/>: this machine ignites prisms (the server, or offline).
        /// <paramref name="excludeCrystalId"/> (the crystal just collected) never chains. False when the pools are full.
        /// </summary>
        public bool Strike(FulmenConfigSO config, Vector3 centre, float range, int seed, bool authority,
            string igniter, Domains domain, int excludeCrystalId = int.MinValue)
        {
            if (config == null || range <= 0f) return false;
            using var _ = s_mStrike.Auto();
            var chain = FreeChain();
            if (chain == null) return false;
            chain.Active = true;
            chain.Authority = authority;
            chain.Igniter = igniter;
            chain.Domain = domain;
            chain.Children = chain.LiveBolts = chain.Pending = 0;
            chain.Struck.Clear();
            chain.Burned.Clear();
            if (excludeCrystalId != int.MinValue) chain.Struck.Add(excludeCrystalId);
            if (!SpawnBolt(chain, config, centre, range, seed, 0, config.Width))
            {
                chain.Active = false;
                return false;
            }
            return true;
        }

        /// <summary>A child's seed: its parent's mixed with the struck crystal's id (the same on every peer).</summary>
        public static int ChildSeed(int parentSeed, int crystalId)
        {
            unchecked
            {
                uint h = (uint)parentSeed * 0x9E3779B1u ^ (uint)crystalId * 0x85EBCA77u;
                h ^= h >> 15; h *= 0x2C1B3C6Du; h ^= h >> 12; h *= 0x297A2D39u; h ^= h >> 15;
                return (int)h;
            }
        }

        /// <summary>Clears every bolt, chain and queued child (turn end, scene change).</summary>
        public void ResetAll()
        {
            foreach (var b in _bolts) if (b != null) Release(b);
            foreach (var c in _chains) if (c != null) { c.Active = false; c.Struck.Clear(); c.Burned.Clear(); }
            for (int i = 0; i < MaxPending; i++) _pending[i] = default;
            _pendingHead = _pendingCount = 0;
            _hits.Clear();
            LiveBolts = 0;
        }

        void OnDestroy() => ResetAll();

        Chain FreeChain()
        {
            foreach (var c in _chains) if (!c.Active) return c;
            return null;
        }

        Bolt FreeBolt()
        {
            foreach (var b in _bolts) if (!b.Active) return b;
            return null;
        }

        bool SpawnBolt(Chain chain, FulmenConfigSO cfg, Vector3 origin, float range, int seed, int generation, float width)
        {
            var b = FreeBolt();
            if (b == null) return false;

            int need = Mathf.Max(1, cfg.MaxSegments);
            if (b.Segments.Length < need)
            {
                b.Segments = new FulmenMath.Segment[need];   // grows once per larger config, then reused
                b.Keys = new float[need];
                b.Order = new int[need];
            }
            var shape = cfg.Shape(range);
            b.Count = FulmenMath.Generate(in shape, seed, origin, b.Segments);
            if (b.Count == 0) return false;
            for (int i = 0; i < b.Count; i++) { b.Keys[i] = b.Segments[i].EndDistance; b.Order[i] = i; }
            Array.Sort(b.Keys, b.Order, 0, b.Count);

            b.Active = true;
            b.Config = cfg;
            b.Chain = chain;
            b.Origin = origin;
            b.Range = range;
            b.Seed = seed;
            b.Generation = generation;
            b.Width = width;
            b.Front = 0f;
            b.Next = 0;
            b.DoneAt = -1f;

            // crystals this bolt can reach at all, gathered once (the per-segment test walks only these)
            b.CandidateCount = 0;
            if (generation < cfg.MaxChainDepth && chain.Children < cfg.MaxChildren)
            {
                float reach = range + cfg.CrystalStrikeRadius;
                float reachSq = reach * reach;
                var live = Crystal.Active;
                for (int i = 0; i < live.Count && b.CandidateCount < MaxCandidates; i++)
                {
                    var c = live[i];
                    if (c == null || !c.isActiveAndEnabled || c.IsExploding) continue;
                    if (cfg.OmniCrystalsOnly && c.crystalProperties.Element != Element.Omni) continue;
                    if (chain.Struck.Contains(c.Id)) continue;
                    if ((c.transform.position - origin).sqrMagnitude > reachSq) continue;
                    b.Candidates[b.CandidateCount++] = c;
                }
            }

            chain.LiveBolts++;
            LiveBolts++;
            return true;
        }

        void Release(Bolt b)
        {
            if (!b.Active) return;
            b.Active = false;
            if (b.Chain != null)
            {
                b.Chain.LiveBolts--;
                if (b.Chain.LiveBolts <= 0 && b.Chain.Pending <= 0)
                {
                    b.Chain.Active = false;
                    b.Chain.Struck.Clear();
                    b.Chain.Burned.Clear();
                }
            }
            for (int i = 0; i < b.CandidateCount; i++) b.Candidates[i] = null;
            b.CandidateCount = 0;
            b.Chain = null;
            b.Config = null;
            LiveBolts--;
        }

        void Update()
        {
            if (LiveBolts == 0 && _pendingCount == 0) return;
            using var _ = s_mTick.Auto();
            float now = Time.time, dt = Time.deltaTime;
            int queries = 0, ignites = 0, spawns = 0;

            // queued children, a few a frame
            while (_pendingCount > 0)
            {
                ref var p = ref _pending[_pendingHead];
                if (p.Config != null && spawns >= p.Config.MaxChildSpawnsPerFrame) break;
                var pc = p;
                p = default;
                _pendingHead = (_pendingHead + 1) % MaxPending;
                _pendingCount--;
                if (pc.Chain == null || pc.Config == null) continue;
                pc.Chain.Pending--;
                SpawnBolt(pc.Chain, pc.Config, pc.At, pc.Range, pc.Seed, pc.Generation, pc.Width);
                spawns++;
                if (pc.Chain.LiveBolts <= 0 && pc.Chain.Pending <= 0) pc.Chain.Active = false;
            }

            var index = PrismSpatialIndex.Instance;
            PrismBurnManager burn = null;

            for (int bi = 0; bi < MaxBolts; bi++)
            {
                var b = _bolts[bi];
                if (!b.Active) continue;
                var cfg = b.Config;
                b.Front += cfg.GrowthSpeed * dt;

                while (b.Next < b.Count && b.Keys[b.Next] <= b.Front && queries < cfg.SegmentQueriesPerFrame)
                {
                    var seg = b.Segments[b.Order[b.Next]];
                    b.Next++;
                    queries++;

                    if (b.Chain.Authority && index != null && ignites < cfg.IgnitesPerFrame)
                    {
                        if (burn == null) burn = PrismBurnManager.EnsureInstance();
                        ignites += BurnAlong(index, burn, b.Chain, cfg, seg, cfg.IgnitesPerFrame - ignites);
                    }
                    if (b.CandidateCount > 0) StrikeCrystals(b, seg);
                }

                if (b.Next >= b.Count && b.DoneAt < 0f) b.DoneAt = now;
                if (b.DoneAt >= 0f && now >= b.DoneAt + cfg.HoldSeconds + cfg.FadeSeconds) Release(b);
            }
        }

        int BurnAlong(PrismSpatialIndex index, PrismBurnManager burn, Chain chain, FulmenConfigSO cfg,
            in FulmenMath.Segment seg, int budget)
        {
            using var _ = s_mBurn.Auto();
            index.QuerySegment(seg.Start, seg.End, cfg.BurnRadius, _hits);
            int lit = 0;
            for (int i = 0; i < _hits.Count && lit < budget; i++)
            {
                var prism = _hits[i];
                if (prism == null || prism.destroyed || prism.Domain == chain.Domain) continue;
                if (!chain.Burned.Add(prism)) continue;
                burn.Ignite(prism, chain.Igniter, chain.Domain, cfg.ConvertOnBurnout);
                lit++;
            }
            _hits.Clear();
            return lit;
        }

        void StrikeCrystals(Bolt b, in FulmenMath.Segment seg)
        {
            var cfg = b.Config;
            var chain = b.Chain;
            float rSq = cfg.CrystalStrikeRadius * cfg.CrystalStrikeRadius;
            for (int i = 0; i < b.CandidateCount; i++)
            {
                var c = b.Candidates[i];
                if (c == null) continue;
                if (!c.isActiveAndEnabled) { b.Candidates[i] = null; continue; }
                var at = c.transform.position;
                if (DistanceToSegmentSq(at, seg.Start, seg.End) > rSq) continue;
                b.Candidates[i] = null;
                if (!chain.Struck.Add(c.Id)) continue;   // struck once per chain; never consumed
                if (b.Generation >= cfg.MaxChainDepth || chain.Children >= cfg.MaxChildren) continue;
                if (_pendingCount >= MaxPending) continue;
                chain.Children++;
                chain.Pending++;
                _pending[(_pendingHead + _pendingCount) % MaxPending] = new PendingChild
                {
                    Chain = chain, Config = cfg, At = at, Range = b.Range * cfg.ChildRangeScale,
                    Width = b.Width * cfg.ChildRangeScale, Seed = ChildSeed(b.Seed, c.Id), Generation = b.Generation + 1,
                };
                _pendingCount++;
            }
        }

        static float DistanceToSegmentSq(Vector3 p, Vector3 a, Vector3 b)
        {
            var ab = b - a;
            float len = ab.sqrMagnitude;
            float t = len > 1e-8f ? Mathf.Clamp01(Vector3.Dot(p - a, ab) / len) : 0f;
            return (a + ab * t - p).sqrMagnitude;
        }
    }
}
