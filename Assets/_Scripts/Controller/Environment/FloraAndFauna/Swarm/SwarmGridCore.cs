// The swarm fauna's SECOND simulation core - the research model `combo` (the GRID MORPHOGEN made
// LOSSLESS: Tools/NCA/combo_model.py over hgrid2_model.py + hgrid_boid.py + hgrid_core.py, on
// cece/gifted-curie-x2cpd0), ported to plain C# over a struct-of-arrays. Same shape as SwarmFieldCore: no UnityEngine, the
// SAME file compiles and RUNS headless (Tools/Build/swarm_core_harness), the glue (SwarmFauna)
// converts at the boundary. Docs/SWARM_FAUNA.md §8 is the design record.
//
// The difference from `field`, in one line: NOTHING ASSIGNS A TADPOLE A PLACE. Every tadpole reads
// only fields at its own position -
//   * a COARSE grid (G^3 cells of `Cell` voxels) that rides on the swarm's centroid. Per cell and per
//     class (element x domain slot) it holds the plan's WANTED density minus the swarm's ACTUAL
//     density; a tadpole climbs its own class's deficit, plus a weaker pull from the all-class deficit;
//   * a FINE per-class morphogen at the scale of one tadpole: a Gaussian bump per wanted unit of the
//     class MINUS a bump per live tadpole of the class. Sites are claimed by being occupied - two
//     tadpoles that want one site push it between them and one drifts to the next hole. The
//     imperfection comes from competition, not from noise added on top;
//   * FEED-FORWARD: a tadpole takes a share of its nearby same-class targets' own motion, so the
//     body's animation leads rather than lags.
// Composition is the grid's too: a tadpole LAYS into its class's deficit (with probability
// proportional to the deficit; a share takes the most-wanted element), and a class in surplus where
// it is not wanted grows HUNGRY. The plan is the majority element's, with an optional lock.
//
// COMBO'S CORRECTOR (round 5, Docs/SWARM_FAUNA.md §10) replaces hunger with a MOLT: a member of a
// class in surplus re-differentiates into a class in deficit instead of withering - so a morph keeps
// every tadpole (finding 17's stranded debris is gone) and composition converges on the plan:
//   * a staggered molt clock (each member its own rate factor), excess/deficit QUOTAS per class,
//     a guard so a class never molts below its want, and slack for elements the plan wants <=2 of;
//   * ratio target 2: spare capacity takes the body's global residual element mix;
//   * lay cap: no egg while the live count is already the plan's whole grid integral;
//   * migrants: a member stranded in an unwanted cell walks to the nearest wanted one;
//   * sigma_rel: the fine bump width is 1.2 x the plan's own mean neighbour spacing.
// orphan_proxy / transfer2 (STEERING a member toward a domain proxy) are ported but INERT with one
// domain: every plan contains all four elements, so no class is ever an orphan.
// The game animates the molt (MoltBegan .. MoltDone over MoltSteps) like the sort core.
//
// What the GAME adds (all switchable, so the harness can run the research model exactly):
//   * laying is FUNDED out of the stomach (an egg costs eaten volume), as in SwarmFieldCore;
//   * HUNGER NEVER KILLS ON ITS OWN. The research withers a misplaced surplus tadpole after ~25
//     steps; under CLAUDE.md's no-imposed-death law that would be a timer cull. In the game the only
//     self-inflicted death is STARVATION (the host decides: the swarm went unfed), and hunger only
//     picks WHO is shed - the most misplaced surplus member first (Docs/SWARM_FAUNA.md §8.3);
//   * one domain (the cell's controlling colour): every slot maps to domain 0 - unless the swarm is
//     MultiDomain (round 8, Docs/SWARM_FAUNA.md §16.4: DomainSlots + FoodDomains, a newborn wears its food's domain);
//   * swimming (the body cruises toward the host's goal and turns to face its heading), the radial
//     band, the membrane, and the vessel reaction ported from SwarmFieldCore (startle wave, per-
//     element flee, Time mobbing a loitering ship, the pufferfish inflating).
using System;
using System.Collections.Generic;
using System.Numerics;

namespace CosmicShore.Gameplay
{
    /// <summary>The grid model's numbers. Defaults are hgrid2's shipped values
    /// (Tools/NCA/results/hgrid2/params.json) and the research World, unless marked GAME.</summary>
    public sealed class SwarmGridParams
    {
        // ── the coarse grid (hgrid_core / hgrid_boid.BoidCfg) ──
        public int G = 16;
        public float Cell = 6f;
        /// <summary>Snap the grid's centre to whole cells (research). The game turns it off: a body
        /// that SWIMS would otherwise see its target jump a cell at a time.</summary>
        public bool Quant = true;
        public float KClass = 10f, KTotal = 1f, KHome = 0.15f, Persist = 0.6f, Noise = 0.05f;
        public float PLay = 0.1f, PCross = 0.25f;
        public int HatchSteps = 2;
        public float Ease = 0.3f;
        public float StarveRate = 0.04f, StarveTol = 0f, StarveLocal = 0f, DieAt = 1f;
        /// <summary>Plan switching margin (x headcount) and the steps a committed plan holds (combo: 60).</summary>
        public float Hyst = 0f;
        public int Lock = 60;
        /// <summary>hgrid2 `dmap_low`: a plan with k &lt; 3 slots maps them onto domains 0..k-1 (research only:
        /// the game has one domain).</summary>
        public bool DmapLow = true;
        public int Period = 8;
        /// <summary>Per-plan animation period override, indexed by research element (0 = use Period).
        /// hgrid2: the dragonfly (Time) runs at 16 - its wings move 10 voxels a frame.</summary>
        public int[] Periods = { 0, 0, 0, 16 };
        // ── the fine morphogen (hgrid2_model.Cfg) ──
        public float KFine = 2f, Sigma = 3.5f, KFF = 1.5f;
        /// <summary>hgrid2 `sigma_rel`: the fine bump width is this x the plan's own mean nearest-neighbour
        /// spacing (frame 0), per plan. 0 = use the fixed <see cref="Sigma"/> (the pre-round-5 port).</summary>
        public float SigmaRel = 1.2f;
        public bool Settle = true, Interp = true;
        /// <summary>hgrid2 MIGRANTS (`k_mig`): a tadpole where its class is barely wanted (own-class want
        /// below <see cref="MigTh"/> of a bump) heads at KMig x its top speed for the nearest site where its
        /// class is MISSING (score = deficit - distance / MigL). No site is ever assigned. 0 = off.</summary>
        public float KMig = 1f, MigTh = 0.3f, MigL = 40f;

        // ── combo (Tools/NCA/combo_model.py, results/combo/params.json): hgrid2 made LOSSLESS ──
        /// <summary>The composition corrector MOLTS instead of starving: a surplus member re-forms its crystal
        /// into the element its own domain is most short of (combo's `molt`). False = the pre-round-5 game
        /// core's hunger (it only ever picked the starvation victim; nothing corrected a morph's surplus -
        /// finding 17), or, with <see cref="HungerKills"/>, the old research cull.</summary>
        public bool Molt = true;
        /// <summary>Molt clock per step, x a per-member factor in [0.5, 1.5]: a molt takes 17-50 steps of being surplus.</summary>
        public float MoltRate = 0.04f;
        /// <summary>No molt may bring a non-majority element level with the plan's major (`molt_guard`).</summary>
        public bool MoltGuard = true;
        /// <summary>A class may exceed its (ratio) target by this many before it molts (`starve_slack`)...</summary>
        public float StarveSlack = 0f;
        /// <summary>...except the classes of an element the plan holds &lt;= 2 of, which get this (`small_slack`):
        /// a tiny element kept a little over the plan stays viable as a future majority.</summary>
        public float SmallSlack = 2f;
        /// <summary>`ratio` 2 (`ratio_target2`): headcount is not a goal, element RATIOS are - a domain's spare
        /// capacity (members beyond its slot, or a whole domain the plan has no slot for) takes the element mix
        /// the BODY AS A WHOLE is still missing. 0 = molt toward the plan's own counts.</summary>
        public int Ratio = 2;
        /// <summary>`orphan_proxy`: a member whose class the plan wants none of steers by its ELEMENT's best
        /// slot. `transfer2`: extras of an overfull class steer to their element's unfilled sites in another
        /// region. Both are about DOMAINS, so both are inert with the game's one domain (every element of every
        /// plan has a slot there); they are kept so research mode is combo exactly.</summary>
        public bool OrphanProxy = true, Transfer2 = true;
        /// <summary>`lay_cap`: no egg while the body holds this x the plan's headcount (eggs included). 0 = off.
        /// Not creating mass is allowed; aging it out is not.</summary>
        public float LayCap = 1f;
        /// <summary>GAME: a committed molt is ANIMATED over this many steps (MoltBegan .. MoltDone; the glue
        /// shrinks the heart away and re-forms it as the new element). The member counts and steers as its
        /// new element from the first step, exactly as combo flips it. 0 = no events (research).</summary>
        public int MoltSteps = 0;
        // ── the research World (swarm_nca.World) ──
        public float R0 = 2.4f, Rep = 0.4f, RBud = 2.6f;
        public float[] VMax = { 0.8f, 0.8f, 0.8f, 2f };
        public float Membrane = 80f;
        public int Cap = 280;

        // ── GAME ──
        /// <summary>Keep the research's three domain slots (true: only for the harness). The game's
        /// one-colour law maps every slot to domain 0.</summary>
        public bool DomainSlots = false;
        /// <summary>Round 8 MultiDomain (Docs/SWARM_FAUNA.md §16.4): a member's domain slot is its population's
        /// DIET HISTORY - every seed takes slot 0 (the cell's controlling domain) and a newborn takes the slot of the
        /// mass that FUNDED its egg (StomachDom). Off: seeds follow the plan's slot mix and a child keeps its parent's.</summary>
        public bool FoodDomains = false;
        /// <summary>Hunger withers a member by itself (research). False = hunger only picks the
        /// starvation victim when the HOST starves the swarm (the game's law).</summary>
        public bool HungerKills = false;
        /// <summary>Laying pays out of the stomach (game). False = free (research).</summary>
        public bool Funded = true;
        public float[] EggCost = { 1f, 1f, 1f, 1f };
        public float CrossCost = 2f;
        /// <summary>Eggs at most per step (research: unlimited).</summary>
        public int LayMaxPerStep = int.MaxValue;
        /// <summary>GAME: a WOUNDED swarm holds its eggs - every kill postpones laying this many steps
        /// (SwarmFieldParams.KillLayHoldSteps, the same rule): a burst of kills is a window the body
        /// cannot refill, which is what keeps a morph reachable against a fed swarm. 0 = research.</summary>
        public int KillLayHoldSteps = 0;
        /// <summary>Turn the plan into the body's heading (swimming). False = the research's fixed frame.</summary>
        public bool Oriented = true;
        public float Cruise = 0.25f, Turn = 0.03f;
        /// <summary>Within this many body radii of its swim target the body keeps its heading and
        /// station-keeps (it does not chase its own centroid's drift around a goal it has reached).</summary>
        public float AimHold = 1.5f;
        public float BandInner, BandOuter;
        // vessel reaction (SwarmFieldParams' values)
        public float Sense = 2.2f, Relay = 0.8f, StartleDecay = 0.9f, Lookahead = 10f, FleeSwirl = 0.8f, RelayR = 5f;
        public float FleeGain = 2f;
        public float[] Flee = { 0.6f, 0.5f, 1.4f, 2f };
        public float[] Mob = { 0f, 0f, 0f, 1f };
        public float MobSpeed = 1f;
        public float[] Inflate = { 0.45f, 0f, 0f, 0f };
    }

    public sealed class SwarmGridCore : ISwarmCore
    {
        // coarse field channels (hgrid_core): density 12 (elem x slot) | raw prism 12 | Charge tier 3 |
        // facing 12 | spindle 8
        public const int NCLS = 12, O_PR = 12, O_TI = 24, O_FA = 27, O_SP = 39, FIELD_C = 47;

        public readonly SwarmPlanData[] Plans;   // indexed by research element (0 Charge .. 3 Time)
        public readonly SwarmGridParams C;
        public readonly int Cap;

        // ── per tadpole (struct of arrays) ──
        public readonly Vector3[] Pos, Vel, Facing;
        public readonly int[] Elem, Dom, Age, MoltTo;
        /// <summary>The domain a member STEERS by this step (combo's orphan proxy / region transfer); its own
        /// domain unless it is an orphan or an overfull class's extra. Never changes what it IS.</summary>
        public readonly int[] SteerDom;
        public readonly bool[] Active, Hatched;
        public readonly float[] Startle, Hunger, Alpha, Molt;
        /// <summary>The look state the scorer decodes: raw prism 3 | tier logits 3 | facing 3 | spindle 2.</summary>
        public readonly float[] Look;
        public const int LOOK = 11;

        // ── per swarm ──
        public int PlanIx = -1, Clock, Deaths;
        /// <summary>Committed molts so far (combo's `molts` counter).</summary>
        public int Molts;
        public readonly int[] DMap = { 0, 1, 2 };   // slot -> domain
        public float ThreatLevel;
        public Vector3 Anchor, SwimTarget;
        public Vector3 Heading = Vector3.UnitX;
        public Vector3 BX = Vector3.UnitX, BY = Vector3.UnitY, BZ = Vector3.UnitZ;
        public readonly float[] Stomach = new float[4];
        /// <summary>Banked volume by [element * 3 + domain slot] (ISwarmCore.StomachDom).</summary>
        public readonly float[] StomachDom = new float[12];
        public readonly List<SwarmEvent> Events = new();

        readonly Random _rng;
        readonly int _g3, _maxN;
        int _chg = -1000000000, _layHoldUntil;
        // per-step scratch (Step allocates nothing)
        readonly float[] _wanted = new float[NCLS], _have = new float[NCLS], _rel = new float[NCLS], _bestRel = new float[3];
        // combo corrector scratch
        readonly float[] _cw = new float[NCLS], _ch = new float[NCLS], _ct = new float[NCLS], _cex = new float[NCLS], _cdef = new float[NCLS], _cq = new float[NCLS];
        readonly int[] _cand; readonly float[] _candH;
        /// <summary>Per-slot molt-clock rate factor in [0.5, 1.5] (combo's staggered `_U`).</summary>
        readonly float[] _u;
        // migrants scratch (per plan unit)
        readonly float[] _wantT, _haveT;
        static readonly Dictionary<SwarmPlanData, float> s_nn = new();
        readonly int[] _cnt = new int[4], _dcnt = new int[3], _bestE = new int[3];
        readonly List<int> _parents = new();
        int[] _childE;
        // grids
        readonly float[] _dd, _a, _tmp, _def;   // 13 x G^3 (12 for _a/_tmp)
        // per-member scratch
        readonly Vector3[] _pos0, _gOwn, _desire, _newVel;
        readonly float[] _dOwn, _st;
        readonly bool[] _live0;
        readonly int[] _cls;
        // fine-layer scratch
        readonly Vector3[] _tp, _tv; readonly int[] _tc, _liveIx;
        // neighbour hash
        const int HG = 4096;
        readonly int[] _nb = new int[27];
        readonly int[] _cellStart = new int[HG], _cellCount = new int[HG], _fill = new int[HG];
        readonly int[] _sorted, _cellOf;
        readonly Vector3[] _push;

        // the cached plan fields: per plan, per frame, FIELD_C x G^3 (shared by every swarm)
        static readonly Dictionary<(SwarmPlanData, int, float), float[][]> s_fields = new();   // per plan: frames, each built on first use

        public SwarmGridCore(SwarmPlanData[] plansByElement, SwarmGridParams c, int seed)
        {
            Plans = plansByElement; C = c; Cap = Math.Max(1, c.Cap);
            _rng = new Random(seed);
            Pos = new Vector3[Cap]; Vel = new Vector3[Cap]; Facing = new Vector3[Cap];
            Elem = new int[Cap]; Dom = new int[Cap]; Age = new int[Cap]; MoltTo = new int[Cap];
            Active = new bool[Cap]; Hatched = new bool[Cap];
            Startle = new float[Cap]; Hunger = new float[Cap]; Alpha = new float[Cap]; Molt = new float[Cap];
            SteerDom = new int[Cap]; _cand = new int[Cap]; _candH = new float[Cap];
            _u = new float[Cap]; var ur = new Random(1234); for (int i = 0; i < Cap; i++) _u[i] = 0.5f + (float)ur.NextDouble();
            Look = new float[Cap * LOOK];
            _g3 = c.G * c.G * c.G;
            _dd = new float[13 * _g3]; _a = new float[NCLS * _g3]; _tmp = new float[NCLS * _g3];
            _def = new float[13 * _g3];
            _pos0 = new Vector3[Cap]; _gOwn = new Vector3[Cap]; _desire = new Vector3[Cap]; _newVel = new Vector3[Cap]; _dOwn = new float[Cap]; _st = new float[Cap];
            _live0 = new bool[Cap]; _cls = new int[Cap];
            _maxN = 1; foreach (var p in plansByElement) _maxN = Math.Max(_maxN, p.N);
            _tp = new Vector3[_maxN]; _tv = new Vector3[_maxN]; _tc = new int[_maxN]; _liveIx = new int[Cap];
            _wantT = new float[_maxN]; _haveT = new float[_maxN];
            _sorted = new int[Cap]; _cellOf = new int[Cap]; _push = new Vector3[Cap]; _childE = new int[Cap];
            for (int i = 0; i < Cap; i++) Facing[i] = Vector3.UnitZ;
        }

        // ──────────────────────────────────────────────────────────────── ISwarmCore

        public SwarmPlanData Plan => Plans[Math.Max(0, PlanIx)];
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
        float[] ISwarmCore.StomachDom => StomachDom;
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
        public int EffectiveElement(int i) => Elem[i];

        public int[] Counts(bool eff)
        {
            var c = new int[4];
            for (int i = 0; i < Cap; i++) if (Active[i]) c[Elem[i]]++;
            return c;
        }

        public void Kill(int i)
        {
            if (i < 0 || i >= Cap || !Active[i]) return;
            if (C.KillLayHoldSteps > 0) _layHoldUntil = Math.Max(_layHoldUntil, Clock + C.KillLayHoldSteps);
            Active[i] = false; Hatched[i] = false; Hunger[i] = 0; Startle[i] = 0; Vel[i] = Vector3.Zero; Molt[i] = 0f;
            Array.Clear(Look, i * LOOK, LOOK);
        }

        public bool TryGetLook(int i, int element, out Vector3 half, out int tier)
        {
            half = default; tier = 0;
            if (!Active[i] || !Hatched[i]) return false;
            int o = i * LOOK;
            half = PrismH(new Vector3(Look[o], Look[o + 1], Look[o + 2]), element);
            // quantise, so the host's prism re-stamp is a step change rather than a stream of them
            half = new Vector3(MathF.Round(half.X * 16f) / 16f, MathF.Round(half.Y * 16f) / 16f, MathF.Round(half.Z * 16f) / 16f);
            if (element == 0)
            {
                float t0 = Look[o + 3], t1 = Look[o + 4], t2 = Look[o + 5];
                tier = t2 > t0 + 0.2f && t2 > t1 + 0.2f ? 2 : t1 > t0 + 0.2f && t1 > t2 + 0.2f ? 1 : 0;
            }
            return true;
        }

        /// <summary>The hungriest member - a surplus element sitting where its class is not wanted
        /// (the research's starvation rule, as a choice of victim); else one of the most-surplus
        /// element against the plan.</summary>
        public int StarvationVictim()
        {
            int best = -1; float bh = 0f;
            for (int i = 0; i < Cap; i++) if (Active[i] && Hatched[i] && Hunger[i] > bh) { bh = Hunger[i]; best = i; }
            if (best >= 0) return best;
            var counts = Counts(false); var plan = Plan; int n = AliveCount, worst = -1; float ws = float.MinValue;
            for (int e = 0; e < 4; e++)
            {
                if (counts[e] == 0) continue;
                float s = counts[e] - plan.Mix[e] / (float)plan.N * n;
                if (s > ws) { ws = s; worst = e; }
            }
            for (int i = 0; i < Cap; i++) if (Active[i] && Elem[i] == worst) return i;
            return -1;
        }

        // ──────────────────────────────────────────────────────────────── seeding

        /// <summary>
        /// The GAME's seed: <paramref name="count"/> hatched tadpoles at the plan's element mix (every
        /// element the plan uses present) in a small clump at <paramref name="anchor"/> - the research's
        /// seed (a knot of 16 at randn x 2) at the game's headcount. The body then grows out of it.
        /// </summary>
        public void Seed(int planElement, int count, Vector3 anchor, Vector3 heading)
        {
            var plan = Plans[planElement];
            count = Math.Min(count, Math.Min(Cap, plan.N));   // never more than the creature holds
            var em = LargestRemainder(plan.Mix, count, true);
            var dm = C.DomainSlots && !C.FoodDomains ? LargestRemainder(plan.SlotMix, count, true) : new[] { count, 0, 0 };
            SeedWith(em, dm, anchor, 2f);
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
                Active[i] = true; Hatched[i] = true; Alpha[i] = 1f; Elem[i] = es[i]; Dom[i] = ds[i];
                Pos[i] = centre + spread * Gauss3(); Vel[i] = Vector3.Zero;
            }
            Anchor = centre;
            PlanIx = -1; Clock = 0;
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

        // ──────────────────────────────────────────────────────────────── step

        public void Step(ReadOnlySpan<SwarmPredator> preds)
        {
            int G = C.G;
            // ── live set before the step
            int nLive = 0;
            for (int i = 0; i < Cap; i++) { _live0[i] = Active[i] && Hatched[i]; if (_live0[i]) nLive++; }
            if (nLive == 0) { Clock++; return; }

            // ── plan by majority (hgrid_boid.decide_plan) with its lock (hgrid2 Cfg.lock)
            DecidePlan();
            LowDmap();
            var plan = Plans[PlanIx];

            // ── swimming: head for the swim target before the body frame is used this step
            Vector3 cen = Vector3.Zero;
            for (int i = 0; i < Cap; i++) if (_live0[i]) cen += Pos[i];
            cen /= nLive;
            Anchor = cen;
            if (C.Oriented)
            {
                // Re-aim only while the target is a real distance away. A body hovering over its goal
                // (a plant it grazes) has a centroid that jitters around that goal, and chasing the
                // jitter turned the whole plan a few degrees every step - the grid then rebuilt the
                // body around a moving frame and the shape smeared (measured: whale own-plan loss
                // 8.1 vs 5.6 with the frame held; Docs/SWARM_FAUNA.md §8.6).
                var toT = SwimTarget - cen; float dT = toT.Length();
                if (dT > C.AimHold * plan.Radius) SetHeading(toT / dT);
            }
            var centre = C.Quant ? Snap(cen) : cen;
            float swell = 1f + C.Inflate[PlanIx] * ThreatLevel;

            // ── the coarse field (hgrid_boid.FieldBoid.step)
            int per = PeriodOf(PlanIx);
            int F = plan.P.Length;
            int frame = (Clock / per) % F;
            var D = FieldOf(plan, frame);
            // wanted density per (element, DOMAIN): the plan's slots mapped through the slot->domain map
            Array.Clear(_dd, 0, _dd.Length);
            for (int e = 0; e < 4; e++)
                for (int s = 0; s < 3; s++)
                {
                    int d = DMap[s];
                    int src = (e * 3 + s) * _g3, o = (e * 3 + d) * _g3;
                    for (int q = 0; q < _g3; q++) _dd[o + q] += D[src + q];
                }
            // actual density: the live tadpoles splatted by class, blurred once
            Array.Clear(_a, 0, _a.Length);
            for (int i = 0; i < Cap; i++)
            {
                if (!_live0[i]) continue;
                _cls[i] = Elem[i] * 3 + Dom[i];
                Splat(_a, _cls[i], GridCoord(Pos[i], centre, swell), 1f);
            }
            Blur(_a, NCLS);
            var wanted = _wanted;
            int ot = 12 * _g3;
            Array.Clear(_def, ot, _g3);
            Array.Clear(_dd, ot, _g3);
            for (int c = 0; c < NCLS; c++)
            {
                float sum = 0; int o = c * _g3;
                for (int q = 0; q < _g3; q++)
                {
                    float w = _dd[o + q], d = w - _a[o + q];
                    _def[o + q] = d; sum += w;
                    _def[ot + q] += d; _dd[ot + q] += w;   // channel 12: the all-class deficit / total wanted
                }
                wanted[c] = sum;
            }

            // ── per tadpole: climb the deficits
            var have = _have; Array.Clear(have, 0, NCLS);
            Vector3 sumV = Vector3.Zero;
            for (int i = 0; i < Cap; i++)
            {
                if (!_live0[i]) { _pos0[i] = Pos[i]; continue; }
                _pos0[i] = Pos[i];
                have[_cls[i]] += 1f;
                var u = GridCoord(Pos[i], centre, swell);
                int c = _cls[i];
                _dOwn[i] = Sample(_def, c, u);
                float wantTot = Sample(_dd, 12, u);
                var gOwn = SampleGrad(_def, c, u) / swell; var gTot = SampleGrad(_def, 12, u) / swell;
                _gOwn[i] = gOwn;
                bool niche = wanted[c] > 0.5f;
                var v = Rotate(C.KClass * (niche ? gOwn : Vector3.Zero) + C.KTotal * gTot);
                if (wantTot < 0.05f)
                {
                    var cv = centre - Pos[i]; v += C.KHome * cv / MathF.Max(cv.Length(), 1f);
                }
                if (C.Noise > 0) v += C.Noise * Gauss3();
                _desire[i] = v;
            }

            // ── the vessel reaction (GAME; SwarmFieldCore's predator layer, applied to the desire)
            BuildHash(_live0);
            float stSum = 0;
            for (int i = 0; i < Cap; i++)
            {
                if (!_live0[i]) continue;
                float st = Startle[i] * C.StartleDecay, relay = 0;
                Vector3 flee = Vector3.Zero, x = Pos[i];
                if (preds.Length > 0 || Startle[i] > 1e-3f)
                {
                    relay = MaxStartleNear(x, C.RelayR, i);
                    for (int p = 0; p < preds.Length; p++) flee += FleeFrom(preds[p], x, Elem[i], ref st);
                    st = MathF.Max(st, relay * C.Relay);
                }
                _st[i] = st; stSum += st;
                var desired = (1 - 0.8f * st) * _desire[i] + C.FleeGain * flee;
                // persistence (Vel still holds last step's velocity) + the element's top speed (a
                // startled tadpole may outrun it)
                var vel = C.Persist * Vel[i] + (1 - C.Persist) * desired;
                float vmax = C.VMax[Elem[i]] * (1 + 0.8f * st), sp = vel.Length();
                if (sp > vmax) vel *= vmax / sp;
                _newVel[i] = vel;
            }
            for (int i = 0; i < Cap; i++)
            {
                if (_live0[i]) { Vel[i] = _newVel[i]; Startle[i] = _st[i]; Pos[i] += Vel[i]; }
                else { Vel[i] = Vector3.Zero; if (!Active[i]) Startle[i] = 0; }
            }

            // ── the body swims (GAME): every member, eggs included, rides the cruise
            if (C.Oriented && C.Cruise > 0f)
            {
                var toT = SwimTarget - cen; float dT = toT.Length();
                float pace = C.Cruise * Math.Clamp(dT / MathF.Max(1f, 1.5f * plan.Radius), 0f, 1f);
                if (plan.SwimAxis.Y > 0.5f)   // the jellyfish jets in pulses
                    pace *= 0.4f + 0.6f * MathF.Max(0f, MathF.Sin(Clock * 2f * MathF.PI / (2f * per * 7f)));
                // Far from the goal the body swims nose-first along its heading (only as far as it is
                // aimed at the goal). Near it, it STATION-KEEPS: it sidles back toward the goal without
                // turning. Unlike the field core's anchor, nothing pins a grid body's centre - it drifts
                // as it lays and re-sorts - and re-aiming at every drift turned a grazing whale ~200
                // degrees a minute to swim a few voxels back (measured, harness G7b).
                Vector3 swim;
                if (dT > C.AimHold * plan.Radius)
                    swim = pace * MathF.Max(0f, Vector3.Dot(Heading, toT / dT)) * Heading;
                else
                    swim = dT > 1e-3f ? pace * toT / dT : Vector3.Zero;
                swim += BandCorrection(cen);
                for (int i = 0; i < Cap; i++) if (Active[i]) Pos[i] += swim;
            }

            // ── collision (swarm_nca's designed boid) + the membrane
            Collide();

            // ── looks: ease toward the field's attributes for the tadpole's element
            for (int i = 0; i < Cap; i++)
            {
                if (!_live0[i]) continue;
                EaseLook(i, D, GridCoord(Pos[i], centre, swell));
            }

            // ── hatching: an egg blooms over HatchSteps
            for (int i = 0; i < Cap; i++)
            {
                if (Active[i] && !Hatched[i])
                {
                    Age[i]++;
                    Alpha[i] = MathF.Min(1f, Age[i] / (float)C.HatchSteps) * 0.1f;
                    if (Age[i] >= C.HatchSteps) { Hatched[i] = true; Alpha[i] = 1f; Age[i] = 0; }
                }
                else if (Active[i]) { Alpha[i] = 1f; Age[i] = 0; }
            }

            // ── (pre-round-5) hunger: a surplus class (whole swarm) sitting where its class is not wanted.
            // With Molt on, the molt clock replaces it (below, after laying - combo's order).
            for (int i = 0; i < Cap && !C.Molt; i++)
            {
                if (!_live0[i]) continue;
                int c = _cls[i];
                bool surplus = have[c] > (1 + C.StarveTol) * wanted[c] + 1f && _dOwn[i] < C.StarveLocal;
                Hunger[i] = surplus ? MathF.Min(Hunger[i] + C.StarveRate, 4f * C.DieAt) : MathF.Max(0f, Hunger[i] - C.StarveRate);
                if (C.HungerKills && Hunger[i] > C.DieAt)
                {
                    Kill(i); Deaths++;
                    Events.Add(new SwarmEvent { Kind = SwarmEventKind.Starved, Index = i });   // research mode only
                }
            }

            Clock++;

            // ── laying into the deficits (hgrid_boid.FieldBoid._lay) - unless the body is wounded
            if (Clock >= _layHoldUntil) Lay(have, wanted, centre, swell);

            // ── composition: molt instead of starve (combo_model.ComboBoid.starve_staggered)
            AdvanceMolts();
            if (C.Molt) MoltCorrector(plan);

            // ── who steers by which domain's sites (combo's orphan proxy + region transfer)
            SteerDomains(plan);

            // ── the fine morphogen (hgrid2_model.Boid2.fine_disp), then the migrants (Boid2.migrate)
            if (BuildTargets(plan, swell))
            {
                FineLayer(plan);
                if (C.KMig > 0f) Migrate(plan);
            }

            ThreatLevel = 0.85f * ThreatLevel + 0.15f * MathF.Min(1f, 3f * stSum / Math.Max(1, nLive));
            for (int i = 0; i < Cap; i++) if (Active[i] && Hatched[i]) Facing[i] = FacingOf(i);
        }

        // ──────────────────────────────────────────────────────────────── plan

        void DecidePlan()
        {
            var cnt = _cnt; var dcnt = _dcnt; Array.Clear(cnt, 0, 4); Array.Clear(dcnt, 0, 3); int n = 0;
            for (int i = 0; i < Cap; i++) if (_live0[i]) { cnt[Elem[i]]++; dcnt[Dom[i]]++; n++; }
            int maj = 0; for (int e = 1; e < 4; e++) if (cnt[e] > cnt[maj]) maj = e;
            int cur = PlanIx, next = cur;
            if (cur < 0) next = maj;
            else if (cnt[maj] > cnt[Plans[cur].MajorElement] + C.Hyst * n) next = maj;
            if (next == cur) return;
            if (cur >= 0)
            {
                if (C.Lock > 0 && Clock - _chg < C.Lock) return;   // a committed plan holds its lock
                _chg = Clock;
                Events.Add(new SwarmEvent { Kind = SwarmEventKind.Switched, Index = cur, Other = next });
            }
            PlanIx = next;
            MapDomains(Plans[next], dcnt, n);
            if (C.Oriented) SetHeading(Heading, snap: true);
        }

        /// <summary>hgrid2_model.Boid2.low_dmap: a plan with k &lt; 3 slots uses domains 0..k-1 (the larger slot
        /// to the more numerous of them). Run every step after the plan decision; a no-op once it holds.</summary>
        void LowDmap()
        {
            if (!C.DmapLow || !C.DomainSlots || PlanIx < 0) return;
            var plan = Plans[PlanIx];
            int k = 0; for (int q = 0; q < 3; q++) if (plan.SlotMix[q] > 0) k++;
            if (k >= 3) return;
            if (k == 1) { if (DMap[0] != 0) { DMap[0] = 0; DMap[1] = 1; DMap[2] = 2; } return; }
            if ((DMap[0] == 0 && DMap[1] == 1) || (DMap[0] == 1 && DMap[1] == 0)) return;
            var dcnt = _dcnt;
            // domains 0..1 sorted by -count (stable), slots 0..1 sorted by -share (stable)
            int d0 = dcnt[1] > dcnt[0] ? 1 : 0, d1 = 1 - d0;
            int s0 = plan.SlotMix[1] > plan.SlotMix[0] ? 1 : 0, s1 = 1 - s0;
            DMap[s0] = d0; DMap[s1] = d1; DMap[2] = 2;
        }

        static readonly int[][] PERM3 = { new[] { 0, 1, 2 }, new[] { 0, 2, 1 }, new[] { 1, 0, 2 }, new[] { 1, 2, 0 }, new[] { 2, 0, 1 }, new[] { 2, 1, 0 } };

        /// <summary>hgrid_boid.domain_map: the slot->domain permutation that best covers the plan's slot
        /// shares with the domains present. One domain (the game): every slot is domain 0.</summary>
        void MapDomains(SwarmPlanData plan, int[] dcnt, int n)
        {
            if (!C.DomainSlots) { DMap[0] = DMap[1] = DMap[2] = 0; return; }
            float tot = plan.SlotMix[0] + plan.SlotMix[1] + plan.SlotMix[2];
            float best = -1; int[] arg = PERM3[0];
            foreach (var p in PERM3)
            {
                float v = 0;
                for (int s = 0; s < 3; s++) v += MathF.Min(dcnt[p[s]], plan.SlotMix[s] / tot * n);
                if (v > best + 1e-6f) { best = v; arg = p; }
            }
            DMap[0] = arg[0]; DMap[1] = arg[1]; DMap[2] = arg[2];
        }

        int PeriodOf(int planIx) => C.Periods != null && planIx < C.Periods.Length && C.Periods[planIx] > 0 ? C.Periods[planIx] : C.Period;

        Vector3 Snap(Vector3 c) => new(MathF.Round(c.X / C.Cell) * C.Cell, MathF.Round(c.Y / C.Cell) * C.Cell, MathF.Round(c.Z / C.Cell) * C.Cell);

        Vector3 BandCorrection(Vector3 cen)
        {
            if (C.BandOuter <= 0f) return Vector3.Zero;
            float d = cen.Length();
            if (d < 1e-3f) return Heading * 0.5f;
            float c = Math.Clamp(d, C.BandInner, C.BandOuter);
            return c == d ? Vector3.Zero : (c - d) * 0.2f * cen / d;
        }

        // ──────────────────────────────────────────────────────────────── the coarse grid

        /// <summary>World (sim) position -> continuous cell coordinates of the body-frame grid.</summary>
        Vector3 GridCoord(Vector3 x, Vector3 centre, float swell)
        {
            var u = InvRotate(x - centre) / swell;
            float h = (C.G - 1) / 2f;
            return u / C.Cell + new Vector3(h, h, h);
        }

        int Flat(int ix, int iy, int iz) => (ix * C.G + iy) * C.G + iz;

        /// <summary>Trilinear splat of one unit value into channel c (outside corners are dropped).</summary>
        void Splat(float[] grid, int c, Vector3 u, float val)
        {
            int G = C.G;
            int x0 = (int)MathF.Floor(u.X), y0 = (int)MathF.Floor(u.Y), z0 = (int)MathF.Floor(u.Z);
            float fx = u.X - x0, fy = u.Y - y0, fz = u.Z - z0;
            int o = c * _g3;
            for (int dx = 0; dx < 2; dx++)
            {
                int ix = x0 + dx; if (ix < 0 || ix >= G) continue; float wx = dx == 1 ? fx : 1 - fx;
                for (int dy = 0; dy < 2; dy++)
                {
                    int iy = y0 + dy; if (iy < 0 || iy >= G) continue; float wy = dy == 1 ? fy : 1 - fy;
                    for (int dz = 0; dz < 2; dz++)
                    {
                        int iz = z0 + dz; if (iz < 0 || iz >= G) continue; float wz = dz == 1 ? fz : 1 - fz;
                        grid[o + Flat(ix, iy, iz)] += wx * wy * wz * val;
                    }
                }
            }
        }

        /// <summary>Trilinear sample of channel c at u (zero outside: grid_sample, align_corners, zeros).</summary>
        float Sample(float[] grid, int c, Vector3 u)
        {
            int G = C.G;
            int x0 = (int)MathF.Floor(u.X), y0 = (int)MathF.Floor(u.Y), z0 = (int)MathF.Floor(u.Z);
            float fx = u.X - x0, fy = u.Y - y0, fz = u.Z - z0, s = 0;
            int o = c * _g3;
            for (int dx = 0; dx < 2; dx++)
            {
                int ix = x0 + dx; if (ix < 0 || ix >= G) continue; float wx = dx == 1 ? fx : 1 - fx;
                for (int dy = 0; dy < 2; dy++)
                {
                    int iy = y0 + dy; if (iy < 0 || iy >= G) continue; float wy = dy == 1 ? fy : 1 - fy;
                    for (int dz = 0; dz < 2; dz++)
                    {
                        int iz = z0 + dz; if (iz < 0 || iz >= G) continue; float wz = dz == 1 ? fz : 1 - fz;
                        s += wx * wy * wz * grid[o + Flat(ix, iy, iz)];
                    }
                }
            }
            return s;
        }

        /// <summary>hgrid_core.blur: separable [1,2,1]/4 along each axis, zero padding, channels [0, K).</summary>
        void Blur(float[] grid, int K) => Blur(grid, K, _tmp, C.G, _g3);

        static void Blur(float[] grid, int K, float[] tmp, int G, int g3)
        {
            int[] strides = { G * G, G, 1 };
            for (int axis = 0; axis < 3; axis++)
            {
                int st = strides[axis];
                for (int c = 0; c < K; c++)
                {
                    int o = c * g3;
                    // walk every line along this axis: the two other coordinates pick the line
                    for (int a1 = 0; a1 < G; a1++)
                    for (int a2 = 0; a2 < G; a2++)
                    {
                        int start = o + (axis == 0 ? a1 * G + a2 : axis == 1 ? a1 * G * G + a2 : a1 * G * G + a2 * G);
                        float prev = 0f, cur = grid[start];
                        for (int t = 0; t < G; t++)
                        {
                            int q = start + t * st;
                            float next = t < G - 1 ? grid[q + st] : 0f;
                            tmp[q] = 0.25f * prev + 0.5f * cur + 0.25f * next;
                            prev = cur; cur = next;
                        }
                    }
                }
                Array.Copy(tmp, 0, grid, 0, K * g3);
            }
        }

        /// <summary>
        /// hgrid_core.grad then sample: the trilinear sample at u of the central-difference gradient
        /// (replicate padding, per world voxel) of channel c of <paramref name="field"/> - computed at the
        /// eight corners on demand rather than over the whole grid (a body samples a few hundred points;
        /// the grid has 13 x 3 x 4096). Bit-for-bit the same arithmetic as the full grid would give.
        /// </summary>
        Vector3 SampleGrad(float[] field, int c, Vector3 u)
        {
            int G = C.G; float inv = 1f / (2f * C.Cell);
            int x0 = (int)MathF.Floor(u.X), y0 = (int)MathF.Floor(u.Y), z0 = (int)MathF.Floor(u.Z);
            float fx = u.X - x0, fy = u.Y - y0, fz = u.Z - z0;
            int o = c * _g3;
            Vector3 s = Vector3.Zero;
            for (int dx = 0; dx < 2; dx++)
            {
                int ix = x0 + dx; if (ix < 0 || ix >= G) continue; float wx = dx == 1 ? fx : 1 - fx;
                int ixp = Math.Min(ix + 1, G - 1), ixm = Math.Max(ix - 1, 0);
                for (int dy = 0; dy < 2; dy++)
                {
                    int iy = y0 + dy; if (iy < 0 || iy >= G) continue; float wy = dy == 1 ? fy : 1 - fy;
                    int iyp = Math.Min(iy + 1, G - 1), iym = Math.Max(iy - 1, 0);
                    for (int dz = 0; dz < 2; dz++)
                    {
                        int iz = z0 + dz; if (iz < 0 || iz >= G) continue; float wz = dz == 1 ? fz : 1 - fz;
                        int izp = Math.Min(iz + 1, G - 1), izm = Math.Max(iz - 1, 0);
                        float w = wx * wy * wz * inv;
                        s += w * new Vector3(
                            field[o + Flat(ixp, iy, iz)] - field[o + Flat(ixm, iy, iz)],
                            field[o + Flat(ix, iyp, iz)] - field[o + Flat(ix, iym, iz)],
                            field[o + Flat(ix, iy, izp)] - field[o + Flat(ix, iy, izm)]);
                    }
                }
            }
            return s;
        }

        /// <summary>
        /// hgrid_core.PlanFields: one plan frame rasterised onto the grid centred on its own centroid -
        /// the wanted density per (element, slot) plus the per-element looks - splatted and blurred once.
        /// Cached per (plan, frame) and shared by every swarm: with the grid centred on the body, the
        /// field in grid coordinates does not depend on where the body is.
        /// </summary>
        float[] FieldOf(SwarmPlanData plan, int frame) => FieldOf(plan, frame, C.G, C.Cell);

        /// <summary>
        /// One frame's field, built on first use (FIELD_C x G^3 floats = 770 KB at G = 16; a plan's eight
        /// frames are 6.2 MB, so a swarm that only ever grows one plan pays for one). Built lazily per
        /// FRAME so the cost lands across the plan's first animation cycle instead of in one hitch.
        /// </summary>
        static float[] FieldOf(SwarmPlanData plan, int fr, int G, float cell)
        {
            lock (s_fields)
            {
                if (!s_fields.TryGetValue((plan, G, cell), out var frames))
                    s_fields[(plan, G, cell)] = frames = new float[plan.P.Length][];
                if (frames[fr] != null) return frames[fr];
                int g3 = G * G * G;
                var grid = new float[FIELD_C * g3];
                var cen = Vector3.Zero;
                for (int k = 0; k < plan.N; k++) cen += plan.P[fr][k];
                cen /= plan.N;
                var vals = new float[FIELD_C];
                float h = (G - 1) / 2f;
                for (int k = 0; k < plan.N; k++)
                {
                    UnitValues(plan, fr, k, vals);
                    var u = (plan.P[fr][k] - cen) / cell + new Vector3(h, h, h);
                    int x0 = (int)MathF.Floor(u.X), y0 = (int)MathF.Floor(u.Y), z0 = (int)MathF.Floor(u.Z);
                    float fx = u.X - x0, fy = u.Y - y0, fz = u.Z - z0;
                    for (int dx = 0; dx < 2; dx++)
                    {
                        int ix = x0 + dx; if (ix < 0 || ix >= G) continue; float wx = dx == 1 ? fx : 1 - fx;
                        for (int dy = 0; dy < 2; dy++)
                        {
                            int iy = y0 + dy; if (iy < 0 || iy >= G) continue; float wy = dy == 1 ? fy : 1 - fy;
                            for (int dz = 0; dz < 2; dz++)
                            {
                                int iz = z0 + dz; if (iz < 0 || iz >= G) continue; float wz = dz == 1 ? fz : 1 - fz;
                                float w = wx * wy * wz; int q = (ix * G + iy) * G + iz;
                                for (int c = 0; c < FIELD_C; c++) if (vals[c] != 0f) grid[c * g3 + q] += w * vals[c];
                            }
                        }
                    }
                }
                Blur(grid, FIELD_C, new float[FIELD_C * g3], G, g3);
                frames[fr] = grid;
                return grid;
            }
        }

        /// <summary>hgrid_core.unit_values: what one plan unit splats.</summary>
        static void UnitValues(SwarmPlanData plan, int fr, int k, float[] v)
        {
            Array.Clear(v, 0, v.Length);
            int e = plan.Elem[k], sl = plan.Slot[k];
            v[e * 3 + sl] = 1f;
            var rp = RawPrism(plan.HalfF[fr][k], e);
            v[O_PR + e * 3] = rp.X; v[O_PR + e * 3 + 1] = rp.Y; v[O_PR + e * 3 + 2] = rp.Z;
            if (e == 0)
            {
                int t = plan.TierF[fr][k];
                for (int q = 0; q < 3; q++) v[O_TI + q] = MathF.Log((q == t ? 1f : 0f) * 0.9f + 0.1f / 3f);
            }
            var f = plan.Face[fr][k];
            v[O_FA + e * 3] = f.X; v[O_FA + e * 3 + 1] = f.Y; v[O_FA + e * 3 + 2] = f.Z;
            var sp = plan.SpF[fr][k];
            float a = Math.Clamp(sp.X / 0.6f, 0.02f, 0.98f), b = Math.Clamp(sp.Y / 0.5f, -0.98f, 0.98f);
            v[O_SP + e * 2] = MathF.Log(a / (1 - a));
            v[O_SP + e * 2 + 1] = 0.5f * MathF.Log((1 + b) / (1 - b));
        }

        static float Logit(float x, float lo, float hi)
        {
            float s = Math.Clamp((x - lo) / MathF.Max(hi - lo, 1e-6f), 0.02f, 0.98f);
            return MathF.Log(s / (1 - s));
        }

        static float LogitS(float s) { s = Math.Clamp(s, 0.02f, 0.98f); return MathF.Log(s / (1 - s)); }

        /// <summary>hgrid_core.raw_prism, public: the look state swarm_nca.decode reads for a prism of
        /// half-extents h (SwarmSortCore's export uses it).</summary>
        public static Vector3 RawPrismOf(Vector3 h, int e) => RawPrism(h, e);

        /// <summary>hgrid_core.raw_prism: half-extents -> the raw look state (inverse of PrismH).</summary>
        static Vector3 RawPrism(Vector3 h, int e)
        {
            switch (e)
            {
                case 0: return new Vector3(Logit(h.X, 0.3f, 2.2f), Logit(h.Y, 0.3f, 2.2f), Logit(h.Z, 0.3f, 2.2f));
                case 1: return new Vector3(Logit(h.X, 0.5f, 1.8f), Logit(h.Y, 0.5f, 1.8f), Logit(h.Z, 0.5f, 1.8f));
                case 2:
                {
                    float L = h.X, cmax = Math.Clamp(L / 4f, 0.25f, 0.42f), den = MathF.Max(cmax - 0.25f, 1e-3f);
                    return new Vector3(Logit(L, 0.9f, 3.0f), LogitS((h.Y - 0.25f) / den), LogitS((h.Z - 0.25f) / den));
                }
                default:
                {
                    float L = h.X, cmax = Math.Clamp(L / 1.5f, 0.3f, 0.6f), den = MathF.Max(cmax - 0.3f, 1e-3f);
                    return new Vector3(Logit(L, 0.45f, 1.2f), LogitS((h.Y - 0.3f) / den), LogitS((h.Z - 0.3f) / den));
                }
            }
        }

        static float Sig(float x) => 1f / (1f + MathF.Exp(-x));

        /// <summary>swarm_nca.prism_h: the raw look state -> half-extents inside the element's family.</summary>
        public static Vector3 PrismH(Vector3 raw, int e)
        {
            float a = Sig(raw.X), b = Sig(raw.Y), c = Sig(raw.Z);
            switch (e)
            {
                case 0: return new Vector3(0.3f + 1.9f * a, 0.3f + 1.9f * b, 0.3f + 1.9f * c);
                case 1:
                {
                    var o = new Vector3(0.5f + 1.3f * a, 0.5f + 1.3f * b, 0.5f + 1.3f * c);
                    float m = 1.6f * MathF.Min(o.X, MathF.Min(o.Y, o.Z));
                    return Vector3.Min(o, new Vector3(m, m, m));
                }
                case 2:
                {
                    float L = 0.9f + 2.1f * a, cm = Math.Clamp(L / 4f, 0.25f, 0.42f);
                    return new Vector3(L, 0.25f + (cm - 0.25f) * b, 0.25f + (cm - 0.25f) * c);
                }
                default:
                {
                    float L = 0.45f + 0.75f * a, cm = Math.Clamp(L / 1.5f, 0.3f, 0.6f);
                    return new Vector3(L, 0.3f + (cm - 0.3f) * b, 0.3f + (cm - 0.3f) * c);
                }
            }
        }

        /// <summary>hgrid_core.attr_at + the easing in FieldBoid.step: the look state eases toward the
        /// field's attributes for the tadpole's element at its position.</summary>
        void EaseLook(int i, float[] D, Vector3 u)
        {
            int e = Elem[i], o = i * LOOK;
            float dens = 0; for (int s = 0; s < 3; s++) dens += Sample(D, e * 3 + s, u);
            bool has = dens > 1e-3f; float dn = MathF.Max(dens, 1e-3f);
            float dc = 0; for (int s = 0; s < 3; s++) dc += Sample(D, s, u);
            dc = MathF.Max(dc, 1e-3f);
            Span<float> tgt = stackalloc float[LOOK];
            for (int k = 0; k < 3; k++) tgt[k] = has ? Sample(D, O_PR + e * 3 + k, u) / dn : 0f;
            for (int k = 0; k < 3; k++) tgt[3 + k] = Sample(D, O_TI + k, u) / dc;
            var fa = Rotate(new Vector3(Sample(D, O_FA + e * 3, u), Sample(D, O_FA + e * 3 + 1, u), Sample(D, O_FA + e * 3 + 2, u)));
            tgt[6] = 5 * fa.X; tgt[7] = 5 * fa.Y; tgt[8] = 5 * fa.Z;
            for (int k = 0; k < 2; k++) tgt[9 + k] = has ? Sample(D, O_SP + e * 2 + k, u) / dn : 0f;
            for (int k = 0; k < LOOK; k++) Look[o + k] += C.Ease * (tgt[k] - Look[o + k]);
        }

        Vector3 FacingOf(int i)
        {
            int o = i * LOOK;
            var f = new Vector3(Look[o + 6], Look[o + 7], Look[o + 8]);
            float l = f.Length();
            if (l > 1e-3f) return f / l;
            float s = Vel[i].Length();
            return s > 1e-4f ? Vel[i] / s : Facing[i];
        }

        // ──────────────────────────────────────────────────────────────── neighbours, collision

        int CellKey(Vector3 x, out int cx, out int cy, out int cz)
        {
            float r = C.RelayR;
            cx = (int)MathF.Floor(x.X / r); cy = (int)MathF.Floor(x.Y / r); cz = (int)MathF.Floor(x.Z / r);
            return Hash(cx, cy, cz);
        }

        static int Hash(int x, int y, int z) => (int)(((uint)(x * 73856093) ^ (uint)(y * 19349663) ^ (uint)(z * 83492791)) % HG);

        void BuildHash(bool[] member)
        {
            Array.Clear(_cellCount, 0, HG);
            for (int i = 0; i < Cap; i++) if (member[i]) { _cellOf[i] = CellKey(Pos[i], out _, out _, out _); _cellCount[_cellOf[i]]++; }
            int s = 0; for (int g = 0; g < HG; g++) { _cellStart[g] = s; s += _cellCount[g]; }
            Array.Clear(_fill, 0, HG);
            for (int i = 0; i < Cap; i++) if (member[i]) { int g = _cellOf[i]; _sorted[_cellStart[g] + _fill[g]++] = i; }
        }

        /// <summary>The largest startle among live members within r of x (the startle relay). A plain
        /// loop, not a callback: this runs per member per step and must not allocate.</summary>
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

        /// <summary>swarm_nca's designed collision over every ACTIVE member (eggs included), on the
        /// moved positions, then the membrane pull (a soft wall: half the overshoot per step).</summary>
        void Collide()
        {
            BuildHash(Active);
            float k = C.Rep * C.R0 * 0.5f, r02 = C.R0 * C.R0;
            for (int i = 0; i < Cap; i++)
            {
                _push[i] = Vector3.Zero;
                if (!Active[i]) continue;
                var x = Pos[i];
                CellKey(x, out int cx, out int cy, out int cz);
                int nbN = SwarmCoreShared.NeighbourBuckets(cx, cy, cz, HG, _nb);
                for (int nbi = 0; nbi < nbN; nbi++)
                {
                    int g = _nb[nbi];
                    for (int q = _cellStart[g], e = q + _cellCount[g]; q < e; q++)
                    {
                        int j = _sorted[q]; if (j == i) continue;
                        var d = Pos[j] - x; float r2 = d.LengthSquared();
                        if (r2 >= r02) continue;
                        float r = MathF.Sqrt(MathF.Max(r2, 1e-8f)), ov = (C.R0 - r) / C.R0;
                        _push[i] -= k * ov * d / r;
                    }
                }
            }
            for (int i = 0; i < Cap; i++)
            {
                if (!Active[i]) continue;
                var p = Pos[i] + _push[i];
                float rad = MathF.Max(p.Length(), 1e-6f);
                if (rad > C.Membrane) p -= 0.5f * (rad - C.Membrane) * p / rad;
                Pos[i] = p;
            }
        }

        /// <summary>SwarmFieldCore's predator layer (one copy, shared with the sort core:
        /// <see cref="SwarmCoreShared.FleeFrom"/>).</summary>
        Vector3 FleeFrom(SwarmPredator pr, Vector3 x, int el, ref float st) =>
            SwarmCoreShared.FleeFrom(pr, x, C.Flee[el], C.Mob[el], C.Sense, C.Lookahead, C.MobSpeed, C.FleeSwirl, ref st);

        // ──────────────────────────────────────────────────────────────── laying

        /// <summary>
        /// hgrid_boid._lay: growth pressure per class is its relative deficit in the whole swarm. A
        /// class with room breeds true; a share PCross of births (and every birth from a class with no
        /// room) takes the element of the parent's domain with the largest relative deficit. The egg is
        /// laid one bud-length from the parent, up its class's deficit gradient. GAME: each egg is PAID
        /// for out of the stomach - an unaffordable egg is simply not laid.
        /// </summary>
        void Lay(float[] have, float[] want, Vector3 centre, float swell)
        {
            if (C.LayCap > 0f)
            {
                // combo `lay_cap`: no egg while the body holds the plan's headcount, eggs included (the grid's
                // own integral of the wanted density, as combo reads it)
                int nAct = 0; for (int i = 0; i < Cap; i++) if (Active[i]) nAct++;
                float wsum = 0; for (int c = 0; c < NCLS; c++) wsum += want[c];
                if (nAct >= C.LayCap * wsum) return;
            }
            var rel = _rel;
            for (int c = 0; c < NCLS; c++) rel[c] = Math.Clamp((want[c] - have[c]) / MathF.Max(have[c], 1f), 0f, 1f);
            var bestRel = _bestRel; var bestE = _bestE;
            for (int d = 0; d < 3; d++)
            {
                bestRel[d] = rel[d]; bestE[d] = 0;
                for (int e = 1; e < 4; e++) if (rel[e * 3 + d] > bestRel[d]) { bestRel[d] = rel[e * 3 + d]; bestE[d] = e; }
            }
            var parents = _parents; parents.Clear(); var childE = _childE;
            for (int i = 0; i < Cap; i++)
            {
                if (!_live0[i] || !Active[i]) continue;
                int c = Elem[i] * 3 + Dom[i];
                float own = rel[c], alt = bestRel[Dom[i]];
                float p = C.PLay * MathF.Max(own, C.PCross * alt);
                bool ok = (float)_rng.NextDouble() < p;
                bool cross = (float)_rng.NextDouble() < C.PCross;
                if (!ok) continue;
                parents.Add(i);
                childE[i] = own <= 0f || (cross && alt > own) ? bestE[Dom[i]] : Elem[i];   // indexed by member
            }
            if (parents.Count == 0) return;
            for (int q = parents.Count - 1; q > 0; q--) { int j = _rng.Next(q + 1); (parents[q], parents[j]) = (parents[j], parents[q]); }
            int laid = 0;
            foreach (int par in parents)
            {
                if (laid >= C.LayMaxPerStep) break;
                int free = -1; for (int i = 0; i < Cap; i++) if (!Active[i]) { free = i; break; }
                if (free < 0) break;
                int e = childE[par], d = Dom[par];
                if (C.Funded)
                {
                    if (!SwarmCoreShared.TryFund(Stomach, C.FoodDomains ? StomachDom : null, e, C.EggCost[e], C.CrossCost, out int fd)) continue;   // hungry: this parent waits for the next meal
                    if (fd >= 0) d = fd;   // MultiDomain: the domain of the mass that paid
                }
                var u = GridCoord(_pos0[par], centre, swell);
                var g = Rotate(SampleGrad(_def, e * 3 + d, u));
                g /= MathF.Max(g.Length(), 1e-6f);
                var nz = Gauss3(); nz /= MathF.Max(nz.Length(), 1e-6f);
                var dir = g + 0.7f * nz; dir /= MathF.Max(dir.Length(), 1e-6f);
                Active[free] = true; Hatched[free] = false; Age[free] = 0; Alpha[free] = 0f;
                Pos[free] = Pos[par] + C.RBud * dir; Vel[free] = Vector3.Zero; Elem[free] = e; Dom[free] = d;
                Startle[free] = 0; Hunger[free] = 0; Facing[free] = Facing[par];
                Array.Clear(Look, free * LOOK, LOOK);
                laid++;
                Events.Add(new SwarmEvent { Kind = SwarmEventKind.Laid, Index = free, Other = par });
            }
        }

        /// <summary>SwarmFieldCore.TryFund, the same rule (one copy: <see cref="SwarmCoreShared.TryFund"/>).</summary>
        public bool TryFund(int e) => SwarmCoreShared.TryFund(Stomach, e, C.EggCost[e], C.CrossCost);

        // ──────────────────────────────────────────────────────────────── the fine morphogen

        // ──────────────────────────────────────────────────────────────── the lossless corrector (combo)

        /// <summary>The fine bump width for a plan: SigmaRel x its own mean nearest-neighbour spacing
        /// (hgrid2 `sigma_rel`, frame 0), cached per plan; or the fixed Sigma.</summary>
        float SigmaOf(SwarmPlanData plan)
        {
            if (C.SigmaRel <= 0f) return C.Sigma;
            float nn;
            lock (s_nn)
            {
                if (!s_nn.TryGetValue(plan, out nn))
                {
                    var P = plan.P[0]; double sum = 0;
                    for (int a = 0; a < plan.N; a++)
                    {
                        float best = float.MaxValue;
                        for (int b = 0; b < plan.N; b++) if (b != a) best = MathF.Min(best, Vector3.DistanceSquared(P[a], P[b]));
                        sum += MathF.Sqrt(best);
                    }
                    s_nn[plan] = nn = (float)(sum / plan.N);
                }
            }
            return C.SigmaRel * nn;
        }

        /// <summary>The plan's wanted count per class (element x DOMAIN, through the slot map), frame 0.</summary>
        void PlanClassCounts(SwarmPlanData plan, float[] w)
        {
            Array.Clear(w, 0, NCLS);
            for (int k = 0; k < plan.N; k++) w[plan.Elem[k] * 3 + DMap[plan.Slot[k]]] += 1f;
        }

        /// <summary>GAME: a committed molt plays out over MoltSteps (the glue animates the heart re-forming).</summary>
        void AdvanceMolts()
        {
            if (C.MoltSteps <= 0) return;
            for (int i = 0; i < Cap; i++)
            {
                if (!Active[i] || Molt[i] <= 0f) continue;
                Molt[i] += 1f / C.MoltSteps;
                if (Molt[i] >= 1f)
                {
                    Molt[i] = 0f;
                    Events.Add(new SwarmEvent { Kind = SwarmEventKind.MoltDone, Index = i, Other = Elem[i] });
                }
            }
        }

        /// <summary>
        /// combo_model.ComboBoid.starve_staggered with `molt`: hgrid2's identical selection - a member of a class
        /// the swarm holds more of than the plan's (ratio) target, whose own domain has somewhere to put it,
        /// winds a staggered clock - but at the end of the clock it MOLTS: it re-forms its crystal into the
        /// element its own domain is most short of. Quotas: a class molts at most its excess, a receiving class
        /// takes at most its deficit, and no molt may bring a non-majority element level with the plan's major.
        /// Domain never changes. Nothing dies.
        /// </summary>
        void MoltCorrector(SwarmPlanData plan)
        {
            var want = _cw; var have = _ch; var tgt = _ct; var excess = _cex; var deficit = _cdef;
            PlanClassCounts(plan, want);
            Array.Clear(have, 0, NCLS);
            var ecnt = _cnt; Array.Clear(ecnt, 0, 4);
            for (int i = 0; i < Cap; i++) if (Active[i] && Hatched[i]) { have[Elem[i] * 3 + Dom[i]] += 1f; ecnt[Elem[i]]++; }
            // slack: classes of an element the plan holds <= 2 of get SmallSlack
            Span<float> slack = stackalloc float[NCLS];
            for (int e = 0; e < 4; e++)
            {
                float we = want[e * 3] + want[e * 3 + 1] + want[e * 3 + 2];
                for (int d = 0; d < 3; d++) slack[e * 3 + d] = C.SmallSlack >= 0f && we <= 2f ? C.SmallSlack : C.StarveSlack;
            }
            if (C.Ratio == 2) RatioTarget2(want, have, tgt); else Array.Copy(want, tgt, NCLS);
            Span<float> ddef = stackalloc float[3];
            for (int c = 0; c < NCLS; c++)
            {
                excess[c] = MathF.Max(0f, have[c] - MathF.Floor((1 + C.StarveTol) * tgt[c]) - slack[c]);
                deficit[c] = MathF.Max(0f, tgt[c] - have[c]);
                ddef[c % 3] += deficit[c];
            }
            int nc = 0;
            for (int i = 0; i < Cap; i++)
            {
                bool live = Active[i] && Hatched[i];
                int c = Elem[i] * 3 + Dom[i];
                bool moltable = live && excess[c] > 0f && ddef[Dom[i]] > 0.5f;
                Hunger[i] = moltable ? Hunger[i] + C.MoltRate * _u[i] : MathF.Max(0f, Hunger[i] - C.MoltRate);
                if (moltable && Hunger[i] >= 1f) { _cand[nc] = i; _candH[nc] = Hunger[i]; nc++; }
            }
            if (nc == 0 || PlanIx < 0) return;
            // most-advanced clock first (stable on index for ties)
            for (int a = 1; a < nc; a++)
            {
                int ci = _cand[a]; float ch = _candH[a]; int b = a - 1;
                while (b >= 0 && _candH[b] < ch) { _cand[b + 1] = _cand[b]; _candH[b + 1] = _candH[b]; b--; }
                _cand[b + 1] = ci; _candH[b + 1] = ch;
            }
            var quota = _cq; Array.Copy(excess, quota, NCLS);
            int maj = plan.MajorElement;
            Span<float> opts = stackalloc float[4];
            for (int a = 0; a < nc; a++)
            {
                int i = _cand[a], c = Elem[i] * 3 + Dom[i], d = Dom[i], e0 = Elem[i];
                if (quota[c] < 1f) { Hunger[i] = 1f; continue; }
                for (int e = 0; e < 4; e++) opts[e] = deficit[e * 3 + d];
                opts[e0] = 0f;
                if (C.MoltGuard)
                    for (int e = 0; e < 4; e++)
                        if (e != maj && e != e0 && ecnt[e] + 1 >= ecnt[maj] - (e0 == maj ? 1 : 0)) opts[e] = 0f;
                int ne = 0; for (int e = 1; e < 4; e++) if (opts[e] > opts[ne]) ne = e;
                if (opts[ne] < 0.5f) { Hunger[i] = 1f; continue; }
                Elem[i] = ne;
                quota[c] -= 1f; deficit[ne * 3 + d] -= 1f;
                ecnt[e0]--; ecnt[ne]++;
                Hunger[i] = 0f;
                Molts++;
                if (C.MoltSteps > 0)
                {
                    Molt[i] = 1e-3f; MoltTo[i] = ne;
                    Events.Add(new SwarmEvent { Kind = SwarmEventKind.MoltBegan, Index = i, Other = ne });
                }
            }
        }

        /// <summary>combo_model.ComboBoid.ratio_target2: a domain at or above its slot's headcount wants its slot's
        /// counts PLUS its spare capacity in the element mix the whole body still lacks against the plan's ratios.</summary>
        static void RatioTarget2(float[] w, float[] h, float[] t)
        {
            Span<float> wd = stackalloc float[3], hd = stackalloc float[3], mix = stackalloc float[4], resid = stackalloc float[4];
            float n = 0, wall = 0;
            for (int d = 0; d < 3; d++) { for (int e = 0; e < 4; e++) { wd[d] += w[e * 3 + d]; hd[d] += h[e * 3 + d]; } n += hd[d]; }
            for (int e = 0; e < 4; e++) { mix[e] = w[e * 3] + w[e * 3 + 1] + w[e * 3 + 2]; wall += mix[e]; }
            float rs = 0;
            for (int e = 0; e < 4; e++)
            {
                mix[e] /= MathF.Max(wall, 1e-6f);
                float weff = 0;
                for (int d = 0; d < 3; d++) weff += w[e * 3 + d] * MathF.Min(1f, hd[d] / MathF.Max(wd[d], 1e-6f));
                resid[e] = MathF.Max(0f, mix[e] * n - weff); rs += resid[e];
            }
            for (int e = 0; e < 4; e++)
                for (int d = 0; d < 3; d++)
                {
                    float spare = MathF.Max(0f, hd[d] - wd[d]);
                    t[e * 3 + d] = hd[d] >= wd[d] ? w[e * 3 + d] + resid[e] / MathF.Max(rs, 1e-6f) * spare : w[e * 3 + d];
                }
        }

        /// <summary>
        /// combo_model.ComboBoid._proxy_dom: the domain each live member STEERS by. An ORPHAN (a class the plan
        /// wants none of) borrows the domain whose slot wants its element most; with Transfer2, the extras of an
        /// overfull class (the highest slot indices) steer to their element's unfilled sites in another domain.
        /// What a member IS never changes. With one domain every member steers by its own.
        /// </summary>
        void SteerDomains(SwarmPlanData plan)
        {
            for (int i = 0; i < Cap; i++) SteerDom[i] = Dom[i];
            if (!C.OrphanProxy || !C.DomainSlots) return;
            var want = _cw; var have = _ch;
            PlanClassCounts(plan, want);
            Array.Clear(have, 0, NCLS);
            for (int i = 0; i < Cap; i++) if (Active[i] && Hatched[i]) have[Elem[i] * 3 + Dom[i]] += 1f;
            for (int i = 0; i < Cap; i++)
            {
                if (!(Active[i] && Hatched[i])) continue;
                int e = Elem[i];
                if (want[e * 3 + Dom[i]] >= 0.5f) continue;
                int bd = 0; for (int d = 1; d < 3; d++) if (want[e * 3 + d] > want[e * 3 + bd]) bd = d;
                if (want[e * 3 + bd] > 0.5f) SteerDom[i] = bd;
            }
            if (!C.Transfer2) return;
            var h = _ct; Array.Copy(have, h, NCLS);
            for (int e = 0; e < 4; e++)
                for (int d = 0; d < 3; d++)
                {
                    int ex = (int)(h[e * 3 + d] - want[e * 3 + d]);
                    if (ex <= 0 || want[e * 3 + d] < 0.5f) continue;
                    for (int i = Cap - 1; i >= 0 && ex > 0; i--)
                    {
                        if (!(Active[i] && Hatched[i]) || Elem[i] != e || Dom[i] != d) continue;
                        ex--;
                        int dd = 0; for (int q = 1; q < 3; q++) if (want[e * 3 + q] - h[e * 3 + q] > want[e * 3 + dd] - h[e * 3 + dd]) dd = q;
                        if (want[e * 3 + dd] - h[e * 3 + dd] < 0.5f) break;
                        SteerDom[i] = dd; h[e * 3 + dd] += 1f; h[e * 3 + d] -= 1f;
                    }
                }
        }

        // ──────────────────────────────────────────────────────────────── the fine morphogen

        int _nl; float _sigma;

        /// <summary>The fine layer's targets this step (hgrid2_model.Boid2.fine_targets): the plan's units placed at
        /// the live centroid, moving continuously between frames, with their own velocity and class. False when
        /// fewer than two members are live.</summary>
        bool BuildTargets(SwarmPlanData plan, float swell)
        {
            int nl = 0; Vector3 cen = Vector3.Zero;
            for (int i = 0; i < Cap; i++) if (_live0[i] && Active[i] && Hatched[i]) { _liveIx[nl++] = i; cen += _pos0[i]; }
            _nl = nl;
            if (nl < 2) return false;
            cen /= nl;
            if (C.Quant) cen = Snap(cen);
            int per = PeriodOf(PlanIx), F = plan.P.Length;
            int f = (Clock / per) % F, f1 = (f + 1) % F;
            float a = C.Interp ? (Clock % per) / (float)per : 0f;
            Vector3 c0 = Vector3.Zero, c1 = Vector3.Zero;
            for (int k = 0; k < plan.N; k++) { c0 += plan.P[f][k]; c1 += plan.P[f1][k]; }
            c0 /= plan.N; c1 /= plan.N;
            for (int k = 0; k < plan.N; k++)
            {
                var p0 = plan.P[f][k] - c0; var p1 = plan.P[f1][k] - c1;
                _tp[k] = Rotate((p0 + a * (p1 - p0)) * swell) + cen;
                _tv[k] = Rotate((p1 - p0) / per);
                _tc[k] = plan.Elem[k] * 3 + DMap[plan.Slot[k]];
            }
            _sigma = SigmaOf(plan);
            return true;
        }

        /// <summary>
        /// hgrid2_model.Boid2.fine_disp: per live tadpole, climb grad phi_c where phi_c is a Gaussian bump
        /// per wanted unit of its class minus a bump per live tadpole of its class (the target moves
        /// CONTINUOUSLY between the plan's frames), plus feed-forward of the nearby same-class targets'
        /// motion. Gain ramps with how settled the body is (have / want). Clamped at the element's top speed.
        /// A member's class here uses the domain it STEERS by (combo's orphan proxy).
        /// </summary>
        void FineLayer(SwarmPlanData plan)
        {
            if (C.KFine == 0f && C.KFF == 0f) return;
            int nl = _nl;
            float sg = _sigma, s2 = 2f * sg * sg, isig2 = 1f / (sg * sg);
            float kf = C.KFine * (C.Settle ? Math.Clamp(nl / (float)plan.N, 0f, 1f) : 1f);
            float cut2 = 9f * s2;   // exp(-9) ~ 1e-4: beyond three bump widths a bump contributes nothing
            for (int a1 = 0; a1 < nl; a1++)
            {
                int i = _liveIx[a1];
                var x = Pos[i]; int ci = Elem[i] * 3 + SteerDom[i];
                Vector3 gWant = Vector3.Zero, gHave = Vector3.Zero, ff = Vector3.Zero; float wsum = 0;
                for (int k = 0; k < plan.N; k++)
                {
                    if (_tc[k] != ci) continue;
                    var dt = x - _tp[k]; float d2 = dt.LengthSquared(); if (d2 > cut2) continue;
                    float w = MathF.Exp(-d2 / s2);
                    gWant -= w * dt; ff += w * _tv[k]; wsum += w;
                }
                for (int b1 = 0; b1 < nl; b1++)
                {
                    int j = _liveIx[b1]; if (j == i || Elem[j] * 3 + SteerDom[j] != ci) continue;
                    var dx = x - Pos[j]; float d2 = dx.LengthSquared(); if (d2 > cut2) continue;
                    gHave -= MathF.Exp(-d2 / s2) * dx;
                }
                var d = kf * (gWant - gHave) * isig2;
                if (C.KFF != 0f) d += C.KFF * ff / MathF.Max(wsum, 0.3f);
                d *= 1f - 0.8f * Startle[i];
                float vmax = C.VMax[Elem[i]], dn = d.Length();
                if (dn > vmax) d *= vmax / dn;
                _push[i] = d;
            }
            for (int a1 = 0; a1 < nl; a1++) { int i = _liveIx[a1]; Pos[i] += _push[i]; }
        }

        /// <summary>
        /// hgrid2_model.Boid2.migrate: a member where its class is barely wanted (own-class wanted density under
        /// MigTh of a bump) heads, at KMig x its top speed, for the plan site of its class with the best
        /// (deficit - distance / MigL), where deficit = the class's wanted density there minus its members'.
        /// No site is ever assigned - two migrants may pick one site and the fine layer sorts it out.
        /// </summary>
        void Migrate(SwarmPlanData plan)
        {
            int nl = _nl, M = plan.N;
            float sg = _sigma, s2 = 2f * sg * sg, cut2 = 9f * s2;
            bool any = false;
            for (int a1 = 0; a1 < nl; a1++)
            {
                int i = _liveIx[a1]; var x = Pos[i]; int ci = Elem[i] * 3 + SteerDom[i];
                float own = 0;
                for (int k = 0; k < M; k++)
                {
                    if (_tc[k] != ci) continue;
                    float d2 = Vector3.DistanceSquared(x, _tp[k]); if (d2 <= cut2) own += MathF.Exp(-d2 / s2);
                }
                _st[i] = own;   // reuse: own-class wanted density at the member
                if (own < C.MigTh) any = true;
            }
            if (!any) return;
            for (int k = 0; k < M; k++)
            {
                float wt = 0, ht = 0; var p = _tp[k]; int ck = _tc[k];
                for (int q = 0; q < M; q++)
                {
                    if (_tc[q] != ck) continue;
                    float d2 = Vector3.DistanceSquared(p, _tp[q]); if (d2 <= cut2) wt += MathF.Exp(-d2 / s2);
                }
                for (int b1 = 0; b1 < nl; b1++)
                {
                    int j = _liveIx[b1]; if (Elem[j] * 3 + SteerDom[j] != ck) continue;
                    float d2 = Vector3.DistanceSquared(p, Pos[j]); if (d2 <= cut2) ht += MathF.Exp(-d2 / s2);
                }
                _wantT[k] = wt; _haveT[k] = ht;
            }
            for (int a1 = 0; a1 < nl; a1++)
            {
                int i = _liveIx[a1]; if (_st[i] >= C.MigTh) continue;
                var x = Pos[i]; int ci = Elem[i] * 3 + SteerDom[i];
                int best = -1; float bs = float.MinValue;
                for (int k = 0; k < M; k++)
                {
                    if (_tc[k] != ci) continue;
                    float sc = _wantT[k] - _haveT[k] - Vector3.Distance(_tp[k], x) / C.MigL;
                    if (sc > bs) { bs = sc; best = k; }
                }
                if (best < 0) { _push[i] = Vector3.Zero; continue; }
                var dv = _tp[best] - x; float dl = dv.Length();
                float stepL = C.KMig * C.VMax[Elem[i]];
                _push[i] = dl < stepL ? dv : dv * (stepL / MathF.Max(dl, 1e-6f));
            }
            for (int a1 = 0; a1 < nl; a1++)
            {
                int i = _liveIx[a1]; if (_st[i] >= C.MigTh) continue;
                Pos[i] += _push[i];
            }
        }
    }
}
