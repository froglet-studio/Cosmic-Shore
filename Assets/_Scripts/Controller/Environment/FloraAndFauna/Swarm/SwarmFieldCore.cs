// The swarm fauna's SIMULATION CORE - the research model `field` (designed attractor fields +
// boids, Tools/NCA/field_swarm.py on cece/gifted-curie-x2cpd0) ported to plain C# over a
// struct-of-arrays. It is deliberately free of UnityEngine: it uses System.Numerics, so the SAME
// file compiles and RUNS headless (Tools/Build/swarm_core_harness/) and the Unity glue
// (SwarmFauna) only converts at the boundary. Docs/SWARM_FAUNA.md is the design record.
//
// What it is, per step (one fixed tick; the host interpolates what is rendered):
//   1. PLAN CHOICE - the living majority element names the body plan (Mass whale, Space
//      jellyfish, Charge pufferfish, Time dragonfly); a new majority must hold Dwell steps
//      before the swarm commits, and while contested it neither lays nor molts.
//   2. COMPOSITION - molting (a SURPLUS-element tadpole re-forms into a DEFICIT element; the
//      one relaxed constraint, flagged in the doc) and FUNDED laying: an egg is laid beside the
//      nearest empty slot of its element, breeds true (element = parent's), and is PAID FOR out
//      of the host's per-element stomach. Nothing here dies on a clock.
//   3. HOMES - greedy slot assignment (one sort; the research measured it as good as
//      Hungarian), re-solved every ReassignEvery steps or when membership changes.
//   4. STEERING - arrive-to-home with velocity feed-forward, separation + alignment over a hash
//      grid, the morph vortex, and the vessel reaction (startle wave, per-element flee, Time
//      mobbing a loitering ship, the pufferfish inflating).
//   5. SWIMMING - the body's anchor cruises toward the host's swim target and the plan turns
//      to face its heading.
//
// The game's one-colour fauna law collapses the research's domain SLOTS: every tadpole of a
// swarm wears the cell's controlling colour, so domain is not part of the core at all.
using System;
using System.Collections.Generic;
using System.Numerics;

namespace CosmicShore.Gameplay
{
    /// <summary>One body plan: its animated slot cloud (centred, in sim units) and each slot's
    /// element, prism half-extents and tier. Built once from the baked plan JSON.</summary>
    public sealed class SwarmPlanData
    {
        public string Kind = "";
        /// <summary>Research element index of the plan's majority (0 Charge, 1 Mass, 2 Space, 3 Time).</summary>
        public int MajorElement;
        public int N;
        public Vector3[][] P = Array.Empty<Vector3[]>();      // [frame][slot]
        public Vector3[][] Face = Array.Empty<Vector3[]>();   // [frame][slot] unit facing
        public int[] Order = Array.Empty<int>();              // frame order (ping-pong when the loop does not close)
        public int[] Elem = Array.Empty<int>();
        public int[] Tier = Array.Empty<int>();               // 0 plain, 1 danger, 2 shield (Charge only)
        public Vector3[] Half = Array.Empty<Vector3>();       // prism half-extents [long, wide, thin]
        public int[] Mix = new int[4];
        public int FrameSteps = 6;
        /// <summary>The plan's swim axis (+X for whale/puffer/dragonfly, +Y for the jellyfish).</summary>
        public Vector3 SwimAxis = Vector3.UnitX;
        public Vector3 UpAxis = Vector3.UnitY;
        /// <summary>RMS radius of frame 0 - the body's size, used for sense and swim pacing.</summary>
        public float Radius;

        // ── the research's full per-frame target (read by the GRID model, SwarmGridCore) ──
        /// <summary>Each unit's domain SLOT (0..2) in the research plan. The game's one-colour law
        /// collapses slots onto one domain; the research mode of SwarmGridCore keeps them.</summary>
        public int[] Slot = Array.Empty<int>();
        public int[] SlotMix = new int[3];
        /// <summary>Slots the plan uses (1..3).</summary>
        public int NSlots = 1;
        public Vector3[][] HalfF = Array.Empty<Vector3[]>();  // [frame][slot] prism half-extents (identity-clamped)
        public int[][] TierF = Array.Empty<int[]>();          // [frame][slot]
        public Vector2[][] SpF = Array.Empty<Vector2[]>();    // [frame][slot] spindle (len, bend)

        public void Finish()
        {
            Mix = new int[4];
            for (int k = 0; k < N; k++) Mix[Elem[k]]++;
            if (Slot.Length != N) Slot = new int[N];
            SlotMix = new int[3];
            for (int k = 0; k < N; k++) SlotMix[Slot[k]]++;
            NSlots = 0;
            for (int q = 0; q < 3; q++) if (SlotMix[q] > 0) NSlots++;
            NSlots = Math.Max(1, NSlots);
            double s = 0;
            for (int k = 0; k < N; k++) s += P[0][k].LengthSquared();
            Radius = (float)Math.Sqrt(s / Math.Max(1, N));
        }

        /// <summary>
        /// The same creature with <paramref name="m"/> tadpoles where this plan has one (round 7,
        /// Docs/SWARM_FAUNA.md §14). The body is scaled by m^(1/3) so the DENSITY - and with it the
        /// tadpole spacing the sort core's collision and adhesion were tuned at - is unchanged, and each
        /// unit is replaced by m copies on a small Fibonacci sphere around its scaled position (radius
        /// <see cref="DefaultUpsampleRadius"/> x the scaled mean neighbour spacing),
        /// turned by a per-unit hash so the copies do not lattice. Every copy inherits its unit's element,
        /// tier, prism, facing and per-frame look, and the offset is constant across frames, so the
        /// animation is the plan's own. Deterministic: no RNG state. m = 1 returns this plan.
        /// </summary>
        /// <summary>
        /// Copy radius as a fraction of the scaled mean neighbour spacing. Picked by measurement at
        /// density 5 (score_sortfeel, seeds 7/23, worst own-plan loss per element against the loss-8 bar):
        /// 0.15 -> dragonfly 8.8 (FAILS), 0.45 -> 6.6, 0.6 -> 5.6 (others <= 2.1), 0.75 -> 5.5 but space
        /// 2.3. 0.6 minimises the worst loss without pushing the other three up (§14).
        /// </summary>
        public const float DefaultUpsampleRadius = 0.6f;

        public SwarmPlanData Upsample(int m) => Upsample(m, DefaultUpsampleRadius);

        public SwarmPlanData Upsample(int m, float radiusFraction)
        {
            if (m <= 1) return this;
            int F = P.Length, n2 = N * m;
            float s = MathF.Pow(m, 1f / 3f);

            // mean nearest-neighbour spacing at frame 0 (once per plan, at load)
            double nn = 0;
            for (int i = 0; i < N; i++)
            {
                float best = float.MaxValue;
                for (int j = 0; j < N; j++) if (j != i) best = MathF.Min(best, Vector3.DistanceSquared(P[0][i], P[0][j]));
                nn += MathF.Sqrt(best);
            }
            float r = radiusFraction * s * (float)(nn / Math.Max(1, N));

            // m directions on a Fibonacci sphere (m = 2 is antipodal)
            var dirs = new Vector3[m];
            float ga = MathF.PI * (3f - MathF.Sqrt(5f));
            for (int c = 0; c < m; c++)
            {
                float y = 1f - 2f * (c + 0.5f) / m, rr = MathF.Sqrt(MathF.Max(0f, 1f - y * y));
                dirs[c] = new Vector3(MathF.Cos(ga * c) * rr, y, MathF.Sin(ga * c) * rr);
            }

            var d = new SwarmPlanData
            {
                Kind = Kind, MajorElement = MajorElement, N = n2, FrameSteps = FrameSteps, Order = Order,
                SwimAxis = SwimAxis, UpAxis = UpAxis,
                P = new Vector3[F][], Face = new Vector3[F][], HalfF = new Vector3[F][], TierF = new int[F][], SpF = new Vector2[F][],
                Elem = new int[n2], Tier = new int[n2], Half = new Vector3[n2], Slot = new int[n2],
            };
            var off = new Vector3[n2];
            for (int k = 0; k < N; k++)
            {
                // a per-unit rotation from an integer hash (Rodrigues about a hashed axis)
                uint h = (uint)k * 2654435761u ^ 0x9E3779B9u;
                float a1 = (h & 0xFFFF) / 65535f * 2f * MathF.PI; h = h * 1664525u + 1013904223u;
                float zc = (h & 0xFFFF) / 65535f * 2f - 1f; h = h * 1664525u + 1013904223u;
                float a2 = (h & 0xFFFF) / 65535f * 2f * MathF.PI;
                float zr = MathF.Sqrt(MathF.Max(0f, 1f - zc * zc));
                var axis = new Vector3(MathF.Cos(a1) * zr, zc, MathF.Sin(a1) * zr);
                var q = Quaternion.CreateFromAxisAngle(axis, a2);
                for (int c = 0; c < m; c++)
                {
                    int o = k * m + c;
                    off[o] = Vector3.Transform(dirs[c], q) * r;
                    d.Elem[o] = Elem[k];
                    d.Tier[o] = k < Tier.Length ? Tier[k] : 0;
                    d.Half[o] = Half[k];
                    d.Slot[o] = k < Slot.Length ? Slot[k] : 0;
                }
            }
            for (int f = 0; f < F; f++)
            {
                d.P[f] = new Vector3[n2]; d.Face[f] = new Vector3[n2];
                d.HalfF[f] = new Vector3[n2]; d.TierF[f] = new int[n2]; d.SpF[f] = new Vector2[n2];
                for (int k = 0; k < N; k++)
                    for (int c = 0; c < m; c++)
                    {
                        int o = k * m + c;
                        d.P[f][o] = P[f][k] * s + off[o];
                        d.Face[f][o] = Face[f][k];
                        d.HalfF[f][o] = f < HalfF.Length && HalfF[f] != null && k < HalfF[f].Length ? HalfF[f][k] : Half[k];
                        d.TierF[f][o] = f < TierF.Length && TierF[f] != null && k < TierF[f].Length ? TierF[f][k] : d.Tier[o];
                        d.SpF[f][o] = f < SpF.Length && SpF[f] != null && k < SpF[f].Length ? SpF[f][k] : new Vector2(0.3f, 0f);
                    }
            }
            d.Finish();
            return d;
        }

        public void At(float t, Vector3[] outP, Vector3[] outV, Vector3[] outF)
        {
            float u = t / FrameSteps; int L = Order.Length;
            int i = ((int)MathF.Floor(u)) % L; int j = (i + 1) % L; float a = u - MathF.Floor(u);
            var pi = P[Order[i]]; var pj = P[Order[j]];
            var fi = Face[Order[i]]; var fj = Face[Order[j]];
            for (int k = 0; k < N; k++)
            {
                outP[k] = Vector3.Lerp(pi[k], pj[k], a);
                outV[k] = (pj[k] - pi[k]) / FrameSteps;
                var f = Vector3.Lerp(fi[k], fj[k], a); float fl = f.Length();
                outF[k] = fl > 1e-5f ? f / fl : fi[k];
            }
        }
    }

    /// <summary>
    /// The baked plan as it is stored (Tools/Build/swarm_plans.py writes it; JsonUtility reads it in
    /// the game, System.Text.Json in the headless harness). Flat arrays because a nested array does
    /// not survive JsonUtility.
    /// </summary>
    [Serializable]
    public sealed class SwarmPlanJson
    {
        public string kind = "";
        public string name = "";
        public int major, n, frames, frameSteps = 6;
        public int[] order = Array.Empty<int>(), elem = Array.Empty<int>(), tier = Array.Empty<int>();
        public float[] half = Array.Empty<float>(), pos = Array.Empty<float>(), face = Array.Empty<float>();
        public float[] swimAxis = { 1, 0, 0 }, upAxis = { 0, 1, 0 };
        // the per-frame research target the grid model reads (absent in an old bake: frame 0 is reused)
        public int[] slot = Array.Empty<int>(), tierF = Array.Empty<int>();
        public float[] halfF = Array.Empty<float>(), sp = Array.Empty<float>();

        public SwarmPlanData ToPlanData()
        {
            var d = new SwarmPlanData
            {
                Kind = kind, MajorElement = major, N = n, FrameSteps = Math.Max(1, frameSteps),
                Order = order, Elem = elem, Tier = tier,
                SwimAxis = new Vector3(swimAxis[0], swimAxis[1], swimAxis[2]),
                UpAxis = new Vector3(upAxis[0], upAxis[1], upAxis[2]),
                P = new Vector3[frames][], Face = new Vector3[frames][], Half = new Vector3[n],
                Slot = slot.Length == n ? slot : new int[n],
                HalfF = new Vector3[frames][], TierF = new int[frames][], SpF = new Vector2[frames][],
            };
            for (int k = 0; k < n; k++) d.Half[k] = new Vector3(half[3 * k], half[3 * k + 1], half[3 * k + 2]);
            for (int f = 0; f < frames; f++)
            {
                d.P[f] = new Vector3[n]; d.Face[f] = new Vector3[n];
                for (int k = 0; k < n; k++)
                {
                    int o = 3 * (f * n + k);
                    d.P[f][k] = new Vector3(pos[o], pos[o + 1], pos[o + 2]);
                    d.Face[f][k] = new Vector3(face[o], face[o + 1], face[o + 2]);
                }
                d.HalfF[f] = new Vector3[n]; d.TierF[f] = new int[n]; d.SpF[f] = new Vector2[n];
                bool full = halfF.Length == 3 * frames * n && tierF.Length == frames * n && sp.Length == 2 * frames * n;
                for (int k = 0; k < n; k++)
                {
                    int q = f * n + k;
                    d.HalfF[f][k] = full ? new Vector3(halfF[3 * q], halfF[3 * q + 1], halfF[3 * q + 2]) : d.Half[k];
                    d.TierF[f][k] = full ? tierF[q] : (k < tier.Length ? tier[k] : 0);
                    d.SpF[f][k] = full ? new Vector2(sp[2 * q], sp[2 * q + 1]) : new Vector2(0.3f, 0f);
                }
            }
            d.Finish();
            return d;
        }
    }

    /// <summary>A vessel as the swarm sees it, in sim units (velocity per STEP).</summary>
    public struct SwarmPredator { public Vector3 C, V; public float R; }

    /// <summary>The model's numbers. Defaults are the research's shipped values unless noted.</summary>
    public sealed class SwarmFieldParams
    {
        public int Dwell = 12;
        public float KArrive = 0.35f, Accel = 0.45f, SepR = 2f, SepK = 0.5f, AlignR = 5f, AlignK = 0.15f;
        public float[] VMax = { 0.8f, 0.8f, 0.8f, 2f };
        public int ReassignEvery = 8; public float Sticky = 4f;
        // Laying: in the GAME it is funded (see TryFund). Research: 0.04 / 4.
        public float LayRate = 0.02f; public int LayMax = 2; public float RBud = 2.6f;
        public float MoltRate = 0.03f; public int MoltSteps = 10;
        /// <summary>
        /// GAME CHANGE: molting only runs for this many steps after the swarm COMMITS to a new plan
        /// (the morph's clean-up). Research molted at all times, and in the game that made the core
        /// loop impossible: a swarm whose majority a player was killing simply molted its minority
        /// elements back into the majority (3% of the body per step, ~30 a second) and shrank in
        /// proportion instead of changing shape. Measured by the harness, test 3.
        /// </summary>
        public int SettleSteps = 300;
        /// <summary>
        /// GAME CHANGE: a WOUNDED swarm holds its eggs. Every kill postpones laying by this many steps,
        /// so a burst of kills is a window in which the body cannot refill what it lost. Without it, a
        /// fed swarm at the game's fast lay rate re-lays its majority faster than any ship can kill
        /// it and never morphs (measured, harness test 3c). 0 = the research behaviour (no hold).
        /// </summary>
        public int KillLayHoldSteps = 0;
        public int MorphSteps = 60; public float Swirl = 0.9f;
        public float Sense = 2.2f, Relay = 0.8f, StartleDecay = 0.9f, Lookahead = 10f, FleeSwirl = 0.8f;
        public float[] Flee = { 0.6f, 0.5f, 1.4f, 2f };
        public float[] Mob = { 0f, 0f, 0f, 1f };
        public float MobSpeed = 1f;
        /// <summary>Per-plan inflation under threat, indexed by the plan's MAJOR element (Charge puffs).</summary>
        public float[] Inflate = { 0.45f, 0f, 0f, 0f };
        // Swimming (game): anchor cruise per step and max heading turn per step (radians).
        public float Cruise = 0.25f, Turn = 0.03f;
        /// <summary>GAME: the body keeps its heading while its swim target is within this many body radii.</summary>
        public float AimHold = 0.5f;
        public float Membrane = 600f;
        /// <summary>Optional radial band for the ANCHOR (sim units from the origin); 0 = none.</summary>
        public float BandInner, BandOuter;
        // Funding: an egg of element e costs EggCost[e] from Stomach[e]; from any other element's
        // reserve it costs CrossCost times as much (the feeding-ground lever, Docs/SWARM_FAUNA.md).
        public float[] EggCost = { 1f, 1f, 1f, 1f };
        public float CrossCost = 2f;
        /// <summary>Headcount ceiling - never more tadpoles than the largest plan holds.</summary>
        public int Cap = 192;
    }

    /// <summary>What the host reacts to. Starved: a core killed a member by itself - only SwarmGridCore
    /// in its research mode (SwarmGridParams.HungerKills), never in the game.</summary>
    public enum SwarmEventKind { Laid = 0, MoltBegan = 1, MoltDone = 2, Switched = 3, Starved = 4 }

    public struct SwarmEvent
    {
        public SwarmEventKind Kind;
        public int Index, Other;   // Laid: (child, parent). Molt: (index, element). Switched: (fromPlan, toPlan)
    }

    public sealed class SwarmFieldCore : ISwarmCore
    {
        public readonly SwarmPlanData[] Plans;   // indexed by research element (0 Charge .. 3 Time)
        public readonly SwarmFieldParams C;
        public readonly int Cap;

        // ── per tadpole (struct of arrays) ──
        public readonly Vector3[] Pos, Vel, Facing;
        public readonly int[] Elem, Home, MoltTo;
        public readonly float[] Molt, Startle;
        public readonly bool[] Alive;

        // ── per swarm ──
        public int PlanIx, Cand, CandN, MorphFrom = -1, MorphT, LastAssign = -1000000, Clock, SettleUntil;
        public float T, ThreatLevel;
        public Vector3 Anchor;
        public Vector3 Heading = Vector3.UnitX;
        /// <summary>The body's current world basis: where the plan's swim/up/side axes point.</summary>
        public Vector3 BX = Vector3.UnitX, BY = Vector3.UnitY, BZ = Vector3.UnitZ;
        public Vector3 SwimTarget;
        public readonly float[] Stomach = new float[4];
        readonly int[] _dom;   // a field swarm is one colour: every member is slot 0
        public readonly List<SwarmEvent> Events = new();

        readonly Vector3[] _sp, _sv, _sf, _sw;   // _sw: slot positions in the world (sim) this step
        readonly Vector3[] _newVel; readonly float[] _newSt;
        readonly Random _rng;
        int _aliveSig = -1;
        // grid
        const int G = 4096;
        bool _arrived;
        readonly int[] _nb = new int[27];
        readonly int[] _cellStart = new int[G], _cellCount = new int[G], _fill = new int[G];
        readonly int[] _sorted, _cellOf;
        // assignment scratch
        readonly float[] _keys; readonly int[] _ids; readonly int[] _alive; readonly bool[] _ru, _cu; readonly int[] _nh;
        readonly bool[] _taken;

        public SwarmFieldCore(SwarmPlanData[] plansByElement, SwarmFieldParams c, int seed)
        {
            Plans = plansByElement; C = c; Cap = Math.Max(1, c.Cap);
            _rng = new Random(seed);
            Pos = new Vector3[Cap]; Vel = new Vector3[Cap]; Facing = new Vector3[Cap];
            Elem = new int[Cap]; Home = new int[Cap]; MoltTo = new int[Cap]; _dom = new int[Cap];
            Molt = new float[Cap]; Startle = new float[Cap]; Alive = new bool[Cap];
            int mx = 1; foreach (var p in plansByElement) mx = Math.Max(mx, p.N);
            _sp = new Vector3[mx]; _sv = new Vector3[mx]; _sf = new Vector3[mx]; _sw = new Vector3[mx];
            _newVel = new Vector3[Cap]; _newSt = new float[Cap];
            _sorted = new int[Cap]; _cellOf = new int[Cap];
            _keys = new float[Cap * mx]; _ids = new int[Cap * mx]; _alive = new int[Cap];
            _ru = new bool[Cap]; _cu = new bool[mx]; _nh = new int[Cap]; _taken = new bool[mx];
            for (int i = 0; i < Cap; i++) { Home[i] = -1; Facing[i] = Vector3.UnitZ; }
        }

        public SwarmPlanData Plan => Plans[PlanIx];
        public int AliveCount { get { int n = 0; for (int i = 0; i < Cap; i++) if (Alive[i]) n++; return n; } }
        public bool Morphing => MorphFrom >= 0;
        public bool Contested => CandN > 0;
        public bool Settling => Clock < SettleUntil;

        /// <summary>
        /// Seed a starting knot: <paramref name="count"/> tadpoles drawn at the plan's own element
        /// mix, each placed ON a slot of its element (so a newborn swarm already reads as a sparse
        /// ghost of its creature), centred on <paramref name="anchor"/>.
        /// </summary>
        public void Seed(int planElement, int count, Vector3 anchor, Vector3 heading)
        {
            PlanIx = planElement; Cand = PlanIx; Anchor = anchor; SwimTarget = anchor;
            SetHeading(heading, snap: true);
            var plan = Plans[PlanIx];
            plan.At(0f, _sp, _sv, _sf);
            count = Math.Min(count, Math.Min(Cap, plan.N));
            // stratified by element: share of the mix, at least one of every element the plan uses
            var want = new int[4]; int sum = 0;
            for (int e = 0; e < 4; e++)
            {
                if (plan.Mix[e] == 0) continue;
                want[e] = Math.Max(1, (int)MathF.Round(count * plan.Mix[e] / (float)plan.N));
                sum += want[e];
            }
            while (sum > count) { int e = ArgMax(want); want[e]--; sum--; }
            var slots = new List<int>();
            int n = 0;
            for (int e = 0; e < 4; e++)
            {
                slots.Clear();
                for (int k = 0; k < plan.N; k++) if (plan.Elem[k] == e) slots.Add(k);
                for (int q = 0; q < want[e] && slots.Count > 0; q++)
                {
                    int pick = _rng.Next(slots.Count); int k = slots[pick]; slots.RemoveAt(pick);
                    Alive[n] = true; Elem[n] = e; Home[n] = k; _dom[n] = 0;   // every seed: the anchor domain
                    Pos[n] = Rotate(_sp[k]) + Anchor; Vel[n] = Vector3.Zero; Facing[n] = Rotate(_sf[k]);
                    n++;
                }
            }
            LastAssign = -1000000;
        }

        // ──────────────────────────────────────────────────────────────── body frame

        /// <summary>Plan-space vector into the body's current world orientation.</summary>
        public Vector3 Rotate(Vector3 p)
        {
            var plan = Plans[PlanIx];
            var side = Vector3.Cross(plan.SwimAxis, plan.UpAxis);
            return Vector3.Dot(p, plan.SwimAxis) * BX + Vector3.Dot(p, plan.UpAxis) * BY + Vector3.Dot(p, side) * BZ;
        }

        public void SetHeading(Vector3 h, bool snap = false)
        {
            float hl = h.Length(); if (hl < 1e-5f) return; h /= hl;
            if (!snap)
            {
                float cos = Math.Clamp(Vector3.Dot(Heading, h), -1f, 1f);
                float ang = MathF.Acos(cos);
                if (ang > C.Turn)
                {
                    // rotate Heading toward h by C.Turn about their common normal
                    var axis = Vector3.Cross(Heading, h); float al = axis.Length();
                    if (al < 1e-5f) axis = Math.Abs(Heading.Y) < 0.9f ? Vector3.Cross(Heading, Vector3.UnitY) : Vector3.Cross(Heading, Vector3.UnitX);
                    axis = Vector3.Normalize(axis);
                    var q = Quaternion.CreateFromAxisAngle(axis, C.Turn);
                    h = Vector3.Normalize(Vector3.Transform(Heading, q));
                }
            }
            Heading = h;
            // the body basis: swim axis -> heading, up axis -> world up projected off it
            var upRef = MathF.Abs(Vector3.Dot(h, Vector3.UnitY)) < 0.95f ? Vector3.UnitY : (BY.LengthSquared() > 0.5f ? BY : Vector3.UnitX);
            var y = upRef - Vector3.Dot(upRef, h) * h; float yl = y.Length();
            if (yl < 1e-4f) return;
            y /= yl;
            BX = h; BY = y; BZ = Vector3.Cross(BX, BY);
        }

        // ──────────────────────────────────────────────────────────────── step

        public void Step(ReadOnlySpan<SwarmPredator> preds)
        {
            var plan = Plans[PlanIx];

            // 1. plan by element ratios + hysteresis
            var counts = Counts(true);
            int cur = PlanIx, top = ArgMax(counts);
            int maj = counts[cur] == counts[top] ? cur : top;
            bool contested = false;
            if (maj != cur)
            {
                if (Cand == maj) CandN++; else { Cand = maj; CandN = 1; }
                if (CandN >= C.Dwell)
                {
                    Events.Add(new SwarmEvent { Kind = SwarmEventKind.Switched, Index = PlanIx, Other = maj });
                    MorphFrom = PlanIx; PlanIx = maj; MorphT = 0; LastAssign = -1000000; CandN = 0; SettleUntil = Clock + C.SettleSteps;
                    for (int i = 0; i < Cap; i++) Molt[i] = 0;
                    plan = Plans[PlanIx];
                    SetHeading(Heading, snap: true);
                }
                else contested = true;
            }
            else { Cand = PlanIx; CandN = 0; }

            // 5. swimming - head toward the swim target (decided before the slots are placed)
            // Re-aim only while the target is a real distance away: a body hovering over its goal has
            // an anchor that jitters around it, and chasing that jitter spun the whole creature ~1000
            // degrees a minute while it grazed (measured; Docs/SWARM_FAUNA.md §7 finding 13).
            // The arrival is LATCHED: a body that has arrived re-aims only once the target is twice the hold
            // distance away. Without the latch the body parks exactly ON the threshold (it cruises until
            // dT = AimHold R, then stops) and its jitter flips it across, re-aiming every few steps.
            var toT = SwimTarget - Anchor; float dT = toT.Length();
            bool reaim = dT > C.AimHold * plan.Radius * (_arrived ? 2f : 1f);
            _arrived = !reaim;
            if (reaim) SetHeading(toT / dT);
            T += 1f; plan.At(T, _sp, _sv, _sf);
            for (int k = 0; k < plan.N; k++) _sw[k] = Rotate(_sp[k]) + Anchor;
            float swell = 1f + C.Inflate[PlanIx] * ThreatLevel;

            // 2. composition (paused while contested)
            if (!contested) { if (Clock < SettleUntil) MoltStep(plan, counts); if (Clock >= _layHoldUntil) Lay(plan); }
            for (int i = 0; i < Cap; i++) if (Alive[i] && Molt[i] > 0)
            {
                Molt[i] += 1f / C.MoltSteps;
                if (Molt[i] >= 1f)
                {
                    Elem[i] = MoltTo[i]; Molt[i] = 0; LastAssign = -1000000;
                    Events.Add(new SwarmEvent { Kind = SwarmEventKind.MoltDone, Index = i, Other = Elem[i] });
                }
            }

            // 3. homes (and a membership change forces a re-solve)
            int sig = AliveSignature();
            if (Clock - LastAssign >= C.ReassignEvery || sig != _aliveSig) { AssignGreedy(plan); LastAssign = Clock; _aliveSig = sig; }

            // 4. steering
            BuildGrid();
            float u = MorphFrom >= 0 ? (float)MorphT / C.MorphSteps : 1f;
            if (MorphFrom >= 0 && u >= 1f) MorphFrom = -1;
            float amp = MorphFrom >= 0 ? C.Swirl * MathF.Sin(MathF.PI * u) : 0f;
            if (MorphFrom >= 0) MorphT++;
            Vector3 spMean = Vector3.Zero;
            for (int k = 0; k < plan.N; k++) spMean += _sp[k];
            spMean = Rotate(spMean / plan.N);
            float stSum = 0;
            for (int i = 0; i < Cap; i++)
            {
                if (!Alive[i]) continue;
                Vector3 x = Pos[i], v = Vel[i], desired = Vector3.Zero;
                if (Home[i] >= 0)
                {
                    var h = Rotate(_sp[Home[i]]) * swell + Anchor;
                    desired = Rotate(_sv[Home[i]]) + C.KArrive * (h - x);
                }
                if (amp > 0)
                {
                    var r = x - Anchor; var tang = Vector3.Cross(BY, r); float tl = tang.Length();
                    if (tl > 1e-3f) desired += amp * tang / tl * MathF.Sqrt(r.Length()) * 0.3f;
                }
                Vector3 sep = Vector3.Zero, vs = Vector3.Zero; int cnt = 0; float st = Startle[i] * C.StartleDecay, relay = 0;
                CellOf(x, out int cx, out int cy, out int cz);
                int nbN = SwarmCoreShared.NeighbourBuckets(cx, cy, cz, G, _nb);
                for (int nbi = 0; nbi < nbN; nbi++)
                {
                    int g = _nb[nbi];
                    for (int q = _cellStart[g], e = q + _cellCount[g]; q < e; q++)
                    {
                        int j = _sorted[q]; if (j == i) continue;
                        var d = x - Pos[j]; float dist = d.Length();
                        if (dist < C.SepR) sep += d / MathF.Max(dist, 1e-3f) * (C.SepR - dist);
                        if (dist < C.AlignR) { vs += Vel[j]; cnt++; relay = MathF.Max(relay, Startle[j]); }
                    }
                }
                Vector3 align = cnt > 0 ? vs / cnt - v : Vector3.Zero;
                Vector3 flee = Vector3.Zero;
                int el = Elem[i];
                for (int p = 0; p < preds.Length; p++)
                {
                    var pr = preds[p];
                    var rel = x - pr.C; float dd = rel.Length(), sense = C.Sense * pr.R, spd = MathF.Max(pr.V.Length(), 1e-6f);
                    var pvn = pr.V / spd; float along = Vector3.Dot(rel, pvn); var lat = rel - along * pvn; float dl = lat.Length();
                    var latn = lat / MathF.Max(dl, 1e-3f);
                    float ahead = along > -pr.R ? Math.Clamp(1 - along / (C.Lookahead * spd + pr.R), 0, 1) : 0;
                    float w = MathF.Max(Math.Clamp(1 - dl / sense, 0, 1) * ahead, Math.Clamp(1 - dd / sense, 0, 1));
                    st = MathF.Max(st, Math.Clamp(1.4f * w, 0, 1));
                    var radial = rel / MathF.Max(dd, 1e-3f);
                    float fk = C.Flee[el];
                    if (spd < C.MobSpeed && C.Mob[el] > 0f)
                    {
                        // a LOITERING ship is mobbed, not fled: orbit at 1.4 radii, swirl around it
                        float mk = C.Mob[el];
                        float wMob = Math.Clamp(1 - dd / (2.5f * sense), 0, 1) * mk;
                        var tang = Vector3.Cross(Vector3.UnitY, radial); float tl = tang.Length();
                        tang = tl > 1e-3f ? tang / tl : Vector3.UnitX;
                        flee += wMob * (0.4f * (1.4f * pr.R - dd) * radial + 1.5f * tang);
                        fk *= 1 - mk;
                    }
                    flee += w * fk * (0.75f * latn + 0.25f * radial + C.FleeSwirl * 0.5f * Vector3.Cross(pvn, latn));
                }
                st = MathF.Max(st, relay * C.Relay);
                var steer = (1 - 0.8f * st) * desired + C.SepK * sep + C.AlignK * align + 2f * flee;
                v = (1 - C.Accel) * v + C.Accel * steer;
                float vmax = C.VMax[el] * (1 + 0.8f * st), sp = v.Length();
                if (sp > vmax) v *= vmax / sp;
                _newVel[i] = v; _newSt[i] = st; stSum += st;
            }
            Vector3 mean = Vector3.Zero; int n = 0;
            for (int i = 0; i < Cap; i++)
            {
                if (!Alive[i]) continue;
                Vel[i] = _newVel[i]; Startle[i] = _newSt[i]; var x = Pos[i] + Vel[i];
                float r = x.Length(); if (r > C.Membrane) x *= C.Membrane / r;
                Pos[i] = x; mean += x; n++;
                // facing: the slot's own facing, bent toward the swim when the tadpole is moving fast
                var f = Home[i] >= 0 ? Rotate(_sf[Home[i]]) : Facing[i];
                float s = Vel[i].Length(), vm = C.VMax[Elem[i]];
                if (s > 1e-4f) f = Vector3.Lerp(f, Vel[i] / s, Math.Clamp(s / vm, 0f, 1f) * 0.5f);
                float fl = f.Length(); if (fl > 1e-4f) Facing[i] = f / fl;
            }

            // the anchor follows the body (translation is free) and cruises toward the swim target;
            // slowing as it arrives so a grazing body hovers over its plant instead of orbiting it
            // cruise only toward a target the body is aimed at and has not reached (a held heading must
            // not carry an arrived body off its goal)
            float aim = reaim ? MathF.Max(0f, Vector3.Dot(Heading, toT / dT)) : 0f;
            float cruise = C.Cruise * aim * Math.Clamp(dT / MathF.Max(1f, 1.5f * plan.Radius), 0f, 1f);
            if (plan.SwimAxis.Y > 0.5f)   // the jellyfish jets in pulses
                cruise *= 0.4f + 0.6f * MathF.Max(0f, MathF.Sin(T * 2f * MathF.PI / (2f * plan.FrameSteps * 7f)));
            if (n > 0) Anchor = 0.9f * Anchor + 0.1f * (mean / n - spMean) + cruise * Heading;
            ClampAnchorToBand();
            ThreatLevel = 0.85f * ThreatLevel + 0.15f * MathF.Min(1f, 3f * (n > 0 ? stSum / n : 0));
            Clock++;
        }

        void ClampAnchorToBand()
        {
            if (C.BandOuter <= 0f) return;
            float d = Anchor.Length();
            if (d < 1e-3f) { Anchor = Heading * C.BandInner; return; }
            float c = Math.Clamp(d, C.BandInner, C.BandOuter);
            if (c != d) Anchor *= c / d;
        }

        /// <summary>A tadpole died (vessel, predator, starvation). The body re-solves its homes.</summary>
        public void Kill(int i)
        {
            if (i < 0 || i >= Cap || !Alive[i]) return;
            Alive[i] = false; Home[i] = -1; Molt[i] = 0;
            if (C.KillLayHoldSteps > 0) _layHoldUntil = Math.Max(_layHoldUntil, Clock + C.KillLayHoldSteps);
        }

        /// <summary>Current element of a tadpole as composition sees it (a molting one counts as what it becomes).</summary>
        public int EffectiveElement(int i) => Molt[i] > 0 ? MoltTo[i] : Elem[i];

        public int[] Counts(bool eff)
        {
            var c = new int[4];
            for (int i = 0; i < Cap; i++) if (Alive[i]) c[eff && Molt[i] > 0 ? MoltTo[i] : Elem[i]]++;
            return c;
        }

        int AliveSignature()
        {
            int h = 17;
            for (int i = 0; i < Cap; i++) if (Alive[i]) h = h * 31 + i * 7 + Elem[i];
            return h;
        }

        static int ArgMax(int[] a) { int b = 0; for (int i = 1; i < a.Length; i++) if (a[i] > a[b]) b = i; return b; }

        // ──────────────────────────────────────────────────────────────── grid

        void CellOf(Vector3 x, out int cx, out int cy, out int cz)
        { cx = (int)MathF.Floor(x.X / C.AlignR); cy = (int)MathF.Floor(x.Y / C.AlignR); cz = (int)MathF.Floor(x.Z / C.AlignR); }
        static int Hash(int x, int y, int z) => (int)(((uint)(x * 73856093) ^ (uint)(y * 19349663) ^ (uint)(z * 83492791)) % G);
        void BuildGrid()
        {
            Array.Clear(_cellCount, 0, G);
            for (int i = 0; i < Cap; i++) if (Alive[i]) { CellOf(Pos[i], out var a, out var b, out var c); _cellOf[i] = Hash(a, b, c); _cellCount[_cellOf[i]]++; }
            int s = 0; for (int g = 0; g < G; g++) { _cellStart[g] = s; s += _cellCount[g]; }
            Array.Clear(_fill, 0, G);
            for (int i = 0; i < Cap; i++) if (Alive[i]) { int g = _cellOf[i]; _sorted[_cellStart[g] + _fill[g]++] = i; }
        }

        // ──────────────────────────────────────────────────────────────── assignment

        float Cost(int i, int k, SwarmPlanData plan)
        {
            float d2 = Vector3.DistanceSquared(Pos[i], _sw[k]) / 8f;
            float c = d2 < 16 ? d2 : 8 * MathF.Sqrt(d2) - 16;
            if (Elem[i] != plan.Elem[k]) c += 60;
            if (Home[i] == k) c -= C.Sticky;
            return c;
        }

        void AssignGreedy(SwarmPlanData plan)
        {
            int A = 0; for (int i = 0; i < Cap; i++) if (Alive[i]) _alive[A++] = i;
            int M = plan.N, KM = A * M;
            for (int a = 0; a < A; a++) for (int k = 0; k < M; k++) { _keys[a * M + k] = Cost(_alive[a], k, plan); _ids[a * M + k] = a * M + k; }
            Array.Sort(_keys, _ids, 0, KM);
            Array.Clear(_ru, 0, A); Array.Clear(_cu, 0, M);
            for (int a = 0; a < A; a++) _nh[a] = -1;
            int got = 0, need = Math.Min(A, M);
            for (int q = 0; q < KM && got < need; q++)
            {
                int a = _ids[q] / M, k = _ids[q] % M; if (_ru[a] || _cu[k]) continue;
                _ru[a] = _cu[k] = true; _nh[a] = k; got++;
            }
            for (int a = 0; a < A; a++)
            {
                if (_nh[a] < 0) { float b = float.MaxValue; for (int k = 0; k < M; k++) { float c = Cost(_alive[a], k, plan); if (c < b) { b = c; _nh[a] = k; } } }
                Home[_alive[a]] = _nh[a];
            }
        }

        // ──────────────────────────────────────────────────────────────── composition

        /// <summary>
        /// Pays for one egg of element <paramref name="e"/> out of the stomach: its own element's
        /// reserve at EggCost, else the other reserves at CrossCost times that (largest first).
        /// Returns false - and spends nothing - when the swarm cannot afford it. This is the one
        /// place laying differs from the research model, where laying was free.
        /// </summary>
        public bool TryFund(int e) => SwarmCoreShared.TryFund(Stomach, e, C.EggCost[e], C.CrossCost);

        readonly int[] _want = new int[4], _have = new int[4];
        int _layHoldUntil;

        void Lay(SwarmPlanData plan)
        {
            int n = 0; for (int i = 0; i < Cap; i++) if (Alive[i]) n++;
            if (n >= plan.N || n == 0) return;
            int k = Math.Min(C.LayMax, Math.Min(plan.N - n, Math.Max(1, (int)MathF.Ceiling(C.LayRate * n))));
            var want = _want; var have = _have; Array.Clear(want, 0, 4); Array.Clear(have, 0, 4);
            for (int s = 0; s < plan.N; s++) want[plan.Elem[s]]++;
            for (int i = 0; i < Cap; i++) if (Alive[i]) have[Molt[i] > 0 ? MoltTo[i] : Elem[i]]++;
            Array.Clear(_taken, 0, plan.N);
            for (int i = 0; i < Cap; i++) if (Alive[i] && Home[i] >= 0 && Home[i] < plan.N) _taken[Home[i]] = true;
            for (int egg = 0; egg < k; egg++)
            {
                int be = -1, bdef = 0;
                for (int e = 0; e < 4; e++)
                {
                    int def = want[e] - have[e]; if (def <= bdef) continue;
                    bool par = false; for (int i = 0; i < Cap && !par; i++) par = Alive[i] && Elem[i] == e && Molt[i] == 0;
                    if (par) { be = e; bdef = def; }
                }
                if (be < 0) return;
                int free = Array.IndexOf(Alive, false); if (free < 0) return;
                if (!TryFund(be)) return;      // hungry: the body waits for its next meal
                int bp = -1, bs = -1; float bdist = float.MaxValue;
                for (int s = 0; s < plan.N; s++)
                {
                    if (plan.Elem[s] != be || _taken[s]) continue;
                    var w = _sw[s];
                    for (int i = 0; i < Cap; i++) if (Alive[i] && Elem[i] == be && Molt[i] == 0)
                    { float d2 = Vector3.DistanceSquared(Pos[i], w); if (d2 < bdist) { bdist = d2; bp = i; bs = s; } }
                }
                if (bp < 0) { for (int i = 0; i < Cap; i++) if (Alive[i] && Elem[i] == be) { bp = i; break; } }
                var dir = bs >= 0 ? _sw[bs] - Pos[bp]
                    : new Vector3((float)_rng.NextDouble() - .5f, (float)_rng.NextDouble() - .5f, (float)_rng.NextDouble() - .5f);
                dir /= MathF.Max(dir.Length(), 1e-6f);
                Alive[free] = true; Pos[free] = Pos[bp] + C.RBud * dir; Vel[free] = Vel[bp]; Elem[free] = be;
                Home[free] = bs; Molt[free] = 0; Startle[free] = 0; Facing[free] = Facing[bp];
                _dom[free] = _dom[bp];
                have[be]++; if (bs >= 0) _taken[bs] = true; LastAssign = -1000000;
                Events.Add(new SwarmEvent { Kind = SwarmEventKind.Laid, Index = free, Other = bp });
            }
        }

        void MoltStep(SwarmPlanData plan, int[] counts)
        {
            int n = 0; for (int e = 0; e < 4; e++) n += counts[e];
            var surplus = new float[4];
            for (int e = 0; e < 4; e++) surplus[e] = counts[e] - (float)plan.Mix[e] / plan.N * Math.Max(n, 1);
            int k = Math.Max(1, (int)MathF.Ceiling(C.MoltRate * n));
            for (int q = 0; q < k; q++)
            {
                int ef = 0, et = 0; for (int e = 1; e < 4; e++) { if (surplus[e] > surplus[ef]) ef = e; if (surplus[e] < surplus[et]) et = e; }
                if (surplus[ef] < 1 || surplus[et] > -1) return;
                int best = -1; float bd = float.MaxValue;
                for (int i = 0; i < Cap; i++)
                {
                    if (!Alive[i] || Elem[i] != ef || Molt[i] > 0) continue;
                    for (int s = 0; s < plan.N; s++) if (plan.Elem[s] == et)
                    { float d2 = Vector3.DistanceSquared(Pos[i], _sw[s]); if (d2 < bd) { bd = d2; best = i; } }
                }
                if (best < 0) return;
                Molt[best] = 1e-3f; MoltTo[best] = et; surplus[ef]--; surplus[et]++;
                Events.Add(new SwarmEvent { Kind = SwarmEventKind.MoltBegan, Index = best, Other = et });
            }
        }

        /// <summary>The current home of tadpole <paramref name="i"/> in plan coordinates, and the
        /// slot's attributes, for the host's per-unit look (prism shape, tier).</summary>
        public bool TryGetSlot(int i, out int slot) { slot = Home[i]; return slot >= 0 && slot < Plans[PlanIx].N; }

        /// <summary>The home slot's prism and tier, when the slot is of the member's element.</summary>
        public bool TryGetLook(int i, int element, out Vector3 half, out int tier)
        {
            half = default; tier = 0;
            if (!TryGetSlot(i, out int k) || Plan.Elem[k] != element) return false;
            half = Plan.Half[k]; tier = Plan.Tier[k];
            return true;
        }

        /// <summary>Starvation sheds a member of the element in largest surplus against the plan.</summary>
        public int StarvationVictim()
        {
            var counts = Counts(false);
            var plan = Plan;
            int alive = AliveCount, worst = -1; float worstSurplus = float.MinValue;
            for (int e = 0; e < 4; e++)
            {
                if (counts[e] == 0) continue;
                float s = counts[e] - plan.Mix[e] / (float)plan.N * alive;
                if (s > worstSurplus) { worstSurplus = s; worst = e; }
            }
            for (int i = 0; i < Cap; i++) if (Alive[i] && Elem[i] == worst) return i;
            return -1;
        }

        // ISwarmCore - the field core keeps its fields public (the harness reads them directly)
        int ISwarmCore.Cap => Cap;
        Vector3[] ISwarmCore.Pos => Pos;
        Vector3[] ISwarmCore.Vel => Vel;
        Vector3[] ISwarmCore.Facing => Facing;
        int[] ISwarmCore.Elem => Elem;
        bool[] ISwarmCore.Alive => Alive;
        float[] ISwarmCore.Startle => Startle;
        float[] ISwarmCore.Molt => Molt;
        int[] ISwarmCore.MoltTo => MoltTo;
        float[] ISwarmCore.Stomach => Stomach;
        int[] ISwarmCore.Dom => _dom;
        List<SwarmEvent> ISwarmCore.Events => Events;
        int ISwarmCore.Clock => Clock;
        int ISwarmCore.PlanIx => PlanIx;
        Vector3 ISwarmCore.Anchor => Anchor;
        Vector3 ISwarmCore.BX => BX;
        Vector3 ISwarmCore.BY => BY;
        Vector3 ISwarmCore.BZ => BZ;
        Vector3 ISwarmCore.SwimTarget { get => SwimTarget; set => SwimTarget = value; }
    }
}
