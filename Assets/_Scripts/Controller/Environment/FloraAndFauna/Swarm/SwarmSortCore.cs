// The swarm fauna's THIRD simulation core - the research model `sort` (EMERGENT CELL SORTING:
// Tools/NCA/sort_model.py on cece/gifted-curie-x2cpd0, its config results/sort/params.json), ported
// to plain C# over a struct-of-arrays. Same shape as SwarmFieldCore and SwarmGridCore: no UnityEngine,
// the SAME file compiles and RUNS headless (Tools/Build/swarm_core_harness), the glue (SwarmFauna)
// converts at the boundary. Docs/SWARM_FAUNA.md §9 is the design record.
//
// No network and no slot assignment. Each tadpole knows only:
//   * its TYPE = (element, region). A region is the body part its domain plays in the current plan
//     (the swarm picks the domain -> region map from its own census). The game's one-colour law
//     leaves ONE region, so in the game a type is just an element;
//   * POSITIONAL INFORMATION for its type: a small mixture of Gaussian "morphogen wells" in BODY
//     coordinates - at most K per type, at most one per PerWell units of it. Nothing tells a tadpole
//     where in its type's tissue to go;
//   * its FATE: a newborn commits to ONE well of its type - the one its type under-occupies right now
//     (lateral inhibition read through a census) - and climbs that well's log-density. The research's
//     decisive mechanism: without it the far parts of a body stay empty;
//   * its NEIGHBOURS: collision, a (type x type) DIFFERENTIAL ADHESION matrix (CMA-ES made it all
//     repulsive and MORE repulsive between unlike types than like ones - Steinberg's rule in its
//     relative form, so like cells pack together and tissue borders sharpen), and Potts-style swaps.
// Composition is a joint HOMEOSTAT: a parent lays while its class is short of the plan; a parent
// whose class is full lays its region's most-needed element instead. A tadpole of a SURPLUS class
// MOLTS into a deficit element of its own region, and one with no region left may TRANSFER to the
// neediest region. NOTHING DIES ON A CLOCK: the corrector the grid model needed a hunger cull for is
// lossless here.
//
// What the GAME adds (all switchable, so the harness can run the research model exactly):
//   * laying is FUNDED out of the stomach, at most LayMax a step, held while wounded (shared rules);
//   * a molt is an ANIMATION, not a jump: it takes MoltSteps steps (the field core's precedent - the
//     glue shrinks the heart away and re-forms it as the new element) and, while it runs, the member
//     already steers as what it is becoming;
//   * the wells TURN AND TRAVEL WITH THE SWIMMING BODY: the code is read in the body frame (the plan
//     yawed onto the heading, centred on the swarm's centroid, scaled by the pufferfish's inflation),
//     and - unlike the research, whose code is frame 0 of each plan - each well rides its plan's
//     animation (a well is a fixed set of plan units, so its centre in any frame is theirs);
//   * each tadpole wears the look of its fated WELL (the research's `well_look` option), so the
//     jellyfish's shielded bell and the pufferfish's spines sit where the plan puts them;
//   * swimming in a band, the membrane, and the vessel reaction (SwarmCoreShared.FleeFrom).
using System;
using System.Collections.Generic;
using System.Numerics;

namespace CosmicShore.Gameplay
{
    /// <summary>The sort model's numbers. Defaults are sort's published values
    /// (Tools/NCA/results/sort/params.json: K = 12, per_well = 4, molt on) unless marked GAME.</summary>
    public sealed class SwarmSortParams
    {
        // ── positional information ──
        public int K = 12, PerWell = 4;
        public float CovScale = 0.8901047f, KWell = 0.41231294f, WellClip = 0.52463809f;
        // ── neighbours ──
        public float R0 = 2.2476657f, KRep = 0.15101389f, RAdh = 5.3778802f;
        public float ASame = -0.05f, AElem = -0.037361621f, ARole = -0.027014625f, AOther = -0.063682117f;
        public float Swap = 0.70860148f, RSwap = 3.6314775f, SwapMargin = 0.00086020392f;
        // ── motion ──
        public float Inertia = 0.68680722f;
        public float[] VMax = { 0.8f, 0.8f, 0.8f, 2f };
        // ── composition ──
        public int Dwell = 12;
        public float LayRate = 0.083983335f;
        public int LayMax = 5;
        public float RBud = 2.6f, FillTol = 0.15f, PCross = 0.46608711f, Over = 0.93910768f;
        public int HatchSteps = 3;
        public bool Molt = true, Transfer = true;
        public float MoltRate = 0.03f;
        public int RoleEvery = 10;
        /// <summary>Wear the look of one's fated WELL rather than the type's mean (research option `well_look`).</summary>
        public bool WellLook = false;
        public float Membrane = float.PositiveInfinity;
        public int Cap = 280;

        // ── GAME ──
        /// <summary>Keep the research's domain regions (true: only for the harness). The game's
        /// one-colour law leaves one region, so a type is just an element.</summary>
        public bool DomainSlots = false;
        /// <summary>Round 9 LINEAGES (Docs/SWARM_FAUNA.md §17; needs DomainSlots). Every seed wears slot 0 (the cell's
        /// controlling domain) and a child keeps its parent's domain - food never colours anyone. The plan's regions
        /// (a whale's back and belly) are ANATOMY, not colours: a region is OWNED by whichever lineage holds most of
        /// its tissue, and a region no lineage owns is grown by any parent with room to lay. A child laid into such
        /// UNOWNED tissue by a parent whose lineage owns a region of its own founds a NEW lineage with probability
        /// <see cref="Drift"/> - a domain the swarm does not hold yet, drawn uniformly, so nothing picks which colour
        /// lands where. The founder's line then breeds true into the region it holds, and strangers (members sitting
        /// in tissue another lineage owns) drift home whenever their own region has room.</summary>
        public bool Lineages = false;
        /// <summary>Chance a child laid into unowned tissue founds a new lineage (round 9).</summary>
        public float Drift = 0.01f;
        /// <summary>Laying pays out of the stomach (game). False = free (research).</summary>
        public bool Funded = true;
        public float[] EggCost = { 1f, 1f, 1f, 1f };
        public float CrossCost = 2f;
        /// <summary>A wounded swarm holds its eggs this many steps after every kill (the shared rule). 0 = research.</summary>
        public int KillLayHoldSteps = 0;
        /// <summary>Steps a molt takes (the glue animates it). 0 = instant (research).</summary>
        public int MoltSteps = 0;
        /// <summary>Molting runs only this many steps after a committed plan switch. NEGATIVE = always
        /// (research). The field core needed a window because its molting is proportional; this
        /// model's is ABSOLUTE (a class over its plan COUNT), so killing a majority creates no
        /// surplus anywhere and cannot be molted back - measured by the harness (S4), which is why
        /// the game ships with molting always on.</summary>
        public int MoltWindow = -1;
        /// <summary>Wells ride the plan's animation (game); false = frame 0 only (research).</summary>
        public bool Animate = false;
        /// <summary>Feed-forward: the share of its well's own motion a tadpole takes (game; research 0).</summary>
        public float KWellFF = 0f;
        /// <summary>Velocity noise per step in voxels (game; research 0) - the grid core's term. The
        /// research body is near-static and gets its jostle from adhesion alone; a body that SWIMS and
        /// animates moves its members in step with their wells, and without a little noise a member on
        /// a still well reads as frozen while the body around it moves (swarm_feel `stuck`).</summary>
        public float Noise = 0f;
        /// <summary>Steps per plan animation frame, indexed by research element (the dragonfly's wings: 16).</summary>
        public int[] Periods = { 8, 8, 8, 16 };
        /// <summary>Turn the code into the body's heading (swimming). False = the research's fixed frame.</summary>
        public bool Oriented = false;
        public float Cruise = 0f, Turn = 0.03f;
        /// <summary>Within this many body radii of its swim target the body holds its heading and
        /// station-keeps (finding 13: a body must not chase its own centroid's jitter).</summary>
        public float AimHold = 1.5f;
        public float BandInner, BandOuter;
        // vessel reaction (SwarmFieldParams' values)
        public float Sense = 2.2f, Relay = 0.8f, StartleDecay = 0.9f, Lookahead = 10f, FleeSwirl = 0.8f, RelayR = 5f;
        public float FleeGain = 2f;
        public float[] Flee = { 0.6f, 0.5f, 1.4f, 2f };
        public float[] Mob = { 0f, 0f, 0f, 1f };
        public float MobSpeed = 1f;
        public float[] Inflate = { 0.45f, 0f, 0f, 0f };
        /// <summary>Threat = min(1, ThreatGain x startled fraction). A ship of fixed size sweeps a TUBE through
        /// the body, so the fraction it startles falls as density^(-2/3) when the plan is upsampled (round 7,
        /// Docs/SWARM_FAUNA.md §14): the game sets 3 x PlanDensity^(2/3) so a pufferfish still inflates.</summary>
        public float ThreatGain = 3f;

        // ── sortfeel + the fractional update (research sortfeel_model.py / lite_sortfeel_model.py;
        //    Docs/SWARM_FAUNA.md §12). Every default is OFF, so these fields leave sort byte-identical.
        /// <summary>FLAT-BOTTOMED fate wells (research `well_dead`, m0 in well sigmas): the fate pull is
        /// the gradient of 0.5 max(0, m - m0)^2, m the Mahalanobis distance to the fated well - inside m0
        /// there is no pull, so a tissue FILLS its well as a liquid instead of being crushed into a sheet.
        /// 0 = sort's plain quadratic well.</summary>
        public float WellDead = 0f;
        /// <summary>m0 on the dragonfly (Time) plan's wells only (research `well_dead_time`). NEGATIVE =
        /// WellDead. sortfeel v2 sets 0 here (its thin wings want tight wells); the held lite config ran
        /// without the override (0.7 everywhere).</summary>
        public float WellDeadTime = -1f;
        /// <summary>Per-tadpole Ornstein-Uhlenbeck wander (research `wander`, voxels/step), added to the
        /// position and kept OUT of the inertia state. 0 = off.</summary>
        public float Wander = 0f;
        /// <summary>The wander's correlation time in steps (research `wander_tau`); each tadpole runs at
        /// tau x (0.6 + 0.8 frac(slot x 0.618)) - everyone does the same thing slightly differently.</summary>
        public float WanderTau = 12f;
        /// <summary>FRACTIONAL UPDATE k (research `frac`): each step only members with (slot + step) % k == 0
        /// re-steer (neighbours, adhesion, swaps, the velocity blend - which then uses inertia^k, the
        /// k-step equivalent); the rest COAST on their last velocity. Everything rate-based (fate, wells,
        /// wander, hatching, laying, molting, the plan's dwell) still runs every step for everyone. A
        /// member a vessel has startled re-steers every step whatever its phase (GAME). 1 = sort.</summary>
        public int Frac = 1;

        /// <summary>Turn on the research's published sortfeel (well_dead 0.7, wander 0.05, tau 12) and the
        /// held lite schedule (frac 8). <paramref name="wellDeadTime"/> -1 reproduces the HELD config
        /// (results/lite_sortfeel/params.json, which carries no override); sortfeel v2 used 0.</summary>
        public SwarmSortParams WithSortFeel(int frac = 8, float wellDeadTime = -1f)
        {
            WellDead = 0.7f; WellDeadTime = wellDeadTime; Wander = 0.05f; WanderTau = 12f; Frac = Math.Max(1, frac);
            return this;
        }
    }

    /// <summary>
    /// One plan's positional-information code (sort_model.PlanCode): per type (element x region) its
    /// plan count, its wells (weight, centre, inverse covariance, log-determinant) and its look - the
    /// type's mean and each well's own. Built once per plan and shared by every swarm.
    /// </summary>
    public sealed class SwarmSortCode
    {
        public const int NT = 12;   // types: element * 3 + region
        public int N, NSlots;
        public readonly int[] Counts = new int[NT];
        /// <summary>Wells of type t are [WStart[t], WStart[t + 1]).</summary>
        public readonly int[] WStart = new int[NT + 1];
        public float[] W = Array.Empty<float>(), LogDet = Array.Empty<float>();
        /// <summary>Inverse covariance, 9 floats per well (row-major).</summary>
        public float[] Inv = Array.Empty<float>();
        /// <summary>Well centres per plan FRAME [frame][well] (frame 0 is the research code).</summary>
        public Vector3[][] Mu = Array.Empty<Vector3[]>();
        // looks (plan coordinates): per type, per well
        public readonly bool[] HasType = new bool[NT];
        public readonly Vector3[] TypeHalf = new Vector3[NT], TypeFace = new Vector3[NT];
        public readonly Vector2[] TypeSp = new Vector2[NT];
        public readonly int[] TypeTier = new int[NT];
        public Vector3[] WellHalf = Array.Empty<Vector3>(), WellFace = Array.Empty<Vector3>();
        public Vector2[] WellSp = Array.Empty<Vector2>();
        public int[] WellTier = Array.Empty<int>();

        static readonly Dictionary<(SwarmPlanData, bool, int, int, float), SwarmSortCode> s_cache = new();

        public static SwarmSortCode For(SwarmPlanData plan, SwarmSortParams c)
        {
            var key = (plan, c.DomainSlots, c.K, c.PerWell, c.CovScale);
            lock (s_cache)
            {
                if (!s_cache.TryGetValue(key, out var code)) s_cache[key] = code = new SwarmSortCode(plan, c);
                return code;
            }
        }

        SwarmSortCode(SwarmPlanData plan, SwarmSortParams c)
        {
            N = plan.N; NSlots = c.DomainSlots ? plan.NSlots : 1;
            int F = plan.P.Length;
            var P0 = plan.P[0];
            var cen0 = Vector3.Zero; for (int k = 0; k < N; k++) cen0 += P0[k]; cen0 /= N;
            var fc = new Vector3[F];
            for (int f = 0; f < F; f++) { var s = Vector3.Zero; for (int k = 0; k < N; k++) s += plan.P[f][k]; fc[f] = s / N; }

            var w = new List<float>(); var ld = new List<float>(); var inv = new List<float>();
            var units = new List<int[]>();   // each well's plan units
            var wh = new List<Vector3>(); var wf = new List<Vector3>(); var ws = new List<Vector2>(); var wt = new List<int>();
            var rng = new Random(1234);
            for (int t = 0; t < NT; t++)
            {
                WStart[t] = w.Count;
                int e = t / 3, s = t % 3;
                var m = new List<int>();
                for (int k = 0; k < N; k++) if (plan.Elem[k] == e && SlotOf(plan, k, c.DomainSlots) == s) m.Add(k);
                Counts[t] = m.Count;
                if (m.Count == 0) continue;
                HasType[t] = true;
                Look(plan, m, out TypeHalf[t], out TypeTier[t], out TypeFace[t], out TypeSp[t]);
                int K = Math.Max(1, Math.Min(c.K, m.Count / Math.Max(1, c.PerWell)));
                var Q = new double[m.Count][];
                for (int q = 0; q < m.Count; q++) { var p = P0[m[q]] - cen0; Q[q] = new double[] { p.X, p.Y, p.Z }; }
                var a = K > 1 ? KMeans(Q, K, rng) : new int[m.Count];
                for (int k = 0; k < K; k++)
                {
                    var R = new List<int>();
                    for (int q = 0; q < m.Count; q++) if (a[q] == k) R.Add(q);
                    if (R.Count == 0) continue;
                    var mu = new double[3]; foreach (int q in R) for (int d = 0; d < 3; d++) mu[d] += Q[q][d];
                    for (int d = 0; d < 3; d++) mu[d] /= R.Count;
                    var cov = new double[9];
                    if (R.Count > 2)
                    {
                        foreach (int q in R) for (int i = 0; i < 3; i++) for (int j = 0; j < 3; j++) cov[i * 3 + j] += (Q[q][i] - mu[i]) * (Q[q][j] - mu[j]);
                        for (int i = 0; i < 9; i++) cov[i] /= R.Count - 1;   // np.cov: unbiased
                    }
                    for (int i = 0; i < 9; i++) cov[i] *= c.CovScale;
                    cov[0] += 1; cov[4] += 1; cov[8] += 1;
                    var iv = Inverse3(cov, out double det);
                    w.Add(R.Count / (float)m.Count); ld.Add((float)Math.Log(det));
                    for (int i = 0; i < 9; i++) inv.Add((float)iv[i]);
                    var mk = new int[R.Count]; for (int q = 0; q < R.Count; q++) mk[q] = m[R[q]];
                    units.Add(mk);
                    Look(plan, mk, out var h, out int tier, out var face, out var sp);
                    wh.Add(h); wt.Add(tier); wf.Add(face); ws.Add(sp);
                }
            }
            WStart[NT] = w.Count;
            W = w.ToArray(); LogDet = ld.ToArray(); Inv = inv.ToArray();
            WellHalf = wh.ToArray(); WellTier = wt.ToArray(); WellFace = wf.ToArray(); WellSp = ws.ToArray();
            // each well's centre in every frame: the mean of ITS units there, relative to that frame's centroid
            Mu = new Vector3[F][];
            for (int f = 0; f < F; f++)
            {
                Mu[f] = new Vector3[W.Length];
                for (int q = 0; q < W.Length; q++)
                {
                    var s = Vector3.Zero; foreach (int k in units[q]) s += plan.P[f][k];
                    Mu[f][q] = s / units[q].Length - fc[f];
                }
            }
        }

        static int SlotOf(SwarmPlanData plan, int k, bool domainSlots) => domainSlots ? plan.Slot[k] : 0;

        /// <summary>The look of a group of plan units (frame 0): mean prism, modal tier, mean facing, mean spindle.</summary>
        static void Look(SwarmPlanData plan, List<int> m, out Vector3 half, out int tier, out Vector3 face, out Vector2 sp) =>
            Look(plan, m.ToArray(), out half, out tier, out face, out sp);

        static void Look(SwarmPlanData plan, int[] m, out Vector3 half, out int tier, out Vector3 face, out Vector2 sp)
        {
            half = Vector3.Zero; face = Vector3.Zero; sp = Vector2.Zero;
            var tc = new int[3];
            foreach (int k in m) { half += plan.HalfF[0][k]; face += plan.Face[0][k]; sp += plan.SpF[0][k]; tc[Math.Clamp(plan.TierF[0][k], 0, 2)]++; }
            half /= m.Length; sp /= m.Length;
            float fl = face.Length(); face = fl > 1e-6f ? face / fl : Vector3.UnitZ;
            tier = 0; for (int q = 1; q < 3; q++) if (tc[q] > tc[tier]) tier = q;
        }

        /// <summary>sort_model._kmeans: Lloyd's, 40 iterations, a random distinct start (an empty
        /// cluster keeps its centre).</summary>
        static int[] KMeans(double[][] P, int K, Random rng)
        {
            int n = P.Length;
            var idx = new int[n]; for (int i = 0; i < n; i++) idx[i] = i;
            for (int i = 0; i < K; i++) { int j = i + rng.Next(n - i); (idx[i], idx[j]) = (idx[j], idx[i]); }
            var c = new double[K][]; for (int k = 0; k < K; k++) c[k] = (double[])P[idx[k]].Clone();
            var a = new int[n];
            for (int it = 0; it < 40; it++)
            {
                for (int i = 0; i < n; i++)
                {
                    double best = double.MaxValue;
                    for (int k = 0; k < K; k++)
                    {
                        double d = 0; for (int q = 0; q < 3; q++) { double x = P[i][q] - c[k][q]; d += x * x; }
                        if (d < best) { best = d; a[i] = k; }
                    }
                }
                for (int k = 0; k < K; k++)
                {
                    var s = new double[3]; int cnt = 0;
                    for (int i = 0; i < n; i++) if (a[i] == k) { cnt++; for (int q = 0; q < 3; q++) s[q] += P[i][q]; }
                    if (cnt > 0) for (int q = 0; q < 3; q++) c[k][q] = s[q] / cnt;
                }
            }
            return a;
        }

        static double[] Inverse3(double[] m, out double det)
        {
            double a = m[0], b = m[1], c = m[2], d = m[3], e = m[4], f = m[5], g = m[6], h = m[7], i = m[8];
            double A = e * i - f * h, B = -(d * i - f * g), C = d * h - e * g;
            det = a * A + b * B + c * C;
            double r = 1.0 / det;
            return new[]
            {
                A * r, -(b * i - c * h) * r, (b * f - c * e) * r,
                B * r, (a * i - c * g) * r, -(a * f - c * d) * r,
                C * r, -(a * h - b * g) * r, (a * e - b * d) * r,
            };
        }
    }

    public sealed class SwarmSortCore : ISwarmCore
    {
        public readonly SwarmPlanData[] Plans;   // indexed by research element (0 Charge .. 3 Time)
        public readonly SwarmSortParams C;
        public readonly int Cap;

        // ── per tadpole (struct of arrays) ──
        public readonly Vector3[] Pos, Vel, Facing;
        public readonly int[] Elem, Dom, Age, MoltTo, Fate, FKey, XferRole, XferPlan;
        public readonly bool[] Active, Hatched;
        public readonly float[] Startle, Molt, Energy;
        /// <summary>Per-tadpole wander velocity (OU state; zero unless Wander > 0).</summary>
        public readonly Vector3[] Wand;

        // ── per swarm ──
        public int PlanIx = -1, Clock, Deaths;
        public float ThreatLevel;
        public Vector3 Anchor, SwimTarget;
        public Vector3 Heading = Vector3.UnitX;
        public Vector3 BX = Vector3.UnitX, BY = Vector3.UnitY, BZ = Vector3.UnitZ;
        public readonly float[] Stomach = new float[4];
        public readonly List<SwarmEvent> Events = new();
        /// <summary>slot -> domain (research); -1 where a region has no domain. One region in the game.</summary>
        public readonly int[] Perm = { 0, 1, 2 };
        public readonly int[] RoleOfDom = { 0, -1, -1 };

        readonly Random _rng;
        int _cand = -1, _candN, _layHoldUntil, _settleUntil = int.MinValue;
        bool _permSet;
        // per-step scratch (Step allocates nothing)
        readonly bool[] _live;
        readonly int[] _type, _wk, _liveIx;
        readonly Vector3[] _xb, _grad, _fCol, _fAdh, _fSwap, _newVel, _ff;
        readonly float[] _st, _wA, _wB;   // _wA/_wB: per-slot OU decay and drive (constants of the params)
        readonly bool[] _upd;
        readonly float _inertiaK;   // Inertia^Frac
        readonly int[] _cnt = new int[4], _members, _occ, _cen = new int[16], _cenM = new int[16], _ec = new int[4];
        readonly float[] _need, _want = new float[16], _fill = new float[16], _deficit = new float[16], _fl = new float[4];
        readonly int[,] _dcen = new int[4, 3];
        readonly int[] _hold = new int[9];
        readonly bool[] _dpresent = new bool[3];
        // neighbour hash
        const int HG = 4096;
        readonly int[] _nb = new int[27];
        readonly int[] _cellStart = new int[HG], _cellCount = new int[HG], _fill2 = new int[HG];
        readonly int[] _sorted, _cellOf;
        float _hashR;

        public SwarmSortCore(SwarmPlanData[] plansByElement, SwarmSortParams c, int seed)
        {
            Plans = plansByElement; C = c; Cap = Math.Max(1, c.Cap);
            _rng = new Random(seed);
            Pos = new Vector3[Cap]; Vel = new Vector3[Cap]; Facing = new Vector3[Cap];
            Elem = new int[Cap]; Dom = new int[Cap]; Age = new int[Cap]; MoltTo = new int[Cap];
            Fate = new int[Cap]; FKey = new int[Cap]; XferRole = new int[Cap]; XferPlan = new int[Cap];
            Active = new bool[Cap]; Hatched = new bool[Cap];
            Startle = new float[Cap]; Molt = new float[Cap]; Energy = new float[Cap];
            _live = new bool[Cap]; _type = new int[Cap]; _wk = new int[Cap]; _liveIx = new int[Cap]; _members = new int[Cap];
            _xb = new Vector3[Cap]; _grad = new Vector3[Cap]; _fCol = new Vector3[Cap]; _fAdh = new Vector3[Cap];
            _fSwap = new Vector3[Cap]; _newVel = new Vector3[Cap]; _ff = new Vector3[Cap]; _st = new float[Cap];
            _sorted = new int[Cap]; _cellOf = new int[Cap];
            _inertiaK = MathF.Pow(c.Inertia, Math.Max(1, c.Frac));
            Wand = new Vector3[Cap]; _wA = new float[Cap]; _wB = new float[Cap]; _upd = new bool[Cap];
            for (int i = 0; i < Cap; i++)
            {
                double frac01 = (i * 0.6180339) % 1.0;
                float tau = MathF.Max(1e-3f, c.WanderTau * (float)(0.6 + 0.8 * frac01));
                _wA[i] = MathF.Exp(-1f / tau); _wB[i] = MathF.Sqrt(1f - _wA[i] * _wA[i]) * c.Wander;
            }
            int maxW = 1;
            foreach (var p in plansByElement) maxW = Math.Max(maxW, SwarmSortCode.For(p, c).W.Length);
            _occ = new int[maxW]; _need = new float[maxW];
            for (int i = 0; i < Cap; i++) { Facing[i] = Vector3.UnitZ; XferPlan[i] = -1; }
            _hashR = MathF.Max(c.RAdh, MathF.Max(c.RSwap, MathF.Max(c.R0, c.RelayR)));
        }

        // ──────────────────────────────────────────────────────────────── ISwarmCore

        public SwarmPlanData Plan => Plans[Math.Max(0, PlanIx)];
        public SwarmSortCode Code => SwarmSortCode.For(Plan, C);
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
        int[] ISwarmCore.Dom => Dom;
        List<SwarmEvent> ISwarmCore.Events => Events;
        int ISwarmCore.Clock => Clock;
        int ISwarmCore.PlanIx => Math.Max(0, PlanIx);
        Vector3 ISwarmCore.Anchor => Anchor;
        Vector3 ISwarmCore.BX => BX;
        Vector3 ISwarmCore.BY => BY;
        Vector3 ISwarmCore.BZ => BZ;
        Vector3 ISwarmCore.SwimTarget { get => SwimTarget; set => SwimTarget = value; }

        /// <summary>Live members, eggs included (a laid egg is a member the glue already shows blooming).</summary>
        public int AliveCount { get { int n = 0; for (int i = 0; i < Cap; i++) if (Active[i]) n++; return n; } }

        /// <summary>A molting member counts as what it is becoming (the field core's rule).</summary>
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
            if (C.KillLayHoldSteps > 0) _layHoldUntil = Math.Max(_layHoldUntil, Clock + C.KillLayHoldSteps);
            Active[i] = false; Hatched[i] = false; Startle[i] = 0; Vel[i] = Vector3.Zero; Wand[i] = Vector3.Zero; Molt[i] = 0; Fate[i] = 0; FKey[i] = 0;
            XferPlan[i] = -1;
        }

        public bool TryGetLook(int i, int element, out Vector3 half, out int tier)
        {
            half = default; tier = 0;
            if (i < 0 || i >= Cap || !Active[i] || !Hatched[i]) return false;
            LookOf(i, element, out half, out tier, out _);
            return true;
        }

        /// <summary>
        /// Starvation (the HOST's decision) sheds a member of the class furthest over its plan count -
        /// the one sitting furthest from its well; else the most misplaced member of all. Never on a
        /// clock: this core has no death of its own.
        /// </summary>
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
                float sur = _cnt[e] - want;
                if (sur > ws) { ws = sur; worst = e; }
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

        /// <summary>The GAME's seed: <paramref name="count"/> hatched tadpoles at the plan's element mix
        /// (every element present) in a small knot at <paramref name="anchor"/>; the body grows out of it.</summary>
        public void Seed(int planElement, int count, Vector3 anchor, Vector3 heading)
        {
            var plan = Plans[planElement];
            count = Math.Min(count, Math.Min(Cap, plan.N));
            var em = LargestRemainder(plan.Mix, count, true);
            var dm = C.DomainSlots && !C.Lineages ? LargestRemainder(plan.SlotMix, count, true) : new[] { count, 0, 0 };
            SeedWith(em, dm, anchor, 2f);
            if (C.DomainSlots && C.Lineages)
            {
                // round 9: the seed lineage settles in ONE region, drawn by tissue size - no region is the controlling
                // colour's by fiat; the rest of the body is unowned until a lineage grows into it
                int ns = Math.Clamp(SwarmSortCode.For(plan, C).NSlots, 1, 3), tot = 0;
                for (int sl = 0; sl < ns; sl++) tot += plan.SlotMix[sl];
                int pick = _rng.Next(Math.Max(1, tot)), r0 = 0;
                for (int sl = 0; sl < ns; sl++) { if (pick < plan.SlotMix[sl]) { r0 = sl; break; } pick -= plan.SlotMix[sl]; }
                Perm[0] = Perm[1] = Perm[2] = -1; Perm[r0] = 0;
                RoleOfDom[0] = r0; RoleOfDom[1] = RoleOfDom[2] = -1;
                _permSet = true;
            }
            SetHeading(heading, snap: true);
            SwimTarget = anchor;
        }

        /// <summary>Seed with explicit element / domain counts (the harness's research seed).</summary>
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
                Pos[i] = centre + spread * Gauss3(); Vel[i] = Vector3.Zero; Wand[i] = Vector3.Zero; Fate[i] = 0; FKey[i] = 0; XferPlan[i] = -1;
            }
            Anchor = centre; Clock = 0; _permSet = false;
            // sort_model._reset: the opening plan is the seed's majority (no event: nothing switched)
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

        // Box-Muller makes normals in PAIRS; the second is kept for the next call (round 6: the wander draws
        // three per member per step, and throwing half away was ~15% of a frac-8 step)
        float _gSpare; bool _gHas;
        float Gauss()
        {
            if (_gHas) { _gHas = false; return _gSpare; }
            double u1 = 1.0 - _rng.NextDouble(), u2 = _rng.NextDouble();
            double r = Math.Sqrt(-2.0 * Math.Log(u1)), th = 2.0 * Math.PI * u2;
            _gSpare = (float)(r * Math.Sin(th)); _gHas = true;
            return (float)(r * Math.Cos(th));
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

        /// <summary>Plan-space vector into the body's current world orientation (identity when not Oriented).</summary>
        public Vector3 Rotate(Vector3 p)
        {
            if (!C.Oriented) return p;
            var plan = Plan;
            var side = Vector3.Cross(plan.SwimAxis, plan.UpAxis);
            return Vector3.Dot(p, plan.SwimAxis) * BX + Vector3.Dot(p, plan.UpAxis) * BY + Vector3.Dot(p, side) * BZ;
        }

        /// <summary>World vector into plan space (the inverse of <see cref="Rotate"/>).</summary>
        public Vector3 InvRotate(Vector3 w)
        {
            if (!C.Oriented) return w;
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
                if (ang > C.Turn)
                {
                    var axis = Vector3.Cross(Heading, h); float al = axis.Length();
                    if (al < 1e-5f) axis = Math.Abs(Heading.Y) < 0.9f ? Vector3.Cross(Heading, Vector3.UnitY) : Vector3.Cross(Heading, Vector3.UnitX);
                    axis = Vector3.Normalize(axis);
                    h = Vector3.Normalize(Vector3.Transform(Heading, Quaternion.CreateFromAxisAngle(axis, C.Turn)));
                }
            }
            Heading = h;
            var upRef = MathF.Abs(Vector3.Dot(h, Vector3.UnitY)) < 0.95f ? Vector3.UnitY : (BY.LengthSquared() > 0.5f ? BY : Vector3.UnitX);
            var y = upRef - Vector3.Dot(upRef, h) * h; float yl = y.Length();
            if (yl < 1e-4f) return;
            y /= yl;
            BX = h; BY = y; BZ = Vector3.Cross(BX, BY);
        }

        int PeriodOf(int planIx) => C.Periods != null && planIx >= 0 && planIx < C.Periods.Length && C.Periods[planIx] > 0 ? C.Periods[planIx] : 8;

        // ──────────────────────────────────────────────────────────────── step

        public void Step(ReadOnlySpan<SwarmPredator> preds)
        {
            int nLive = 0;
            for (int i = 0; i < Cap; i++) if (Active[i] && Hatched[i]) nLive++;
            if (nLive == 0) { Clock++; return; }

            // ── 1. plan by majority, with the dwell (sort_model._step)
            bool contested = DecidePlan();
            var plan = Plans[PlanIx];
            var code = SwarmSortCode.For(plan, C);
            if (!_permSet || Clock % C.RoleEvery == 0) PickPerm(code);

            // ── 2. hatching: an egg becomes a tadpole after HatchSteps
            for (int i = 0; i < Cap; i++)
            {
                if (!Active[i] || Hatched[i]) continue;
                if (++Age[i] >= C.HatchSteps) { Hatched[i] = true; Age[i] = 0; Wand[i] = Vector3.Zero; }
            }
            int nl = 0; Vector3 cen = Vector3.Zero;
            for (int i = 0; i < Cap; i++)
            {
                _live[i] = Active[i] && Hatched[i];
                if (!_live[i]) continue;
                _liveIx[nl++] = i; cen += Pos[i];
            }
            cen /= nl;
            Anchor = cen;

            // ── swimming (GAME): aim the body at its goal before the frame is used this step
            if (C.Oriented)
            {
                var toT = SwimTarget - cen; float dT = toT.Length();
                if (dT > C.AimHold * plan.Radius) SetHeading(toT / dT);
            }
            float swell = 1f + C.Inflate[PlanIx] * ThreatLevel;

            // the code's frame (research: frame 0; game: the plan's animation, interpolated)
            int fA = 0, fB = 0; float fa = 0f, per = PeriodOf(PlanIx);
            if (C.Animate && plan.Order.Length > 0)
            {
                int L = plan.Order.Length, slot = (int)(Clock / per);
                fA = plan.Order[slot % L]; fB = plan.Order[(slot + 1) % L]; fa = (Clock % per) / per;
            }
            var muA = code.Mu[fA]; var muB = code.Mu[fB];

            // ── 3. types and body coordinates
            for (int a = 0; a < nl; a++)
            {
                int i = _liveIx[a];
                int r = EffRole(i);
                _type[i] = r < 0 ? -1 : EffectiveElement(i) * 3 + r;
                _xb[i] = InvRotate(Pos[i] - cen) / swell;
            }

            // ── 4. fate: a member whose fate is stale (newborn, molted, new plan, new region) commits
            //       to the well its type under-occupies most (sort_model: need = w * n - occupied)
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

            // ── 5. own-well chemotaxis (the fated well's log-density gradient), orphans climb the whole body
            float m0 = PlanIx == 3 && C.WellDeadTime >= 0f ? C.WellDeadTime : C.WellDead;
            for (int a = 0; a < nl; a++)
            {
                int i = _liveIx[a], t = _type[i];
                int nw = t >= 0 ? code.WStart[t + 1] - code.WStart[t] : 0;
                Vector3 g; float E;
                _ff[i] = Vector3.Zero;
                if (nw > 0)
                {
                    int k = code.WStart[t] + Fate[i] - 1;
                    var mu = muA[k] + fa * (muB[k] - muA[k]);
                    var d = _xb[i] - mu;
                    g = Mul(code.Inv, k, d); E = 0.5f * Vector3.Dot(d, g);
                    // sortfeel: a FLAT-BOTTOMED well. E stays sort's quadratic (StarvationVictim reads it as
                    // "how far from its place"); only the pull is cut inside m0 sigmas.
                    if (m0 > 0f)
                    {
                        float m = MathF.Sqrt(MathF.Max(2f * E, 1e-12f));
                        g *= MathF.Max(0f, 1f - m0 / m);
                    }
                    _wk[i] = k;
                    if (C.KWellFF != 0f) _ff[i] = C.KWellFF * Rotate((muB[k] - muA[k]) / per) * swell;
                }
                else
                {
                    _wk[i] = -1;
                    E = float.PositiveInfinity; g = Vector3.Zero;
                    for (int t2 = 0; t2 < SwarmSortCode.NT; t2++)
                    {
                        if (code.WStart[t2 + 1] == code.WStart[t2]) continue;
                        float e2 = MixtureEnergy(code, t2, _xb[i], muA, muB, fa, out var g2);
                        if (e2 < E) { E = e2; g = g2; }
                    }
                }
                Energy[i] = E;
                var step = -C.KWell * Rotate(g) / swell;
                float sn = step.Length();
                if (sn > C.WellClip) step *= C.WellClip / sn;
                _grad[i] = step;
            }

            // ── 6. neighbours: collision, differential adhesion, swaps (over a hash grid)
            BuildHash(_live);
            float r0 = C.R0, rAdh2 = C.RAdh * C.RAdh, nearMin = 0.9f * r0, rSwap2 = C.RSwap * C.RSwap;
            for (int a = 0; a < nl; a++) { int i = _liveIx[a]; _fCol[i] = _fAdh[i] = _fSwap[i] = Vector3.Zero; }
            int kFrac = Math.Max(1, C.Frac);
            if (kFrac > 1) NeighboursFractional(code, preds, nl, kFrac, muA, muB, fa);
            else for (int a = 0; a < nl; a++)
            {
                int i = _liveIx[a]; var x = Pos[i];
                CellKey(x, out int cx, out int cy, out int cz);
                int ei = EffectiveElement(i), ri = EffRole(i);
                int nbN = SwarmCoreShared.NeighbourBuckets(cx, cy, cz, HG, _nb);
                for (int nbi = 0; nbi < nbN; nbi++)
                {
                    int gk = _nb[nbi];
                    for (int q = _cellStart[gk], qe = q + _cellCount[gk]; q < qe; q++)
                    {
                        int j = _sorted[q]; if (j == i) continue;
                        var v = Pos[j] - x; float d2 = v.LengthSquared();
                        if (d2 >= rAdh2 && d2 >= rSwap2) continue;
                        float d = MathF.Sqrt(MathF.Max(d2, 1e-12f));
                        if (d < r0) _fCol[i] -= C.KRep * (r0 - d) / d * v;
                        if (d2 < rAdh2 && d > nearMin)
                        {
                            int ej = EffectiveElement(j), rj = EffRole(j);
                            float A = ei == ej && ri == rj ? C.ASame : ei == ej ? C.AElem : ri == rj ? C.ARole : C.AOther;
                            _fAdh[i] += A / d * v;
                        }
                        if (C.Swap > 0f && j > i && d2 < rSwap2)
                        {
                            // each would sit better in the other's spot: they slide past one another
                            float gain = EAt(code, i, _xb[i], muA, muB, fa) + EAt(code, j, _xb[j], muA, muB, fa)
                                       - EAt(code, i, _xb[j], muA, muB, fa) - EAt(code, j, _xb[i], muA, muB, fa);
                            if (gain > C.SwapMargin)
                            {
                                _fSwap[i] += C.Swap * 0.5f * v; _fSwap[j] -= C.Swap * 0.5f * v;
                                var rr = MathF.Max(r0 - d, 0f) / d * v;
                                _fCol[i] += C.KRep * rr; _fCol[j] -= C.KRep * rr;
                            }
                        }
                    }
                }
            }

            // ── 7. the vessel reaction (GAME) and the move: inertia, the element's top speed
            float stSum = 0;
            for (int a = 0; a < nl; a++)
            {
                int i = _liveIx[a]; int e = EffectiveElement(i);
                float st = Startle[i] * C.StartleDecay;
                Vector3 flee = Vector3.Zero;
                if (preds.Length > 0 || Startle[i] > 1e-3f)
                {
                    float relay = MaxStartleNear(Pos[i], C.RelayR, i);
                    for (int p = 0; p < preds.Length; p++)
                        flee += SwarmCoreShared.FleeFrom(preds[p], Pos[i], C.Flee[e], C.Mob[e], C.Sense, C.Lookahead, C.MobSpeed, C.FleeSwirl, ref st);
                    st = MathF.Max(st, relay * C.Relay);
                }
                _st[i] = st; stSum += st;
                Vector3 v;
                if (kFrac == 1 || _upd[i])
                {
                    var want = (1 - 0.8f * st) * (_grad[i] + _ff[i]) + _fCol[i] + _fAdh[i] + _fSwap[i] + C.FleeGain * flee;
                    if (C.Noise > 0f) want += C.Noise * Gauss3();
                    // a member on its phase blends over the k steps it coasted (inertia^k); one a vessel
                    // woke out of phase blends over the one step it is taking
                    float inr = kFrac == 1 || ((i + Clock) % kFrac) != 0 ? C.Inertia : _inertiaK;
                    v = inr * Vel[i] + (1 - inr) * want;
                }
                else v = Vel[i];   // coasting: keeps its last velocity (a low-pass on every change)
                float vmax = C.VMax[e] * (1 + 0.8f * st), sp = v.Length();
                if (sp > vmax) v *= vmax / sp;
                _newVel[i] = v;
            }
            for (int a = 0; a < nl; a++) { int i = _liveIx[a]; Vel[i] = _newVel[i]; Startle[i] = _st[i]; Pos[i] += Vel[i]; }
            // sortfeel: per-tadpole OU wander, added to the position and kept OUT of Vel (the inertia
            // state) - folded into Vel it would integrate into a drift (research sortfeel NOTE)
            if (C.Wander > 0f)
                for (int a = 0; a < nl; a++)
                {
                    int i = _liveIx[a];
                    Wand[i] = _wA[i] * Wand[i] + _wB[i] * Gauss3();
                    Pos[i] += Wand[i];
                }

            // ── the body swims (GAME): every member, eggs included, rides the cruise
            if (C.Oriented && C.Cruise > 0f)
            {
                var toT = SwimTarget - cen; float dT = toT.Length();
                float pace = C.Cruise * Math.Clamp(dT / MathF.Max(1f, 1.5f * plan.Radius), 0f, 1f);
                if (plan.SwimAxis.Y > 0.5f)   // the jellyfish jets in pulses
                    pace *= 0.4f + 0.6f * MathF.Max(0f, MathF.Sin(Clock * 2f * MathF.PI / (2f * per * 7f)));
                Vector3 swim;
                if (dT > C.AimHold * plan.Radius) swim = pace * MathF.Max(0f, Vector3.Dot(Heading, toT / dT)) * Heading;
                else swim = dT > 1e-3f ? pace * toT / dT : Vector3.Zero;   // station-keep: sidle back, do not turn
                swim += BandCorrection(cen);
                for (int i = 0; i < Cap; i++) if (Active[i]) Pos[i] += swim;
            }
            if (!float.IsPositiveInfinity(C.Membrane))
                for (int i = 0; i < Cap; i++)
                {
                    if (!Active[i]) continue;
                    float rad = Pos[i].Length();
                    if (rad > C.Membrane) Pos[i] -= 0.5f * (rad - C.Membrane) * Pos[i] / rad;
                }

            // ── facing: the well's (or type's) facing in the body frame, bent toward a fast swim
            for (int a = 0; a < nl; a++)
            {
                int i = _liveIx[a];
                LookOf(i, EffectiveElement(i), out _, out _, out var lf);
                var f = Rotate(lf);
                float s = Vel[i].Length(), vm = C.VMax[EffectiveElement(i)];
                if (s > 1e-4f) f = Vector3.Lerp(f, Vel[i] / s, Math.Clamp(s / vm, 0f, 1f) * 0.5f);
                float fl = f.Length(); if (fl > 1e-4f) Facing[i] = f / fl;
            }

            // ── molts in progress (GAME: an animation of MoltSteps; research molts are instant)
            for (int i = 0; i < Cap; i++)
            {
                if (!Active[i] || Molt[i] <= 0f) continue;
                Molt[i] += 1f / Math.Max(1, C.MoltSteps);
                if (Molt[i] >= 1f)
                {
                    Elem[i] = MoltTo[i]; Molt[i] = 0f;
                    Events.Add(new SwarmEvent { Kind = SwarmEventKind.MoltDone, Index = i, Other = Elem[i] });
                }
            }

            Clock++;

            // ── 8. composition (paused while contested): the homeostat lays, surplus molts
            if (!contested)
            {
                if (Clock >= _layHoldUntil) Lay(code);
                if (C.Molt && (C.MoltWindow < 0 || Clock < _settleUntil)) MoltStep(code);
            }

            ThreatLevel = 0.85f * ThreatLevel + 0.15f * MathF.Min(1f, C.ThreatGain * stSum / Math.Max(1, nl));
        }

        /// <summary>lite_sortfeel's neighbour pass: only the members re-steering this step (slot + step on
        /// the 1-in-k phase, or woken by a vessel) read their neighbours - rows U, columns everyone - so the
        /// pair work is O(N^2 / k). A swap is seen from the updating side only (each side, on its own
        /// phase, takes its own half). Writes _upd for the move.</summary>
        void NeighboursFractional(SwarmSortCode code, ReadOnlySpan<SwarmPredator> preds, int nl, int k, Vector3[] muA, Vector3[] muB, float fa)
        {
            float r0 = C.R0, rAdh2 = C.RAdh * C.RAdh, nearMin = 0.9f * r0, rSwap2 = C.RSwap * C.RSwap;
            for (int a = 0; a < nl; a++)
            {
                int i = _liveIx[a];
                bool on = ((i + Clock) % k) == 0 || Startle[i] > 0.02f;
                for (int p = 0; !on && p < preds.Length; p++)
                {
                    float reach = (2.5f * C.Sense + 1f) * preds[p].R + C.Lookahead * preds[p].V.Length();
                    on = Vector3.DistanceSquared(Pos[i], preds[p].C) < reach * reach;
                }
                _upd[i] = on;
                if (!on) continue;
                var x = Pos[i];
                CellKey(x, out int cx, out int cy, out int cz);
                int ei = EffectiveElement(i), ri = EffRole(i);
                int nbN = SwarmCoreShared.NeighbourBuckets(cx, cy, cz, HG, _nb);
                for (int nbi = 0; nbi < nbN; nbi++)
                {
                    int gk = _nb[nbi];
                    for (int q = _cellStart[gk], qe = q + _cellCount[gk]; q < qe; q++)
                    {
                        int j = _sorted[q]; if (j == i) continue;
                        var v = Pos[j] - x; float d2 = v.LengthSquared();
                        if (d2 >= rAdh2 && d2 >= rSwap2) continue;
                        float d = MathF.Sqrt(MathF.Max(d2, 1e-12f));
                        if (d < r0) _fCol[i] -= C.KRep * (r0 - d) / d * v;
                        if (d2 < rAdh2 && d > nearMin)
                        {
                            int ej = EffectiveElement(j), rj = EffRole(j);
                            float A = ei == ej && ri == rj ? C.ASame : ei == ej ? C.AElem : ri == rj ? C.ARole : C.AOther;
                            _fAdh[i] += A / d * v;
                        }
                        if (C.Swap > 0f && d2 < rSwap2)
                        {
                            float gain = EAt(code, i, _xb[i], muA, muB, fa) + EAt(code, j, _xb[j], muA, muB, fa)
                                       - EAt(code, i, _xb[j], muA, muB, fa) - EAt(code, j, _xb[i], muA, muB, fa);
                            if (gain > C.SwapMargin)
                            {
                                _fSwap[i] += C.Swap * 0.5f * v;
                                _fCol[i] += C.KRep * (MathF.Max(r0 - d, 0f) / d) * v;
                            }
                        }
                    }
                }
            }
        }

        static Vector3 Mul(float[] inv, int k, Vector3 d)
        {
            int o = k * 9;
            return new Vector3(inv[o] * d.X + inv[o + 1] * d.Y + inv[o + 2] * d.Z,
                               inv[o + 3] * d.X + inv[o + 4] * d.Y + inv[o + 5] * d.Z,
                               inv[o + 6] * d.X + inv[o + 7] * d.Y + inv[o + 8] * d.Z);
        }

        /// <summary>Energy of member a at body point y under its OWN fated well (0 for an orphan, which
        /// has no opinion) - the swap's comparison.</summary>
        float EAt(SwarmSortCode code, int a, Vector3 y, Vector3[] muA, Vector3[] muB, float fa)
        {
            int k = _wk[a]; if (k < 0) return 0f;
            var d = y - (muA[k] + fa * (muB[k] - muA[k]));
            return 0.5f * Vector3.Dot(d, Mul(code.Inv, k, d));
        }

        /// <summary>sort_model.PlanCode.energy_grad: -log of type t's well MIXTURE at y and its gradient.</summary>
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

        // ──────────────────────────────────────────────────────────────── plan, regions

        /// <summary>sort_model: the majority of the hatched members names the plan; a new majority must
        /// hold Dwell steps (contested meanwhile: no laying, no molting) before the swarm commits.</summary>
        bool DecidePlan()
        {
            Array.Clear(_cnt, 0, 4);
            for (int i = 0; i < Cap; i++) if (Active[i] && Hatched[i]) _cnt[EffectiveElement(i)]++;
            int cur = PlanIx, top = ArgMax(_cnt);
            int maj = _cnt[cur] == _cnt[top] ? cur : top;
            if (maj == cur) { _cand = cur; _candN = 0; return false; }
            _candN = _cand == maj ? _candN + 1 : 1;
            _cand = maj;
            if (_candN < C.Dwell) return true;
            Events.Add(new SwarmEvent { Kind = SwarmEventKind.Switched, Index = cur, Other = maj });
            PlanIx = maj; _candN = 0; _permSet = false;
            if (C.MoltWindow >= 0) _settleUntil = Clock + C.MoltWindow;
            if (C.Oriented) SetHeading(Heading, snap: true);
            return false;
        }

        static readonly int[][][] PERMS =
        {
            null,
            new[] { new[] { 0 } },
            new[] { new[] { 0, 1 }, new[] { 1, 0 } },
            new[] { new[] { 0, 1, 2 }, new[] { 0, 2, 1 }, new[] { 1, 0, 2 }, new[] { 1, 2, 0 }, new[] { 2, 0, 1 }, new[] { 2, 1, 0 } },
        };

        /// <summary>sort_model._pick_perm: the slot -> domain map that best matches the census to the
        /// plan's classes (sticky). One region in the game: domain 0 is region 0.</summary>
        void PickPerm(SwarmSortCode code)
        {
            _permSet = true;
            int ns = Math.Clamp(code.NSlots, 1, 3);
            if (C.DomainSlots && C.Lineages) { PickOwners(ns); return; }
            if (!C.DomainSlots || ns == 1)
            {
                Perm[0] = 0; RoleOfDom[0] = 0; RoleOfDom[1] = RoleOfDom[2] = -1;
                if (C.DomainSlots) RoleOfDom[0] = 0;
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
                bool same = true; for (int s = 0; s < ns; s++) same &= Perm[s] == p[s];
                if (same) cost -= 2f;
                if (cost < best) { best = cost; arg = p; }
            }
            RoleOfDom[0] = RoleOfDom[1] = RoleOfDom[2] = -1;
            for (int s = 0; s < ns; s++) { Perm[s] = arg[s]; RoleOfDom[arg[s]] = s; }
        }

        /// <summary>Round 9 lineages: each region is OWNED by the lineage (domain) holding most of its hatched tissue,
        /// one region per lineage, chosen to maximise the tissue owners hold (sticky by 2 members). A region nobody
        /// holds stays unowned (-1) - the tissue any parent may grow, and where a new lineage can be founded.</summary>
        void PickOwners(int ns)
        {
            Array.Clear(_hold, 0, _hold.Length);
            for (int i = 0; i < Cap; i++)
            {
                if (!Active[i] || !Hatched[i]) continue;
                int r = EffRole(i);
                if (r >= 0 && r < ns) _hold[r * 3 + Math.Clamp(Dom[i], 0, 2)]++;
            }
            float best = float.MinValue; int b0 = -1, b1 = -1, b2 = -1;
            for (int a0 = -1; a0 < 3; a0++)
            for (int a1 = -1; a1 < (ns > 1 ? 3 : 0); a1++)
            for (int a2 = -1; a2 < (ns > 2 ? 3 : 0); a2++)
            {
                if ((a0 >= 0 && (a0 == a1 || a0 == a2)) || (a1 >= 0 && a1 == a2)) continue;
                float score = 0f; bool ok = true;
                for (int sl = 0; sl < ns && ok; sl++)
                {
                    int d = sl == 0 ? a0 : sl == 1 ? a1 : a2;
                    if (d < 0) continue;
                    int h = _hold[sl * 3 + d];
                    if (h <= 0) { ok = false; break; }   // nobody owns tissue it does not hold
                    score += h + (Perm[sl] == d ? 2f : 0f);
                }
                if (ok && score > best) { best = score; b0 = a0; b1 = a1; b2 = a2; }
            }
            Perm[0] = b0; Perm[1] = ns > 1 ? b1 : -1; Perm[2] = ns > 2 ? b2 : -1;
            RoleOfDom[0] = RoleOfDom[1] = RoleOfDom[2] = -1;
            for (int sl = 0; sl < 3; sl++) if (Perm[sl] >= 0) RoleOfDom[Perm[sl]] = sl;
        }

        /// <summary>Round 9: the (element, region) of UNOWNED tissue with the largest laying deficit, or -1.</summary>
        int UnownedNeed(int ns, out int element)
        {
            element = -1; int region = -1; float bd = 0f;
            for (int sl = 0; sl < ns; sl++)
            {
                if (Perm[sl] >= 0) continue;
                for (int e = 0; e < 4; e++) if (_deficit[e * 4 + sl] > bd) { bd = _deficit[e * 4 + sl]; element = e; region = sl; }
            }
            return region;
        }

        /// <summary>Round 9: a domain the swarm holds no live member of, drawn uniformly (-1 if it holds all three).</summary>
        int AbsentDomain()
        {
            _dpresent[0] = _dpresent[1] = _dpresent[2] = false;
            for (int i = 0; i < Cap; i++) if (Active[i]) _dpresent[Math.Clamp(Dom[i], 0, 2)] = true;
            int n = 0; for (int d = 0; d < 3; d++) if (!_dpresent[d]) n++;
            if (n == 0) return -1;
            int k = _rng.Next(n);
            for (int d = 0; d < 3; d++) if (!_dpresent[d] && k-- == 0) return d;
            return -1;
        }

        /// <summary>A member's region: its domain's, unless it TRANSFERRED to another region of the
        /// current plan (-1: no region in this plan).</summary>
        /// <summary>Member i's body region in the current plan (-1: none) - the harness's view of the regions.</summary>
        public int RegionOf(int i) => EffRole(i);

        int EffRole(int i) => XferPlan[i] == PlanIx && XferPlan[i] >= 0 ? XferRole[i] : RoleOfDom[Math.Clamp(Dom[i], 0, 2)];

        void LookOf(int i, int element, out Vector3 half, out int tier, out Vector3 face)
        {
            var code = Code;
            int r = Math.Max(0, EffRole(i)), t = element * 3 + r;
            int nw = code.WStart[t + 1] - code.WStart[t];
            if (C.WellLook && nw > 0 && element == EffectiveElement(i) && Fate[i] >= 1 && Fate[i] <= nw
                && FKey[i] == 1 + PlanIx * 16 + element * 4 + r)
            {
                int k = code.WStart[t] + Fate[i] - 1;
                half = code.WellHalf[k]; tier = code.WellTier[k]; face = code.WellFace[k];
                return;
            }
            for (int s = 0; s < 3; s++)
            {
                int tt = element * 3 + (s == 0 ? r : (r + s) % 3);
                if (code.HasType[tt]) { half = code.TypeHalf[tt]; tier = code.TypeTier[tt]; face = code.TypeFace[tt]; return; }
            }
            half = element != 2 ? new Vector3(1, 1, 1) : new Vector3(2f, 0.3f, 0.3f); tier = 0; face = Vector3.UnitZ;
        }

        /// <summary>The research look state (raw prism 3 | tier logits 3 | facing 3 | spindle 2) - the
        /// channels swarm_nca.decode reads - for the harness's export to the unchanged scorer.</summary>
        public void LookState(int i, float[] o)
        {
            var code = Code;
            int e = EffectiveElement(i), r = Math.Max(0, EffRole(i)), t = e * 3 + r;
            LookOf(i, e, out var h, out int tier, out var f);
            var sp = new Vector2(0.3f, 0f);
            int nw = code.WStart[t + 1] - code.WStart[t];
            if (C.WellLook && nw > 0 && Fate[i] >= 1 && Fate[i] <= nw) sp = code.WellSp[code.WStart[t] + Fate[i] - 1];
            else if (code.HasType[t]) sp = code.TypeSp[t];
            var raw = SwarmGridCore.RawPrismOf(h, e);
            o[0] = raw.X; o[1] = raw.Y; o[2] = raw.Z;
            for (int q = 0; q < 3; q++) o[3 + q] = q == tier ? 8f : 0f;
            o[6] = 4f * f.X; o[7] = 4f * f.Y; o[8] = 4f * f.Z;
            float aa = Math.Clamp(sp.X / 0.6f, 0.02f, 0.98f), bb = Math.Clamp(sp.Y / 0.5f, -0.98f, 0.98f);
            o[9] = MathF.Log(aa / (1 - aa)); o[10] = 0.5f * MathF.Log((1 + bb) / (1 - bb));
        }

        Vector3 BandCorrection(Vector3 cen)
        {
            if (C.BandOuter <= 0f) return Vector3.Zero;
            float d = cen.Length();
            if (d < 1e-3f) return Heading * 0.5f;
            float c = Math.Clamp(d, C.BandInner, C.BandOuter);
            return c == d ? Vector3.Zero : (c - d) * 0.2f * cen / d;
        }

        // ──────────────────────────────────────────────────────────────── composition

        /// <summary>Census of (element, region) - region -1 in column 3 - over active members (eggs
        /// included) or hatched only. Indexed e * 4 + column.</summary>
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

        /// <summary>
        /// sort_model._lay - the joint composition homeostat. A hatched parent lays while its own class
        /// is short of the plan and no more filled than its region's least-filled class (+ FillTol); a
        /// parent whose class is full may (PCross) lay its region's most-needed element instead. No
        /// non-majority element may tie the majority through laying. Domain breeds true. GAME: each egg
        /// is PAID for out of the stomach - an unaffordable egg is simply not laid.
        /// </summary>
        void Lay(SwarmSortCode code)
        {
            var cen = _cen; Census(cen, true);
            float pos = 0;
            for (int e = 0; e < 4; e++)
                for (int col = 0; col < 4; col++)
                {
                    int q = e * 4 + col;
                    _want[q] = col < 3 ? MathF.Ceiling(code.Counts[e * 3 + col] * C.Over) : 0f;
                    _deficit[q] = _want[q] - cen[q];
                    if (col < 3 && _deficit[q] > 0) pos += _deficit[q];
                    _fill[q] = _want[q] > 0 ? cen[q] / MathF.Max(_want[q], 1f) : 9f;
                }
            if (pos <= 0) return;
            int nAct = 0, nHat = 0, nFree = 0;
            for (int i = 0; i < Cap; i++) { if (Active[i]) { nAct++; if (Hatched[i]) nHat++; } else nFree++; }
            int room = (int)MathF.Ceiling(code.N * C.Over) - nAct;
            int nlay = Math.Min(C.LayMax, Math.Min(nFree, Math.Min(room, Poisson(MathF.Max(C.LayRate * nHat, 0.2f)))));
            if (nlay <= 0) return;
            Array.Clear(_ec, 0, 4);
            for (int i = 0; i < Cap; i++) if (Active[i]) _ec[EffectiveElement(i)]++;
            int maj = PlanIx, laid = 0, freeFrom = 0, ns = Math.Clamp(code.NSlots, 1, 3);
            bool lineages = C.DomainSlots && C.Lineages;
            // parents in random order (a partial Fisher-Yates over the hatched members)
            int np = 0; for (int i = 0; i < Cap; i++) if (Active[i] && Hatched[i]) _members[np++] = i;
            for (int q = np - 1; q > 0; q--) { int j = _rng.Next(q + 1); (_members[q], _members[j]) = (_members[j], _members[q]); }
            for (int pq = 0; pq < np && laid < nlay; pq++)
            {
                int par = _members[pq];
                int r = RoleOfDom[Math.Clamp(Dom[par], 0, 2)];
                if (r < 0 && !lineages) continue;
                int e = EffectiveElement(par);
                int ce, into = r;
                int low = 0;
                if (r >= 0) for (int e2 = 1; e2 < 4; e2++) if (_fill[e2 * 4 + r] < _fill[low * 4 + r]) low = e2;
                if (r >= 0 && _deficit[e * 4 + r] > 0 && _fill[e * 4 + r] <= _fill[low * 4 + r] + C.FillTol) ce = e;   // breeds true
                else if (r >= 0 && _rng.NextDouble() < C.PCross && _deficit[low * 4 + r] > 0) ce = low;          // its region's most-needed element
                else if (lineages && (into = UnownedNeed(ns, out int ue)) >= 0 && (r < 0 || _rng.NextDouble() < C.PCross)) ce = ue;   // round 9: grow unowned tissue
                else continue;
                if (ce != maj && _ec[ce] + 1 >= _ec[maj])
                {
                    if (_rng.NextDouble() < C.PCross && _deficit[maj * 4 + into] > 0) ce = maj;          // the plan's element keeps its lead
                    else continue;
                }
                int cd = Dom[par];
                bool unowned = into != r;
                // round 9: a child laid into unowned tissue by a parent whose lineage holds a region may FOUND a lineage
                if (unowned && r >= 0 && _rng.NextDouble() < C.Drift) { int nd = AbsentDomain(); if (nd >= 0) cd = nd; }
                if (C.Funded && !SwarmCoreShared.TryFund(Stomach, ce, C.EggCost[ce], C.CrossCost)) continue;   // hungry: waits for a meal
                int j2 = -1; for (int i = freeFrom; i < Cap; i++) if (!Active[i]) { j2 = i; break; }
                if (j2 < 0) break;
                freeFrom = j2 + 1;
                _ec[ce]++;
                var dir = Gauss3(); dir /= MathF.Max(dir.Length(), 1e-6f);
                Pos[j2] = Pos[par] + C.RBud * dir; Vel[j2] = Vector3.Zero; Wand[j2] = Vector3.Zero;
                Elem[j2] = ce; Dom[j2] = cd; Active[j2] = true; Hatched[j2] = false; Age[j2] = 0;
                Startle[j2] = 0; Molt[j2] = 0; Fate[j2] = 0; FKey[j2] = 0; XferPlan[j2] = -1; Facing[j2] = Facing[par];
                if (unowned) { XferRole[j2] = into; XferPlan[j2] = PlanIx; }   // it is tissue of the region it was laid into
                int rq = into;   // the CHILD region
                if (rq >= 0)
                {
                    int q2 = ce * 4 + rq;
                    _deficit[q2] -= 1; cen[q2] += 1; _fill[q2] = cen[q2] / MathF.Max(_want[q2], 1f);
                }
                laid++;
                Events.Add(new SwarmEvent { Kind = SwarmEventKind.Laid, Index = j2, Other = par });
            }
        }

        /// <summary>
        /// sort_model._molt - the LOSSLESS composition corrector. A member of a SURPLUS class (over its
        /// plan count) re-forms its crystal into the least-filled element of its region; if its region
        /// has no deficit (a region the plan has too many of, or none for it) it may TRANSFER to the
        /// neediest region. No non-majority element may tie the majority. Nothing dies.
        /// GAME: the re-forming is an animation of MoltSteps steps (MoltBegan .. MoltDone).
        /// </summary>
        void MoltStep(SwarmSortCode code)
        {
            var cen = _cenM; Census(cen, false);
            for (int e = 0; e < 4; e++) for (int col = 0; col < 4; col++) _want[e * 4 + col] = col < 3 ? code.Counts[e * 3 + col] : 0f;
            int np = 0; for (int i = 0; i < Cap; i++) if (Active[i] && Hatched[i]) _members[np++] = i;
            // roles are read BEFORE any transfer this step (sort_model computes them once)
            for (int q = 0; q < np; q++) _type[_members[q]] = EffRole(_members[q]);
            for (int q = np - 1; q > 0; q--) { int j = _rng.Next(q + 1); (_members[q], _members[j]) = (_members[j], _members[q]); }
            int maj = PlanIx;
            bool lineages = C.DomainSlots && C.Lineages;
            for (int pq = 0; pq < np; pq++)
            {
                int i = _members[pq], r = _type[i];
                if (_rng.NextDouble() > C.MoltRate) continue;
                if (Molt[i] > 0f) continue;   // already re-forming
                int e = Elem[i], rc = r < 0 ? 3 : r;
                // round 9: a STRANGER (in tissue another lineage owns) goes home when its own lineage's region has room
                if (lineages && r >= 0 && Perm[r] >= 0 && Perm[r] != Dom[i])
                {
                    int home = RoleOfDom[Math.Clamp(Dom[i], 0, 2)];
                    bool room = false;
                    if (home >= 0 && home != r) for (int e2 = 0; e2 < 4 && !room; e2++) room = _want[e2 * 4 + home] - cen[e2 * 4 + home] > 0;
                    if (room)
                    {
                        XferPlan[i] = -1;   // EffRole is its lineage's region again
                        cen[e * 4 + rc]--; cen[e * 4 + home]++;
                        continue;
                    }
                }
                if (cen[e * 4 + rc] <= _want[e * 4 + rc]) continue;   // its class is not in surplus
                int r2 = -1;
                if (r >= 0) { for (int e2 = 0; e2 < 4; e2++) if (_want[e2 * 4 + r] - cen[e2 * 4 + r] > 0) { r2 = r; break; } }
                if (r2 < 0 && C.Transfer)
                {
                    float bd = 0;
                    for (int e2 = 0; e2 < 4; e2++) for (int col = 0; col < 3; col++)
                    {
                        if (lineages && Perm[col] >= 0 && Perm[col] != Dom[i]) continue;   // round 9: never into another lineage's tissue
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
                if (ne == e) continue;   // a pure region transfer: same crystal, new tissue
                if (C.MoltSteps <= 0) { Elem[i] = ne; continue; }   // research: instant
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

        void BuildHash(bool[] member)
        {
            Array.Clear(_cellCount, 0, HG);
            for (int i = 0; i < Cap; i++)
            {
                if (!member[i]) continue;
                CellKey(Pos[i], out int a, out int b, out int c);
                _cellOf[i] = Hash(a, b, c); _cellCount[_cellOf[i]]++;
            }
            int s = 0; for (int g = 0; g < HG; g++) { _cellStart[g] = s; s += _cellCount[g]; }
            Array.Clear(_fill2, 0, HG);
            for (int i = 0; i < Cap; i++) if (member[i]) { int g = _cellOf[i]; _sorted[_cellStart[g] + _fill2[g]++] = i; }
        }

        /// <summary>The largest startle among live members within r of x (the startle relay).</summary>
        float MaxStartleNear(Vector3 x, float r, int self)
        {
            CellKey(x, out int cx, out int cy, out int cz);
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
