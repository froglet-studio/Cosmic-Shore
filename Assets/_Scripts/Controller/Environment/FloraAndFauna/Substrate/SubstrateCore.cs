// Round 11b (Docs/SUBSTRATE_FAUNA.md): the Living Ecology SUBSTRATE - one agents + fields + quorum simulation that
// every species in a cell is a PARAMETER SET of (research Tools/Ecology/substrate, DESIGN_BURST.md is the port spec).
//
// Burst-shaped and pure C#: struct-of-arrays sized once (never resized in play), a fixed tick, one neighbour hash per
// population per tick (per-cell MOMENTS read over 27 cells, O(1) per agent), a rotating 1/k re-steer slice plus the
// attention LOD, context steering over D directions, a quorum that flips phase with hysteresis, and a world pass that
// only ever touches the agent it is about. StepAgent is research kernels_nb.fused_step, one agent per call, with no
// allocation and no cross-agent write - what an IJobParallelFor would run.
//
// The world rules (Docs/SUBSTRATE_FAUNA.md §4) keep every lifeform law: mass is CONSERVED (an agent's body IS its
// stock: it grows by eating, halves to breed, and leaves exactly its stock behind as a skeleton when it dies), there
// is no imposed death (only a weapon, a predator or STARVATION - a stomach run dry, then its reserve), and nothing
// pops (a newborn blooms, a death withers through the platform). The core never kills on its own: it reports a
// starving agent and the owner kills it through the sealed Fauna death, so the crystal always drops.
//
// Compiled and RUN by Tools/Build/substrate_harness.
using System;
using System.Collections.Generic;
using System.Numerics;

namespace CosmicShore.Gameplay
{
    /// <summary>A vessel as the substrate senses it (sim space, relative to the cell centre).</summary>
    public struct SubstratePilot
    {
        public Vector3 Pos, Vel;
        public float Radius;
        /// <summary>Stable per vessel across ticks (a bite cooldown is per pilot).</summary>
        public int Id;
    }

    public enum SubstrateEventKind
    {
        Born = 0,
        /// <summary>The agent's stomach and reserve are empty: the owner must kill it (through its proxy).</summary>
        Starving = 1,
        /// <summary>A dangerous agent touched a pilot. Index = first biter, Other = pilot id, Value = biters.</summary>
        Bite = 2,
        /// <summary>The agent turned dangerous (its strike began). Value = the population's striking fraction.</summary>
        Strike = 3,
        /// <summary>The agent is spent (posture clock): slow and harmless for RestS seconds.</summary>
        Rest = 4,
    }

    public struct SubstrateEvent
    {
        public SubstrateEventKind Kind;
        public int Index, Other;
        public float Value;
    }

    /// <summary>A hungry predator caught a prey agent; the owner kills the prey through its proxy and feeds its body
    /// to the predator (the food web: mass moves, it never vanishes).</summary>
    public struct SubstratePredation
    {
        public int Predator, Prey;
    }

    /// <summary>One species living in the cell: a contiguous block of the core's slots and its parameter set.</summary>
    public sealed class SubstratePopulation
    {
        public readonly int Index, Start, Cap;
        public SubstrateSpeciesParams P;
        public bool Active;
        /// <summary>Research element index (0 Charge, 1 Mass, 2 Space, 3 Time) - the heart every agent carries.</summary>
        public int Element;
        /// <summary>Soft annulus (sim units from the cell centre) the agents are steered back into; 0 = none.</summary>
        public float BandInner, BandOuter;
        public int Alive, Striking;
        public long Births, Starvations, Bites, Strikes;

        internal readonly Vector3[] Dirs;
        internal readonly int[] Live;
        internal int LiveCount;
        internal readonly long[] Key;
        internal readonly int[] Slot;
        internal readonly long[] Tab;
        internal readonly double[] Agg;
        internal long M;
        internal readonly Dictionary<int, float> LastHit = new();
        /// <summary>The population this one preys on this tick (resolved by name), or -1.</summary>
        internal int PreyPop = -1;

        internal SubstratePopulation(int index, int start, SubstrateSpeciesParams p)
        {
            Index = index; Start = start; Cap = Math.Max(1, p.Capacity); P = p; Active = true;
            Dirs = SubstrateCore.FibDirs(Math.Max(6, p.NDirs));
            Live = new int[Cap]; Key = new long[Cap]; Slot = new int[Cap];
            int tc = 1; while (tc < 2 * Cap + 8) tc <<= 1;
            Tab = new long[tc]; Agg = new double[tc * 8];
        }
    }

    public sealed class SubstrateCore
    {
        public const int MaxPilots = 8;
        public readonly int Capacity;
        /// <summary>The cell's membrane radius in sim units (world units; the sim origin is the cell centre).</summary>
        public readonly float R;
        public readonly float Dt;
        public readonly SubstrateFields Fields;
        public long Tick;
        public float T;

        // ── state (one array per field, capacity-sized; DESIGN_BURST.md §1) ──
        public readonly Vector3[] Pos, Vel, IDir, WSeed, Home, GaitV;
        public readonly float[] ISpeed, Hunger, Fear, Curious, Aggr, Phase, QTarget, Stock, Grow, Stamina, Rest, BiteCool, Closure;
        public readonly bool[] Alive, Starving, Steered, Watched, Danger, Creeping;
        public readonly int[] PopOf;
        public readonly long[] FreedTick, BornTick, ClaimedTick;

        public readonly List<SubstratePopulation> Pops = new();
        public readonly List<SubstrateEvent> Events = new();
        /// <summary>Agents of this tick's re-steer slice hungry enough to eat (the owner resolves each against real food).</summary>
        public readonly List<int> EatRequests = new();
        /// <summary>Predators that caught prey this tick (each prey claimed once).</summary>
        public readonly List<SubstratePredation> PreyRequests = new();

        /// <summary>Mass ledger: everything that ever entered agents (seeds + food) and left them (deaths).</summary>
        public double MassIn, MassOut;
        /// <summary>Worker threads for the agent kernel (1 = this thread; the harness benches more).</summary>
        public int Workers = 1;
        /// <summary>Accumulated stage costs in ms (fields, hash + moments, agent kernel, world) - the cost readout.</summary>
        public double MsFields, MsHash, MsAgents, MsWorld;
        static double Ms(long t0) => (System.Diagnostics.Stopwatch.GetTimestamp() - t0) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;

        readonly Random _rng;
        readonly Vector3[] _pilotSum = new Vector3[MaxPilots];
        readonly int[] _pilotCnt = new int[MaxPilots];
        SubstratePilot[] _pilots = new SubstratePilot[MaxPilots];
        int _npil;

        public SubstrateCore(int capacity, float radius, float dt = 0.1f, int fieldGrid = 40, int seed = 1)
        {
            Capacity = Math.Max(1, capacity); R = radius; Dt = dt;
            Fields = new SubstrateFields(radius, fieldGrid);
            _rng = new Random(seed);
            int n = Capacity;
            Pos = new Vector3[n]; Vel = new Vector3[n]; IDir = new Vector3[n]; WSeed = new Vector3[n]; Home = new Vector3[n]; GaitV = new Vector3[n];
            ISpeed = new float[n]; Hunger = new float[n]; Fear = new float[n]; Curious = new float[n]; Aggr = new float[n];
            Phase = new float[n]; QTarget = new float[n]; Stock = new float[n]; Grow = new float[n]; Stamina = new float[n];
            Rest = new float[n]; BiteCool = new float[n]; Closure = new float[n];
            Alive = new bool[n]; Starving = new bool[n]; Steered = new bool[n]; Watched = new bool[n]; Danger = new bool[n]; Creeping = new bool[n];
            PopOf = new int[n]; FreedTick = new long[n]; BornTick = new long[n]; ClaimedTick = new long[n];
            for (int i = 0; i < n; i++) { PopOf[i] = -1; FreedTick[i] = -1; ClaimedTick[i] = -1; }
        }

        // ───────────────────────────────────────────────────────────── populations

        /// <summary>Gives a species a block of slots. Reuses an inactive block that is big enough. -1 when the cell is full.</summary>
        public int AddPopulation(SubstrateSpeciesParams p, int element, float bandInner = 0f, float bandOuter = 0f)
        {
            int cap = Math.Max(1, p.Capacity);
            for (int q = 0; q < Pops.Count; q++)
            {
                var old = Pops[q];
                if (old.Active || old.Cap < cap) continue;
                var re = new SubstratePopulation(q, old.Start, p) { Element = element, BandInner = bandInner, BandOuter = bandOuter };
                Pops[q] = re;
                for (int i = re.Start; i < re.Start + re.Cap; i++) PopOf[i] = q;
                return q;
            }
            int start = 0;
            for (int q = 0; q < Pops.Count; q++) start = Math.Max(start, Pops[q].Start + Pops[q].Cap);
            if (start + cap > Capacity) return -1;
            var pop = new SubstratePopulation(Pops.Count, start, p) { Element = element, BandInner = bandInner, BandOuter = bandOuter };
            Pops.Add(pop);
            for (int i = start; i < start + cap; i++) PopOf[i] = pop.Index;
            return pop.Index;
        }

        /// <summary>Retires a population whose last agent is gone (its block is free for the next species).</summary>
        public void RemovePopulation(int q)
        {
            if (q < 0 || q >= Pops.Count) return;
            var pop = Pops[q];
            for (int i = pop.Start; i < pop.Start + pop.Cap; i++) if (Alive[i]) Kill(i);
            pop.Active = false;
        }

        /// <summary>Seeds n agents around a point (a spawner is a seeder: each starts with Stock0 of body).</summary>
        public int Seed(int q, int n, Vector3 centre, float spread)
        {
            int made = 0;
            for (int k = 0; k < n; k++)
                if (SeedOne(q, centre + Gauss3() * spread) >= 0) made++;
            return made;
        }

        /// <summary>Seeds one agent at each given point (an ambusher is seeded IN the flora; research anchor="mass").</summary>
        public int SeedAt(int q, ReadOnlySpan<Vector3> at, float jitter = 6f)
        {
            int made = 0;
            for (int k = 0; k < at.Length; k++)
                if (SeedOne(q, at[k] + Gauss3() * jitter) >= 0) made++;
            return made;
        }

        int SeedOne(int q, Vector3 p)
        {
            var pop = Pops[q];
            int i = FreeSlot(pop);
            if (i < 0) return -1;
            var P = pop.P;
            var d = Unit(Gauss3());
            Pos[i] = p; IDir[i] = d; Vel[i] = d * (P.Solitary.Speed * 0.5f); ISpeed[i] = P.Solitary.Speed;
            Hunger[i] = 0.1f + 0.3f * (float)_rng.NextDouble();
            Fear[i] = 0f; Curious[i] = 0f; Aggr[i] = 0f; Phase[i] = 0f; QTarget[i] = 0f;
            Stock[i] = P.Stock0; Grow[i] = 1f; Stamina[i] = P.StaminaS; Rest[i] = 0f; BiteCool[i] = 0f; Closure[i] = 0f;
            WSeed[i] = new Vector3((float)_rng.NextDouble() * 100f, (float)_rng.NextDouble() * 100f, (float)_rng.NextDouble() * 100f);
            Home[i] = p; GaitV[i] = Vector3.Zero;
            Alive[i] = true; Starving[i] = false; Danger[i] = false; Watched[i] = false; Creeping[i] = false;
            BornTick[i] = Tick;
            MassIn += P.Stock0;
            return i;
        }

        int FreeSlot(SubstratePopulation pop)
        {
            // never reuse a slot freed THIS tick: an index must not change identity inside one tick (research finding 8)
            for (int i = pop.Start; i < pop.Start + pop.Cap; i++)
                if (!Alive[i] && FreedTick[i] != Tick) return i;
            return -1;
        }

        // ───────────────────────────────────────────────────────────── inputs (applied between steps)

        /// <summary>A bite of real food landed: its volume becomes body (stock) 1:1, and fills the stomach.</summary>
        public void Feed(int i, float volume)
        {
            if (i < 0 || i >= Capacity || !Alive[i] || !(volume > 0f)) return;
            var P = Pops[PopOf[i]].P;
            Stock[i] += volume;
            Hunger[i] = MathF.Max(0f, Hunger[i] - volume * P.HungerPerVol);
            MassIn += volume;
        }

        /// <summary>The agent died (always through its proxy in the game). Returns the stock it took with it - the
        /// skeleton the platform leaves behind.</summary>
        public float Kill(int i)
        {
            if (i < 0 || i >= Capacity || !Alive[i]) return 0f;
            float s = Stock[i];
            MassOut += s;
            Stock[i] = 0f;
            Alive[i] = false; Starving[i] = false; Danger[i] = false; Creeping[i] = false;
            FreedTick[i] = Tick;
            Vel[i] = Vector3.Zero;
            return s;
        }

        public double MassHeld()
        {
            double m = 0;
            for (int i = 0; i < Capacity; i++) if (Alive[i]) m += Stock[i];
            return m;
        }

        // ───────────────────────────────────────────────────────────── the tick

        public void Step(ReadOnlySpan<SubstratePilot> pilots, ReadOnlySpan<SubstrateFood> food)
        {
            Events.Clear();
            EatRequests.Clear();
            PreyRequests.Clear();
            _npil = Math.Min(pilots.Length, MaxPilots);
            for (int j = 0; j < _npil; j++) _pilots[j] = pilots[j];
            long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
            Fields.Update(food, pilots.Slice(0, _npil));
            MsFields += Ms(t0);
            Tick++;
            for (int q = 0; q < Pops.Count; q++)
            {
                var pop = Pops[q];
                pop.PreyPop = -1;
                if (!pop.Active || string.IsNullOrEmpty(pop.P.PreyName)) continue;
                for (int o = 0; o < Pops.Count; o++)
                    if (o != q && Pops[o].Active && Pops[o].P.Name == pop.P.PreyName) { pop.PreyPop = o; break; }
            }
            for (int q = 0; q < Pops.Count; q++)
                if (Pops[q].Active) StepPopulation(Pops[q]);
            T += Dt;
        }

        void StepPopulation(SubstratePopulation pop)
        {
            var P = pop.P;
            float dt = Dt;
            // live, not-starving agents (research A = alive & ~dying)
            int n = 0;
            for (int i = pop.Start; i < pop.Start + pop.Cap; i++)
                if (Alive[i] && !Starving[i]) pop.Live[n++] = i;
            pop.LiveCount = n;
            long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
            BuildMoments(pop);
            PilotMoments(pop);
            MsHash += Ms(t0);
            t0 = System.Diagnostics.Stopwatch.GetTimestamp();

            if (Workers > 1 && n > 256)
                System.Threading.Tasks.Parallel.For(0, Workers, w =>
                {
                    Span<float> I = stackalloc float[pop.Dirs.Length];
                    Span<float> G = stackalloc float[pop.Dirs.Length];
                    for (int q = w; q < pop.LiveCount; q += Workers) StepAgent(pop, q, I, G);
                });
            else
            {
                Span<float> I = stackalloc float[pop.Dirs.Length];
                Span<float> G = stackalloc float[pop.Dirs.Length];
                for (int q = 0; q < n; q++) StepAgent(pop, q, I, G);
            }

            // a starving agent is not steered: it slows to a stop where it is, waiting for its death (owner's)
            for (int i = pop.Start; i < pop.Start + pop.Cap; i++)
                if (Alive[i] && Starving[i]) { Vel[i] *= MathF.Max(0f, 1f - 2f * dt); Pos[i] += Vel[i] * dt; ClampMembrane(i); }

            // gait: a vertical bob of the body, never the heading (research _gait; finding 6: cute needs a gait)
            if (P.Solitary.GaitAmp > 0f || P.Gregarious.GaitAmp > 0f)
                for (int q = 0; q < n; q++)
                {
                    int i = pop.Live[q];
                    float amp = P.Solitary.GaitAmp + (P.Gregarious.GaitAmp - P.Solitary.GaitAmp) * Phase[i];
                    float hz = P.Solitary.GaitHz + (P.Gregarious.GaitHz - P.Solitary.GaitHz) * Phase[i];
                    GaitV[i] = new Vector3(0f, amp * MathF.Cos(2f * MathF.PI * hz * (T + dt) + WSeed[i].X), 0f);
                    Pos[i] += GaitV[i] * dt;
                    ClampMembrane(i);
                }

            // deposits into the shared fields
            for (int q = 0; q < n; q++)
            {
                int i = pop.Live[q];
                if (P.DepositAlarm > 0f && Fear[i] > 0.3f) Fields.Deposit(SubstrateFields.Alarm, Pos[i], P.DepositAlarm * Fear[i] * dt);
                if (P.DepositThreat > 0f) Fields.Deposit(SubstrateFields.Threat, Pos[i], P.DepositThreat * dt);
                if (P.ScentDeposit > 0f) Fields.Deposit(SubstrateFields.Scent, Pos[i], P.ScentDeposit * dt);
            }
            MsAgents += Ms(t0);
            t0 = System.Diagnostics.Stopwatch.GetTimestamp();
            World(pop);
            MsWorld += Ms(t0);
        }

        /// <summary>Hash every live agent into cells of the neighbour radius and reduce per-cell MOMENTS once
        /// (count, sum pos, sum vel, sum phase) - research _hash_build + the agg reduction.</summary>
        void BuildMoments(SubstratePopulation pop)
        {
            float h = pop.P.NbrR;
            long M = (long)(2f * R / h) + 4;
            pop.M = M;
            var tab = pop.Tab; var agg = pop.Agg;
            Array.Fill(tab, -1L);
            Array.Clear(agg, 0, agg.Length);
            long mask = tab.Length - 1;
            for (int q = 0; q < pop.LiveCount; q++)
            {
                int i = pop.Live[q];
                long k = KeyOf(Pos[i], h, M);
                pop.Key[q] = k;
                long j = Hash(k) & mask;
                while (tab[j] != -1L && tab[j] != k) j = (j + 1) & mask;
                tab[j] = k;
                int s = (int)j;
                pop.Slot[q] = s;
                int o = s * 8;
                agg[o] += 1.0;
                agg[o + 1] += Pos[i].X; agg[o + 2] += Pos[i].Y; agg[o + 3] += Pos[i].Z;
                agg[o + 4] += Vel[i].X; agg[o + 5] += Vel[i].Y; agg[o + 6] += Vel[i].Z;
                agg[o + 7] += Phase[i];
            }
        }

        /// <summary>The bestiary's CLOSURE, as a per-pilot moment: the summed bearings of the agents near each pilot
        /// (O(N + pilots)). An agent's closure = how evenly its packmates surround its pilot.</summary>
        void PilotMoments(SubstratePopulation pop)
        {
            var P = pop.P;
            if (P.QWClose <= 0f && !P.RestTogether) return;
            for (int j = 0; j < _npil; j++) { _pilotSum[j] = Vector3.Zero; _pilotCnt[j] = 0; }
            for (int q = 0; q < pop.LiveCount; q++)
            {
                int i = pop.Live[q];
                int j = NearestPilot(Pos[i], out float d);
                if (j < 0 || d >= P.CloseR) continue;
                _pilotSum[j] += Unit(Pos[i] - _pilots[j].Pos);
                _pilotCnt[j]++;
            }
            for (int q = 0; q < pop.LiveCount; q++)
            {
                int i = pop.Live[q];
                int j = NearestPilot(Pos[i], out float d);
                if (j < 0 || d >= P.CloseR || _pilotCnt[j] == 0) { Closure[i] = 0f; continue; }
                int cnt = _pilotCnt[j];
                float res = _pilotSum[j].Length() / cnt;
                // bestiary pack.py: clip((1 - res) * clip((cnt - 1) / 3, 0, 1) * 1.6, 0, 1)
                Closure[i] = Math.Clamp((1f - res) * Math.Clamp((cnt - 1) / 3f, 0f, 1f) * 1.6f, 0f, 1f);
            }
        }

        /// <summary>
        /// ONE agent's step (research kernels_nb.fused_step): drives for everyone; if in the re-steer slice (or engaged,
        /// the attention LOD) the 27-cell moment read, the quorum target and the context map; then integrate. Plus the
        /// bestiary primitives: closure is a quorum input, the posture clock, the gaze sensor.
        /// </summary>
        void StepAgent(SubstratePopulation pop, int q, Span<float> I, Span<float> G)
        {
            var P = pop.P;
            ref readonly var Rs = ref P.Solitary;
            ref readonly var Rg = ref P.Gregarious;
            float dt = Dt;
            int i = pop.Live[q];
            var p = Pos[i];
            float ph = Phase[i];
            int fc = Fields.Cell(p);

            // ── drives ──
            float hu = Hunger[i] + P.Metabolism * dt;   // above 1 = the stomach is empty and the reserve is burning
            Hunger[i] = hu;
            float h = MathF.Min(1f, hu);
            int pj = NearestPilot(p, out float pd);
            float prox = Math.Clamp(1f - pd / P.Sense, 0f, 1f);
            float threat = Fields.Sample(SubstrateFields.Threat, fc), alarm = Fields.Sample(SubstrateFields.Alarm, fc);
            float fe = Fear[i];
            fe += dt * (P.FearGain * (prox * prox + 0.5f * MathF.Min(threat, 2f) + MathF.Min(alarm, 2f))) - dt * P.FearDecay * fe;
            fe = Math.Clamp(fe, 0f, 1f); Fear[i] = fe;
            float calm = (1f - fe) * (1f - h);
            float cu = Curious[i] + dt * P.CuriosityRate * (calm * (1f - ph) - Curious[i]); Curious[i] = cu;
            float capw = MathF.Min(1f, (Rs.WHunt + Rs.WRing) + ((Rg.WHunt + Rg.WRing) - (Rs.WHunt + Rs.WRing)) * ph);
            float ag = Math.Clamp(MathF.Max(h * 1.4f - 0.3f, P.AggrBase), 0f, 1f) * capw; Aggr[i] = ag;
            bool resting = Rest[i] > 0f;

            // ── re-steer the 1/k slice, plus the attention LOD (finding 4) ──
            int k = Math.Max(1, P.FracK);
            bool st = ((i + Tick) % k == 0) || pd < P.AttnR || MathF.Max(fe, ag) > P.AttnUrg;
            Steered[i] = st;
            bool freeze = false;
            if (st)
            {
                // 27-cell moment read
                double c0 = 0, c1 = 0, c2 = 0, c3 = 0, c4 = 0, c5 = 0, c6 = 0, c7 = 0;
                long k0 = pop.Key[q], M = pop.M, mask = pop.Tab.Length - 1;
                for (int dx = -1; dx <= 1; dx++)
                    for (int dy = -1; dy <= 1; dy++)
                        for (int dz = -1; dz <= 1; dz++)
                        {
                            long kk = k0 + (dx * M + dy) * M + dz;
                            long j = Hash(kk) & mask;
                            while (pop.Tab[j] != -1L)
                            {
                                if (pop.Tab[j] == kk)
                                {
                                    int o = (int)j * 8; var a = pop.Agg;
                                    c0 += a[o]; c1 += a[o + 1]; c2 += a[o + 2]; c3 += a[o + 3];
                                    c4 += a[o + 4]; c5 += a[o + 5]; c6 += a[o + 6]; c7 += a[o + 7];
                                    break;
                                }
                                j = (j + 1) & mask;
                            }
                        }
                float cnt = (float)(c0 - 1.0);
                float inv = 1f / MathF.Max(cnt, 1f);
                var cen = new Vector3((float)(c1 - p.X) * inv, (float)(c2 - p.Y) * inv, (float)(c3 - p.Z) * inv);
                var ali = new Vector3((float)(c4 - Vel[i].X) * inv, (float)(c5 - Vel[i].Y) * inv, (float)(c6 - Vel[i].Z) * inv);
                float mph = cnt > 0f ? (float)(c7 - ph) * inv : ph;

                // quorum target (one signal for every species) - a resting agent's target is the solitary end
                if (P.QUp < 9f)
                {
                    float sig = P.QWDens * cnt / P.DensNorm + P.QWProx * prox + P.QWAlarm * MathF.Min(alarm, 2f) + P.QWClose * Closure[i];
                    float s = sig * MathF.Pow(h, P.QHunger);
                    float th = QTarget[i] > 0.5f ? P.QDown : P.QUp;
                    float tg = 1f / (1f + MathF.Exp(-(s - th) / P.QWidth));
                    float c = P.QContagion;
                    if (cnt > 0f) tg = (1f - c) * tg + c * MathF.Max(tg, mph);
                    QTarget[i] = resting ? 0f : tg;
                }

                var W = SubstrateRegime.Lerp(Rs, Rg, ph);
                var dirs = pop.Dirs;
                I.Clear(); G.Clear();
                Paint(I, dirs, Fields.Grad(SubstrateFields.Food, fc), W.WFood * h);
                if (P.WPrey > 0f && pop.PreyPop >= 0)
                {
                    // food web: follow the prey's scent; within PreySense make straight for the nearest one
                    Paint(I, dirs, Fields.Grad(SubstrateFields.Scent, fc), P.WPrey * h);
                    int prey = NearestPrey(Pops[pop.PreyPop], p, P.PreySense, out _);
                    if (prey >= 0) Paint(I, dirs, Pos[prey] - p, 2f * P.WPrey * h);
                }
                if (cnt > 0f)
                {
                    if (P.SpacingSpring)
                    {
                        // ONE signed spring along the neighbour-centroid axis (finding 2): toward when sparse, away when
                        // crowded, crossing zero at the regime's target crowding
                        float sg = Math.Clamp(1f - cnt / P.DensNorm / MathF.Max(W.Crowd, 1e-3f), -1.5f, 1f);
                        float wsp = sg > 0f ? W.WCoh : W.WSep;
                        Paint(I, dirs, (cen - p) * sg, MathF.Abs(sg) * wsp);
                    }
                    else Paint(I, dirs, cen - p, W.WCoh);
                    Paint(I, dirs, ali, W.WAlign);
                }
                float tt = Tick * 0.05f;
                var ws = WSeed[i];
                var wv = new Vector3(
                    MathF.Sin(ws.X + tt) + 0.6f * MathF.Sin(1.7f * ws.Z + tt * 2.1f),
                    MathF.Sin(ws.Y + tt * 1.3f) + 0.6f * MathF.Sin(1.7f * ws.Y + tt * 2.1f),
                    MathF.Sin(ws.Z + tt * 0.7f) + 0.6f * MathF.Sin(1.7f * ws.X + tt * 2.1f));
                Paint(I, dirs, wv, W.WWander);
                bool creeping = false;
                if (pj >= 0)
                {
                    var pil = _pilots[pj];
                    var tp = pil.Pos - p;
                    float near = pd < P.Sense * 1.5f ? 1f : 0f;
                    float sp = Math.Clamp((pd - W.Comfort) / MathF.Max(W.Comfort, 1f), -1f, 1f);
                    Paint(I, dirs, tp * sp, W.WCurious * cu * MathF.Abs(sp) * near);
                    float ld = Math.Clamp(pd / 150f, 0f, 2f);
                    Paint(I, dirs, tp + pil.Vel * ld, W.WHunt * ag * near);
                    if (P.RingRoles > 0)
                    {
                        // ring slots around the pilot in the plane normal to its velocity, slightly AHEAD (a cut-off)
                        var f = Unit(pil.Vel + new Vector3(1e-9f, 0f, 0f));
                        var a = Unit(new Vector3(-f.Z + 1e-6f, 1e-6f, f.X + 1e-6f));   // cross(f, up)
                        var b = Vector3.Cross(f, a);
                        float an = 2f * MathF.PI * ((i - pop.Start) % P.RingRoles) / P.RingRoles;
                        var slot = pil.Pos + f * 40f + W.RingR * (MathF.Cos(an) * a + MathF.Sin(an) * b);
                        Paint(I, dirs, slot - p, W.WRing * ag * near);
                    }
                    PaintD(G, dirs, -tp, W.WFlee * fe * prox);
                    if (resting && P.WRestRetreat > 0f) Paint(I, dirs, -tp, P.WRestRetreat);   // winded: fall back, widen
                    // GAZE: a calm agent within creep range slides toward where the pilot will be - only while it is
                    // OUTSIDE the pilot's forward cone; inside it, it freezes (bestiary lurker)
                    if (P.WCreep > 0f && !resting && ph < 0.2f && pd > P.CreepMin && pd < P.CreepR)
                    {
                        bool looked = pil.Vel.LengthSquared() > 1f && Vector3.Dot(Unit(pil.Vel), Unit(p - pil.Pos)) > P.GazeCos;
                        Watched[i] = looked;
                        if (!looked)
                        {
                            Paint(I, dirs, pil.Pos + pil.Vel * P.CreepLeadS - p, P.WCreep);
                            creeping = true;
                            Home[i] = p;   // it leaves its seat: home is wherever it is now
                        }
                        else freeze = P.Freeze > 0f;
                    }
                    else Watched[i] = false;
                }
                else Watched[i] = false;
                Creeping[i] = creeping;
                Paint(I, dirs, Home[i] - p, W.WHome * (1f - h) * (creeping ? 0f : 1f));
                float r = p.Length();
                Paint(I, dirs, -p, Math.Clamp((r - 0.8f * R) / (0.15f * R), 0f, 1f) * 3f);
                PaintD(G, dirs, p, Math.Clamp((r - 0.85f * R) / (0.1f * R), 0f, 1f) * 3f);
                if (pop.BandOuter > 0f)
                {
                    // the species' pen (FaunaConfigurationSO band): steered back in, never walled
                    float soft = 0.1f * MathF.Max(pop.BandOuter - pop.BandInner, 50f);
                    Paint(I, dirs, -p, Math.Clamp((r - pop.BandOuter) / soft, 0f, 1f) * 2f);
                    Paint(I, dirs, p, Math.Clamp((pop.BandInner - r) / soft, 0f, 1f) * 2f);
                }
                if (cnt > 0f && !P.SpacingSpring)
                {
                    float sc = cnt / P.DensNorm / P.NbrR;
                    var sv = (p - cen) * sc;
                    PaintD(G, dirs, sv, W.WSep * MathF.Min(sv.Length(), 2f));
                }
                PaintD(G, dirs, -Fields.Grad(SubstrateFields.Alarm, fc), W.WAlarm * (0.3f + fe));
                PaintD(G, dirs, -Fields.Grad(SubstrateFields.Threat, fc), W.WThreat * (0.3f + fe));

                // context choice: momentum, soft danger mask, soft-argmax around the best direction
                var cur = IDir[i];
                float mx = -1e30f;
                for (int d = 0; d < dirs.Length; d++)
                {
                    float c = Vector3.Dot(cur, dirs[d]);
                    if (c > 0f) I[d] += P.Momentum * c;
                    float e = I[d] * (1f - Math.Clamp(G[d], 0f, 1f)) - 0.25f * MathF.Max(G[d] - 1f, 0f);
                    I[d] = e;
                    if (e > mx) mx = e;
                }
                var v = Vector3.Zero;
                if (mx > 1e-6f)
                    for (int d = 0; d < dirs.Length; d++)
                    {
                        float w = I[d] - 0.75f * mx;
                        if (w > 0f) v += w * w * dirs[d];
                    }
                float nn = v.Length();
                if (nn > 1e-9f)
                {
                    float bl = P.IntentBlend;
                    var o = bl * cur + (1f - bl) * (v / nn);
                    IDir[i] = o / MathF.Max(o.Length(), 1e-9f);
                }
                float urg = MathF.Max(W.WFlee > 0f ? fe : 0f, ag);
                float speed = W.Speed * (1f + (W.Burst - 1f) * urg);
                if (creeping) speed = P.CreepSpeed;
                if (resting) speed *= P.RestSpeed;
                ISpeed[i] = speed;
            }

            // ── integrate (bounded turn and acceleration: smooth by construction) ──
            if (freeze)
            {
                Vel[i] = Vector3.Zero;   // looked at: dead still
            }
            else
            {
                float turn = Rs.Turn + (Rg.Turn - Rs.Turn) * ph;
                float acl = Rs.Accel + (Rg.Accel - Rs.Accel) * ph;
                var vel = Vel[i];
                float vs = vel.Length();
                var hd = vs > 1e-6f ? vel / vs : IDir[i];
                var t = IDir[i];
                float c = Math.Clamp(Vector3.Dot(hd, t), -1f, 1f);
                float ang = MathF.Acos(c);
                float kk = MathF.Min(1f, turn * dt / MathF.Max(ang, 1e-6f));
                var nd = hd + (t - hd) * kk;
                nd /= MathF.Max(nd.Length(), 1e-9f);
                float ns = vs + Math.Clamp(ISpeed[i] - vs, -acl * dt, acl * dt);
                Vel[i] = nd * ns;
                Pos[i] = p + Vel[i] * dt;
                ClampMembrane(i);
            }
            Phase[i] = ph + dt * P.QRate * (QTarget[i] - ph);
        }

        // ───────────────────────────────────────────────────────────── world

        void World(SubstratePopulation pop)
        {
            var P = pop.P;
            float dt = Dt;
            int n = pop.LiveCount;
            float reserve = 1f + P.StarveS * P.Metabolism;
            int striking = 0, alive = 0;
            for (int i = pop.Start; i < pop.Start + pop.Cap; i++) if (Alive[i]) alive++;

            for (int q = 0; q < n; q++)
            {
                int i = pop.Live[q];
                Grow[i] = MathF.Min(1f, Grow[i] + dt / MathF.Max(P.GrowS, 1e-3f));
                BiteCool[i] -= dt;

                // posture clock: a strike spends stamina; spent (or bitten-with) rests; rested, it recovers
                if (P.StaminaS > 0f)
                {
                    if (Rest[i] > 0f)
                    {
                        Rest[i] -= dt;
                        if (Rest[i] <= 0f) { Rest[i] = 0f; Stamina[i] = MathF.Max(Stamina[i], 0.5f * P.StaminaS); }
                    }
                    else if (Phase[i] > P.DangerPhase && Aggr[i] > 0.5f)
                    {
                        Stamina[i] -= dt;
                        if (Stamina[i] <= 0f) SendToRest(pop, i);
                    }
                    else Stamina[i] = MathF.Min(P.StaminaS, Stamina[i] + 0.5f * dt);
                }

                // danger: an aggressive agent at the gregarious end that is not spent (research harm = aggr > 0.5)
                bool was = Danger[i];
                bool now = Aggr[i] > 0.5f && Phase[i] > P.DangerPhase && Rest[i] <= 0f;
                Danger[i] = now;
                if (now) striking++;
                if (now && !was) { pop.Strikes++; Events.Add(new SubstrateEvent { Kind = SubstrateEventKind.Strike, Index = i }); }

                // food: the slice that is hungry asks the owner for a bite of REAL food - flora, or prey it has caught
                if (Steered[i] && MathF.Min(1f, Hunger[i]) > P.EatHunger)
                {
                    EatRequests.Add(i);
                    if (pop.PreyPop >= 0)
                    {
                        int prey = NearestPrey(Pops[pop.PreyPop], Pos[i], P.EatR + 0.5f * Vel[i].Length() * dt, out _);
                        if (prey >= 0)
                        {
                            ClaimedTick[prey] = Tick;
                            PreyRequests.Add(new SubstratePredation { Predator = i, Prey = prey });
                        }
                    }
                }

                // starvation: the stomach is empty AND the reserve is spent (no clock: food pushes it back exactly)
                if (Hunger[i] >= reserve && !Starving[i])
                {
                    Starving[i] = true; Danger[i] = false;
                    pop.Starvations++;
                    Events.Add(new SubstrateEvent { Kind = SubstrateEventKind.Starving, Index = i });
                }
            }
            pop.Striking = striking;

            // bites: one harm EVENT per pilot per bite_cool (a swarm nibbles, it does not machine-gun)
            for (int j = 0; j < _npil; j++)
            {
                var pil = _pilots[j];
                int first = -1, count = 0;
                for (int q = 0; q < n; q++)
                {
                    int i = pop.Live[q];
                    if (!Danger[i] || BiteCool[i] > 0f) continue;
                    if (Vector3.Distance(Pos[i], pil.Pos) >= P.BiteR + pil.Radius) continue;
                    if (first < 0) first = i;
                    count++;
                }
                if (count == 0) continue;
                if (pop.LastHit.TryGetValue(pil.Id, out float last) && T - last < P.BiteCool) continue;
                pop.LastHit[pil.Id] = T;
                pop.Bites++;
                Events.Add(new SubstrateEvent { Kind = SubstrateEventKind.Bite, Index = first, Other = pil.Id, Value = count });
                for (int q = 0; q < n; q++)
                {
                    int i = pop.Live[q];
                    if (!Danger[i]) continue;
                    float d = Vector3.Distance(Pos[i], pil.Pos);
                    if (d < P.BiteR + pil.Radius) BiteCool[i] = P.BiteCool;
                    // the biters are spent; with RestTogether every striker around that pilot backs off too
                    if (P.RestS > 0f && P.StaminaS > 0f && (d < P.BiteR + pil.Radius || (P.RestTogether && d < P.CloseR)))
                        SendToRest(pop, i);
                }
            }

            // reproduce: a full body splits; the child grows in at the parent (production gated by free slots)
            for (int q = 0; q < n; q++)
            {
                int i = pop.Live[q];
                if (Stock[i] < P.BirthStock || Grow[i] < 1f || Starving[i]) continue;
                int c = FreeSlot(pop);
                if (c < 0) break;
                float half = Stock[i] * 0.5f;
                Stock[i] -= half; Stock[c] = half;
                Pos[c] = Pos[i]; Vel[c] = Vel[i] * 0.5f; Home[c] = Home[i]; IDir[c] = IDir[i]; ISpeed[c] = ISpeed[i];
                Hunger[c] = Hunger[i]; Fear[c] = Fear[i]; Curious[c] = Curious[i]; Aggr[c] = Aggr[i];
                Phase[c] = Phase[i]; QTarget[c] = QTarget[i];
                Stamina[c] = P.StaminaS; Rest[c] = 0f; BiteCool[c] = 0f; Grow[c] = 0f; Closure[c] = 0f; GaitV[c] = Vector3.Zero;
                WSeed[c] = new Vector3((float)_rng.NextDouble() * 100f, (float)_rng.NextDouble() * 100f, (float)_rng.NextDouble() * 100f);
                Alive[c] = true; Starving[c] = false; Danger[c] = false; Watched[c] = false; Creeping[c] = false;
                BornTick[c] = Tick;
                pop.Births++; alive++;
                Events.Add(new SubstrateEvent { Kind = SubstrateEventKind.Born, Index = c, Other = i });
            }
            pop.Alive = alive;
        }

        void SendToRest(SubstratePopulation pop, int i)
        {
            if (Rest[i] > 0f) return;
            var P = pop.P;
            Rest[i] = P.RestS; Stamina[i] = 0f; Danger[i] = false; QTarget[i] = 0f;
            Events.Add(new SubstrateEvent { Kind = SubstrateEventKind.Rest, Index = i });
        }

        // ───────────────────────────────────────────────────────────── helpers

        void ClampMembrane(int i)
        {
            float r = Pos[i].Length();
            if (r > 0.98f * R) Pos[i] *= 0.98f * R / r;
        }

        /// <summary>The nearest living, unclaimed prey agent within <paramref name="radius"/> (prey pools are small: a scan).</summary>
        int NearestPrey(SubstratePopulation prey, Vector3 p, float radius, out float dist)
        {
            dist = radius;
            int best = -1;
            for (int j = prey.Start; j < prey.Start + prey.Cap; j++)
            {
                if (!Alive[j] || Starving[j] || ClaimedTick[j] == Tick) continue;
                float d = Vector3.Distance(p, Pos[j]);
                if (d < dist) { dist = d; best = j; }
            }
            return best;
        }

        int NearestPilot(Vector3 p, out float dist)
        {
            dist = 1e9f;
            int best = -1;
            for (int j = 0; j < _npil; j++)
            {
                float d = Vector3.Distance(p, _pilots[j].Pos);
                if (d < dist) { dist = d; best = j; }
            }
            return best;
        }

        static long KeyOf(Vector3 p, float h, long M, float R0)
        {
            long cx = Math.Clamp((long)MathF.Floor((p.X + R0) / h) + 1, 0, M - 1);
            long cy = Math.Clamp((long)MathF.Floor((p.Y + R0) / h) + 1, 0, M - 1);
            long cz = Math.Clamp((long)MathF.Floor((p.Z + R0) / h) + 1, 0, M - 1);
            return (cx * M + cy) * M + cz;
        }

        long KeyOf(Vector3 p, float h, long M) => KeyOf(p, h, M, R);

        static long Hash(long k) => unchecked(k * (long)0x9E3779B97F4A7C15) & 0x7FFFFFFFFFFFFFFF;

        static void Paint(Span<float> I, Vector3[] dirs, Vector3 v, float w)
        {
            float n = v.Length();
            if (n <= 1e-9f || w == 0f) return;
            v /= n;
            for (int d = 0; d < dirs.Length; d++)
            {
                float c = Vector3.Dot(v, dirs[d]);
                if (c > 0f) I[d] += c * w;
            }
        }

        static void PaintD(Span<float> G, Vector3[] dirs, Vector3 v, float w)
        {
            float n = v.Length();
            if (n <= 1e-9f || w == 0f) return;
            v /= n;
            for (int d = 0; d < dirs.Length; d++)
            {
                float c = Vector3.Dot(v, dirs[d]);
                if (c > 0f) G[d] += c * c * w;
            }
        }

        public static Vector3 Unit(Vector3 v)
        {
            float n = v.Length();
            return n > 1e-9f ? v / n : Vector3.Zero;
        }

        Vector3 Gauss3() => new Vector3(Gauss(), Gauss(), Gauss());

        float Gauss()
        {
            double u1 = 1.0 - _rng.NextDouble(), u2 = _rng.NextDouble();
            return (float)(Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2));
        }

        /// <summary>n directions on a Fibonacci sphere (research fib_dirs).</summary>
        public static Vector3[] FibDirs(int n)
        {
            var d = new Vector3[n];
            double g = Math.PI * (1.0 + Math.Sqrt(5.0));
            for (int k = 0; k < n; k++)
            {
                double i = k + 0.5;
                double phi = Math.Acos(1.0 - 2.0 * i / n), th = g * i;
                d[k] = new Vector3((float)(Math.Cos(th) * Math.Sin(phi)), (float)(Math.Sin(th) * Math.Sin(phi)), (float)Math.Cos(phi));
            }
            return d;
        }
    }
}
