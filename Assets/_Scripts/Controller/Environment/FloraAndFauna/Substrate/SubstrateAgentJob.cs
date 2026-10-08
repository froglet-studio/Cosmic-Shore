using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using SVector3 = System.Numerics.Vector3;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Round 11b-2 (Docs/SUBSTRATE_FAUNA.md §7): the substrate's agent pass, Burst-compiled. One
    /// <see cref="SubstrateKernel.StepAgent"/> per live agent of ONE population - the SAME static function the harness
    /// runs and bit-matches against the managed step it replaced (Tools/Build/substrate_harness group K;
    /// check_burst_substrate.py is the gate that it stays Burst-compilable). Agent q writes only its own slot
    /// <c>Live[q]</c>, so the written arrays opt out of the parallel-for index restriction; a predator reads its prey's
    /// positions, so the populations' jobs are chained in population order (<see cref="SubstrateAgentPass"/>).
    /// </summary>
    [BurstCompile]
    public struct SubstrateAgentJob : IJobParallelFor
    {
        // written: slot Live[q] by agent q only
        [NativeDisableParallelForRestriction] public NativeArray<SVector3> Pos;
        [NativeDisableParallelForRestriction] public NativeArray<SVector3> Vel;
        [NativeDisableParallelForRestriction] public NativeArray<SVector3> IDir;
        [NativeDisableParallelForRestriction] public NativeArray<SVector3> Home;
        [NativeDisableParallelForRestriction] public NativeArray<float> Hunger;
        [NativeDisableParallelForRestriction] public NativeArray<float> Fear;
        [NativeDisableParallelForRestriction] public NativeArray<float> Curious;
        [NativeDisableParallelForRestriction] public NativeArray<float> Aggr;
        [NativeDisableParallelForRestriction] public NativeArray<float> Phase;
        [NativeDisableParallelForRestriction] public NativeArray<float> QTarget;
        [NativeDisableParallelForRestriction] public NativeArray<float> ISpeed;
        [NativeDisableParallelForRestriction] public NativeArray<bool> Steered;
        [NativeDisableParallelForRestriction] public NativeArray<bool> Watched;
        [NativeDisableParallelForRestriction] public NativeArray<bool> Creeping;
        // read
        [ReadOnly] public NativeArray<SVector3> WSeed;
        [ReadOnly] public NativeArray<float> Closure;
        [ReadOnly] public NativeArray<float> RingGate;
        [ReadOnly] public NativeArray<float> Rest;
        [ReadOnly] public NativeArray<bool> Alive;
        [ReadOnly] public NativeArray<bool> Starving;
        [ReadOnly] public NativeArray<long> ClaimedTick;
        [ReadOnly] public NativeArray<SubstratePilot> Pilots;
        [ReadOnly] public NativeArray<int> Live;
        [ReadOnly] public NativeArray<long> Key;
        [ReadOnly] public NativeArray<long> Tab;
        [ReadOnly] public NativeArray<double> Agg;
        [ReadOnly] public NativeArray<SVector3> Dirs;
        [ReadOnly] public NativeArray<float> FThreat;
        [ReadOnly] public NativeArray<float> FAlarm;
        [ReadOnly] public NativeArray<SVector3> GFood;
        [ReadOnly] public NativeArray<SVector3> GScent;
        [ReadOnly] public NativeArray<SVector3> GAlarm;
        [ReadOnly] public NativeArray<SVector3> GThreat;
        // round 11-11: the ramp clock, the body attachment, a rider's host, each member's body slot
        [ReadOnly] public NativeArray<float> Ramp;
        [ReadOnly] public NativeArray<float> Attach;
        [ReadOnly] public NativeArray<int> Host;
        [ReadOnly] public NativeArray<SVector3> SlotGoal;
        [ReadOnly] public NativeArray<SVector3> SlotVel;
        public SubstrateKernelPop Pop;
        public SubstrateKernelWorld World;

        /// <summary>Agents per worker batch: a steered agent is a few thousand flops, an unsteered one a few hundred.</summary>
        public const int BatchSize = 32;

        public void Execute(int q)
        {
            SubstrateAgentSoA s = default;
            s.Pos = Pos.AsSpan(); s.Vel = Vel.AsSpan(); s.IDir = IDir.AsSpan(); s.Home = Home.AsSpan();
            s.Hunger = Hunger.AsSpan(); s.Fear = Fear.AsSpan(); s.Curious = Curious.AsSpan(); s.Aggr = Aggr.AsSpan();
            s.Phase = Phase.AsSpan(); s.QTarget = QTarget.AsSpan(); s.ISpeed = ISpeed.AsSpan();
            s.Steered = Steered.AsSpan(); s.Watched = Watched.AsSpan(); s.Creeping = Creeping.AsSpan();
            s.WSeed = WSeed.AsReadOnlySpan(); s.Closure = Closure.AsReadOnlySpan(); s.RingGate = RingGate.AsReadOnlySpan(); s.Rest = Rest.AsReadOnlySpan();
            s.Alive = Alive.AsReadOnlySpan(); s.Starving = Starving.AsReadOnlySpan(); s.ClaimedTick = ClaimedTick.AsReadOnlySpan();
            s.Pilots = Pilots.AsReadOnlySpan(); s.Live = Live.AsReadOnlySpan(); s.Key = Key.AsReadOnlySpan();
            s.Tab = Tab.AsReadOnlySpan(); s.Agg = Agg.AsReadOnlySpan(); s.Dirs = Dirs.AsReadOnlySpan();
            s.FThreat = FThreat.AsReadOnlySpan(); s.FAlarm = FAlarm.AsReadOnlySpan();
            s.GFood = GFood.AsReadOnlySpan(); s.GScent = GScent.AsReadOnlySpan();
            s.GAlarm = GAlarm.AsReadOnlySpan(); s.GThreat = GThreat.AsReadOnlySpan();
            s.Ramp = Ramp.AsReadOnlySpan(); s.Attach = Attach.AsReadOnlySpan(); s.Host = Host.AsReadOnlySpan();
            s.SlotGoal = SlotGoal.AsReadOnlySpan(); s.SlotVel = SlotVel.AsReadOnlySpan();
            Span<float> I = stackalloc float[SubstrateKernel.MaxDirs];
            Span<float> G = stackalloc float[SubstrateKernel.MaxDirs];
            SubstrateKernel.StepAgent(s, Pop, World, q, I, G);
        }
    }

    /// <summary>
    /// The main thread's half of the split tick (round 11b-2): while the cell's <see cref="SubstrateTickJob"/> is parked
    /// after <see cref="SubstrateCore.BeginStep"/>, copy the core's arrays into persistent NativeArrays, schedule one
    /// <see cref="SubstrateAgentJob"/> per population (chained in population order), and - on the host's next Advance -
    /// complete them and copy what the kernel writes back into the core before the tick job resumes. Owned by
    /// <see cref="SubstrateCellHost"/>; disposed when the host goes.
    /// </summary>
    public sealed class SubstrateAgentPass : IDisposable
    {
        sealed class PopTables
        {
            public NativeArray<int> Live;
            public NativeArray<long> Key, Tab;
            public NativeArray<double> Agg;
            public NativeArray<SVector3> Dirs;

            public bool Fits(SubstratePopulation p) =>
                Live.IsCreated && Live.Length == p.Live.Length && Tab.Length == p.Tab.Length && Dirs.Length == p.Dirs.Length;

            public void Dispose()
            {
                if (Live.IsCreated) Live.Dispose();
                if (Key.IsCreated) Key.Dispose();
                if (Tab.IsCreated) Tab.Dispose();
                if (Agg.IsCreated) Agg.Dispose();
                if (Dirs.IsCreated) Dirs.Dispose();
            }
        }

        readonly SubstrateCore _c;
        NativeArray<SVector3> _pos, _vel, _idir, _home, _wseed, _gfood, _gscent, _galarm, _gthreat, _slotGoal, _slotVel;
        NativeArray<float> _hunger, _fear, _curious, _aggr, _phase, _qtarget, _ispeed, _closure, _ringgate, _rest, _fthreat, _falarm;
        NativeArray<float> _ramp, _attach;
        NativeArray<int> _host;
        NativeArray<bool> _steered, _watched, _creeping, _alive, _starving;
        NativeArray<long> _claimed;
        NativeArray<SubstratePilot> _pilots;
        PopTables[] _tables = new PopTables[0];
        JobHandle _handle;
        bool _disposed;

        /// <summary>True between <see cref="Schedule"/> and <see cref="Complete"/>.</summary>
        public bool Scheduled { get; private set; }

        public SubstrateAgentPass(SubstrateCore core)
        {
            _c = core;
            int n = core.Capacity;
            const Allocator A = Allocator.Persistent;
            const NativeArrayOptions U = NativeArrayOptions.UninitializedMemory;
            _pos = new NativeArray<SVector3>(n, A, U); _vel = new NativeArray<SVector3>(n, A, U);
            _idir = new NativeArray<SVector3>(n, A, U); _home = new NativeArray<SVector3>(n, A, U);
            _wseed = new NativeArray<SVector3>(n, A, U); _gfood = new NativeArray<SVector3>(n, A, U);
            _gscent = new NativeArray<SVector3>(n, A, U); _galarm = new NativeArray<SVector3>(n, A, U);
            _gthreat = new NativeArray<SVector3>(n, A, U);
            _slotGoal = new NativeArray<SVector3>(n, A, U); _slotVel = new NativeArray<SVector3>(n, A, U);
            _ramp = new NativeArray<float>(n, A, U); _attach = new NativeArray<float>(n, A, U);
            _host = new NativeArray<int>(n, A, U);
            _hunger = new NativeArray<float>(n, A, U); _fear = new NativeArray<float>(n, A, U);
            _curious = new NativeArray<float>(n, A, U); _aggr = new NativeArray<float>(n, A, U);
            _phase = new NativeArray<float>(n, A, U); _qtarget = new NativeArray<float>(n, A, U);
            _ispeed = new NativeArray<float>(n, A, U); _closure = new NativeArray<float>(n, A, U); _ringgate = new NativeArray<float>(n, A, U);
            _rest = new NativeArray<float>(n, A, U); _fthreat = new NativeArray<float>(n, A, U);
            _falarm = new NativeArray<float>(n, A, U);
            _steered = new NativeArray<bool>(n, A, U); _watched = new NativeArray<bool>(n, A, U);
            _creeping = new NativeArray<bool>(n, A, U); _alive = new NativeArray<bool>(n, A, U);
            _starving = new NativeArray<bool>(n, A, U);
            _claimed = new NativeArray<long>(n, A, U);
            _pilots = new NativeArray<SubstratePilot>(SubstrateCore.MaxPilots, A, NativeArrayOptions.ClearMemory);
        }

        /// <summary>Main thread, with the tick job parked after BeginStep: copy in and schedule the agent pass.</summary>
        public void Schedule()
        {
            if (_disposed || Scheduled) return;
            var c = _c;
            _pos.CopyFrom(c.Pos); _vel.CopyFrom(c.Vel); _idir.CopyFrom(c.IDir); _home.CopyFrom(c.Home);
            _wseed.CopyFrom(c.WSeed); _gfood.CopyFrom(c.GFood); _gscent.CopyFrom(c.GScent);
            _galarm.CopyFrom(c.GAlarm); _gthreat.CopyFrom(c.GThreat);
            _hunger.CopyFrom(c.Hunger); _fear.CopyFrom(c.Fear); _curious.CopyFrom(c.Curious); _aggr.CopyFrom(c.Aggr);
            _phase.CopyFrom(c.Phase); _qtarget.CopyFrom(c.QTarget); _ispeed.CopyFrom(c.ISpeed);
            _closure.CopyFrom(c.Closure); _ringgate.CopyFrom(c.RingGate); _rest.CopyFrom(c.Rest); _fthreat.CopyFrom(c.FThreat); _falarm.CopyFrom(c.FAlarm);
            _steered.CopyFrom(c.Steered); _watched.CopyFrom(c.Watched); _creeping.CopyFrom(c.Creeping);
            _alive.CopyFrom(c.Alive); _starving.CopyFrom(c.Starving); _claimed.CopyFrom(c.ClaimedTick);
            _ramp.CopyFrom(c.Ramp); _attach.CopyFrom(c.Attach); _host.CopyFrom(c.Host);
            _slotGoal.CopyFrom(c.SlotGoal); _slotVel.CopyFrom(c.SlotVel);
            var pil = c.TickPilots;
            for (int j = 0; j < pil.Length; j++) _pilots[j] = pil[j];

            if (_tables.Length < c.Pops.Count) Array.Resize(ref _tables, c.Pops.Count);
            var world = c.KernelWorld;
            JobHandle dep = default;
            for (int q = 0; q < c.Pops.Count; q++)
            {
                var pop = c.Pops[q];
                if (!pop.Active || pop.LiveCount == 0 || pop.Siege != null) continue;   // a siege moved in BeginStep
                var t = Tables(q, pop);
                t.Live.CopyFrom(pop.Live); t.Key.CopyFrom(pop.Key); t.Tab.CopyFrom(pop.Tab); t.Agg.CopyFrom(pop.Agg);
                t.Dirs.CopyFrom(pop.Dirs);
                var job = new SubstrateAgentJob
                {
                    Pos = _pos, Vel = _vel, IDir = _idir, Home = _home,
                    Hunger = _hunger, Fear = _fear, Curious = _curious, Aggr = _aggr, Phase = _phase, QTarget = _qtarget,
                    ISpeed = _ispeed, Steered = _steered, Watched = _watched, Creeping = _creeping,
                    WSeed = _wseed, Closure = _closure, RingGate = _ringgate, Rest = _rest, Alive = _alive, Starving = _starving,
                    ClaimedTick = _claimed, Pilots = _pilots,
                    Live = t.Live, Key = t.Key, Tab = t.Tab, Agg = t.Agg, Dirs = t.Dirs,
                    FThreat = _fthreat, FAlarm = _falarm, GFood = _gfood, GScent = _gscent, GAlarm = _galarm, GThreat = _gthreat,
                    Ramp = _ramp, Attach = _attach, Host = _host, SlotGoal = _slotGoal, SlotVel = _slotVel,
                    Pop = pop.Kernel, World = world,
                };
                dep = job.Schedule(pop.LiveCount, SubstrateAgentJob.BatchSize, dep);   // a predator reads its prey's moves
            }
            _handle = dep;
            JobHandle.ScheduleBatchedJobs();
            Scheduled = true;
        }

        /// <summary>Main thread: wait for the agent pass and copy what the kernel writes back into the core.</summary>
        public void Complete()
        {
            if (!Scheduled) return;
            _handle.Complete();
            Scheduled = false;
            var c = _c;
            _pos.CopyTo(c.Pos); _vel.CopyTo(c.Vel); _idir.CopyTo(c.IDir); _home.CopyTo(c.Home);
            _hunger.CopyTo(c.Hunger); _fear.CopyTo(c.Fear); _curious.CopyTo(c.Curious); _aggr.CopyTo(c.Aggr);
            _phase.CopyTo(c.Phase); _qtarget.CopyTo(c.QTarget); _ispeed.CopyTo(c.ISpeed);
            _steered.CopyTo(c.Steered); _watched.CopyTo(c.Watched); _creeping.CopyTo(c.Creeping);
        }

        PopTables Tables(int q, SubstratePopulation pop)
        {
            var t = _tables[q];
            if (t != null && t.Fits(pop)) return t;
            t?.Dispose();
            const Allocator A = Allocator.Persistent;
            const NativeArrayOptions U = NativeArrayOptions.UninitializedMemory;
            t = new PopTables
            {
                Live = new NativeArray<int>(pop.Live.Length, A, U), Key = new NativeArray<long>(pop.Key.Length, A, U),
                Tab = new NativeArray<long>(pop.Tab.Length, A, U), Agg = new NativeArray<double>(pop.Agg.Length, A, U),
                Dirs = new NativeArray<SVector3>(pop.Dirs.Length, A, U),
            };
            _tables[q] = t;
            return t;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _handle.Complete();
            Scheduled = false;
            _disposed = true;
            _pos.Dispose(); _vel.Dispose(); _idir.Dispose(); _home.Dispose(); _wseed.Dispose();
            _gfood.Dispose(); _gscent.Dispose(); _galarm.Dispose(); _gthreat.Dispose();
            _hunger.Dispose(); _fear.Dispose(); _curious.Dispose(); _aggr.Dispose(); _phase.Dispose(); _qtarget.Dispose();
            _ispeed.Dispose(); _closure.Dispose(); _ringgate.Dispose(); _rest.Dispose(); _fthreat.Dispose(); _falarm.Dispose();
            _steered.Dispose(); _watched.Dispose(); _creeping.Dispose(); _alive.Dispose(); _starving.Dispose();
            _claimed.Dispose(); _pilots.Dispose();
            _ramp.Dispose(); _attach.Dispose(); _host.Dispose(); _slotGoal.Dispose(); _slotVel.Dispose();
            for (int q = 0; q < _tables.Length; q++) _tables[q]?.Dispose();
        }
    }
}
