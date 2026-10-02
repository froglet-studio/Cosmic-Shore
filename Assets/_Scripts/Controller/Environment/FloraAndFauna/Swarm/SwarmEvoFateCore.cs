// The swarm fauna's FOURTH simulation core - the research model `evofate` (THE EVOLVED RULE, GIVEN A
// FATE: Tools/NCA/evofate_model.py over evo_model.py + swarm_nca.SwarmRule + sort_model.PlanCode, on
// cece/gifted-curie-x2cpd0; its config results/evofate/params.json, the published C2), ported to plain
// C# over a struct-of-arrays. Same shape as the other three cores: no UnityEngine, the SAME file compiles
// and RUNS headless (Tools/Build/swarm_core_harness), the glue (SwarmFauna) converts at the boundary.
// Docs/SWARM_FAUNA.md §11 is the design record.
//
// What moves a tadpole is LEARNED. Every step half the tadpoles "fire" (G2's 0.5 fire rate) and run the
// trained G2 network: each perceives its neighbours within 8 voxels (own state, the same-domain and
// other-domain neighbour means, the gradients of every channel, two densities, the swarm's headcount and
// element mix - 232 numbers) and a 232 -> 192 -> 192 (+residual) -> 35 MLP writes its 32 state channels
// and proposes its velocity. The weights are the research checkpoint's (results/swarm_coevo_g2/rule.pt,
// exported float32 as results/live/evo_rule.json; the evo genome leaves them unchanged), shipped as a
// TextAsset and read by SwarmEvoRule.Parse.
//
// What SORTS them is designed, and small - sort's FATE: each hatched tadpole commits to one positional-
// information well of its type (the one its type under-occupies) and is pulled up that well's log-density -
//   * only outside a DEAD ZONE (no pull while the well's energy is under E0, full by 2 E0): inside its well
//     a tadpole is pure G2, which is what keeps the swarming texture;
//   * only on the steps its G2 cell FIRES (x 1 / fire rate, same mean pull): pulling on the idle steps
//     reverses the motion every other step (the research's "jerky" signature);
//   * G2 may not push a tadpole AWAY from its well while it is outside the dead zone (the RECTIFIER - the
//     research measured it inert once the two above are on; kept so research mode is evofate exactly);
// plus a weak differential adhesion (sort's Steinberg matrix x0.35) and two Time-only genes (Time runners'
// dead zone x0.3 and G2 speed x0.7). After G2, the visual channels are set to the type's look (the designed
// look calms G2). G2's learned death is suppressed: nothing dies on its own.
//
// Composition is sort's: the joint (element x region) lay homeostat with a headcount cap and cross-laying,
// and MOLTING + region transfer as the corrector. Lossless.
//
// What the GAME adds (all switchable, so the harness can run the research model exactly):
//   * laying is FUNDED out of the stomach, at most LayMax a step, held while wounded (shared rules);
//   * an egg G2 has not hatched by EggLife steps HATCHES rather than vanishing (KeepEggs): a laid egg was
//     paid for with eaten mass, and the research's silent egg loss would be mass leaving the world;
//   * a molt is an ANIMATION of MoltSteps (the sort core's MoltBegan .. MoltDone);
//   * the network perceives in BODY coordinates and its velocity is turned back to the world: the body
//     SWIMS and turns to face its heading, and G2 (trained in a fixed frame) must see the frame it was
//     trained in. The wells turn and travel with it and ride the plan's animation;
//   * swimming in a band, the membrane, and the vessel reaction (SwarmCoreShared.FleeFrom).
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using System.Runtime.InteropServices;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The trained G2 network (swarm_nca.SwarmRule: perceive + 2-layer residual MLP), weights immutable and
    /// shared by every evofate swarm. Parse reads the TextAsset the author script writes from the research
    /// export (results/live/evo_rule.json): base64 little-endian float32, row-major [out, in].
    /// </summary>
    public sealed class SwarmEvoRule
    {
        public const int C = 32, X = C + 5, G = 5, F = X * 3 + X * 3 + 3 + 2 + G;   // 232
        public const int OUT = C + 3;                                               // 35
        public int H = 192;
        public float[] W1 = Array.Empty<float>(), B1 = Array.Empty<float>(), W2 = Array.Empty<float>(), B2 = Array.Empty<float>(),
                       W3 = Array.Empty<float>(), B3 = Array.Empty<float>();
        public float FireRate = 0.5f;

        static readonly Dictionary<string, SwarmEvoRule> s_cache = new();

        /// <summary>Parse once per text (every swarm shares the weights). Throws on a malformed asset - fail loud.</summary>
        public static SwarmEvoRule Parse(string json)
        {
            lock (s_cache)
            {
                if (s_cache.TryGetValue(json, out var r)) return r;
                r = new SwarmEvoRule
                {
                    H = (int)Num(json, "hidden"), FireRate = Num(json, "fire_rate"),
                };
                r.W1 = Floats(json, "w1", r.H * F); r.B1 = Floats(json, "b1", r.H);
                r.W2 = Floats(json, "w2", r.H * r.H); r.B2 = Floats(json, "b2", r.H);
                r.W3 = Floats(json, "w3", OUT * r.H); r.B3 = Floats(json, "b3", OUT);
                s_cache[json] = r;
                return r;
            }
        }

        static float Num(string json, string key)
        {
            int i = json.IndexOf("\"" + key + "\"", StringComparison.Ordinal);
            if (i < 0) throw new FormatException($"SwarmEvoRule: no \"{key}\"");
            i = json.IndexOf(':', i) + 1;
            int j = i; while (j < json.Length && "-+.0123456789eE ".IndexOf(json[j]) >= 0) j++;
            return float.Parse(json.Substring(i, j - i).Trim(), CultureInfo.InvariantCulture);
        }

        static float[] Floats(string json, string key, int n)
        {
            int i = json.IndexOf("\"" + key + "\"", StringComparison.Ordinal);
            if (i < 0) throw new FormatException($"SwarmEvoRule: no \"{key}\"");
            i = json.IndexOf('"', json.IndexOf(':', i)) + 1;
            int j = json.IndexOf('"', i);
            var bytes = Convert.FromBase64String(json.Substring(i, j - i));
            if (bytes.Length != n * 4) throw new FormatException($"SwarmEvoRule: \"{key}\" holds {bytes.Length / 4} floats, expected {n}");
            var f = new float[n];
            Buffer.BlockCopy(bytes, 0, f, 0, bytes.Length);
            if (!BitConverter.IsLittleEndian)
                for (int q = 0; q < n; q++) { var b = BitConverter.GetBytes(f[q]); Array.Reverse(b); f[q] = BitConverter.ToSingle(b, 0); }
            return f;
        }

        /// <summary>out = W3 relu(W2 h1 + b2) + h1 ... exactly swarm_nca.SwarmRule.mlp, for one feature row.
        /// Two scratch spans of H floats. Dot products run SIMD where the runtime accelerates Vector&lt;float&gt;
        /// (CoreCLR) and as 4-way unrolled scalar loops where it does not (Unity's Mono).</summary>
        public void Forward(ReadOnlySpan<float> f, Span<float> h1, Span<float> h2, Span<float> o)
        {
            for (int r = 0; r < H; r++) h1[r] = MathF.Max(0f, Dot(W1.AsSpan(r * F, F), f) + B1[r]);
            for (int r = 0; r < H; r++) h2[r] = MathF.Max(0f, Dot(W2.AsSpan(r * H, H), h1) + B2[r]) + h1[r];
            for (int r = 0; r < OUT; r++) o[r] = Dot(W3.AsSpan(r * H, H), h2) + B3[r];
        }

        static float Dot(ReadOnlySpan<float> a, ReadOnlySpan<float> b)
        {
            int n = a.Length, i = 0; float s = 0f;
            if (Vector.IsHardwareAccelerated && n >= Vector<float>.Count * 2)
            {
                // Reinterpret the spans as vectors rather than constructing each one: Unity's profile of
                // System.Numerics has only the Vector<T>(Span<T>) constructor and no ReadOnlySpan<T> overload
                // (CS1503 there, while CoreCLR compiles it), and MemoryMarshal.Cast takes a ReadOnlySpan on both.
                var va = MemoryMarshal.Cast<float, Vector<float>>(a);
                var vb = MemoryMarshal.Cast<float, Vector<float>>(b.Slice(0, n));
                var acc = Vector<float>.Zero;
                for (int v = 0; v < va.Length; v++) acc += va[v] * vb[v];
                i = va.Length * Vector<float>.Count;
                s = Vector.Dot(acc, Vector<float>.One);
            }
            else
            {
                float s0 = 0, s1 = 0, s2 = 0, s3 = 0;
                for (; i <= n - 4; i += 4) { s0 += a[i] * b[i]; s1 += a[i + 1] * b[i + 1]; s2 += a[i + 2] * b[i + 2]; s3 += a[i + 3] * b[i + 3]; }
                s = (s0 + s1) + (s2 + s3);
            }
            for (; i < n; i++) s += a[i] * b[i];
            return s;
        }
    }

    /// <summary>The evofate model's numbers. Defaults are evofate C2's published values
    /// (Tools/NCA/results/evofate/params.json) and the G2 world, unless marked GAME.</summary>
    public sealed class SwarmEvoFateParams
    {
        // ── the G2 world (results/swarm_coevo_g2/rule.pt) ──
        public float R = 8f, R0 = 2.4f, Rep = 0.4f, Rho0 = 8f;
        public int EggLife = 6;
        public float[] VMax = { 0.8f, 0.8f, 0.8f, 2f };
        public float Membrane = 80f;
        public int Cap = 280;
        // ── fate steering (evofate C2) ──
        public float Pull = 2f, KWell = 0.41f, WellClip = 0.52f, Mix = 1f;
        /// <summary>Dead zone: no pull while the fated well's energy (half the Mahalanobis^2) is under E0, full by 2 E0.</summary>
        public float E0 = 1.5f;
        /// <summary>Time runners: G2 speed x TMix, pull x TP, dead zone x TE0.</summary>
        public float TMix = 0.7f, TP = 1f, TE0 = 0.3f;
        /// <summary>The rectifier: G2 may not move a tadpole AWAY from its well while outside the dead zone (inert in C2).</summary>
        public float Rect = 1f;
        /// <summary>The designed move happens only on the steps the tadpole's G2 cell fires (x 1 / fire rate).</summary>
        public bool Sync = true;
        /// <summary>Differential adhesion scale (sort's matrix below) - 0 = off.</summary>
        public float Adh = 0.35f;
        /// <summary>After G2, the visual channels are set to the type's look (look = 1).</summary>
        public bool Look = true;
        public float RAdh = 5.377880f, ASame = -0.05f, AElem = -0.03736162f, ARole = -0.02701462f, AOther = -0.06368212f, AdhR0 = 2.2476657f;
        // ── the code (sort_model.PlanCode with evofate's K / per_well / cov_scale) ──
        public int K = 12, PerWell = 4;
        public float CovScale = 0.89f;
        // ── composition (sort's) ──
        public int Dwell = 12;
        public float LayRate = 0.084f;
        public int LayMax = 5;
        public float RBud = 2.6f, FillTol = 0.15f, PCross = 0.466f, Over = 0.94f;
        public bool Molt = true, Transfer = true;
        public float MoltRate = 0.03f;
        public int RoleEvery = 10;

        // ── GAME ──
        /// <summary>Keep the research's domain regions (true: only for the harness). The game has one region.</summary>
        public bool DomainSlots = false;
        public bool Funded = true;
        public float[] EggCost = { 1f, 1f, 1f, 1f };
        public float CrossCost = 2f;
        public int KillLayHoldSteps = 0;
        /// <summary>Steps a molt takes (the glue animates it). 0 = instant (research).</summary>
        public int MoltSteps = 0;
        /// <summary>An egg G2 has not hatched by EggLife HATCHES instead of vanishing (it was paid for). False = research.</summary>
        public bool KeepEggs = false;
        /// <summary>Wells ride the plan's animation (game); false = frame 0 only (research).</summary>
        public bool Animate = false;
        public int[] Periods = { 8, 8, 8, 16 };
        /// <summary>Perceive and steer in the body's frame, which turns to the heading. False = the research's fixed frame.</summary>
        public bool Oriented = false;
        public float Cruise = 0f, Turn = 0.03f, AimHold = 1.5f;
        /// <summary>Inside this many body radii of its goal the body is NOT station-kept: it drifts as the network
        /// moves it. That collective drift is part of what makes evofate read alive (its members' world speed is
        /// ~0.1 voxels/step in the research, ~0.05 with the drift cancelled - swarm_feel `stuck` 12% on the
        /// whale, harness E8). Past it the body sidles back.</summary>
        public float HoldFree = 0.75f;
        public float BandInner, BandOuter;
        public float Sense = 2.2f, Relay = 0.8f, StartleDecay = 0.9f, Lookahead = 10f, FleeSwirl = 0.8f, RelayR = 5f;
        public float FleeGain = 2f;
        public float[] Flee = { 0.6f, 0.5f, 1.4f, 2f };
        public float[] Mob = { 0f, 0f, 0f, 1f };
        public float MobSpeed = 1f;
        public float[] Inflate = { 0.45f, 0f, 0f, 0f };
    }

    public sealed class SwarmEvoFateCore : ISwarmCore
    {
        const int C = SwarmEvoRule.C, X = SwarmEvoRule.X, A = 0, DIE = 31;

        public readonly SwarmPlanData[] Plans;
        public readonly SwarmEvoFateParams P;
        public readonly SwarmEvoRule Rule;
        public readonly int Cap;
        readonly SwarmSortParams _codeParams;

        // ── per tadpole ──
        public readonly Vector3[] Pos, Vel, Facing;
        public readonly int[] Elem, Dom, Age, MoltTo, Fate, FKey, XferRole, XferPlan;
        public readonly bool[] Active, Hatched, Fired;
        public readonly float[] Startle, Molt, Energy;
        /// <summary>G2's 32 state channels per member (hatch | facing 3 | prism 3 | tier 3 | spindle 2 | hidden | death).</summary>
        public readonly float[] S;
        readonly Vector3[] _udir; readonly float[] _ugate;

        // ── per swarm ──
        public int PlanIx = -1, Clock, Deaths, EggsLost;
        public float ThreatLevel;
        public Vector3 Anchor, SwimTarget;
        public Vector3 Heading = Vector3.UnitX;
        public Vector3 BX = Vector3.UnitX, BY = Vector3.UnitY, BZ = Vector3.UnitZ;
        public readonly float[] Stomach = new float[4];
        public readonly List<SwarmEvent> Events = new();
        public readonly int[] Perm = { 0, 1, 2 };
        public readonly int[] RoleOfDom = { 0, -1, -1 };
        /// <summary>Wall time of the network this core has run (ms), and how many forward passes - the cost the brief asks for.</summary>
        public double NetMs; public long NetCalls;

        readonly Random _rng;
        int _cand = -1, _candN, _layHoldUntil;
        bool _returning;
        bool _permSet;
        // scratch
        readonly bool[] _live, _keep;
        readonly int[] _type, _liveIx, _members, _occ, _cen = new int[16], _cenM = new int[16], _ec = new int[4], _cnt = new int[4];
        readonly float[] _need, _want = new float[16], _fill = new float[16], _deficit = new float[16];
        readonly int[,] _dcen = new int[4, 3];
        readonly Vector3[] _pb, _v, _push, _move, _flee;
        readonly float[] _st, _feat, _h1, _h2, _out, _ds, _agg;
        readonly float[] _typeState;   // the type look's VIS channels (11 per type), per plan
        // neighbour hash (cell = R)
        const int HG = 4096;
        readonly int[] _nb = new int[27];
        readonly int[] _cellStart = new int[HG], _cellCount = new int[HG], _fill2 = new int[HG];
        readonly int[] _sorted, _cellOf;
        float _hashR;

        public SwarmEvoFateCore(SwarmPlanData[] plansByElement, SwarmEvoFateParams p, SwarmEvoRule rule, int seed)
        {
            Plans = plansByElement; P = p; Rule = rule; Cap = Math.Max(1, p.Cap);
            _codeParams = new SwarmSortParams { K = p.K, PerWell = p.PerWell, CovScale = p.CovScale, DomainSlots = p.DomainSlots };
            _rng = new Random(seed);
            Pos = new Vector3[Cap]; Vel = new Vector3[Cap]; Facing = new Vector3[Cap];
            Elem = new int[Cap]; Dom = new int[Cap]; Age = new int[Cap]; MoltTo = new int[Cap];
            Fate = new int[Cap]; FKey = new int[Cap]; XferRole = new int[Cap]; XferPlan = new int[Cap];
            Active = new bool[Cap]; Hatched = new bool[Cap]; Fired = new bool[Cap];
            Startle = new float[Cap]; Molt = new float[Cap]; Energy = new float[Cap];
            S = new float[Cap * C];
            _udir = new Vector3[Cap]; _ugate = new float[Cap];
            _live = new bool[Cap]; _keep = new bool[Cap];
            _type = new int[Cap]; _liveIx = new int[Cap]; _members = new int[Cap];
            _pb = new Vector3[Cap]; _v = new Vector3[Cap]; _push = new Vector3[Cap]; _move = new Vector3[Cap]; _flee = new Vector3[Cap];
            _st = new float[Cap]; _feat = new float[SwarmEvoRule.F]; _h1 = new float[rule.H]; _h2 = new float[rule.H];
            _out = new float[SwarmEvoRule.OUT]; _ds = new float[Cap * C]; _agg = new float[X * 6 + 8];
            _sorted = new int[Cap]; _cellOf = new int[Cap];
            int maxW = 1;
            foreach (var pl in plansByElement) maxW = Math.Max(maxW, SwarmSortCode.For(pl, _codeParams).W.Length);
            _occ = new int[maxW]; _need = new float[maxW];
            _typeState = new float[SwarmSortCode.NT * 11];
            for (int i = 0; i < Cap; i++) { Facing[i] = Vector3.UnitZ; XferPlan[i] = -1; }
            _hashR = MathF.Max(p.R, MathF.Max(p.RAdh, p.RelayR));
        }

        // ──────────────────────────────────────────────────────────────── ISwarmCore

        public SwarmPlanData Plan => Plans[Math.Max(0, PlanIx)];
        public SwarmSortCode Code => SwarmSortCode.For(Plan, _codeParams);
        int ISwarmCore.Cap => Cap;
        Vector3[] ISwarmCore.Pos => Pos;
        Vector3[] ISwarmCore.Vel => Vel;
        Vector3[] ISwarmCore.Facing => Facing;
        int[] ISwarmCore.Elem => Elem;
        bool[] ISwarmCore.Alive => Active;
        float[] ISwarmCore.Startle => Startle;
        float[] ISwarmCore.Molt => Molt;
        int[] ISwarmCore.MoltTo => MoltTo;
        float[] ISwarmCore.Stomach => Stomach;
        List<SwarmEvent> ISwarmCore.Events => Events;
        int ISwarmCore.Clock => Clock;
        int ISwarmCore.PlanIx => Math.Max(0, PlanIx);
        Vector3 ISwarmCore.Anchor => Anchor;
        Vector3 ISwarmCore.BX => BX;
        Vector3 ISwarmCore.BY => BY;
        Vector3 ISwarmCore.BZ => BZ;
        Vector3 ISwarmCore.SwimTarget { get => SwimTarget; set => SwimTarget = value; }

        public int AliveCount { get { int n = 0; for (int i = 0; i < Cap; i++) if (Active[i]) n++; return n; } }

        /// <summary>A molting member counts as what it is becoming (the sort core's rule).</summary>
        public int EffectiveElement(int i) => Molt[i] > 0f ? MoltTo[i] : Elem[i];

        public int[] Counts(bool eff)
        {
            var c = new int[4];
            for (int i = 0; i < Cap; i++) if (Active[i]) c[eff ? EffectiveElement(i) : Elem[i]]++;
            return c;
        }

        public void Kill(int i)
        {
            if (i < 0 || i >= Cap || !Active[i]) return;
            if (P.KillLayHoldSteps > 0) _layHoldUntil = Math.Max(_layHoldUntil, Clock + P.KillLayHoldSteps);
            Active[i] = false; Hatched[i] = false; Startle[i] = 0; Vel[i] = Vector3.Zero; Molt[i] = 0; Fate[i] = 0; FKey[i] = 0;
            XferPlan[i] = -1; _udir[i] = Vector3.Zero; _ugate[i] = 0;
            Array.Clear(S, i * C, C);
        }

        /// <summary>The prism the member wears: G2's own prism and tier channels, which the designed look keeps
        /// at its type's look (decoded the way swarm_nca.decode reads them).</summary>
        public bool TryGetLook(int i, int element, out Vector3 half, out int tier)
        {
            half = default; tier = 0;
            if (i < 0 || i >= Cap || !Active[i] || !Hatched[i]) return false;
            int o = i * C;
            if (element != Elem[i] || Molt[i] > 0f)
            {
                // a molting member wears its NEW element's type look (G2 has not seen it yet)
                int r = Math.Max(0, EffRole(i)), t = element * 3 + r;
                var code = Code;
                for (int s = 0; s < 3 && !code.HasType[t]; s++) t = element * 3 + s;
                half = code.HasType[t] ? code.TypeHalf[t] : element != 2 ? Vector3.One : new Vector3(2f, 0.3f, 0.3f);
                tier = code.HasType[t] ? code.TypeTier[t] : 0;
            }
            else
            {
                half = SwarmGridCore.PrismH(new Vector3(S[o + 4], S[o + 5], S[o + 6]), element);
                if (element == 0) { float t0 = S[o + 7], t1 = S[o + 8], t2 = S[o + 9]; tier = t2 > t0 && t2 > t1 ? 2 : t1 > t0 ? 1 : 0; }
            }
            half = new Vector3(MathF.Round(half.X * 16f) / 16f, MathF.Round(half.Y * 16f) / 16f, MathF.Round(half.Z * 16f) / 16f);
            return true;
        }

        /// <summary>Starvation (the HOST's decision) sheds a member of the element furthest over its plan count,
        /// the one furthest from its well; else the most misplaced member. Never on a clock.</summary>
        public int StarvationVictim()
        {
            if (PlanIx < 0) return -1;
            var code = Code;
            Array.Clear(_cnt, 0, 4);
            for (int i = 0; i < Cap; i++) if (Active[i] && Hatched[i]) _cnt[EffectiveElement(i)]++;
            int worst = -1; float ws = 0.5f;
            for (int e = 0; e < 4; e++)
            {
                int want = 0; for (int s = 0; s < 3; s++) want += code.Counts[e * 3 + s];
                if (_cnt[e] - want > ws) { ws = _cnt[e] - want; worst = e; }
            }
            int best = -1; float be = float.MinValue;
            for (int i = 0; i < Cap; i++)
            {
                if (!Active[i] || !Hatched[i] || (worst >= 0 && EffectiveElement(i) != worst)) continue;
                if (Energy[i] > be) { be = Energy[i]; best = i; }
            }
            return best;
        }

        // ──────────────────────────────────────────────────────────────── seeding

        public void Seed(int planElement, int count, Vector3 anchor, Vector3 heading)
        {
            var plan = Plans[planElement];
            count = Math.Min(count, Math.Min(Cap, plan.N));
            var em = LargestRemainder(plan.Mix, count, true);
            var dm = P.DomainSlots ? LargestRemainder(plan.SlotMix, count, true) : new[] { count, 0, 0 };
            SeedWith(em, dm, anchor, 2f);
            SetHeading(heading, snap: true);
            SwimTarget = anchor;
        }

        /// <summary>swarm_nca.seed_swarm's shape: hatched tadpoles at the given counts, s[hatch] = 1, every other channel 0.</summary>
        public void SeedWith(int[] elemCounts, int[] domCounts, Vector3 centre, float spread)
        {
            int n = 0; for (int e = 0; e < 4; e++) n += elemCounts[e];
            n = Math.Min(n, Cap);
            var es = new int[n]; var ds = new int[n];
            for (int e = 0, q = 0; e < 4; e++) for (int k = 0; k < elemCounts[e] && q < n; k++) es[q++] = e;
            for (int d = 0, q = 0; d < 3; d++) for (int k = 0; k < domCounts[d] && q < n; k++) ds[q++] = d;
            Shuffle(es); Shuffle(ds);
            for (int i = 0; i < n; i++)
            {
                Active[i] = true; Hatched[i] = true; Elem[i] = es[i]; Dom[i] = ds[i];
                Pos[i] = centre + spread * Gauss3(); Vel[i] = Vector3.Zero; Fate[i] = 0; FKey[i] = 0; XferPlan[i] = -1;
                Array.Clear(S, i * C, C); S[i * C + A] = 1f;
            }
            Anchor = centre; Clock = 0; _permSet = false;
            Array.Clear(_cnt, 0, 4);
            for (int i = 0; i < n; i++) _cnt[es[i]]++;
            PlanIx = ArgMax(_cnt); _cand = PlanIx; _candN = 0;
        }

        static int[] LargestRemainder(int[] weights, int n, bool needNonZero)
        {
            int K = weights.Length; double sum = 0; foreach (var w in weights) sum += w;
            var b = new int[K]; var rem = new double[K];
            for (int k = 0; k < K; k++) { double x = weights[k] / sum * n; b[k] = (int)Math.Floor(x); rem[k] = x - b[k]; }
            if (needNonZero) for (int k = 0; k < K; k++) if (weights[k] > 0 && b[k] == 0) b[k] = 1;
            int s = 0; foreach (var x in b) s += x;
            while (s < n) { int k = 0; for (int q = 1; q < K; q++) if (rem[q] > rem[k]) k = q; b[k]++; rem[k] = -1; s++; }
            while (s > n) { int k = 0; for (int q = 1; q < K; q++) if (b[q] > b[k]) k = q; b[k]--; s--; }
            return b;
        }

        void Shuffle(int[] a) { for (int i = a.Length - 1; i > 0; i--) { int j = _rng.Next(i + 1); (a[i], a[j]) = (a[j], a[i]); } }

        float Gauss()
        {
            double u1 = 1.0 - _rng.NextDouble(), u2 = _rng.NextDouble();
            return (float)(Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2));
        }

        Vector3 Gauss3() => new(Gauss(), Gauss(), Gauss());

        int Poisson(float lambda)
        {
            double L = Math.Exp(-lambda), p = 1; int k = 0;
            do { k++; p *= _rng.NextDouble(); } while (p > L && k < 1000);
            return k - 1;
        }

        static int ArgMax(int[] a) { int b = 0; for (int i = 1; i < a.Length; i++) if (a[i] > a[b]) b = i; return b; }

        // ──────────────────────────────────────────────────────────────── body frame

        public Vector3 Rotate(Vector3 p)
        {
            if (!P.Oriented) return p;
            var plan = Plan;
            var side = Vector3.Cross(plan.SwimAxis, plan.UpAxis);
            return Vector3.Dot(p, plan.SwimAxis) * BX + Vector3.Dot(p, plan.UpAxis) * BY + Vector3.Dot(p, side) * BZ;
        }

        public Vector3 InvRotate(Vector3 w)
        {
            if (!P.Oriented) return w;
            var plan = Plan;
            var side = Vector3.Cross(plan.SwimAxis, plan.UpAxis);
            return Vector3.Dot(w, BX) * plan.SwimAxis + Vector3.Dot(w, BY) * plan.UpAxis + Vector3.Dot(w, BZ) * side;
        }

        public void SetHeading(Vector3 h, bool snap = false)
        {
            float hl = h.Length(); if (hl < 1e-5f) return; h /= hl;
            if (!snap)
            {
                float ang = MathF.Acos(Math.Clamp(Vector3.Dot(Heading, h), -1f, 1f));
                if (ang > P.Turn)
                {
                    var axis = Vector3.Cross(Heading, h); float al = axis.Length();
                    if (al < 1e-5f) axis = Math.Abs(Heading.Y) < 0.9f ? Vector3.Cross(Heading, Vector3.UnitY) : Vector3.Cross(Heading, Vector3.UnitX);
                    axis = Vector3.Normalize(axis);
                    h = Vector3.Normalize(Vector3.Transform(Heading, Quaternion.CreateFromAxisAngle(axis, P.Turn)));
                }
            }
            Heading = h;
            var upRef = MathF.Abs(Vector3.Dot(h, Vector3.UnitY)) < 0.95f ? Vector3.UnitY : (BY.LengthSquared() > 0.5f ? BY : Vector3.UnitX);
            var y = upRef - Vector3.Dot(upRef, h) * h; float yl = y.Length();
            if (yl < 1e-4f) return;
            y /= yl;
            BX = h; BY = y; BZ = Vector3.Cross(BX, BY);
        }

        int PeriodOf(int planIx) => P.Periods != null && planIx >= 0 && planIx < P.Periods.Length && P.Periods[planIx] > 0 ? P.Periods[planIx] : 8;

        Vector3 BandCorrection(Vector3 cen)
        {
            if (P.BandOuter <= 0f) return Vector3.Zero;
            float d = cen.Length();
            if (d < 1e-3f) return Heading * 0.5f;
            float c = Math.Clamp(d, P.BandInner, P.BandOuter);
            return c == d ? Vector3.Zero : (c - d) * 0.2f * cen / d;
        }

        // ──────────────────────────────────────────────────────────────── step (evofate_model.EvoFate.forward)

        public void Step(ReadOnlySpan<SwarmPredator> preds)
        {
            int nLive = 0;
            for (int i = 0; i < Cap; i++) if (Active[i] && Hatched[i]) nLive++;
            if (nLive == 0) { Clock++; return; }

            // ── 1. plan by majority with the dwell, and the domain -> region map (EvoFate._plan_and_roles)
            bool contested = DecidePlan();
            var plan = Plans[PlanIx];
            var code = SwarmSortCode.For(plan, _codeParams);
            if (!_permSet || Clock % P.RoleEvery == 0) PickPerm(code);

            Vector3 cen = Vector3.Zero;
            for (int i = 0; i < Cap; i++) if (Active[i] && Hatched[i]) cen += Pos[i];
            cen /= nLive;
            Anchor = cen;
            if (P.Oriented)
            {
                var toT = SwimTarget - cen; float dT = toT.Length();
                if (dT > P.AimHold * plan.Radius) SetHeading(toT / dT);
            }
            float swell = 1f + P.Inflate[PlanIx] * ThreatLevel;

            // ── 2. the vessel reaction (GAME) on the positions before the move
            BuildHash(Active);
            float stSum = 0;
            for (int i = 0; i < Cap; i++)
            {
                _flee[i] = Vector3.Zero; _st[i] = 0f;
                if (!Active[i] || !Hatched[i]) continue;
                float st = Startle[i] * P.StartleDecay;
                if (preds.Length > 0 || Startle[i] > 1e-3f)
                {
                    float relay = MaxStartleNear(Pos[i], P.RelayR, i);
                    var e = EffectiveElement(i);
                    for (int q = 0; q < preds.Length; q++)
                        _flee[i] += SwarmCoreShared.FleeFrom(preds[q], Pos[i], P.Flee[e], P.Mob[e], P.Sense, P.Lookahead, P.MobSpeed, P.FleeSwirl, ref st);
                    st = MathF.Max(st, relay * P.Relay);
                }
                _st[i] = st; stSum += st;
            }

            // ── 3. G2: learned state + velocity, its learned death suppressed (EvoFate._g2)
            G2Step(nLive);

            // ── 4. fate: commit, pull (dead zone, sync, rectifier), adhesion, the designed look
            FateStep(code, plan, swell);

            // ── the body swims (GAME): every member, eggs included, rides the cruise
            if (P.Oriented && P.Cruise > 0f)
            {
                int per = PeriodOf(PlanIx);
                var toT = SwimTarget - cen; float dT = toT.Length();
                float pace = P.Cruise * Math.Clamp(dT / MathF.Max(1f, 1.5f * plan.Radius), 0f, 1f);
                if (plan.SwimAxis.Y > 0.5f) pace *= 0.4f + 0.6f * MathF.Max(0f, MathF.Sin(Clock * 2f * MathF.PI / (2f * per * 7f)));
                Vector3 swim;
                if (dT > P.AimHold * plan.Radius) swim = pace * MathF.Max(0f, Vector3.Dot(Heading, toT / dT)) * Heading;
                else
                {
                    // LATCHED: a body that drifts past HoldFree radii is brought back to half of it before it drifts
                    // freely again. Unlatched, it parked on the edge and the swim switched on and off every step -
                    // a member's consecutive fired steps reversed (harness E8 probe: osc 0.19).
                    if (dT > P.HoldFree * plan.Radius) _returning = true;
                    else if (dT < 0.5f * P.HoldFree * plan.Radius) _returning = false;
                    swim = _returning && dT > 1e-3f ? pace * toT / dT : Vector3.Zero;
                }
                swim += BandCorrection(cen);
                // The swim obeys the research's own `sync` rule: a member moves on the steps its network FIRES
                // (x 1 / fire rate, same mean pace) and holds still on the others. Drifting it on its idle steps
                // as well made a member that G2 had just moved against the swim back up every other step -
                // swarm_feel `osc` 0.28-0.42 against the research's 0.05, the "jerky" signature sync exists to
                // remove (harness E8). Eggs never fire, so they ride every step.
                var swimFired = P.Sync ? swim / Rule.FireRate : swim;
                for (int i = 0; i < Cap; i++)
                    if (Active[i]) Pos[i] += !P.Sync || !Hatched[i] ? swim : Fired[i] ? swimFired : Vector3.Zero;
            }

            // ── molts in progress (GAME animation; research molts are instant)
            for (int i = 0; i < Cap; i++)
            {
                if (!Active[i] || Molt[i] <= 0f) continue;
                Molt[i] += 1f / Math.Max(1, P.MoltSteps);
                if (Molt[i] >= 1f)
                {
                    Elem[i] = MoltTo[i]; Molt[i] = 0f; Fate[i] = 0;
                    Events.Add(new SwarmEvent { Kind = SwarmEventKind.MoltDone, Index = i, Other = Elem[i] });
                }
            }

            // ── 5. composition (paused while contested): sort's homeostat lays, surplus molts
            if (!contested)
            {
                if (Clock >= _layHoldUntil) Lay(code);
                if (P.Molt) MoltStep(code);
            }

            // facing: the type look's facing (G2's facing channels) in the body frame, bent toward a fast swim
            for (int i = 0; i < Cap; i++)
            {
                if (!Active[i] || !Hatched[i]) continue;
                int o = i * C;
                var f = Rotate(new Vector3(S[o + 1], S[o + 2], S[o + 3]));
                float s = Vel[i].Length(), vm = P.VMax[EffectiveElement(i)];
                if (s > 1e-4f) f = Vector3.Lerp(f / MathF.Max(f.Length(), 1e-4f), Vel[i] / s, Math.Clamp(s / vm, 0f, 1f) * 0.5f);
                float fl = f.Length(); if (fl > 1e-4f) Facing[i] = f / fl;
            }
            for (int i = 0; i < Cap; i++) if (Active[i]) Startle[i] = _st[i];
            ThreatLevel = 0.85f * ThreatLevel + 0.15f * MathF.Min(1f, 3f * stSum / Math.Max(1, nLive));
        }

        // ──────────────────────────────────────────────────────────────── G2

        /// <summary>
        /// swarm_nca.SwarmRule._step as EvoFate._g2 runs it: a random half of the members fire; each fired member
        /// perceives its active neighbours within R (in BODY coordinates when Oriented) and the MLP writes its
        /// state and proposes its velocity (x vmax x Mix; Time x TMix). The rectifier removes the part of that
        /// velocity which points away from last step's well. Collision over the same pairs, the membrane, then
        /// hatching (channel 0 past 0.1) and egg loss (an egg with no live neighbour or older than EggLife).
        /// GAME adds the vessel reaction to the velocity and keeps eggs (they were paid for).
        /// </summary>
        void G2Step(int nLive)
        {
            var rule = Rule;
            // glob: headcount / 100 and the element mix of the hatched members
            Span<float> glob = stackalloc float[5];
            Array.Clear(_cnt, 0, 4);
            for (int i = 0; i < Cap; i++) if (Active[i] && Hatched[i]) _cnt[Elem[i]]++;
            float cnt = MathF.Max(1, nLive);
            glob[0] = cnt / 100f; for (int e = 0; e < 4; e++) glob[1 + e] = _cnt[e] / cnt;
            // body coordinates (perception reads only differences, so the frame's origin does not matter)
            for (int i = 0; i < Cap; i++) _pb[i] = Active[i] ? InvRotate(Pos[i]) : Vector3.Zero;
            for (int i = 0; i < Cap; i++) Fired[i] = Active[i] && (float)_rng.NextDouble() <= rule.FireRate;

            Array.Clear(_ds, 0, _ds.Length);
            float R = P.R, R2 = R * R;
            var sw = System.Diagnostics.Stopwatch.StartNew();
            for (int i = 0; i < Cap; i++)
            {
                _v[i] = Vector3.Zero;
                if (!Fired[i]) continue;
                Perceive(i, R, R2);
                for (int q = 0; q < 5; q++) _feat[SwarmEvoRule.F - 5 + q] = glob[q];
                rule.Forward(_feat, _h1, _h2, _out);
                NetCalls++;
                Array.Copy(_out, 0, _ds, i * C, C);
                int e = Elem[i];
                float vm = P.VMax[e] * (e == 3 ? P.Mix * P.TMix : P.Mix);
                var vb = new Vector3(MathF.Tanh(_out[C]), MathF.Tanh(_out[C + 1]), MathF.Tanh(_out[C + 2])) * vm;
                var v = Rotate(vb);
                if (P.Rect > 0f && _ugate[i] > 0f)
                {
                    float oc = MathF.Min(0f, Vector3.Dot(v, _udir[i]));
                    v -= P.Rect * _ugate[i] * oc * _udir[i];
                }
                _v[i] = v;
            }
            NetMs += sw.Elapsed.TotalMilliseconds;

            // vessel reaction (GAME): startle damps the learned motion and adds the flee, every hatched member
            for (int i = 0; i < Cap; i++)
            {
                if (!Active[i] || !Hatched[i] || (_st[i] <= 1e-4f && _flee[i] == Vector3.Zero)) continue;
                var v = (1 - 0.8f * _st[i]) * _v[i] + P.FleeGain * _flee[i];
                float vmax = P.VMax[Elem[i]] * (1 + 0.8f * _st[i]), sp = v.Length();
                if (sp > vmax) v *= vmax / sp;
                _v[i] = v;
            }

            // state: s += ds (fired), clamped; the learned death is suppressed (no_death)
            for (int i = 0; i < Cap; i++)
            {
                if (!Active[i]) continue;
                int o = i * C;
                if (Fired[i]) for (int c = 0; c < C; c++) S[o + c] = Math.Clamp(S[o + c] + _ds[o + c], -1e3f, 1e3f);
                S[o + DIE] = MathF.Min(S[o + DIE], 0.5f);
            }
            // move, then collision over the PRE-move neighbour pairs (swarm_nca: edges from the start of the step)
            for (int i = 0; i < Cap; i++) { if (Active[i]) { Pos[i] += _v[i]; Vel[i] = _v[i]; } _push[i] = Vector3.Zero; }
            float k = P.Rep * P.R0 * 0.5f;
            for (int i = 0; i < Cap; i++)
            {
                if (!Active[i]) continue;
                CellKey(_pb[i], out int cx, out int cy, out int cz);
                int nbN = SwarmCoreShared.NeighbourBuckets(cx, cy, cz, HG, _nb);
                for (int nbi = 0; nbi < nbN; nbi++)
                {
                    int g = _nb[nbi];
                    for (int q = _cellStart[g], qe = q + _cellCount[g]; q < qe; q++)
                    {
                        int j = _sorted[q]; if (j == i) continue;
                        if (Vector3.DistanceSquared(_pb[i], _pb[j]) >= R2) continue;   // an edge of the pre-move graph
                        var d = Pos[j] - Pos[i]; float r = MathF.Sqrt(MathF.Max(d.LengthSquared(), 1e-8f));
                        float ov = MathF.Max(P.R0 - r, 0f) / P.R0;
                        if (ov > 0f) _push[i] -= k * ov * d / r;
                    }
                }
            }
            for (int i = 0; i < Cap; i++)
            {
                if (!Active[i]) continue;
                var p = Pos[i] + _push[i];
                float rad = MathF.Max(p.Length(), 1e-6f);
                if (rad > P.Membrane) p -= 0.5f * (rad - P.Membrane) * p / rad;
                Pos[i] = p;
            }
            // hatching and egg loss
            for (int i = 0; i < Cap; i++) _keep[i] = Active[i] && (Hatched[i] || S[i * C + A] > 0.1f);   // hat2 (no deaths)
            for (int i = 0; i < Cap; i++)
            {
                if (!Active[i] || _keep[i]) continue;
                Age[i]++;
                bool near = false;
                CellKey(_pb[i], out int cx, out int cy, out int cz);
                int nbN = SwarmCoreShared.NeighbourBuckets(cx, cy, cz, HG, _nb);
                for (int nbi = 0; nbi < nbN && !near; nbi++)
                {
                    int g = _nb[nbi];
                    for (int q = _cellStart[g], qe = q + _cellCount[g]; q < qe; q++)
                    {
                        int j = _sorted[q];
                        if (j != i && _keep[j] && Vector3.DistanceSquared(_pb[i], _pb[j]) < R2) { near = true; break; }
                    }
                }
                if (near && Age[i] <= P.EggLife) continue;   // still an egg
                if (P.KeepEggs) { Hatched[i] = true; Age[i] = 0; continue; }   // GAME: a paid-for egg hatches
                Active[i] = false; Hatched[i] = false; Array.Clear(S, i * C, C); Age[i] = 0; EggsLost++;
                Events.Add(new SwarmEvent { Kind = SwarmEventKind.Starved, Index = i });   // research only: never silent
            }
            for (int i = 0; i < Cap; i++) if (Active[i] && !Hatched[i] && _keep[i]) { Hatched[i] = true; Age[i] = 0; }
            Clock++;
        }

        /// <summary>
        /// The 232 features member <paramref name="i"/> would perceive this step (perception + glob), written to
        /// <paramref name="dst"/> - the harness's exactness check against swarm_nca.SwarmRule.perceive. Rebuilds the
        /// neighbour hash over the current positions; does not advance anything.
        /// </summary>
        public void FeaturesOf(int i, float[] dst)
        {
            int nLive = 0; Array.Clear(_cnt, 0, 4);
            for (int k = 0; k < Cap; k++) if (Active[k] && Hatched[k]) { nLive++; _cnt[Elem[k]]++; }
            BuildHash(Active);
            Perceive(i, P.R, P.R * P.R);
            float cnt = MathF.Max(1, nLive);
            _feat[SwarmEvoRule.F - 5] = cnt / 100f;
            for (int e = 0; e < 4; e++) _feat[SwarmEvoRule.F - 4 + e] = _cnt[e] / cnt;
            Array.Copy(_feat, dst, SwarmEvoRule.F);
        }

        /// <summary>swarm_nca.SwarmRule.perceive for member i: own [X] | same-domain mean [X] | other-domain mean
        /// [X] | gradient [3 x X] | same-domain gradient [3] | rho, rho_same - over ACTIVE neighbours within R.</summary>
        void Perceive(int i, float R, float R2)
        {
            var f = _feat; Array.Clear(f, 0, SwarmEvoRule.F);
            int oi = i * C;
            // x_i
            for (int c = 0; c < C; c++) f[c] = S[oi + c];
            f[C + Elem[i]] = 1f; f[C + 4] = Hatched[i] ? 1f : 0f;
            float rs = 0, ro = 0, gs = 0; Vector3 gsame = Vector3.Zero;
            const int MS = X, MO = 2 * X, GR = 3 * X, GSAME = 6 * X;
            var pi = _pb[i];
            CellKey(pi, out int cx, out int cy, out int cz);
            int nbN = SwarmCoreShared.NeighbourBuckets(cx, cy, cz, HG, _nb);
            for (int nbi = 0; nbi < nbN; nbi++)
            {
                int g = _nb[nbi];
                for (int q = _cellStart[g], qe = q + _cellCount[g]; q < qe; q++)
                {
                    int j = _sorted[q]; if (j == i) continue;
                    var d = _pb[j] - pi; float d2 = d.LengthSquared();
                    if (d2 >= R2) continue;
                    float qq = MathF.Max(0f, 1f - d2 / R2), w = qq * qq * qq, gw = qq * qq;
                    bool same = Dom[j] == Dom[i];
                    int oj = j * C;
                    var u = d / R;
                    int dst = same ? MS : MO;
                    if (same) rs += w; else ro += w;
                    // neighbour mean (unnormalised here) and the channel gradients
                    for (int c = 0; c < C; c++)
                    {
                        float xj = S[oj + c];
                        f[dst + c] += w * xj;
                        float dxc = gw * (xj - f[c]);
                        f[GR + c] += dxc * u.X; f[GR + X + c] += dxc * u.Y; f[GR + 2 * X + c] += dxc * u.Z;
                    }
                    f[dst + C + Elem[j]] += w;
                    float dh = (Hatched[j] ? 1f : 0f);
                    f[dst + C + 4] += w * dh;
                    for (int e = 0; e < 4; e++)
                    {
                        float dxe = gw * ((Elem[j] == e ? 1f : 0f) - f[C + e]);
                        f[GR + C + e] += dxe * u.X; f[GR + X + C + e] += dxe * u.Y; f[GR + 2 * X + C + e] += dxe * u.Z;
                    }
                    float dhx = gw * (dh - f[C + 4]);
                    f[GR + C + 4] += dhx * u.X; f[GR + X + C + 4] += dhx * u.Y; f[GR + 2 * X + C + 4] += dhx * u.Z;
                    gs += gw;
                    if (!same) gsame -= gw * u;
                }
            }
            float ns = 1f / (1f + rs), no = 1f / (1f + ro), ng = 1f / (1f + gs);
            for (int c = 0; c < X; c++) { f[MS + c] *= ns; f[MO + c] *= no; }
            for (int c = 0; c < 3 * X; c++) f[GR + c] *= ng;
            f[GSAME] = gsame.X * ng; f[GSAME + 1] = gsame.Y * ng; f[GSAME + 2] = gsame.Z * ng;
            f[GSAME + 3] = (rs + ro) / P.Rho0; f[GSAME + 4] = rs / P.Rho0;
        }

        // ──────────────────────────────────────────────────────────────── fate (EvoFate._fate_step)

        void FateStep(SwarmSortCode code, SwarmPlanData plan, float swell)
        {
            int nl = 0; Vector3 c0 = Vector3.Zero;
            for (int i = 0; i < Cap; i++)
            {
                _live[i] = Active[i] && Hatched[i];
                _udir[i] = Vector3.Zero; _ugate[i] = 0f;
                if (!_live[i]) continue;
                _liveIx[nl++] = i; c0 += Pos[i];
            }
            if (nl == 0) return;
            c0 /= nl;
            int fA = 0, fB = 0; float fa = 0f, per = PeriodOf(PlanIx);
            if (P.Animate && plan.Order.Length > 0)
            {
                int L = plan.Order.Length, slot = (int)(Clock / per);
                fA = plan.Order[slot % L]; fB = plan.Order[(slot + 1) % L]; fa = (Clock % per) / per;
            }
            var muA = code.Mu[fA]; var muB = code.Mu[fB];
            for (int a = 0; a < nl; a++)
            {
                int i = _liveIx[a];
                int r = EffRole(i);
                _type[i] = r < 0 ? -1 : EffectiveElement(i) * 3 + r;
                _pb[i] = InvRotate(Pos[i] - c0) / swell;   // xb
            }
            // commit stale fates (need = w * n - occupied, a 1e-3 tie-break)
            for (int t = 0; t < SwarmSortCode.NT; t++)
            {
                int w0 = code.WStart[t], nw = code.WStart[t + 1] - w0;
                if (nw == 0) continue;
                int key = 1 + PlanIx * 16 + (t / 3) * 4 + t % 3, nm = 0, ns = 0;
                for (int q = 0; q < nw; q++) _occ[q] = 0;
                for (int a = 0; a < nl; a++)
                {
                    int i = _liveIx[a]; if (_type[i] != t) continue;
                    _members[nm++] = i;
                    if (Fate[i] >= 1 && FKey[i] == key && Fate[i] <= nw) _occ[Fate[i] - 1]++; else ns++;
                }
                if (ns == 0) continue;
                for (int q = 0; q < nw; q++) _need[q] = code.W[w0 + q] * nm - _occ[q];
                for (int m = 0; m < nm; m++)
                {
                    int i = _members[m];
                    if (Fate[i] >= 1 && FKey[i] == key && Fate[i] <= nw) continue;
                    int f = 0; float bn = float.MinValue;
                    for (int q = 0; q < nw; q++) { float v = _need[q] + 1e-3f * (float)_rng.NextDouble(); if (v > bn) { bn = v; f = q; } }
                    Fate[i] = f + 1; FKey[i] = key; _need[f] -= 1f;
                }
            }
            // the pull: -k_well x grad E of the fated well (orphans: the best mixture over every type), gated by
            // the dead zone, clipped; the rectifier remembers the well's direction and gate for the next G2 step
            for (int a = 0; a < nl; a++)
            {
                int i = _liveIx[a], t = _type[i];
                int nw = t >= 0 ? code.WStart[t + 1] - code.WStart[t] : 0;
                Vector3 g; float E;
                if (nw > 0)
                {
                    int k = code.WStart[t] + Fate[i] - 1;
                    var mu = muA[k] + fa * (muB[k] - muA[k]);
                    var d = _pb[i] - mu;
                    g = Mul(code.Inv, k, d); E = 0.5f * Vector3.Dot(d, g);
                }
                else
                {
                    E = float.PositiveInfinity; g = Vector3.Zero;
                    for (int t2 = 0; t2 < SwarmSortCode.NT; t2++)
                    {
                        if (code.WStart[t2 + 1] == code.WStart[t2]) continue;
                        float e2 = MixtureEnergy(code, t2, _pb[i], muA, muB, fa, out var g2);
                        if (e2 < E) { E = e2; g = g2; }
                    }
                }
                Energy[i] = E;
                bool isT = EffectiveElement(i) == 3;
                var step = -P.KWell * g;
                float gate = 1f;
                if (P.E0 > 0f)
                {
                    float e0 = isT ? P.E0 * P.TE0 : P.E0;
                    gate = Math.Clamp((E - e0) / MathF.Max(e0, 1e-6f), 0f, 1f);
                    step *= gate;
                }
                if (P.TP != 1f && isT) step *= P.TP;
                float gn = g.Length();
                if (P.Rect > 0f && gn > 1e-9f) { _udir[i] = Rotate(-g / gn); _ugate[i] = gate; }
                float sn = step.Length();
                if (sn > P.WellClip) step *= P.WellClip / sn;
                _move[i] = Rotate(P.Pull * step) * swell;
            }
            // differential adhesion (sort's matrix x Adh) over the hatched members, on the moved positions
            if (P.Adh > 0f)
            {
                BuildHash(_live);
                float rA2 = P.RAdh * P.RAdh, nearMin = 0.9f * P.AdhR0;
                for (int a = 0; a < nl; a++)
                {
                    int i = _liveIx[a]; var x = Pos[i];
                    int ei = EffectiveElement(i), ri = EffRole(i);
                    var acc = Vector3.Zero;
                    CellKey(x, out int cx, out int cy, out int cz);
                    int nbN = SwarmCoreShared.NeighbourBuckets(cx, cy, cz, HG, _nb);
                    for (int nbi = 0; nbi < nbN; nbi++)
                    {
                        int gk = _nb[nbi];
                        for (int q = _cellStart[gk], qe = q + _cellCount[gk]; q < qe; q++)
                        {
                            int j = _sorted[q]; if (j == i) continue;
                            var v = Pos[j] - x; float d2 = v.LengthSquared();
                            if (d2 >= rA2) continue;
                            float d = MathF.Sqrt(d2); if (d <= nearMin) continue;
                            int ej = EffectiveElement(j), rj = EffRole(j);
                            float Aij = ei == ej && ri == rj ? P.ASame : ei == ej ? P.AElem : ri == rj ? P.ARole : P.AOther;
                            acc += Aij / d * v;
                        }
                    }
                    _move[i] += P.Adh * acc;
                }
            }
            for (int a = 0; a < nl; a++)
            {
                int i = _liveIx[a];
                var m = _move[i];
                if (P.Sync) m *= Fired[i] ? 1f / Rule.FireRate : 0f;
                m *= 1f - 0.8f * _st[i];   // GAME: a startled member flees rather than sorts (0 with no vessel)
                Pos[i] += m;
                Vel[i] += m;
            }
            // the designed look: the visual channels (facing, prism, tier, spindle) = the type's look
            if (P.Look)
            {
                BuildTypeStates(code);
                for (int a = 0; a < nl; a++)
                {
                    int i = _liveIx[a];
                    int e = Elem[i], r = EffRole(i);
                    int t = -1;
                    if (r >= 0 && code.HasType[e * 3 + r]) t = e * 3 + r;
                    else for (int s2 = 0; s2 < 3; s2++) if (code.HasType[e * 3 + s2]) { t = e * 3 + s2; break; }
                    if (t < 0) continue;
                    Array.Copy(_typeState, t * 11, S, i * C + 1, 11);
                }
            }
        }

        SwarmSortCode _typeStateFor;

        /// <summary>field_swarm.invert_state of each type's mean look (sort_model.PlanCode.state), channels 1..11.</summary>
        void BuildTypeStates(SwarmSortCode code)
        {
            if (_typeStateFor == code) return;
            _typeStateFor = code;
            for (int t = 0; t < SwarmSortCode.NT; t++)
            {
                if (!code.HasType[t]) continue;
                InvertState(t / 3, code.TypeHalf[t], code.TypeTier[t], code.TypeFace[t], code.TypeSp[t], _typeState, t * 11);
            }
        }

        static float L3(float x) { x = Math.Clamp(x, 1e-3f, 1 - 1e-3f); return MathF.Log(x / (1 - x)); }

        /// <summary>field_swarm.invert_state, channels 1..11 (facing x4 | raw prism | tier one-hot x8 | spindle).</summary>
        public static void InvertState(int e, Vector3 h, int tier, Vector3 f, Vector2 sp, float[] o, int at)
        {
            o[at] = 4f * f.X; o[at + 1] = 4f * f.Y; o[at + 2] = 4f * f.Z;
            Vector3 r;
            if (e == 0) r = new Vector3(L3((h.X - 0.3f) / 1.9f), L3((h.Y - 0.3f) / 1.9f), L3((h.Z - 0.3f) / 1.9f));
            else if (e == 1) r = new Vector3(L3((h.X - 0.5f) / 1.3f), L3((h.Y - 0.5f) / 1.3f), L3((h.Z - 0.5f) / 1.3f));
            else if (e == 2)
            {
                float L = h.X, cmax = Math.Clamp(L / 4f, 0.25f, 0.42f);
                r = new Vector3(L3((L - 0.9f) / 2.1f), cmax > 0.2501f ? L3((h.Y - 0.25f) / (cmax - 0.25f)) : 0f, cmax > 0.2501f ? L3((h.Z - 0.25f) / (cmax - 0.25f)) : 0f);
            }
            else
            {
                float L = h.X, cmax = Math.Clamp(L / 1.5f, 0.3f, 0.6f);
                r = new Vector3(L3((L - 0.45f) / 0.75f), cmax > 0.3001f ? L3((h.Y - 0.3f) / (cmax - 0.3f)) : 0f, cmax > 0.3001f ? L3((h.Z - 0.3f) / (cmax - 0.3f)) : 0f);
            }
            o[at + 3] = r.X; o[at + 4] = r.Y; o[at + 5] = r.Z;
            for (int q = 0; q < 3; q++) o[at + 6 + q] = q == tier ? 8f : 0f;
            float a = Math.Clamp(sp.X / 0.6f, 0.02f, 0.98f), b = Math.Clamp(sp.Y / 0.5f, -0.98f, 0.98f);
            o[at + 9] = MathF.Log(a / (1 - a)); o[at + 10] = 0.5f * MathF.Log((1 + b) / (1 - b));
        }

        static Vector3 Mul(float[] inv, int k, Vector3 d)
        {
            int o = k * 9;
            return new Vector3(inv[o] * d.X + inv[o + 1] * d.Y + inv[o + 2] * d.Z,
                               inv[o + 3] * d.X + inv[o + 4] * d.Y + inv[o + 5] * d.Z,
                               inv[o + 6] * d.X + inv[o + 7] * d.Y + inv[o + 8] * d.Z);
        }

        static float MixtureEnergy(SwarmSortCode code, int t, Vector3 y, Vector3[] muA, Vector3[] muB, float fa, out Vector3 g)
        {
            int w0 = code.WStart[t], w1 = code.WStart[t + 1];
            float mx = float.NegativeInfinity;
            Span<float> lp = stackalloc float[w1 - w0];
            for (int k = w0; k < w1; k++)
            {
                var d = y - (muA[k] + fa * (muB[k] - muA[k]));
                float q = Vector3.Dot(d, Mul(code.Inv, k, d));
                lp[k - w0] = MathF.Log(code.W[k]) - 0.5f * q - 0.5f * code.LogDet[k];
                if (lp[k - w0] > mx) mx = lp[k - w0];
            }
            float Z = 0; g = Vector3.Zero;
            for (int k = w0; k < w1; k++)
            {
                float r = MathF.Exp(lp[k - w0] - mx); Z += r;
                var d = y - (muA[k] + fa * (muB[k] - muA[k]));
                g += r * Mul(code.Inv, k, d);
            }
            g /= Z;
            return -(mx + MathF.Log(Z));
        }

        // ──────────────────────────────────────────────────────────────── plan, regions (sort's)

        bool DecidePlan()
        {
            Array.Clear(_cnt, 0, 4);
            for (int i = 0; i < Cap; i++) if (Active[i] && Hatched[i]) _cnt[EffectiveElement(i)]++;
            int cur = PlanIx, top = ArgMax(_cnt);
            int maj = _cnt[cur] == _cnt[top] ? cur : top;
            if (maj == cur) { _cand = cur; _candN = 0; return false; }
            _candN = _cand == maj ? _candN + 1 : 1;
            _cand = maj;
            if (_candN < P.Dwell) return true;
            Events.Add(new SwarmEvent { Kind = SwarmEventKind.Switched, Index = cur, Other = maj });
            PlanIx = maj; _candN = 0; _permSet = false;
            if (P.Oriented) SetHeading(Heading, snap: true);
            return false;
        }

        static readonly int[][][] PERMS =
        {
            null,
            new[] { new[] { 0 } },
            new[] { new[] { 0, 1 }, new[] { 1, 0 } },
            new[] { new[] { 0, 1, 2 }, new[] { 0, 2, 1 }, new[] { 1, 0, 2 }, new[] { 1, 2, 0 }, new[] { 2, 0, 1 }, new[] { 2, 1, 0 } },
        };

        void PickPerm(SwarmSortCode code)
        {
            bool first = !_permSet;
            _permSet = true;
            int ns = Math.Clamp(code.NSlots, 1, 3);
            if (!P.DomainSlots || ns == 1)
            {
                Perm[0] = 0; RoleOfDom[0] = 0; RoleOfDom[1] = RoleOfDom[2] = -1;
                return;
            }
            Array.Clear(_dcen, 0, _dcen.Length); int tot = 0;
            for (int i = 0; i < Cap; i++) if (Active[i] && Hatched[i]) { _dcen[Elem[i], Dom[i]]++; tot++; }
            float scale = Math.Max(1, tot) / (float)Math.Max(1, code.N);
            float best = float.MaxValue; int[] arg = PERMS[ns][0];
            foreach (var p in PERMS[ns])
            {
                float cost = 0;
                for (int e = 0; e < 4; e++) for (int s = 0; s < ns; s++) cost += MathF.Abs(_dcen[e, p[s]] - code.Counts[e * 3 + s] * scale);
                bool same = !first; for (int s = 0; s < ns && same; s++) same &= Perm[s] == p[s];
                if (same) cost -= 2f;
                if (cost < best) { best = cost; arg = p; }
            }
            RoleOfDom[0] = RoleOfDom[1] = RoleOfDom[2] = -1;
            for (int s = 0; s < ns; s++) { Perm[s] = arg[s]; RoleOfDom[arg[s]] = s; }
        }

        int EffRole(int i) => XferPlan[i] == PlanIx && XferPlan[i] >= 0 ? XferRole[i] : RoleOfDom[Math.Clamp(Dom[i], 0, 2)];

        // ──────────────────────────────────────────────────────────────── composition (sort's; EvoFate._lay_sort / _molt)

        void Census(int[] cen, bool includeEggs)
        {
            Array.Clear(cen, 0, 16);
            for (int i = 0; i < Cap; i++)
            {
                if (!Active[i] || (!includeEggs && !Hatched[i])) continue;
                int r = EffRole(i);
                cen[EffectiveElement(i) * 4 + (r < 0 ? 3 : r)]++;
            }
        }

        void Lay(SwarmSortCode code)
        {
            var cen = _cen; Census(cen, true);
            float pos = 0;
            for (int e = 0; e < 4; e++)
                for (int col = 0; col < 4; col++)
                {
                    int q = e * 4 + col;
                    _want[q] = col < 3 ? MathF.Ceiling(code.Counts[e * 3 + col] * P.Over) : 0f;
                    _deficit[q] = _want[q] - cen[q];
                    if (col < 3 && _deficit[q] > 0) pos += _deficit[q];
                    _fill[q] = _want[q] > 0 ? cen[q] / MathF.Max(_want[q], 1f) : 9f;
                }
            if (pos <= 0) return;
            int nAct = 0, nHat = 0, nFree = 0;
            for (int i = 0; i < Cap; i++) { if (Active[i]) { nAct++; if (Hatched[i]) nHat++; } else nFree++; }
            int room = (int)MathF.Ceiling(code.N * P.Over) - nAct;
            int nlay = Math.Min(P.LayMax, Math.Min(nFree, Math.Min(room, Poisson(MathF.Max(P.LayRate * nHat, 0.2f)))));
            if (nlay <= 0) return;
            Array.Clear(_ec, 0, 4);
            for (int i = 0; i < Cap; i++) if (Active[i]) _ec[EffectiveElement(i)]++;
            int maj = PlanIx, laid = 0, freeFrom = 0;
            int np = 0; for (int i = 0; i < Cap; i++) if (Active[i] && Hatched[i]) _members[np++] = i;
            for (int q = np - 1; q > 0; q--) { int j = _rng.Next(q + 1); (_members[q], _members[j]) = (_members[j], _members[q]); }
            for (int pq = 0; pq < np && laid < nlay; pq++)
            {
                int par = _members[pq];
                int r = RoleOfDom[Math.Clamp(Dom[par], 0, 2)];
                if (r < 0) continue;
                int e = EffectiveElement(par);
                int low = 0; for (int e2 = 1; e2 < 4; e2++) if (_fill[e2 * 4 + r] < _fill[low * 4 + r]) low = e2;
                int ce;
                if (_deficit[e * 4 + r] > 0 && _fill[e * 4 + r] <= _fill[low * 4 + r] + P.FillTol) ce = e;
                else if (_rng.NextDouble() < P.PCross && _deficit[low * 4 + r] > 0) ce = low;
                else continue;
                if (ce != maj && _ec[ce] + 1 >= _ec[maj])
                {
                    if (_rng.NextDouble() < P.PCross && _deficit[maj * 4 + r] > 0) ce = maj;
                    else continue;
                }
                if (P.Funded && !SwarmCoreShared.TryFund(Stomach, ce, P.EggCost[ce], P.CrossCost)) continue;
                int j2 = -1; for (int i = freeFrom; i < Cap; i++) if (!Active[i]) { j2 = i; break; }
                if (j2 < 0) break;
                freeFrom = j2 + 1;
                _ec[ce]++;
                var dir = Gauss3(); dir /= MathF.Max(dir.Length(), 1e-6f);
                Pos[j2] = Pos[par] + P.RBud * dir; Vel[j2] = Vector3.Zero;
                Elem[j2] = ce; Dom[j2] = Dom[par]; Active[j2] = true; Hatched[j2] = false; Age[j2] = 0;
                Array.Clear(S, j2 * C, C); S[j2 * C + A] = 0.2f;
                Startle[j2] = 0; Molt[j2] = 0; Fate[j2] = 0; FKey[j2] = 0; XferPlan[j2] = -1; Facing[j2] = Facing[par];
                _udir[j2] = Vector3.Zero; _ugate[j2] = 0;
                int q2 = ce * 4 + r;
                _deficit[q2] -= 1; cen[q2] += 1; _fill[q2] = cen[q2] / MathF.Max(_want[q2], 1f);
                laid++;
                Events.Add(new SwarmEvent { Kind = SwarmEventKind.Laid, Index = j2, Other = par });
            }
        }

        void MoltStep(SwarmSortCode code)
        {
            var cen = _cenM; Census(cen, false);
            for (int e = 0; e < 4; e++) for (int col = 0; col < 4; col++) _want[e * 4 + col] = col < 3 ? code.Counts[e * 3 + col] : 0f;
            int np = 0; for (int i = 0; i < Cap; i++) if (Active[i] && Hatched[i]) _members[np++] = i;
            for (int q = 0; q < np; q++) _type[_members[q]] = EffRole(_members[q]);
            for (int q = np - 1; q > 0; q--) { int j = _rng.Next(q + 1); (_members[q], _members[j]) = (_members[j], _members[q]); }
            int maj = PlanIx;
            for (int pq = 0; pq < np; pq++)
            {
                int i = _members[pq], r = _type[i];
                if (_rng.NextDouble() > P.MoltRate) continue;
                if (Molt[i] > 0f) continue;
                int e = Elem[i], rc = r < 0 ? 3 : r;
                if (cen[e * 4 + rc] <= _want[e * 4 + rc]) continue;
                int r2 = -1;
                if (r >= 0) { for (int e2 = 0; e2 < 4; e2++) if (_want[e2 * 4 + r] - cen[e2 * 4 + r] > 0) { r2 = r; break; } }
                if (r2 < 0 && P.Transfer)
                {
                    float bd = 0;
                    for (int e2 = 0; e2 < 4; e2++) for (int col = 0; col < 3; col++)
                    {
                        float d = _want[e2 * 4 + col] - cen[e2 * 4 + col];
                        if (d > bd) { bd = d; r2 = col; }
                    }
                    if (r2 >= 0) { XferRole[i] = r2; XferPlan[i] = PlanIx; }
                }
                if (r2 < 0) continue;
                int ne = 0; float bf = float.MaxValue;
                for (int e2 = 0; e2 < 4; e2++)
                {
                    int q = e2 * 4 + r2;
                    float fl = _want[q] > 0 && _want[q] - cen[q] > 0 ? cen[q] / MathF.Max(_want[q], 1f) : 9f;
                    if (fl < bf) { bf = fl; ne = e2; }
                }
                Array.Clear(_ec, 0, 4);
                for (int k = 0; k < Cap; k++) if (Active[k] && Hatched[k]) _ec[EffectiveElement(k)]++;
                if (ne != maj && _ec[ne] + 1 >= _ec[maj]) continue;
                cen[e * 4 + rc]--; cen[ne * 4 + r2]++;
                if (ne == e) { Fate[i] = 0; continue; }
                if (P.MoltSteps <= 0) { Elem[i] = ne; Fate[i] = 0; continue; }
                Molt[i] = 1e-3f; MoltTo[i] = ne;
                Events.Add(new SwarmEvent { Kind = SwarmEventKind.MoltBegan, Index = i, Other = ne });
            }
        }

        // ──────────────────────────────────────────────────────────────── neighbours

        void CellKey(Vector3 x, out int cx, out int cy, out int cz)
        {
            float r = _hashR;
            cx = (int)MathF.Floor(x.X / r); cy = (int)MathF.Floor(x.Y / r); cz = (int)MathF.Floor(x.Z / r);
        }

        static int Hash(int x, int y, int z) => (int)(((uint)(x * 73856093) ^ (uint)(y * 19349663) ^ (uint)(z * 83492791)) % HG);

        /// <summary>Hash the members over Pos (or, for the G2 pass, over the body-frame _pb when it holds them).</summary>
        void BuildHash(bool[] member) => BuildHash(member, member == Active ? _pb : Pos, member == Active);

        void BuildHash(bool[] member, Vector3[] at, bool bodyFrame)
        {
            if (bodyFrame) for (int i = 0; i < Cap; i++) _pb[i] = member[i] ? InvRotate(Pos[i]) : Vector3.Zero;
            Array.Clear(_cellCount, 0, HG);
            for (int i = 0; i < Cap; i++)
            {
                if (!member[i]) continue;
                CellKey(at[i], out int a, out int b, out int c);
                _cellOf[i] = Hash(a, b, c); _cellCount[_cellOf[i]]++;
            }
            int s = 0; for (int g = 0; g < HG; g++) { _cellStart[g] = s; s += _cellCount[g]; }
            Array.Clear(_fill2, 0, HG);
            for (int i = 0; i < Cap; i++) if (member[i]) { int g = _cellOf[i]; _sorted[_cellStart[g] + _fill2[g]++] = i; }
        }

        float MaxStartleNear(Vector3 x, float r, int self)
        {
            var xb = InvRotate(x);
            CellKey(xb, out int cx, out int cy, out int cz);
            float r2 = r * r, best = 0f;
            int nbN = SwarmCoreShared.NeighbourBuckets(cx, cy, cz, HG, _nb);
            for (int nbi = 0; nbi < nbN; nbi++)
            {
                int g = _nb[nbi];
                for (int q = _cellStart[g], e = q + _cellCount[g]; q < e; q++)
                {
                    int j = _sorted[q]; if (j == self) continue;
                    if (Vector3.DistanceSquared(x, Pos[j]) < r2 && Startle[j] > best) best = Startle[j];
                }
            }
            return best;
        }
    }
}
