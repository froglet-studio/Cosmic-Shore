// Round 11b (Docs/SUBSTRATE_FAUNA.md §5): the cell's substrate tick, run OFF the main thread - the round-7 shape
// (SwarmTickJob, Docs/SWARM_FAUNA.md §14) for the substrate. Pure C#: compiled and run by Tools/Build/substrate_harness.
//
//   * the job OWNS the core while it runs; the main thread queues what it wants changed (an agent killed, a bite of
//     food landed, the vessels and the food it can see) and the job applies it first thing, on the worker;
//   * after the step it builds everything the frame needs, so the main thread only COPIES: one SwarmInstance per slot
//     (the member shader's own 80-byte contract - the substrate's agents are drawn by the same shader as the swarm's
//     tadpoles), each agent's TRUE body (volume = its stock, what its proxy's body prism wears), per population the
//     heart list, the engaged agents (the only ones that get a GameObject), its stated volume, and the events;
//   * results are double-buffered and swapped on the main thread in Collect, never on the worker;
//   * round 11b-2: with ExternalAgentPass the worker stops after SubstrateCore.BeginStep and PARKS (AwaitingAgentPass,
//     the state stays Running); the main thread runs the agent pass as Burst jobs (SubstrateAgentPass - jobs can only
//     be scheduled from the main thread) and calls ResumeAfterAgentPass, which queues EndStep and the frame build back
//     on the pool. One step per tick in this mode.
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading;

namespace CosmicShore.Gameplay
{
    /// <summary>The numbers the job needs to turn the core's state into the frame. Set once.</summary>
    public sealed class SubstrateTickSettings
    {
        /// <summary>World position of the sim origin (the host cell's centre).</summary>
        public Vector3 Centre;
        /// <summary>The heart's world scale per research element (Charge, Mass, Space, Time) - the swarm's table.</summary>
        public float[] HeartWorldScale = { 2.298f, 1.737f, 2.298f, 1.737f };
        public float HeartPrismGap = 0.6f;
        /// <summary>A body prism is w wide, Thin*w tall and aspect*w long; its volume is the agent's stock.</summary>
        public float Thin = 0.6f;
        /// <summary>World radius around a vessel inside which an agent becomes a real proxy - the default for a population
        /// that states none of its own (<see cref="SubstratePopulation.EngageRadius"/>).</summary>
        public float EngageRadius = 160f;
        /// <summary>Most proxies one population may hold (nearest first) - the collider budget's per-population share; the
        /// default for a population that states none of its own (<see cref="SubstratePopulation.MaxEngaged"/>).</summary>
        public int MaxEngaged = 24;
    }

    /// <summary>A queued bite of real food for one agent (main thread -> worker).</summary>
    public struct SubstrateFeed
    {
        public int Index;
        public float Volume;
    }

    public sealed class SubstrateTickJob
    {
        public readonly SubstrateCore Core;
        public readonly SubstrateTickSettings S;
        readonly int _cap;

        int _state;
        public SwarmJobState State => (SwarmJobState)Volatile.Read(ref _state);
        public Exception Error { get; private set; }

        // ── inputs (main thread writes them while Idle) ──
        public readonly SubstratePilot[] Pilots = new SubstratePilot[SubstrateCore.MaxPilots];
        public int PilotCount;
        public SubstrateFood[] Food = new SubstrateFood[64];
        public int FoodCount;
        public int Steps = 1;
        /// <summary>Round 11b-2: the agent pass is run by the caller between <see cref="AwaitingAgentPass"/> and
        /// <see cref="ResumeAfterAgentPass"/> (the game's Burst jobs) instead of on the worker. Set before the first Kick.</summary>
        public bool ExternalAgentPass;
        int _awaiting;
        double _parkedMs;
        /// <summary>True while the worker is parked after BeginStep, waiting for the caller's agent pass.</summary>
        public bool AwaitingAgentPass => Volatile.Read(ref _awaiting) == 1;
        readonly object _inLock = new();
        readonly List<int> _kills = new(), _killsRun = new();
        readonly List<SubstrateFeed> _feeds = new(), _feedsRun = new();

        // ── front buffers (main thread reads after Collect) ──
        public SwarmInstance[] Instances;
        /// <summary>Per slot, the TRUE body (x wide, y thin, z long): its volume is the agent's stock.</summary>
        public Vector3[] Body;
        public float[] Speed;
        public uint[] HeartIdx;
        public int[] Engaged;
        public int[] EngagedCount;
        public int[] PopAlive;
        public double[] PopVolume;
        /// <summary>Round 11-11: per slot, the agent RIDES a pilot's hull (a latched leech) - it has no proxy (it would sit
        /// inside the vessel's collider); it is drawn where it rides.</summary>
        public bool[] Riding;
        public readonly List<SubstrateEvent> Events = new();
        public readonly List<int> EatRequests = new();
        /// <summary>Predations the published ticks asked for (the owner kills the prey through its proxy and feeds its
        /// body to the predator).</summary>
        public readonly List<SubstratePredation> PreyRequests = new();
        /// <summary>
        /// Every agent the published tick killed, with the stock it died holding (<see cref="SubstrateCore.Kill"/>'s
        /// return). It can exceed the body published before the kill was queued: a meal queued in the same pass (a bite,
        /// or a prey paid to a hunter that then dies) lands in the body FIRST and leaves with it (QA-SWARM-ROUND11-9).
        /// </summary>
        public readonly List<SubstrateFeed> Killed = new();
        public long Tick;

        // ── back buffers (worker writes) ──
        readonly List<SubstrateFeed> _bKilled = new();
        SwarmInstance[] _bInst;
        Vector3[] _bBody;
        float[] _bSpeed;
        uint[] _bHeart;
        int[] _bEngaged, _bEngCount, _bPopAlive;
        double[] _bPopVol;
        bool[] _bRiding;
        readonly List<SubstrateEvent> _bEvents = new();
        readonly List<int> _bEat = new();
        readonly List<SubstratePredation> _bPrey = new();

        // ── worker-private state carried tick to tick ──
        readonly Vector3[] _lastPos, _lastFace;
        readonly long[] _lastBorn;
        readonly bool[] _lastAlive;
        readonly float[] _born;
        readonly float[] _engD;
        readonly int[] _engI;
        long _tick;
        static readonly WaitCallback s_run = RunOnWorker;
        static readonly WaitCallback s_finish = FinishOnWorker;

        public SubstrateTickJob(SubstrateCore core, SubstrateTickSettings settings)
        {
            Core = core; S = settings; _cap = core.Capacity;
            Instances = new SwarmInstance[_cap]; _bInst = new SwarmInstance[_cap];
            Body = new Vector3[_cap]; _bBody = new Vector3[_cap];
            Speed = new float[_cap]; _bSpeed = new float[_cap];
            HeartIdx = new uint[_cap]; _bHeart = new uint[_cap];
            Engaged = new int[_cap]; _bEngaged = new int[_cap];
            Riding = new bool[_cap]; _bRiding = new bool[_cap];
            int mp = 16;
            EngagedCount = new int[mp]; _bEngCount = new int[mp];
            PopAlive = new int[mp]; _bPopAlive = new int[mp];
            PopVolume = new double[mp]; _bPopVol = new double[mp];
            _lastPos = new Vector3[_cap]; _lastFace = new Vector3[_cap]; _lastBorn = new long[_cap];
            _lastAlive = new bool[_cap]; _born = new float[_cap];
            _engD = new float[_cap]; _engI = new int[_cap];
        }

        /// <summary>Queue a kill (main thread, any time). Applied first thing in the next tick that STARTS after it.</summary>
        public void QueueKill(int i)
        {
            if (i < 0 || i >= _cap) return;
            lock (_inLock) if (!_kills.Contains(i)) _kills.Add(i);
        }

        /// <summary>Queue a bite of real food (main thread, any time): paid into that agent's body next tick.</summary>
        public void QueueFeed(int i, float volume)
        {
            if (i < 0 || i >= _cap || !(volume > 0f)) return;
            lock (_inLock) _feeds.Add(new SubstrateFeed { Index = i, Volume = volume });
        }

        /// <summary>True when slot i's agent in the FRONT buffers was born in the published pair.</summary>
        public bool BornThisTick(int i) => Instances[i].Alive && Instances[i].BirthTick >= Tick;

        /// <summary>Builds the first frame from the seeded core, on the calling thread (no worker has seen it).</summary>
        public void Prime() { Build(); Swap(); }

        /// <summary>Start the tick (main thread). Inline = run it here and now.</summary>
        public void Kick(bool inline)
        {
            if (State != SwarmJobState.Idle) return;
            Volatile.Write(ref _state, (int)SwarmJobState.Running);
            if (inline) RunOnWorker(this);
            else ThreadPool.UnsafeQueueUserWorkItem(s_run, this);
        }

        /// <summary>Main thread: if the tick is done, publish it (swap the buffers) and go Idle.</summary>
        public bool Collect()
        {
            if (State != SwarmJobState.Done) return false;
            Swap();
            Volatile.Write(ref _state, (int)SwarmJobState.Idle);
            return true;
        }

        /// <summary>Wall time of the last finished tick, in ms (worker side; the Profiler does not sample pool threads).</summary>
        public double LastTickMs { get; private set; }

        static double Since(long t0) => (System.Diagnostics.Stopwatch.GetTimestamp() - t0) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;

        static void RunOnWorker(object o)
        {
            var job = (SubstrateTickJob)o;
            long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
            bool parked = false;
            try { parked = job.Run(); }
            catch (Exception e) { job.Error = e; }
            if (parked && job.Error == null)
            {
                job._parkedMs = Since(t0);
                Volatile.Write(ref job._awaiting, 1);   // the core is the caller's until ResumeAfterAgentPass
                return;
            }
            job.LastTickMs = Since(t0);
            Volatile.Write(ref job._state, (int)SwarmJobState.Done);
        }

        static void FinishOnWorker(object o)
        {
            var job = (SubstrateTickJob)o;
            long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
            try { job.Finish(); }
            catch (Exception e) { job.Error = e; }
            job.LastTickMs = job._parkedMs + Since(t0);   // the worker's share; the agent pass ran as jobs
            Volatile.Write(ref job._state, (int)SwarmJobState.Done);
        }

        /// <summary>Main thread, after the caller's agent pass over the parked core: queue the rest of the tick
        /// (EndStep and the frame build) back on the pool. Inline = run it here and now.</summary>
        public void ResumeAfterAgentPass(bool inline)
        {
            if (Interlocked.CompareExchange(ref _awaiting, 0, 1) != 1) return;
            if (inline) FinishOnWorker(this);
            else ThreadPool.UnsafeQueueUserWorkItem(s_finish, this);
        }

        /// <returns>True when the tick parked after BeginStep for an external agent pass.</returns>
        bool Run()
        {
            lock (_inLock)
            {
                _killsRun.Clear(); _killsRun.AddRange(_kills); _kills.Clear();
                _feedsRun.Clear(); _feedsRun.AddRange(_feeds); _feeds.Clear();
            }
            // feeds BEFORE kills (QA-SWARM-ROUND11-9): the prism a bite consumed is already gone from the world, so a meal
            // queued for an agent that dies in the same pass must reach its body and leave with it. Applied the other way
            // round, Core.Feed met a dead agent and the volume vanished.
            for (int q = 0; q < _feedsRun.Count; q++) Core.Feed(_feedsRun[q].Index, _feedsRun[q].Volume);
            _bKilled.Clear();
            for (int q = 0; q < _killsRun.Count; q++)
            {
                int k = _killsRun[q];
                bool was = k >= 0 && k < _cap && Core.Alive[k];
                float stock = Core.Kill(k);
                if (was) _bKilled.Add(new SubstrateFeed { Index = k, Volume = stock });
            }
            _bEvents.Clear(); _bEat.Clear(); _bPrey.Clear();
            var pil = new ReadOnlySpan<SubstratePilot>(Pilots, 0, Math.Min(PilotCount, Pilots.Length));
            var food = new ReadOnlySpan<SubstrateFood>(Food, 0, Math.Min(FoodCount, Food.Length));
            if (ExternalAgentPass)
            {
                Core.BeginStep(pil, food);
                return true;
            }
            for (int s = 0; s < Math.Max(1, Steps); s++)
            {
                Core.Step(pil, food);
                Gather();
            }
            Build();
            return false;
        }

        void Finish()
        {
            Core.EndStep();
            Gather();
            Build();
        }

        void Gather()
        {
            _bEvents.AddRange(Core.Events);
            _bEat.AddRange(Core.EatRequests);
            _bPrey.AddRange(Core.PreyRequests);
        }

        void Swap()
        {
            (Instances, _bInst) = (_bInst, Instances);
            (Body, _bBody) = (_bBody, Body);
            (Speed, _bSpeed) = (_bSpeed, Speed);
            (HeartIdx, _bHeart) = (_bHeart, HeartIdx);
            (Engaged, _bEngaged) = (_bEngaged, Engaged);
            (EngagedCount, _bEngCount) = (_bEngCount, EngagedCount);
            (PopAlive, _bPopAlive) = (_bPopAlive, PopAlive);
            (PopVolume, _bPopVol) = (_bPopVol, PopVolume);
            (Riding, _bRiding) = (_bRiding, Riding);
            Events.Clear(); Events.AddRange(_bEvents);
            EatRequests.Clear(); EatRequests.AddRange(_bEat);
            PreyRequests.Clear(); PreyRequests.AddRange(_bPrey);
            Killed.Clear(); Killed.AddRange(_bKilled);
            Tick = _tick - 1;
        }

        /// <summary>The TRUE body of an agent: a prism w x Thin*w x aspect*w whose volume is exactly its stock.</summary>
        public static Vector3 BodyOf(float stock, float aspect, float thin)
        {
            float a = MathF.Max(aspect, 0.1f);
            float w = MathF.Pow(MathF.Max(stock, 1e-3f) / (thin * a), 1f / 3f);
            return new Vector3(w, thin * w, a * w);
        }

        /// <summary>The frame out of the core's state: instances, true bodies, hearts, engaged agents, volume.</summary>
        void Build()
        {
            var c = Core;
            float tickNow = _tick;
            Array.Clear(_bPopAlive, 0, _bPopAlive.Length);
            Array.Clear(_bPopVol, 0, _bPopVol.Length);
            for (int q = 0; q < c.Pops.Count && q < _bEngCount.Length; q++)
            {
                var pop = c.Pops[q];
                var P = pop.P;
                int elem = Math.Clamp(pop.Element, 0, 3);
                int hearts = 0;
                for (int i = pop.Start; i < pop.Start + pop.Cap; i++)
                {
                    ref var inst = ref _bInst[i];
                    if (!pop.Active || !c.Alive[i])
                    {
                        inst.Flags = 0u; _bSpeed[i] = 0f; _lastAlive[i] = false; _bRiding[i] = false;
                        continue;
                    }
                    _bRiding[i] = c.Host[i] != 0;
                    var cur = c.Pos[i];
                    var vel = c.Vel[i];
                    float vs = vel.Length();
                    var face = vs > 1e-3f ? vel / vs : (_lastAlive[i] ? _lastFace[i] : c.IDir[i]);
                    bool newborn = !_lastAlive[i] || _lastBorn[i] != c.BornTick[i];
                    if (newborn) { _lastPos[i] = cur; _lastFace[i] = face; _born[i] = tickNow; _lastBorn[i] = c.BornTick[i]; }

                    float ph = c.Phase[i];
                    float aspect = P.Solitary.Aspect + (P.Gregarious.Aspect - P.Solitary.Aspect) * ph;
                    var body = BodyOf(c.Stock[i], aspect, S.Thin);
                    _bBody[i] = body;
                    // drawn: the regime's size swell is the telegraph (a lurker's gape, a locust thickening); a mimic
                    // is drawn as a sliver behind its heart while calm, so what you see is a crystal
                    float puff = 1f + (P.Gregarious.Size / MathF.Max(P.Solitary.Size, 1e-3f) - 1f) * ph;
                    float mimic = 1f;
                    if (P.MimicBody < 1f)
                    {
                        float wake = Math.Clamp(ph * 5f + (c.Rest[i] > 0f ? 1f : 0f), 0f, 1f);
                        mimic = P.MimicBody + (1f - P.MimicBody) * wake;
                    }
                    var draw = body * (puff * mimic);
                    int tier = c.Danger[i] ? 1 : 0;

                    inst.PrevPos = S.Centre + _lastPos[i];
                    inst.CurPos = S.Centre + cur;
                    inst.PrevFace = _lastFace[i]; inst.CurFace = face;
                    inst.PrevMolt = 0f; inst.CurMolt = 0f;
                    inst.Scale = draw;
                    inst.PrismZ = -(S.HeartWorldScale[elem] + S.HeartPrismGap + 0.5f * draw.Z);
                    inst.BirthTick = _born[i];
                    inst.Flags = SwarmInstance.Pack(true, tier, elem, elem, 0);
                    _bSpeed[i] = vs;
                    _bPopVol[q] += c.Stock[i];
                    _bPopAlive[q]++;
                    _bHeart[pop.Start + hearts++] = (uint)(i - pop.Start);
                    _lastAlive[i] = true; _lastPos[i] = cur; _lastFace[i] = face;
                }
                BuildEngaged(pop, q);
            }
            _tick++;
        }

        /// <summary>A population's agents within EngageRadius of a vessel, DANGEROUS ones first then nearest first, at
        /// most MaxEngaged - written into Engaged[pop.Start ..] (each population owns the same slice it owns in the core).
        /// Round 11-11: a cap below the swirl's size (a mob of 14 mobbers round a hull, cap 4) must still hand its
        /// proxies to the birds that are diving - the pulled-up diver is not the nearest one.</summary>
        void BuildEngaged(SubstratePopulation pop, int q)
        {
            int n = 0;
            float er = pop.EngageRadius >= 0f ? pop.EngageRadius : S.EngageRadius;
            int max = pop.MaxEngaged >= 0 ? pop.MaxEngaged : S.MaxEngaged;
            float r2 = er * er;
            if (pop.Active && PilotCount > 0 && r2 > 0f && max > 0)
                for (int i = pop.Start; i < pop.Start + pop.Cap; i++)
                {
                    if ((_bInst[i].Flags & 1u) == 0 || _bRiding[i]) continue;
                    float best = float.MaxValue;
                    for (int k = 0; k < PilotCount; k++)
                        best = MathF.Min(best, Vector3.DistanceSquared(Core.Pos[i], Pilots[k].Pos));
                    // the sort key: a dangerous agent ranks below every harmless one (-1 + d^2 / 1e6 < 0 <= d^2)
                    if (best <= r2) { _engD[n] = Core.Danger[i] ? -1f + best * 1e-6f : best; _engI[n] = i; n++; }
                }
            if (n > max) { Array.Sort(_engD, _engI, 0, n); n = max; }
            Array.Copy(_engI, 0, _bEngaged, pop.Start, n);
            _bEngCount[q] = n;
        }

        /// <summary>
        /// One population's slice of the published frame, for the glue's per-population consumers (main thread, after
        /// Collect): <paramref name="drawn"/> is the slice as drawn (the render entities' poses), <paramref name="ledger"/>
        /// the same slice wearing each agent's TRUE body as its Scale - so the swarm's index ledger
        /// (<see cref="SwarmEntryLedger"/>) registers every agent's virtual entry at a volume that IS its stock - and
        /// <paramref name="points"/> the point the index stores for each (the body centre at the middle of the step,
        /// <see cref="SwarmBodyPose.IndexAlpha"/>).
        /// </summary>
        public void Slice(int start, int cap, SwarmInstance[] drawn, SwarmInstance[] ledger, Vector3[] points)
        {
            Array.Copy(Instances, start, drawn, 0, cap);
            for (int k = 0; k < cap; k++)
            {
                ledger[k] = drawn[k];
                ledger[k].Scale = Body[start + k];
                points[k] = BodyAt(start + k, SwarmBodyPose.IndexAlpha);
            }
        }

        /// <summary>The number of hearts (= living agents) published for a population: its heart list is
        /// HeartIdx[pop.Start .. + this], slot indices relative to pop.Start.</summary>
        public int HeartCount(int q) => q >= 0 && q < PopAlive.Length ? PopAlive[q] : 0;

        public Vector3 PoseAt(int i, float alpha) => Vector3.Lerp(Instances[i].PrevPos, Instances[i].CurPos, alpha);

        public Vector3 FaceAt(int i, float alpha)
        {
            var f = Vector3.Lerp(Instances[i].PrevFace, Instances[i].CurFace, alpha);
            float l = f.Length();
            return l > 1e-5f ? f / l : Instances[i].CurFace;
        }

        /// <summary>World centre of slot i's BODY PRISM at display alpha (seated PrismZ behind the heart).</summary>
        public Vector3 BodyAt(int i, float alpha) => PoseAt(i, alpha) + FaceAt(i, alpha) * Instances[i].PrismZ;
    }
}
