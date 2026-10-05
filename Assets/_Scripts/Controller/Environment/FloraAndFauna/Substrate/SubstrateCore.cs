// Round 11b (Docs/SUBSTRATE_FAUNA.md): the Living Ecology SUBSTRATE - one agents + fields + quorum simulation that
// every species in a cell is a PARAMETER SET of (research Tools/Ecology/substrate, DESIGN_BURST.md is the port spec).
//
// Burst-shaped and pure C#: struct-of-arrays sized once (never resized in play), a fixed tick, one neighbour hash per
// population per tick (per-cell MOMENTS read over 27 cells, O(1) per agent), a rotating 1/k re-steer slice plus the
// attention LOD, context steering over D directions, a quorum that flips phase with hysteresis, and a world pass that
// only ever touches the agent it is about. The per-agent step is SubstrateKernel.StepAgent (round 11b-2): research
// kernels_nb.fused_step, one agent per call, no allocation, no cross-agent write - the game runs it as a Burst
// IJobParallelFor between BeginStep and EndStep (SubstrateAgentJob), the harness through RunAgentPass.
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
        /// <summary>Round 11-11: the agent began its RAMP (a bull lowers its head, a mobber pulls up) - the telegraph.</summary>
        Windup = 5,
        /// <summary>A rider sipped its host: a danger contact of weight Value. Other = pilot id.</summary>
        Sip = 6,
        /// <summary>The agent latched onto a pilot's hull. Other = pilot id.</summary>
        Latch = 7,
        /// <summary>A rider lost its grip (its host turned hard) and was flung off, dazed. Other = pilot id.</summary>
        Shaken = 8,
        /// <summary>A body's jaws opened on a pilot: the surge begins. Index = the population's first slot.</summary>
        Gulp = 9,
        /// <summary>The population's body assembled (Value = members attached) / dissolved.</summary>
        Assemble = 10,
        Dissolve = 11,
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
        /// <summary>Round 11-11 tallies: ramps begun, sips, latches, riders shaken off, gulps, assemblies.</summary>
        public long Windups, Sips, Latches, Shaken, Gulps, Assemblies, Dissolves;
        /// <summary>Round 11-11: a SECTOR pen (unit axis, cos of the half-angle) inside the band; SectorCos 0 + no axis = none.</summary>
        public Vector3 SectorAxis;
        public float SectorCos;
        public bool HasSector;

        // ── the body (research BodyPlan; Docs/SUBSTRATE_FAUNA.md §9.4) - one per population ──
        public bool Assembling, BodyActive;
        public Vector3 BodyC, BodyF = new Vector3(0f, 0f, 1f), BodyV;
        public float GulpPrep, Gulp, GulpRest;
        internal readonly Vector3[] SlotW, SlotV;
        internal float MouthZ;
        /// <summary>World radius around a vessel inside which THIS population's agents engage (get a proxy), and the most it
        /// may engage at once. Below 0 = the tick job's <see cref="SubstrateTickSettings"/> numbers. The owner sets its own
        /// species' numbers when it claims the block (SubstrateFauna.ClaimBlock): one host serves every population, and its
        /// settings are the FIRST-joined species' (QA-SWARM-ROUND11-9).</summary>
        public float EngageRadius = -1f;
        public int MaxEngaged = -1;
        /// <summary>
        /// Round 11f-2 (Docs/ECOLOGY_LOD.md §6.1): the population is COLLAPSED - far from every pilot and unseen. Its
        /// agents stay where they are, every one alive, with exactly its stock (so its index entries, and the cell's
        /// LiveVolume, do not change). The tick skips it in every pass - no moments, no agent pass, no world pass, no
        /// deposits, no births - except its metabolism: each agent's hunger still rises at Metabolism per second, the same
        /// rule the kernel applies, so freezing is not immortality. Set between ticks only (the host queues it). Starvation
        /// is never decided frozen: the owner thaws the population before its hungriest agent's reserve runs out.
        /// </summary>
        public bool Frozen;
        /// <summary>The food field this population reads (<see cref="SubstrateFields.FoodGroup"/>): the field of the food
        /// inside its band, or 0 = the cell-wide field when it has no band. Set by <see cref="SubstrateCore.AddPopulation"/>.</summary>
        public int FoodGroup;

        internal readonly Vector3[] Dirs;
        internal readonly int[] Live;
        internal int LiveCount;
        internal readonly long[] Key;
        internal readonly int[] Slot;
        internal readonly long[] Tab;
        internal readonly double[] Agg;
        internal long M;
        internal readonly Dictionary<int, float> LastHit = new();
        /// <summary>The ring-hold clock per pilot id (<see cref="SubstrateSpeciesParams.RingHoldSeconds"/>; empty when 0).</summary>
        internal readonly Dictionary<int, float> RingHold = new();
        /// <summary>The population this one preys on this tick (resolved by name), or -1.</summary>
        internal int PreyPop = -1;
        /// <summary>This tick's kernel numbers (built by SubstrateCore.BeginStep).</summary>
        internal SubstrateKernelPop Kernel;

        internal SubstratePopulation(int index, int start, SubstrateSpeciesParams p)
        {
            Index = index; Start = start; Cap = Math.Max(1, p.Capacity); P = p; Active = true;
            Dirs = SubstrateCore.FibDirs(Math.Clamp(p.NDirs, 6, SubstrateKernel.MaxDirs));   // the job's scratch is MaxDirs
            Live = new int[Cap]; Key = new long[Cap]; Slot = new int[Cap];
            int tc = 1; while (tc < 2 * Cap + 8) tc <<= 1;
            Tab = new long[tc]; Agg = new double[tc * 8];
            int bk = p.BodyK;
            SlotW = new Vector3[bk]; SlotV = new Vector3[bk];
            for (int k = 0; k < bk; k++) MouthZ = Math.Max(MouthZ, p.BodySlots[3 * k + 2]);
        }
    }

    /// <summary>Planted bugs for the substrate LOD gate's negative controls (substrate_harness group lod).</summary>
    internal enum SubstrateFreezeBug
    {
        None = 0,
        /// <summary>A frozen agent stops getting hungry: freezing becomes immortality (breaks "one rule set").</summary>
        NoMetabolism = 1,
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
        /// <summary>The ring hold as each agent sees it: its pilot's hold clock / RingHoldSeconds (>= 1 = released; 0 when
        /// the species has no hold). Written by PilotMoments, read by World's strike gate.</summary>
        public readonly float[] RingGate;
        public readonly bool[] Alive, Starving, Steered, Watched, Danger, Creeping;
        public readonly int[] PopOf;
        /// <summary>Round 11-11: the ramp clock (s held armed), the body attachment (0..1), a rider's grip and sip clock,
        /// its host (pilot id, 0 = free) and its seat on the hull (offset from the host), and each body member's slot
        /// (world position + velocity, gathered in BeginStep).</summary>
        public readonly float[] Ramp, Attach, Grip, SipT;
        public readonly int[] Host;
        public readonly Vector3[] HostOff, SlotGoal, SlotVel;
        public readonly long[] FreedTick, BornTick, ClaimedTick;
        /// <summary>The fields at each live agent's cell, gathered by <see cref="BeginStep"/> (round 11b-2: the agent pass
        /// reads these, never the G^3 grids, so a Burst job needs no field arrays).</summary>
        public readonly float[] FThreat, FAlarm;
        public readonly Vector3[] GFood, GScent, GAlarm, GThreat;

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
        /// <summary>Accumulated stage costs in ms (fields + the per-agent gather, hash + moments, the agent pass - the
        /// kernel, drift/gait/deposits - and world) - the cost readout. MsKernel is the agent pass alone.</summary>
        public double MsFields, MsHash, MsAgents, MsWorld, MsKernel;
        static double Ms(long t0) => (System.Diagnostics.Stopwatch.GetTimestamp() - t0) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;

        readonly Random _rng;
        readonly Vector3[] _pilotSum = new Vector3[MaxPilots];
        readonly int[] _pilotCnt = new int[MaxPilots];
        internal readonly SubstratePilot[] _pilots = new SubstratePilot[MaxPilots];
        internal int _npil;
        readonly float[] _pilotOmega = new float[MaxPilots];
        readonly Vector3[] _pilotPrevDir = new Vector3[MaxPilots];
        readonly Dictionary<int, Vector3> _prevVel = new();
        readonly int[] _load = new int[MaxPilots];
        readonly int[] _turns = new int[MaxPilots];
        bool[] _mayRamp = new bool[0];

        public SubstrateCore(int capacity, float radius, float dt = 0.1f, int fieldGrid = 40, int seed = 1)
        {
            Capacity = Math.Max(1, capacity); R = radius; Dt = dt;
            Fields = new SubstrateFields(radius, fieldGrid);
            _rng = new Random(seed);
            int n = Capacity;
            Pos = new Vector3[n]; Vel = new Vector3[n]; IDir = new Vector3[n]; WSeed = new Vector3[n]; Home = new Vector3[n]; GaitV = new Vector3[n];
            ISpeed = new float[n]; Hunger = new float[n]; Fear = new float[n]; Curious = new float[n]; Aggr = new float[n];
            Phase = new float[n]; QTarget = new float[n]; Stock = new float[n]; Grow = new float[n]; Stamina = new float[n];
            Rest = new float[n]; BiteCool = new float[n]; Closure = new float[n]; RingGate = new float[n];
            Alive = new bool[n]; Starving = new bool[n]; Steered = new bool[n]; Watched = new bool[n]; Danger = new bool[n]; Creeping = new bool[n];
            PopOf = new int[n]; FreedTick = new long[n]; BornTick = new long[n]; ClaimedTick = new long[n];
            FThreat = new float[n]; FAlarm = new float[n];
            GFood = new Vector3[n]; GScent = new Vector3[n]; GAlarm = new Vector3[n]; GThreat = new Vector3[n];
            Ramp = new float[n]; Attach = new float[n]; Grip = new float[n]; SipT = new float[n]; Host = new int[n];
            HostOff = new Vector3[n]; SlotGoal = new Vector3[n]; SlotVel = new Vector3[n];
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
                var re = new SubstratePopulation(q, old.Start, p)
                {
                    Element = element, BandInner = bandInner, BandOuter = bandOuter, FoodGroup = Fields.FoodGroup(bandInner, bandOuter),
                };
                Pops[q] = re;
                for (int i = re.Start; i < re.Start + re.Cap; i++) PopOf[i] = q;
                return q;
            }
            int start = 0;
            for (int q = 0; q < Pops.Count; q++) start = Math.Max(start, Pops[q].Start + Pops[q].Cap);
            if (start + cap > Capacity) return -1;
            var pop = new SubstratePopulation(Pops.Count, start, p)
            {
                Element = element, BandInner = bandInner, BandOuter = bandOuter, FoodGroup = Fields.FoodGroup(bandInner, bandOuter),
            };
            Pops.Add(pop);
            for (int i = start; i < start + cap; i++) PopOf[i] = pop.Index;
            return pop.Index;
        }

        /// <summary>Round 11-11: pens population <paramref name="q"/> into a SECTOR of its band - a cone of half-angle
        /// <paramref name="halfAngleDeg"/> about <paramref name="axis"/> (0 = none). Steered back in, never walled.</summary>
        public void SetSector(int q, Vector3 axis, float halfAngleDeg)
        {
            if (q < 0 || q >= Pops.Count) return;
            var pop = Pops[q];
            pop.HasSector = halfAngleDeg > 0f && axis.LengthSquared() > 1e-9f;
            pop.SectorAxis = pop.HasSector ? Unit(axis) : Vector3.Zero;
            pop.SectorCos = pop.HasSector ? MathF.Cos(halfAngleDeg * MathF.PI / 180f) : 0f;
            // its food field is the food of its band inside the sector (SubstrateFields.FoodGroup)
            pop.FoodGroup = pop.HasSector ? Fields.FoodGroup(pop.BandInner, pop.BandOuter, pop.SectorAxis, pop.SectorCos)
                                          : Fields.FoodGroup(pop.BandInner, pop.BandOuter);
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
            Stock[i] = P.Stock0; Grow[i] = 1f; Stamina[i] = P.StaminaS; Rest[i] = 0f; BiteCool[i] = 0f; Closure[i] = 0f; RingGate[i] = 0f;
            WSeed[i] = new Vector3((float)_rng.NextDouble() * 100f, (float)_rng.NextDouble() * 100f, (float)_rng.NextDouble() * 100f);
            Home[i] = p; GaitV[i] = Vector3.Zero;
            Alive[i] = true; Starving[i] = false; Danger[i] = false; Watched[i] = false; Creeping[i] = false;
            Ramp[i] = 0f; Attach[i] = 0f; Grip[i] = 1f; SipT[i] = 0f; Host[i] = 0; HostOff[i] = Vector3.Zero;
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
            // a homing species (the lurker) takes the seat it last fed at: its seeded crystal is eaten in minutes, and
            // homed to a spent seat a lurker starved beside nothing (round 11-10, Docs/SWARM_FAUNA.md §27)
            if (P.Solitary.WHome > 0f) Home[i] = Pos[i];
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
            Host[i] = 0; Ramp[i] = 0f;
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

        /// <summary>
        /// The way to food for an agent at <paramref name="p"/> (grid cell <paramref name="fc"/>): the gradient of its
        /// population's food field - and, within two grid cells of a food point inside its band, the point itself; where the
        /// field is flat (no food within its reach), the nearest such point.
        /// <para>Round 11-10 (Docs/SWARM_FAUNA.md §27): the field is a 60 u grid, and the game's food is one point per
        /// flora HEART whose leaves sit 6-30 u around it - so the field's peak is one cell wide and its gradient there is
        /// ~0. Agents homed to within a cell (54-110 u) of a heart and circled there, out of their 24 u bite, and
        /// starved beside the food (the showcase cell: 2 bites in 645 asks). The research fed on DENSE food (every leaf
        /// prism scattered over the world), where any cell's peak is a bite away; the final approach restores that at the
        /// game's sparse hearts. It reads only food the agent may eat (inside its band), and the kernel normalises the
        /// heading, so only its direction matters.</para>
        /// </summary>
        Vector3 FoodHeading(int group, Vector3 p, int fc, ReadOnlySpan<SubstrateFood> food)
        {
            float best = float.MaxValue;
            int bi = -1;
            for (int j = 0; j < food.Length; j++)
            {
                if (food[j].Volume <= 0f || !Fields.InFoodBand(group, food[j].Pos)) continue;
                float d = Vector3.DistanceSquared(food[j].Pos, p);
                if (d < best) { best = d; bi = j; }
            }
            if (bi < 0) return Fields.FoodGrad(group, fc);
            if (best <= 4f * Fields.H * Fields.H) return food[bi].Pos - p;   // within two grid cells: the final approach
            var g = Fields.FoodGrad(group, fc);
            // Beyond the field's reach (4 blur passes: ~4 cells, 240 u) the field is exactly flat, and a hungry agent there
            // only wandered: a stampede herd seeded across a 55-degree sector of a 6-10 plant shell asked 5,448 times for
            // food and never found any. It heads for the nearest food it may eat instead - the way a herd walks to a
            // pasture it knows. Still hunger-weighted (w_food x hunger), so a fed agent does not leave its ground.
            return g == Vector3.Zero ? food[bi].Pos - p : g;
        }

        public void Step(ReadOnlySpan<SubstratePilot> pilots, ReadOnlySpan<SubstrateFood> food)
        {
            BeginStep(pilots, food);
            RunAgentPass();
            EndStep();
        }

        /// <summary>
        /// The tick's first third (round 11b-2): inputs, the fields, and per active population its live list, its moments,
        /// its closure, its kernel numbers (<see cref="SubstratePopulation.Kernel"/>) and the field samples every live
        /// agent reads - so the agent pass that follows needs nothing but arrays (the game runs it as a Burst job).
        /// </summary>
        public void BeginStep(ReadOnlySpan<SubstratePilot> pilots, ReadOnlySpan<SubstrateFood> food)
        {
            Events.Clear();
            EatRequests.Clear();
            PreyRequests.Clear();
            _npil = Math.Min(pilots.Length, MaxPilots);
            for (int j = 0; j < _npil; j++) _pilots[j] = pilots[j];
            // each pilot's turn this tick (a rider's grip reads it; its seat on the hull turns with it)
            for (int j = 0; j < _npil; j++)
            {
                var v = _pilots[j].Vel;
                var prev = _prevVel.TryGetValue(_pilots[j].Id, out var pv) ? pv : v;
                var a = Unit(prev); var b = Unit(v);
                _pilotPrevDir[j] = a;
                _pilotOmega[j] = a == Vector3.Zero || b == Vector3.Zero ? 0f : MathF.Acos(Math.Clamp(Vector3.Dot(a, b), -1f, 1f)) / Dt;
            }
            for (int j = 0; j < _npil; j++) _prevVel[_pilots[j].Id] = _pilots[j].Vel;
            long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
            // round 11f-2: with every population frozen nobody reads or writes the fields - they hold until one thaws
            bool anyRunning = false;
            for (int q = 0; q < Pops.Count; q++) anyRunning |= Pops[q].Active && !Pops[q].Frozen;
            if (anyRunning)
            {
                // only the food fields a running population reads are rebuilt
                Fields.MarkFoodUsed(0, false);
                for (int q = 0; q < Pops.Count; q++) Fields.MarkFoodUsed(Pops[q].FoodGroup, false);
                for (int q = 0; q < Pops.Count; q++)
                    if (Pops[q].Active && !Pops[q].Frozen) Fields.MarkFoodUsed(Pops[q].FoodGroup, true);
                Fields.Update(food, pilots.Slice(0, _npil));
            }
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
            {
                var pop = Pops[q];
                if (!pop.Active || pop.Frozen) { pop.LiveCount = 0; continue; }   // a frozen block is in no pass
                // live, not-starving agents (research A = alive & ~dying)
                int n = 0;
                for (int i = pop.Start; i < pop.Start + pop.Cap; i++)
                    if (Alive[i] && !Starving[i]) pop.Live[n++] = i;
                pop.LiveCount = n;
                t0 = System.Diagnostics.Stopwatch.GetTimestamp();
                BuildMoments(pop);
                PilotMoments(pop);
                MsHash += Ms(t0);
                t0 = System.Diagnostics.Stopwatch.GetTimestamp();
                bool seeksFood = pop.P.Solitary.WFood != 0f || pop.P.Gregarious.WFood != 0f;
                for (int k = 0; k < n; k++)
                {
                    int i = pop.Live[k];
                    int fc = Fields.Cell(Pos[i]);
                    FThreat[i] = Fields.Sample(SubstrateFields.Threat, fc);
                    FAlarm[i] = Fields.Sample(SubstrateFields.Alarm, fc);
                    GFood[i] = seeksFood ? FoodHeading(pop.FoodGroup, Pos[i], fc, food) : Vector3.Zero;
                    GScent[i] = Fields.Grad(SubstrateFields.Scent, fc);
                    GAlarm[i] = Fields.Grad(SubstrateFields.Alarm, fc);
                    GThreat[i] = Fields.Grad(SubstrateFields.Threat, fc);
                }
                if (pop.SlotW.Length > 0 && pop.BodyActive)
                {
                    // each member's slot in the body (its world position and velocity) - read by the kernel's body term
                    int bk = pop.SlotW.Length;
                    for (int k = 0; k < n; k++)
                    {
                        int i = pop.Live[k];
                        int slot = (i - pop.Start) % bk;
                        SlotGoal[i] = pop.SlotW[slot]; SlotVel[i] = pop.SlotV[slot];
                    }
                }
                MsFields += Ms(t0);
                pop.Kernel = KernelPop(pop);
            }
        }

        /// <summary>The tick's agent pass, managed: <see cref="SubstrateKernel.StepAgent"/> for every live agent of every
        /// active population, in population order (a predator reads its prey's moved positions), split over
        /// <see cref="Workers"/> threads in contiguous chunks. The game runs the SAME kernel as SubstrateAgentJob.</summary>
        public void RunAgentPass()
        {
            long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
            for (int q = 0; q < Pops.Count; q++)
            {
                var pop = Pops[q];
                int n = pop.LiveCount;
                if (!pop.Active || n == 0) continue;
                if (Stepper != null)
                {
                    Span<float> I = stackalloc float[pop.Dirs.Length];
                    Span<float> G = stackalloc float[pop.Dirs.Length];
                    for (int k = 0; k < n; k++) Stepper(this, pop, k, I, G);
                }
                else if (Workers > 1 && n > 256)
                {
                    int chunk = (n + Workers - 1) / Workers;
                    System.Threading.Tasks.Parallel.For(0, Workers, w => StepRange(pop, w * chunk, Math.Min(n, (w + 1) * chunk)));
                }
                else StepRange(pop, 0, n);
            }
            double ms = Ms(t0);
            MsKernel += ms;
            MsAgents += ms;
        }

        void StepRange(SubstratePopulation pop, int from, int to)
        {
            if (from >= to) return;
            var soa = SoA(pop);
            var world = KernelWorld;
            Span<float> I = stackalloc float[pop.Dirs.Length];
            Span<float> G = stackalloc float[pop.Dirs.Length];
            for (int k = from; k < to; k++) SubstrateKernel.StepAgent(soa, pop.Kernel, world, k, I, G);
        }

        /// <summary>One agent through the kernel (the harness's bit-match test calls it agent by agent).</summary>
        internal void KernelStep(SubstratePopulation pop, int k, Span<float> I, Span<float> G)
        {
            var soa = SoA(pop);
            SubstrateKernel.StepAgent(soa, pop.Kernel, KernelWorld, k, I, G);
        }

        /// <summary>Replaces the agent pass's kernel call, agent by agent and on this thread (harness group K: run the
        /// pre-11c managed step and the kernel side by side). Null in play.</summary>
        internal AgentStepper Stepper;
        internal delegate void AgentStepper(SubstrateCore core, SubstratePopulation pop, int k, Span<float> I, Span<float> G);

        /// <summary>The tick's last third: per active population the starving drift, the gait, the field deposits and
        /// the world pass (posture, danger, food, starvation, bites, births); then the clock.</summary>
        public void EndStep()
        {
            for (int q = 0; q < Pops.Count; q++)
            {
                var pop = Pops[q];
                if (!pop.Active) continue;
                if (pop.Frozen) FrozenPopulation(pop);
                else EndPopulation(pop);
            }
            T += Dt;
        }

        /// <summary>This tick's world numbers as the kernel reads them.</summary>
        public SubstrateKernelWorld KernelWorld => new SubstrateKernelWorld { Dt = Dt, R = R, Tick = Tick, NPil = _npil };

        /// <summary>The pilots this tick sees (the first <see cref="PilotCount"/>).</summary>
        public ReadOnlySpan<SubstratePilot> TickPilots => new ReadOnlySpan<SubstratePilot>(_pilots, 0, _npil);
        public int PilotCount => _npil;

        /// <summary>The core's arrays as the kernel's struct-of-arrays, with population <paramref name="pop"/>'s tables.</summary>
        internal SubstrateAgentSoA SoA(SubstratePopulation pop) => new SubstrateAgentSoA
        {
            Pos = Pos, Vel = Vel, IDir = IDir, Home = Home,
            Hunger = Hunger, Fear = Fear, Curious = Curious, Aggr = Aggr, Phase = Phase, QTarget = QTarget, ISpeed = ISpeed,
            Steered = Steered, Watched = Watched, Creeping = Creeping,
            WSeed = WSeed, Closure = Closure, RingGate = RingGate, Rest = Rest, Alive = Alive, Starving = Starving, ClaimedTick = ClaimedTick,
            Pilots = _pilots, Live = pop.Live, Key = pop.Key, Tab = pop.Tab, Agg = pop.Agg, Dirs = pop.Dirs,
            FThreat = FThreat, FAlarm = FAlarm, GFood = GFood, GScent = GScent, GAlarm = GAlarm, GThreat = GThreat,
            Ramp = Ramp, Attach = Attach, Host = Host, SlotGoal = SlotGoal, SlotVel = SlotVel,
        };

        SubstrateKernelPop KernelPop(SubstratePopulation pop)
        {
            var P = pop.P;
            var prey = pop.PreyPop >= 0 ? Pops[pop.PreyPop] : null;
            return new SubstrateKernelPop
            {
                Rs = P.Solitary, Rg = P.Gregarious,
                Metabolism = P.Metabolism, Sense = P.Sense, FearGain = P.FearGain, FearDecay = P.FearDecay,
                CuriosityRate = P.CuriosityRate, AggrBase = P.AggrBase, AttnR = P.AttnR, AttnUrg = P.AttnUrg,
                QUp = P.QUp, QDown = P.QDown, QRate = P.QRate, QWidth = P.QWidth, QContagion = P.QContagion,
                QWDens = P.QWDens, QWProx = P.QWProx, QWAlarm = P.QWAlarm, QWClose = P.QWClose, QHunger = P.QHunger,
                DensNorm = P.DensNorm, NbrR = P.NbrR,
                WPrey = P.WPrey, PreySense = P.PreySense, Momentum = P.Momentum, IntentBlend = P.IntentBlend,
                RestSpeed = P.RestSpeed, WRestRetreat = P.WRestRetreat,
                WCreep = P.WCreep, CreepMin = P.CreepMin, CreepR = P.CreepR, CreepSpeed = P.CreepSpeed,
                CreepLeadS = P.CreepLeadS, GazeCos = P.GazeCos, Freeze = P.Freeze,
                BandInner = pop.BandInner, BandOuter = pop.BandOuter, RingHoldS = P.RingHoldSeconds,
                SectorX = pop.SectorAxis.X, SectorY = pop.SectorAxis.Y, SectorZ = pop.SectorAxis.Z, SectorCos = pop.SectorCos,
                HasSector = (byte)(pop.HasSector ? 1 : 0),
                RampS = P.RampS, RampSpeed = P.RampSpeed, RampOnSight = (byte)(P.RampOnSight ? 1 : 0), WStrike = P.WStrike, StrikeSpeed = P.StrikeSpeed,
                StrikeAccel = P.StrikeAccel, StrikeTurn = P.StrikeTurn, HuntLeadMax = P.HuntLeadMax, WAlarmClimb = P.WAlarmClimb,
                QWProvoke = P.QWProvoke, SlowBelow = P.SlowBelow, RoostR = P.RoostR, WJink = P.WJink, JinkR = P.JinkR,
                JinkCos = P.JinkCos, BodyWell = P.BodyWell, ChargeEvery = P.ChargeEvery,
                RestHoldsPhase = (byte)(P.RestHoldsPhase ? 1 : 0), Cling = (byte)(P.ClingMax > 0 ? 1 : 0),
                HasBody = (byte)(pop.SlotW.Length > 0 && pop.BodyActive ? 1 : 0),
                FracK = P.FracK, RingRoles = P.RingRoles, NDirs = pop.Dirs.Length, Start = pop.Start,
                PreyStart = prey != null ? prey.Start : 0, PreyCap = prey != null ? prey.Cap : 0, LiveCount = pop.LiveCount,
                SpacingSpring = (byte)(P.SpacingSpring ? 1 : 0), HasPrey = (byte)(prey != null ? 1 : 0),
                M = pop.M, TabMask = pop.Tab.Length - 1,
            };
        }

        void EndPopulation(SubstratePopulation pop)
        {
            var P = pop.P;
            float dt = Dt;
            int n = pop.LiveCount;
            long t0 = System.Diagnostics.Stopwatch.GetTimestamp();

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

            // deposits into the shared fields (pending until the next Fields.Update: no agent reads them this tick)
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
            if (P.RingHoldSeconds > 0f) RingHoldClock(pop);
        }

        /// <summary>The opt-in ring hold (<see cref="SubstrateSpeciesParams.RingHoldSeconds"/>): one clock per pilot. It
        /// runs while the closure around that pilot is saturated (>= q_up), waits while the ring loosens (q_down..q_up),
        /// and resets when the pilot breaks out (below q_down) or a packmate around it is resting (a fresh ring must hold
        /// again). Closure is a per-pilot moment, so every hunter around one pilot reads the same clock: they release
        /// together.</summary>
        /// <summary>During the ring hold a hunter's phase is held at or below this fraction of its danger phase.</summary>
        public const float HoldPhase = 0.4f;

        void RingHoldClock(SubstratePopulation pop)
        {
            var P = pop.P;
            for (int j = 0; j < _npil; j++)
            {
                float c = -1f; bool resting = false;
                for (int q = 0; q < pop.LiveCount; q++)
                {
                    int i = pop.Live[q];
                    if (NearestPilot(Pos[i], out float d) != j || d >= P.CloseR) continue;
                    c = MathF.Max(c, Closure[i]);
                    resting |= Rest[i] > 0f;
                }
                int id = _pilots[j].Id;
                pop.RingHold.TryGetValue(id, out float h);
                if (c < P.QDown || resting) h = 0f;
                else if (c >= P.QUp) h = MathF.Min(P.RingHoldSeconds, h + Dt);
                // q_down..q_up: the ring has loosened but not broken - the clock waits
                pop.RingHold[id] = h;
            }
            for (int q = 0; q < pop.LiveCount; q++)
            {
                int i = pop.Live[q];
                int j = NearestPilot(Pos[i], out float d);
                RingGate[i] = j >= 0 && d < P.CloseR && pop.RingHold.TryGetValue(_pilots[j].Id, out float h) ? h / P.RingHoldSeconds : 0f;
            }
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
            bool cling = P.ClingMax > 0;
            if (cling) Ride(pop);
            if (P.BodyK > 0) Body(pop);
            if (P.RampTurns > 0) PickTurns(pop);

            for (int q = 0; q < n; q++)
            {
                int i = pop.Live[q];
                Grow[i] = MathF.Min(1f, Grow[i] + dt / MathF.Max(P.GrowS, 1e-3f));
                BiteCool[i] -= dt;
                bool riding = cling && Host[i] != 0;

                // the RAMP (round 11-11): a strike-role agent that is gregarious, not spent and has a pilot inside RampR
                // holds its windup RampS seconds before it may strike; once ramped it strikes until spent
                bool role = P.ChargeEvery <= 1 || ((i - pop.Start) % P.ChargeEvery) == 0;
                bool needRamp = P.RampS > 0f && role;
                if (needRamp && !riding)
                {
                    bool armed = false;
                    if ((P.RampOnSight || Phase[i] > P.DangerPhase) && Rest[i] <= 0f)
                    {
                        NearestPilot(Pos[i], out float pd);
                        armed = pd < P.RampR;
                    }
                    bool ramped = Ramp[i] >= P.RampS && Rest[i] <= 0f;   // striking: the posture clock ends it
                    if (ramped) Ramp[i] += dt;
                    else if (armed)
                    {
                        if (Ramp[i] > 0f) Ramp[i] += dt;                                       // mid-windup
                        else if (P.RampTurns > 0 && !_mayRamp[i - pop.Start]) Ramp[i] -= dt;   // waiting its turn (Ramp < 0)
                        else
                        {
                            pop.Windups++; Events.Add(new SubstrateEvent { Kind = SubstrateEventKind.Windup, Index = i });
                            Ramp[i] = dt;
                        }
                    }
                    else Ramp[i] = 0f;
                }

                // the opt-in ring hold (the strike gate): until it releases, a hunter that is not yet striking stays
                // near the stalk end (the kernel circles and tightens the ring meanwhile); released, every hunter
                // around that pilot climbs from the same phase at q_rate, so they cross the danger phase together
                if (P.RingHoldSeconds > 0f && !Danger[i] && RingGate[i] < 1f)
                {
                    float cap = HoldPhase * P.DangerPhase;
                    if (Phase[i] > cap) Phase[i] = cap;
                }

                // posture clock: a strike spends stamina; spent (or bitten-with) rests; rested, it recovers
                if (P.StaminaS > 0f && !riding)
                {
                    bool strikeNow = needRamp ? Ramp[i] >= P.RampS : Phase[i] > P.DangerPhase && Aggr[i] > 0.5f;
                    if (Rest[i] > 0f)
                    {
                        Rest[i] -= dt;
                        if (Rest[i] <= 0f) { Rest[i] = 0f; Stamina[i] = MathF.Max(Stamina[i], 0.5f * P.StaminaS); }
                    }
                    else if (strikeNow)
                    {
                        Stamina[i] -= dt;
                        if (Stamina[i] <= 0f) SendToRest(pop, i);
                    }
                    else Stamina[i] = MathF.Min(P.StaminaS, Stamina[i] + 0.5f * dt);
                }
                else if (!riding && Rest[i] > 0f) Rest[i] = MathF.Max(0f, Rest[i] - dt);   // a daze with no posture clock

                // danger: an aggressive agent at the gregarious end that is not spent (research harm = aggr > 0.5), or a
                // TRAMPLER - fast and running into the pilot (research trample; the bestiary's closing test); a ramped
                // role must have finished its windup; an assembled body member burns to touch
                bool was = Danger[i];
                bool now = false;
                if (!riding && Rest[i] <= 0f)
                {
                    // a ramped role's strike IS its harm (a bull's charge, a mobber's dive); anyone else bites by aggression
                    now = needRamp ? Ramp[i] >= P.RampS : Aggr[i] > 0.5f && Phase[i] > P.DangerPhase;
                    if (!now && P.Solitary.Trample + (P.Gregarious.Trample - P.Solitary.Trample) * Phase[i] > 0.5f)
                        now = Trampling(i, P.TrampleClose);
                }
                if (!riding && P.DangerAttached && pop.BodyActive && Attach[i] > 0.5f && Rest[i] <= 0f) now = true;
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
                    Starving[i] = true; Danger[i] = false; Host[i] = 0;
                    pop.Starvations++;
                    Events.Add(new SubstrateEvent { Kind = SubstrateEventKind.Starving, Index = i });
                }
            }
            pop.Striking = striking;

            if (cling) Latch(pop);
            else
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
                if (Stock[i] < P.BirthStock || Grow[i] < 1f || Starving[i] || Host[i] != 0) continue;
                int c = FreeSlot(pop);
                if (c < 0) break;
                float half = Stock[i] * 0.5f;
                Stock[i] -= half; Stock[c] = half;
                Pos[c] = Pos[i]; Vel[c] = Vel[i] * 0.5f; Home[c] = Home[i]; IDir[c] = IDir[i]; ISpeed[c] = ISpeed[i];
                Hunger[c] = Hunger[i]; Fear[c] = Fear[i]; Curious[c] = Curious[i]; Aggr[c] = Aggr[i];
                Phase[c] = Phase[i]; QTarget[c] = QTarget[i];
                Stamina[c] = P.StaminaS; Rest[c] = 0f; BiteCool[c] = 0f; Grow[c] = 0f; Closure[c] = 0f; RingGate[c] = 0f; GaitV[c] = Vector3.Zero;
                Ramp[c] = 0f; Attach[c] = Attach[i]; Grip[c] = 1f; SipT[c] = 0f; Host[c] = 0; HostOff[c] = Vector3.Zero;
                WSeed[c] = new Vector3((float)_rng.NextDouble() * 100f, (float)_rng.NextDouble() * 100f, (float)_rng.NextDouble() * 100f);
                Alive[c] = true; Starving[c] = false; Danger[c] = false; Watched[c] = false; Creeping[c] = false;
                BornTick[c] = Tick;
                pop.Births++; alive++;
                Events.Add(new SubstrateEvent { Kind = SubstrateEventKind.Born, Index = c, Other = i });
            }
            pop.Alive = alive;
        }

        /// <summary>
        /// TURNS (round 11-11, SubstrateSpeciesParams.RampTurns): per pilot, the agents already winding up or striking at
        /// it are counted; the free turns go to the agents that have WAITED longest (most negative Ramp). Marks
        /// _mayRamp[i - pop.Start]; read by the world pass's ramp.
        /// </summary>
        void PickTurns(SubstratePopulation pop)
        {
            var P = pop.P;
            if (_mayRamp.Length < pop.Cap) _mayRamp = new bool[pop.Cap];
            Array.Clear(_mayRamp, 0, pop.Cap);
            for (int j = 0; j < _npil; j++) _turns[j] = 0;
            int n = pop.LiveCount;
            for (int q = 0; q < n; q++)
            {
                int i = pop.Live[q];
                if (Ramp[i] <= 0f || Rest[i] > 0f) continue;
                int j = NearestPilot(Pos[i], out _);
                if (j >= 0) _turns[j]++;
            }
            for (int j = 0; j < _npil; j++)
                for (int free = P.RampTurns - _turns[j]; free > 0; free--)
                {
                    int pick = -1;
                    for (int q = 0; q < n; q++)
                    {
                        int i = pop.Live[q];
                        if (Ramp[i] >= 0f || _mayRamp[i - pop.Start] || NearestPilot(Pos[i], out _) != j) continue;
                        if (pick < 0 || Ramp[i] < Ramp[pick]) pick = i;
                    }
                    if (pick < 0) break;
                    _mayRamp[pick - pop.Start] = true;
                }
            // a newcomer (Ramp 0) with turns still free starts at once - it has waited no less than nobody
            for (int j = 0; j < _npil; j++)
            {
                int used = _turns[j];
                for (int q = 0; q < n; q++) { int i = pop.Live[q]; if (_mayRamp[i - pop.Start] && NearestPilot(Pos[i], out _) == j) used++; }
                for (int q = 0; q < n && used < P.RampTurns; q++)
                {
                    int i = pop.Live[q];
                    if (Ramp[i] != 0f || Rest[i] > 0f || _mayRamp[i - pop.Start] || NearestPilot(Pos[i], out _) != j) continue;
                    _mayRamp[i - pop.Start] = true; used++;
                }
            }
        }

        /// <summary>A trample: the agent is fast (research: above 60 u/s) and - when <paramref name="close"/> is above 0 -
        /// running INTO the nearest pilot (velocity . bearing above close x speed; bestiary stampede).</summary>
        bool Trampling(int i, float close)
        {
            float sp = Vel[i].Length();
            if (sp <= 60f) return false;
            if (close <= 0f) return true;
            int j = NearestPilot(Pos[i], out _);
            if (j < 0) return false;
            return Vector3.Dot(Vel[i], Unit(_pilots[j].Pos - Pos[i])) > close * sp;
        }

        int PilotById(int id)
        {
            for (int j = 0; j < _npil; j++) if (_pilots[j].Id == id) return j;
            return -1;
        }

        /// <summary>
        /// CLING (round 11-11, bestiary leech): every rider sits on its host's hull - its seat turns with the host's
        /// heading - sips it every SipS seconds (a danger contact of weight SipWeight: a drain), and loses grip while its
        /// host turns faster than GripTurn; at zero grip it is flung sideways and dazed. A host that left the cell drops
        /// its riders the same way.
        /// </summary>
        void Ride(SubstratePopulation pop)
        {
            var P = pop.P;
            float dt = Dt;
            for (int q = 0; q < pop.LiveCount; q++)
            {
                int i = pop.Live[q];
                if (Host[i] == 0) continue;
                int j = PilotById(Host[i]);
                if (j < 0) { Fling(pop, i, Vector3.Zero, Vector3.Zero, Host[i]); continue; }
                var pil = _pilots[j];
                var f = Unit(pil.Vel);
                var f0 = _pilotPrevDir[j];
                if (f != Vector3.Zero && f0 != Vector3.Zero)
                {
                    // the seat rides the hull frame: rotate it by the host's turn this tick
                    var ax = Vector3.Cross(f0, f);
                    float sn = ax.Length(), cs = Math.Clamp(Vector3.Dot(f0, f), -1f, 1f);
                    if (sn > 1e-6f)
                    {
                        ax /= sn;
                        var o = HostOff[i];
                        HostOff[i] = o * cs + Vector3.Cross(ax, o) * sn + ax * Vector3.Dot(ax, o) * (1f - cs);
                    }
                }
                Pos[i] = pil.Pos + HostOff[i];
                Vel[i] = pil.Vel;
                ClampMembrane(i);
                float om = _pilotOmega[j];
                Grip[i] = MathF.Min(1f, Grip[i] + (om > P.GripTurn ? -(om - P.GripTurn) * P.GripLoss : P.GripGain) * dt);
                SipT[i] += dt;
                if (SipT[i] >= P.SipS)
                {
                    SipT[i] = 0f;
                    pop.Sips++;
                    Events.Add(new SubstrateEvent { Kind = SubstrateEventKind.Sip, Index = i, Other = pil.Id, Value = P.SipWeight });
                }
                if (Grip[i] <= 0f) Fling(pop, i, pil.Vel, f, pil.Id);
            }
        }

        void Fling(SubstratePopulation pop, int i, Vector3 hostVel, Vector3 f, int id)
        {
            var P = pop.P;
            var side = Unit(Vector3.Cross(f, Vector3.UnitY) + new Vector3(1e-6f, 0f, 0f));
            if (side == Vector3.Zero) side = Unit(HostOff[i]);
            float sign = ((int)WSeed[i].X & 1) == 0 ? 1f : -1f;
            Vel[i] = hostVel * 0.5f + side * (sign * P.FlingSpeed);
            Host[i] = 0; HostOff[i] = Vector3.Zero; Grip[i] = 1f; SipT[i] = 0f; Ramp[i] = 0f;
            Danger[i] = false; QTarget[i] = 0f; Phase[i] = 0f;
            Rest[i] = MathF.Max(Rest[i], P.DazeS);   // dazed: harmless, slow
            pop.Shaken++;
            Events.Add(new SubstrateEvent { Kind = SubstrateEventKind.Shaken, Index = i, Other = id });
        }

        /// <summary>A free agent that touches a pilot while pouncing (or met slower than ClingRelSpeed) latches onto its
        /// hull - at most ClingMax per hull - instead of biting (bestiary leech: the pounce is harmless, the ride is not).</summary>
        void Latch(SubstratePopulation pop)
        {
            var P = pop.P;
            for (int j = 0; j < _npil; j++) _load[j] = 0;
            for (int q = 0; q < pop.LiveCount; q++)
            {
                int h = Host[pop.Live[q]];
                if (h == 0) continue;
                int j = PilotById(h);
                if (j >= 0) _load[j]++;
            }
            for (int q = 0; q < pop.LiveCount; q++)
            {
                int i = pop.Live[q];
                if (Host[i] != 0 || Rest[i] > 0f) continue;
                int j = NearestPilot(Pos[i], out float d);
                if (j < 0 || _load[j] >= P.ClingMax) continue;
                var pil = _pilots[j];
                if (d >= P.BiteR + pil.Radius) continue;
                float rel = Vector3.Distance(pil.Vel, Vel[i]);
                if (!Danger[i] && rel >= P.ClingRelSpeed) continue;
                var seat = Unit(Pos[i] - pil.Pos);
                if (seat == Vector3.Zero) seat = Vector3.UnitY;
                Host[i] = pil.Id; HostOff[i] = seat * (pil.Radius + 1.5f); Grip[i] = 1f; SipT[i] = 0f;
                Pos[i] = pil.Pos + HostOff[i]; Vel[i] = pil.Vel;
                Danger[i] = false; Ramp[i] = 0f;
                _load[j]++;
                pop.Latches++;
                Events.Add(new SubstrateEvent { Kind = SubstrateEventKind.Latch, Index = i, Other = pil.Id });
            }
        }

        /// <summary>
        /// The BODY (research core.py, round 11-11): a group quorum on the school's mean hunger and fear assembles it
        /// (sated) and dissolves it (hungry again); each member's attachment relaxes toward that; the body - an agent one
        /// level up - steers its heading by the school's summed drives at its centre (food when hungry, away from a pilot
        /// when afraid, curious toward one, off the wall and the pen), flies at BodySpeed, and GULPS a pilot ahead of its
        /// mouth (bestiary: the jaws flare GulpRampS, then a surge). Every slot's world position and rigid-body velocity
        /// are what the members chase next tick.
        /// </summary>
        void Body(SubstratePopulation pop)
        {
            var P = pop.P;
            float dt = Dt;
            int n = pop.LiveCount;
            if (n == 0) { pop.BodyActive = false; return; }
            float gh = 0f, gf = 0f;
            for (int q = 0; q < n; q++) { int i = pop.Live[q]; gh += MathF.Min(1f, Hunger[i]); gf += Fear[i]; }
            gh /= n; gf /= n;
            if (!pop.Assembling && (gh < P.AttachOnH || gf > P.AttachOnF)) pop.Assembling = true;
            else if (pop.Assembling && gh > P.AttachOffH && gf < 0.5f * P.AttachOnF) pop.Assembling = false;
            float tgt = pop.Assembling ? 1f : 0f;
            int att = 0;
            var cm = Vector3.Zero;
            for (int q = 0; q < n; q++)
            {
                int i = pop.Live[q];
                Attach[i] += dt * P.AttachRate * (tgt - Attach[i]);
                if (Attach[i] > 0.5f) { att++; cm += Pos[i]; }
            }
            if (att == 0)
            {
                if (pop.BodyActive) { pop.Dissolves++; Events.Add(new SubstrateEvent { Kind = SubstrateEventKind.Dissolve, Index = pop.Start }); }
                pop.BodyActive = false; pop.GulpPrep = 0f; pop.Gulp = 0f;
                return;
            }
            cm /= att;
            int K = pop.SlotW.Length;
            if (!pop.BodyActive)
            {
                pop.Assemblies++;
                Events.Add(new SubstrateEvent { Kind = SubstrateEventKind.Assemble, Index = pop.Start, Value = att });
            }
            if (!pop.BodyActive || att < Math.Max(4, K / 3)) pop.BodyC = cm;
            else pop.BodyC = pop.BodyC + pop.BodyV * dt + 0.05f * (cm - pop.BodyC);
            pop.BodyActive = true;
            var c = pop.BodyC;

            // the heading: the school's drives at the body centre
            // (a banded school reads its own band's food field, as its members do - round 11-14: the cell-wide field led
            // the leviathan's body to the inner forest, out of its pen)
            var want = Fields.FoodGrad(pop.FoodGroup, Fields.Cell(c)) * (50f * gh);   // group 0 = the cell-wide field, as before
            int pj = NearestPilot(c, out float pd);
            if (pj >= 0)
            {
                want += Unit(c - _pilots[pj].Pos) * (2f * gf);
                if (P.BodyCurious > 0f && pd < P.BodyCuriousR) want += Unit(_pilots[pj].Pos - c) * P.BodyCurious;
            }
            float r = c.Length();
            // the research school's own containment (inside 0.6 R) - unless it is penned: then its pen alone keeps it (the
            // 0.6 R term sits INSIDE the Swarm cell's 690-840 middle shell and held the body below its band)
            if (pop.BandOuter <= 0f) want -= c / MathF.Max(r, 1f) * Math.Clamp((r - 0.6f * R) / (0.2f * R), 0f, 2f);
            want += PenPull(pop, c);
            float tt = Tick * 0.02f;
            want += 0.3f * new Vector3(MathF.Sin(tt), MathF.Sin(1.3f * tt + 1f), MathF.Sin(0.7f * tt + 2f));
            var f0 = pop.BodyF;
            var u = Unit(want);
            if (u != Vector3.Zero)
            {
                float ang = MathF.Acos(Math.Clamp(Vector3.Dot(pop.BodyF, u), -1f, 1f));
                float kk = MathF.Min(1f, 0.35f * dt / MathF.Max(ang, 1e-6f));
                var nf = Unit(pop.BodyF + (u - pop.BodyF) * kk);
                if (nf != Vector3.Zero) pop.BodyF = nf;
            }

            // the GULP (bestiary leviathan): a pilot ahead of the mouth holds the jaws open GulpRampS, then a surge
            float spd = P.BodySpeed * (1f + 1.5f * gf);
            if (P.GulpR > 0f)
            {
                var mouth = c + pop.BodyF * (pop.MouthZ * P.BodyScale);
                bool ahead = false;
                if (pj >= 0)
                {
                    var dm = _pilots[pj].Pos - mouth;
                    ahead = dm.Length() < P.GulpR && Vector3.Dot(Unit(dm), pop.BodyF) > P.GulpCos;
                }
                pop.GulpRest = MathF.Max(0f, pop.GulpRest - dt);
                if (pop.Gulp > 0f)
                {
                    pop.Gulp -= dt;
                    if (pop.Gulp <= 0f) { pop.Gulp = 0f; pop.GulpRest = P.GulpRestS; }
                }
                else if (ahead && pop.GulpRest <= 0f)
                {
                    pop.GulpPrep += dt;
                    if (pop.GulpPrep >= P.GulpRampS)
                    {
                        pop.Gulp = P.GulpS; pop.GulpPrep = 0f; pop.Gulps++;
                        Events.Add(new SubstrateEvent { Kind = SubstrateEventKind.Gulp, Index = pop.Start });
                    }
                }
                else pop.GulpPrep = MathF.Max(0f, pop.GulpPrep - 2f * dt);
                if (pop.Gulp > 0f) spd = P.GulpSpeed;
            }
            pop.BodyV = 0.9f * pop.BodyV + 0.1f * pop.BodyF * spd;

            // every slot: its world position (the jaws flare while the gulp is primed) and its rigid-body velocity
            var om = Vector3.Cross(f0, pop.BodyF) / dt;
            var a = Unit(Vector3.Cross(Vector3.UnitY, pop.BodyF) + new Vector3(1e-6f, 1e-6f, 1e-6f));
            var b = Vector3.Cross(pop.BodyF, a);
            float jawOpen = P.GulpRampS > 0f ? MathF.Min(1f, pop.GulpPrep / P.GulpRampS + (pop.Gulp > 0f ? 1f : 0f)) : 0f;
            float mz = MathF.Max(pop.MouthZ, 1e-3f);
            for (int k = 0; k < K; k++)
            {
                float ox = P.BodySlots[3 * k] * P.BodyScale, oy = P.BodySlots[3 * k + 1] * P.BodyScale, oz = P.BodySlots[3 * k + 2] * P.BodyScale;
                if (jawOpen > 0f)
                {
                    float jaw = Math.Clamp((P.BodySlots[3 * k + 2] / mz - 0.6f) / 0.4f, 0f, 1f) * jawOpen;
                    ox *= 1f + 1.2f * jaw; oy *= 1f + 1.2f * jaw;
                }
                var off = ox * a + oy * b + oz * pop.BodyF;
                pop.SlotW[k] = c + off;
                pop.SlotV[k] = pop.BodyV + Vector3.Cross(om, off);
            }
        }

        /// <summary>The pen's pull at a point (band and sector), for a body that steers one level up.</summary>
        Vector3 PenPull(SubstratePopulation pop, Vector3 p)
        {
            var pull = Vector3.Zero;
            float r = p.Length();
            if (r < 1e-3f) return pull;
            var radial = p / r;
            if (pop.BandOuter > 0f)
            {
                float soft = 0.1f * MathF.Max(pop.BandOuter - pop.BandInner, 50f);
                // ramped inside the edge, full at it (as SubstrateKernel's pen)
                pull -= radial * (Math.Clamp((r - pop.BandOuter + soft) / soft, 0f, 1f) * 2f);
                pull += radial * (Math.Clamp((pop.BandInner + soft - r) / soft, 0f, 1f) * 2f);
            }
            if (pop.HasSector)
            {
                float cs = Vector3.Dot(radial, pop.SectorAxis);
                float w = Math.Clamp((pop.SectorCos + SubstrateKernel.PenSectorSoft - cs) / SubstrateKernel.PenSectorSoft, 0f, 1f) * 2f;
                if (w > 0f) pull += Unit(pop.SectorAxis * r - p) * w;
            }
            return pull;
        }

        /// <summary>
        /// A frozen population's whole tick (round 11f-2): metabolism only - hunger += Metabolism * Dt, exactly the kernel's
        /// rule - so a collapsed population is hungry by the same amount when it thaws. Nothing moves, eats, breeds, strikes
        /// or is flagged starving here; the owner thaws it first (<see cref="ReserveSeconds"/>).
        /// </summary>
        void FrozenPopulation(SubstratePopulation pop)
        {
            float rise = FreezeBug == SubstrateFreezeBug.NoMetabolism ? 0f : pop.P.Metabolism * Dt;
            int alive = 0;
            for (int i = pop.Start; i < pop.Start + pop.Cap; i++)
            {
                if (!Alive[i]) continue;
                alive++;
                Danger[i] = false;
                if (!Starving[i]) Hunger[i] += rise;
            }
            pop.Alive = alive;
            pop.Striking = 0;
        }

        /// <summary>
        /// Seconds until the hungriest living, not-yet-starving agent of population <paramref name="q"/> spends its
        /// reserve (hunger reaches 1 + StarveS * Metabolism) at its metabolism; 0 when one is already starving,
        /// +infinity when none can starve. Read between ticks.
        /// </summary>
        public float ReserveSeconds(int q)
        {
            var pop = Pops[q];
            var P = pop.P;
            if (!(P.Metabolism > 0f)) return float.PositiveInfinity;
            float reserve = 1f + P.StarveS * P.Metabolism, left = float.PositiveInfinity;
            for (int i = pop.Start; i < pop.Start + pop.Cap; i++)
            {
                if (!Alive[i]) continue;
                if (Starving[i]) return 0f;
                left = MathF.Min(left, (reserve - Hunger[i]) / P.Metabolism);
            }
            return MathF.Max(0f, left);
        }

        /// <summary>
        /// Population <paramref name="q"/>'s stomachs as VOLUME (round 11f-2: the IMacroPopulation totals): each agent's
        /// reserve left, (1 + StarveS * Metabolism - hunger) / HungerPerVol - the food volume that would bring it back to
        /// a full stomach's worth of reserve is what it has left to burn. Read between ticks.
        /// </summary>
        public double ReserveVolume(int q)
        {
            var pop = Pops[q];
            var P = pop.P;
            if (!(P.HungerPerVol > 0f)) return 0;
            float reserve = 1f + P.StarveS * P.Metabolism;
            double v = 0;
            for (int i = pop.Start; i < pop.Start + pop.Cap; i++)
                if (Alive[i]) v += Math.Max(0f, reserve - Hunger[i]) / P.HungerPerVol;
            return v;
        }

        /// <summary>
        /// Moves every living agent of population <paramref name="q"/> (and its home) rigidly by <paramref name="d"/> - a
        /// collapsed population drifting as one body. Between ticks only. Stock - the body volume - is untouched. The
        /// tick job draws the move as a glide (its next frame interpolates from where the agents were drawn).
        /// </summary>
        public void Translate(int q, Vector3 d)
        {
            var pop = Pops[q];
            for (int i = pop.Start; i < pop.Start + pop.Cap; i++)
            {
                if (!Alive[i]) continue;
                Pos[i] += d; Home[i] += d;
            }
        }

        /// <summary>Planted bugs for the substrate LOD gate (harness only; always None in play).</summary>
        internal SubstrateFreezeBug FreezeBug;

        void SendToRest(SubstratePopulation pop, int i)
        {
            if (Rest[i] > 0f) return;
            var P = pop.P;
            Rest[i] = P.RestS; Stamina[i] = 0f; Danger[i] = false; Ramp[i] = 0f;
            if (!P.RestHoldsPhase) QTarget[i] = 0f;
            Events.Add(new SubstrateEvent { Kind = SubstrateEventKind.Rest, Index = i });
        }

        // ───────────────────────────────────────────────────────────── helpers

        void ClampMembrane(int i)
        {
            float r = Pos[i].Length();
            if (r > 0.98f * R) Pos[i] *= 0.98f * R / r;
        }

        /// <summary>The nearest living, unclaimed prey agent within <paramref name="radius"/> (prey pools are small: a scan).</summary>
        internal int NearestPrey(SubstratePopulation prey, Vector3 p, float radius, out float dist)
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

        internal int NearestPilot(Vector3 p, out float dist)
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

        internal static long Hash(long k) => unchecked(k * (long)0x9E3779B97F4A7C15) & 0x7FFFFFFFFFFFFFFF;

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
