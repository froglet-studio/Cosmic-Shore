// Round 7 (Docs/SWARM_FAUNA.md §14): ONE swarm's tick, run OFF the main thread.
//
// A swarm used to cost the main thread three things per member: the simulation step, a transform +
// spatial-index + render-entity write every frame (one GameObject per member), and two always-on
// colliders. At ~1,000 members per swarm none of that survives. This file is the half of the fix that
// is plain C#: no UnityEngine, so the SAME file compiles and RUNS in Tools/Build/swarm_core_harness.
//
//   * the job OWNS its core while it runs. The main thread never touches the core between Kick and
//     Done; what it wants changed (a member killed, food eaten) it queues in the job's input arrays
//     and the job applies it first thing, on the worker;
//   * after the step the job builds everything the frame needs, so the main thread only COPIES:
//       - one SwarmInstance per member slot (previous and current pose, prism shape, heart molt,
//         tier, birth tick) - uploaded to the GPU once per tick, interpolated by the shader;
//       - per heart ELEMENT, the member slots whose heart is drawn as that element (a molting heart
//         appears in two lists, the shader shows it in the right one at the right moment);
//       - the ENGAGED members: the ones within reach of a vessel, nearest first, capped - the only
//         members that get a real GameObject (a proxy with colliders and a heart) at all;
//   * results are double-buffered: the main thread reads the FRONT buffers (published at Done), the
//     worker writes the BACK ones. Swap happens on the main thread in Collect, never on the worker.
//
// Threading contract (Docs/THREADING.md spirit, applied to plain C#): State moves Idle -> Running
// (main, Kick) -> Done (worker, Volatile.Write) -> Idle (main, Collect). Everything the worker writes
// is published by the Volatile.Write of State and read after the main thread's Volatile.Read of it.
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Threading;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// One member as the GPU draws it (80 bytes, std430-compatible: float3 + float pairs). Positions are
    /// WORLD space; the shader lerps Prev -> Cur by the swarm's display alpha. Flags: bit 0 alive, bits
    /// 1-2 tier (0 plain, 1 danger, 2 shield), bits 3-4 the heart's element before the molt midpoint,
    /// bits 5-6 after it, bits 7-8 the member's DOMAIN slot (round 8, Docs/SWARM_FAUNA.md §16.4: 0 = the
    /// swarm's controlling domain; only a MultiDomain swarm uses 1 and 2), bit 9 SHIELDED (round 11d-2: the member's
    /// mass is shielded - its plan says shield - whatever its tier shows; a puffed shield member shows danger, 1, and
    /// stays shielded: never food, never a steering target, and weapons see the shield). Scale is the body prism's local scale (x wide, y thin, z long) and PrismZ its
    /// seat behind the heart along -facing. Molt is the core's molt progress (0 = not molting).
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct SwarmInstance
    {
        public Vector3 PrevPos; public float BirthTick;
        public Vector3 CurPos; public uint Flags;
        public Vector3 PrevFace; public float PrevMolt;
        public Vector3 CurFace; public float CurMolt;
        public Vector3 Scale; public float PrismZ;

        public const int Stride = 80;
        public bool Alive => (Flags & 1u) != 0;
        public int Tier => (int)((Flags >> 1) & 3u);
        public int HeartFrom => (int)((Flags >> 3) & 3u);
        public int HeartTo => (int)((Flags >> 5) & 3u);
        public int DomainSlot => (int)((Flags >> 7) & 3u);
        public bool Shielded => (Flags & (1u << 9)) != 0;

        public static uint Pack(bool alive, int tier, int from, int to, int domainSlot = 0, bool shielded = false) =>
            (alive ? 1u : 0u) | ((uint)(tier & 3) << 1) | ((uint)(from & 3) << 3) | ((uint)(to & 3) << 5)
            | ((uint)(domainSlot & 3) << 7) | (shielded ? 1u << 9 : 0u);
    }

    /// <summary>The numbers a tick job needs to turn the core's state into the frame. Set once.</summary>
    public sealed class SwarmTickSettings
    {
        public Vector3 Centre;                  // world position of the sim origin (the host cell's centre)
        public float UnitScale = 2f;            // world units per voxel
        public float PrismScale = 1f;
        public float HeartPrismGap = 0.6f;
        public float[] HeartWorldScale = { 2.298f, 1.737f, 2.298f, 1.737f };   // per research element
        public Vector3[] DefaultHalf = { Vector3.One, Vector3.One, Vector3.One, Vector3.One };
        public float DangerEnter = 0.45f, DangerExit = 0.15f;
        /// <summary>Round 10 bestiary: Mass, Space and Time members strike with danger plates too (SWARM_FAUNA.md §18).</summary>
        public bool Bestiary;
        public float HuntEnter = 0.2f, LurkCalm = 0.05f;
        /// <summary>Ticks a pack hunter must show its startle above HuntEnter before its plate goes up (the lab's fair
        /// burns WINDUP, 0.4 s). 0 = strike on the tick it crosses (round 10).</summary>
        public int HuntWindupTicks;
        /// <summary>Ticks a Charge pufferfish must show its startle above DangerEnter before its plate goes up (the same
        /// wind-up, applied to its strike). 0 = strike on the tick it crosses (round 9).</summary>
        public int PuffWindupTicks;
        /// <summary>Ticks a locust shimmer holds before the dangerous quarter of the cloud moves on.</summary>
        public int LocustPhaseTicks = 20;
        /// <summary>Tandava (TANDAVA.md §3): a Charge member whose plan well is authored DANGER tier (1) wears a danger
        /// plate for as long as it holds that well - a designed body part (a feeding form's protectors), not a startle.
        /// Off (every census-planned swarm) = only the strike state machine raises danger, as shipped: the research plans'
        /// tier-1 marks stay unread.</summary>
        public bool PlanDanger;
        /// <summary>World radius around a vessel inside which a member becomes a real proxy.</summary>
        public float EngageRadius = 160f;
        /// <summary>Most proxies this swarm may hold (nearest first).</summary>
        public int MaxEngaged = 160;
        /// <summary>Round 8: a MULTI-DOMAIN swarm (Docs/SWARM_FAUNA.md §16.4) - members carry the core's own domain
        /// slot, and eaten volume is banked by the domain of the mass it came from. Off = every member is slot 0.</summary>
        public bool MultiDomain;
    }

    public enum SwarmJobState { Idle = 0, Running = 1, Done = 2 }

    /// <summary>
    /// One swarm's tick: apply the queued inputs, step the core, build the frame (Docs/SWARM_FAUNA.md §14).
    /// Pure C#: compiled and run by Tools/Build/swarm_core_harness (test R7).
    /// </summary>
    public sealed class SwarmTickJob
    {
        public readonly ISwarmCore Core;
        public readonly SwarmTickSettings S;
        readonly int _cap;

        int _state;
        public SwarmJobState State => (SwarmJobState)Volatile.Read(ref _state);
        /// <summary>Set if the worker threw; the main thread reports it and stops kicking this swarm.</summary>
        public Exception Error { get; private set; }

        // ── inputs (main thread writes them while Idle; the job consumes them) ──
        public readonly SwarmPredator[] Preds = new SwarmPredator[8];
        public int PredCount;
        public Vector3 SwimTarget;
        public int Steps = 1;
        readonly int[] _kills, _killsRun;
        int _killCount;
        readonly float[] _deposit = new float[4], _depositRun = new float[4];
        readonly object _inLock = new();
        int _requestPlan = -1;
        bool _requestPose;
        public bool WantStarvationVictim;
        /// <summary>Tandava's levers (<see cref="IScriptedSwarmCore.SetLevers"/>), handed to the core at the start of every
        /// tick: cruise and turn scales (1 = as authored) and the laying hold. Every other swarm leaves them at rest.</summary>
        public float CruiseScale = 1f, TurnScale = 1f;
        public bool HoldLaying;

        // ── front buffers (main thread reads after Collect) ──
        public SwarmInstance[] Instances;
        public uint[] HeartIdx;
        public readonly int[] HeartStart = new int[4], HeartCount = new int[4];
        public int[] Engaged;
        public int EngagedCount;
        public float[] Speed;                      // per slot, world units per second (the joust's "outrun")
        public readonly List<SwarmEvent> Events = new();
        public readonly float[] Stomach = new float[4];
        public int PlanIx, AliveCount, StarvationVictim = -1;
        public long Tick;                           // index of the PAIR in Instances: Prev = tick, Cur = tick + 1
        public Vector3 Anchor, BX, BY, BZ;
        /// <summary>Round 11a (Docs/SWARM_FAUNA.md §19.1): per slot, the point the spatial index stores for the member this
        /// tick - its body prism's centre at the middle of the published step (<see cref="SwarmBodyPose.IndexAlpha"/>).
        /// Built on the worker, so the main thread only copies it into the index's bulk position push.</summary>
        public Vector3[] IndexPoint;

        // ── back buffers (worker writes) ──
        SwarmInstance[] _bInst;
        uint[] _bHeart;
        readonly int[] _bHeartStart = new int[4], _bHeartCount = new int[4];
        int[] _bEngaged;
        int _bEngagedCount;
        float[] _bSpeed;
        readonly List<SwarmEvent> _bEvents = new();
        readonly float[] _bStomach = new float[4];
        int _bPlanIx, _bAlive, _bVictim = -1;
        Vector3 _bAnchor, _bBX, _bBY, _bBZ;
        Vector3[] _bIndexPoint;

        // ── worker-private state carried tick to tick ──
        readonly Vector3[] _lastPos, _lastFace;
        readonly float[] _lastMolt, _born;
        readonly bool[] _lastAlive;
        readonly byte[] _strike;      // StrikeState per slot
        readonly sbyte[] _strikeEff;  // the species the state belongs to (a molt into another element starts calm)
        readonly float[] _engD;
        readonly int[] _engI;
        readonly int[] _hc = new int[4];
        long _tick;

        /// <summary>
        /// The danger state machine of one member, one tick (round 10's strikes, hardened in round 11d - Docs/SWARM_FAUNA.md
        /// §22). A danger plate is a hostile danger prism, so an opposing-domain pilot who hits it BURNS petals
        /// (ELEMENTAL_ECONOMY.md §4); the swarm's own domain is only stung. Pure and static so the harness asserts it.
        /// States: 0 calm, 1 noticed (lurker only), 2 striking (the plate is up), 3 bolted (lurker only: safe until calm).
        /// <list type="bullet">
        /// <item>Charge pufferfish: strikes above DangerEnter until below DangerExit (hysteresis), after a wind-up of
        /// PuffWindupTicks shown ticks counted the same way as the pack hunter's.</item>
        /// <item>Mass lurker: bristles while half-startled, but only once it has been so for TWO ticks (a member a rush
        /// carries straight through the band never flashes), and a lurker that BOLTS (startle reaches DangerEnter) stays
        /// safe until it is fully calm again (below LurkCalm / 2). Round 10 read the band every tick, so every bolted
        /// lurker bristled again for ~2 s on the way back down - the rush that should beat it was punished after the
        /// fact - and a member hovering at a band edge flickered.</item>
        /// <item>Space locust: a quarter of the cloud at a time, the quarter moving every LocustPhaseTicks (stateless).</item>
        /// <item>Time pack hunter: strikes above HuntEnter until below min(DangerExit, HuntEnter) - the hysteresis can no
        /// longer invert if a designer sets the exit above the entry. With HuntWindupTicks it first WINDS UP: states
        /// 4 + n count the ticks its startle has shown above HuntEnter, and the plate goes up on the HuntWindupTicks-th
        /// (lab fair burns: a bite never lands in the same moment as its telegraph). A dip holds the count; startle
        /// below 0.4 x HuntEnter (the lab's 0.2 against 0.5) resets it.</item>
        /// </list>
        /// A NaN or negative startle reads as calm. Returns the new state; the plate is up iff it is 2.
        /// </summary>
        /// <summary>A wind-up resets once the startle falls below this fraction of the strike threshold (the lab's 0.2
        /// against 0.5).</summary>
        public const float WindupResetFrac = 0.4f;

        /// <summary>The WIND-UP before a plate goes up (lab fair burns, bestiary WINDUP): states 4 + n count the ticks the
        /// startle has shown above <paramref name="enter"/>; the plate goes up (2) on the <paramref name="ticks"/>-th. A dip
        /// holds the count, a startle below WindupResetFrac x enter resets it (0). ticks 0 or 1 = strike on crossing.</summary>
        static byte WindUp(float st, float enter, byte state, int ticks)
        {
            int wound = state >= 4 ? state - 4 : 0;
            if (st > enter)
            {
                wound++;
                return wound >= ticks ? (byte)2 : (byte)(4 + Math.Min(wound, 250));
            }
            return wound > 0 && st >= WindupResetFrac * enter ? state : (byte)0;
        }

        public static byte StrikeState(int eff, int slot, long tick, float st, byte state, SwarmTickSettings s)
        {
            if (!(st >= 0f)) st = 0f;
            switch (eff)
            {
                case 0:
                    return state == 2 ? (st < MathF.Min(s.DangerExit, s.DangerEnter) ? (byte)0 : (byte)2) : WindUp(st, s.DangerEnter, state, s.PuffWindupTicks);
                case 1 when s.Bestiary:
                {
                    float calm = 0.5f * s.LurkCalm;
                    bool band = st > s.LurkCalm && st < s.DangerEnter;
                    switch (state)
                    {
                        case 0: return st >= s.DangerEnter ? (byte)3 : band ? (byte)1 : (byte)0;
                        case 1: return st >= s.DangerEnter ? (byte)3 : band ? (byte)2 : (byte)0;
                        case 2: return st >= s.DangerEnter ? (byte)3 : st < calm ? (byte)0 : (byte)2;
                        default: return st < calm ? (byte)0 : (byte)3;
                    }
                }
                case 2 when s.Bestiary:
                    return ((slot * 7919L + tick / Math.Max(1, s.LocustPhaseTicks)) & 3L) == 0L ? (byte)2 : (byte)0;
                case 3 when s.Bestiary:
                    return state == 2 ? (st < MathF.Min(s.DangerExit, s.HuntEnter) ? (byte)0 : (byte)2) : WindUp(st, s.HuntEnter, state, s.HuntWindupTicks);
                default:
                    return 0;
            }
        }

        float _toWorldSpeed;
        static readonly WaitCallback s_run = RunOnWorker;

        public SwarmTickJob(ISwarmCore core, SwarmTickSettings settings, float tickHz)
        {
            Core = core; S = settings; _cap = core.Cap;
            _kills = new int[_cap]; _killsRun = new int[_cap];
            Instances = new SwarmInstance[_cap]; _bInst = new SwarmInstance[_cap];
            HeartIdx = new uint[2 * _cap]; _bHeart = new uint[2 * _cap];
            Engaged = new int[_cap]; _bEngaged = new int[_cap];
            Speed = new float[_cap]; _bSpeed = new float[_cap];
            _lastPos = new Vector3[_cap]; _lastFace = new Vector3[_cap];
            _lastMolt = new float[_cap]; _born = new float[_cap];
            _lastAlive = new bool[_cap]; _strike = new byte[_cap]; _strikeEff = new sbyte[_cap];
            _engD = new float[_cap]; _engI = new int[_cap];
            IndexPoint = new Vector3[_cap]; _bIndexPoint = new Vector3[_cap];
            _toWorldSpeed = settings.UnitScale * tickHz;
        }

        /// <summary>
        /// Queue a kill (main thread, ANY time - a death arrives from a physics callback whether or not a
        /// tick is running). Applied first thing in the next tick that STARTS after it. A tick already
        /// running does not see it, so its result still shows the member alive: the caller masks the
        /// slot until <see cref="BornThisTick"/> or a dead slot says the core has caught up.
        /// </summary>
        public void QueueKill(int i)
        {
            if (i < 0 || i >= _cap) return;
            lock (_inLock)
            {
                for (int q = 0; q < _killCount; q++) if (_kills[q] == i) return;
                if (_killCount < _kills.Length) _kills[_killCount++] = i;
            }
        }

        /// <summary>Bank eaten volume (main thread, any time); paid into the core's stomach next tick.
        /// Food never decides a member's colour (round 9, Docs/SWARM_FAUNA.md §17): only volume is banked.</summary>
        public void QueueDeposit(int element, float volume)
        {
            if (element < 0 || element > 3 || !(volume > 0f)) return;
            lock (_inLock) _deposit[element] += volume;
        }

        /// <summary>Tandava: ask a scripted core (<see cref="IScriptedSwarmCore"/>) to commit form <paramref name="planIx"/>.
        /// Main thread, any time; applied at the start of the next tick that STARTS after it, beside the queued kills.
        /// A core that is not scripted ignores it.</summary>
        public void RequestPlan(int planIx)
        {
            if (planIx < 0) return;
            lock (_inLock) { _requestPlan = planIx; _requestPose = false; }
        }

        /// <summary>Tandava: <see cref="RequestPlan"/> for a POSE of the current body (a feed twin) - the commit keeps the
        /// lay ease (<see cref="IScriptedSwarmCore.RequestPose"/>). Same thread rule.</summary>
        public void RequestPose(int planIx)
        {
            if (planIx < 0) return;
            lock (_inLock) { _requestPlan = planIx; _requestPose = true; }
        }

        /// <summary>
        /// Round 11f (Docs/ECOLOGY_LOD.md §5): a collapsed swarm drifts rigidly by <paramref name="dSim"/> (sim units).
        /// Main thread, only while <see cref="State"/> is Idle (the worker owns the core while Running, and a Done
        /// tick's back buffers would publish the old position): the core, the published frame (both ends of the pair),
        /// the index points, the anchor and the next tick's "previous" positions all move together, so nothing is
        /// interpolated across the jump. Returns false (nothing moved) when a tick is in flight.
        /// </summary>
        public bool Translate(Vector3 dSim)
        {
            if (State != SwarmJobState.Idle) return false;
            Core.Translate(dSim);
            var dW = dSim * S.UnitScale;
            for (int i = 0; i < _cap; i++)
            {
                _lastPos[i] += dSim;
                if (!Instances[i].Alive) continue;
                Instances[i].PrevPos += dW; Instances[i].CurPos += dW;
                IndexPoint[i] += dW;
            }
            Anchor += dW;
            SwimTarget += dSim;
            return true;
        }

        /// <summary>True when slot i's member in the FRONT buffers was born in the published pair - i.e.
        /// the slot holds a NEW member, not the one a pending kill was about.</summary>
        public bool BornThisTick(int i) => Instances[i].Alive && Instances[i].BirthTick >= Tick;

        /// <summary>
        /// Builds the first frame from the seeded core, synchronously, on the calling thread (before any
        /// worker has seen the core). Every seeded member is a newborn: Prev = Cur, and it blooms.
        /// </summary>
        public void Prime()
        {
            // every seeded member is a newborn of pair 0: it blooms in (continuity of existence)
            Build();
            Swap();
        }

        /// <summary>Start the tick (main thread). Inline = run it here and now (no worker).</summary>
        public void Kick(bool inline)
        {
            if (State != SwarmJobState.Idle) return;
            Volatile.Write(ref _state, (int)SwarmJobState.Running);
            if (inline) RunOnWorker(this);
            else ThreadPool.UnsafeQueueUserWorkItem(s_run, this);
        }

        /// <summary>Main thread: if the tick is done, publish its results (swap the buffers) and go Idle.</summary>
        public bool Collect()
        {
            if (State != SwarmJobState.Done) return false;
            Swap();
            Volatile.Write(ref _state, (int)SwarmJobState.Idle);
            return true;
        }

        /// <summary>Wall time of the last finished tick, in ms (worker side). The Unity Profiler does not
        /// sample thread-pool threads, so this is the one place the off-thread cost is visible.
        /// Written by the worker before it publishes Done, read by the main thread after Collect.</summary>
        public double LastTickMs { get; private set; }

        static void RunOnWorker(object o)
        {
            var job = (SwarmTickJob)o;
            long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
            try { job.Run(); }
            catch (Exception e) { job.Error = e; }
            job.LastTickMs = (System.Diagnostics.Stopwatch.GetTimestamp() - t0) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
            Volatile.Write(ref job._state, (int)SwarmJobState.Done);
        }

        void Run()
        {
            // inputs first, on the thread that owns the core now
            int kills, plan;
            bool pose;
            lock (_inLock)
            {
                kills = _killCount;
                Array.Copy(_kills, _killsRun, kills);
                _killCount = 0;
                for (int e = 0; e < 4; e++) { _depositRun[e] = _deposit[e]; _deposit[e] = 0f; }
                plan = _requestPlan; _requestPlan = -1;
                pose = _requestPose; _requestPose = false;
            }
            if (Core is IScriptedSwarmCore scripted)
            {
                scripted.SetLevers(CruiseScale, TurnScale, HoldLaying);
                if (plan >= 0)
                {
                    if (pose) scripted.RequestPose(plan);
                    else scripted.RequestPlan(plan);
                }
            }
            for (int q = 0; q < kills; q++) Core.Kill(_killsRun[q]);
            for (int e = 0; e < 4; e++) Core.Stomach[e] += _depositRun[e];
            Core.SwimTarget = SwimTarget;
            _bEvents.Clear();
            for (int s = 0; s < Math.Max(1, Steps); s++)
            {
                Core.Step(new ReadOnlySpan<SwarmPredator>(Preds, 0, PredCount));
                _bEvents.AddRange(Core.Events);
                Core.Events.Clear();
            }
            _bVictim = WantStarvationVictim ? Core.StarvationVictim() : -1;
            WantStarvationVictim = false;
            Build();
        }

        void Swap()
        {
            (Instances, _bInst) = (_bInst, Instances);
            (HeartIdx, _bHeart) = (_bHeart, HeartIdx);
            for (int e = 0; e < 4; e++) { HeartStart[e] = _bHeartStart[e]; HeartCount[e] = _bHeartCount[e]; }
            (Engaged, _bEngaged) = (_bEngaged, Engaged); EngagedCount = _bEngagedCount;
            (Speed, _bSpeed) = (_bSpeed, Speed);
            Events.Clear(); Events.AddRange(_bEvents);
            for (int e = 0; e < 4; e++) Stomach[e] = _bStomach[e];
            PlanIx = _bPlanIx; AliveCount = _bAlive; StarvationVictim = _bVictim;
            Anchor = _bAnchor; BX = _bBX; BY = _bBY; BZ = _bBZ;
            (IndexPoint, _bIndexPoint) = (_bIndexPoint, IndexPoint);
            Tick = _tick - 1;
        }

        /// <summary>The frame out of the core's state: one instance per slot, the heart lists, the engaged set.</summary>
        void Build()
        {
            var c = Core;
            float m = 2f * S.UnitScale * S.PrismScale;
            int alive = 0;
            Array.Clear(_hc, 0, 4);
            var dom = S.MultiDomain ? c.Dom : null;
            float tickNow = _tick;   // members born now bloom from the pair's start

            for (int i = 0; i < _cap; i++)
            {
                bool a = c.Alive[i];
                ref var inst = ref _bInst[i];
                if (!a)
                {
                    inst.Flags = 0u;
                    _bSpeed[i] = 0f;
                    _lastAlive[i] = false;
                    _strike[i] = 0;
                    continue;
                }
                alive++;
                var cur = c.Pos[i]; var face = c.Facing[i];
                bool newborn = !_lastAlive[i];
                if (newborn) { _lastPos[i] = cur; _lastFace[i] = face; _lastMolt[i] = 0f; _born[i] = tickNow; _strike[i] = 0; }

                int eff = c.EffectiveElement(i);
                var h = S.DefaultHalf[eff];
                int tier = 0;
                if (c.TryGetLook(i, eff, out var look, out int lt)) { h = look; if (eff == 0 && (lt == 2 || (lt == 1 && S.PlanDanger))) tier = lt; }
                if (_strikeEff[i] != eff) { _strikeEff[i] = (sbyte)eff; _strike[i] = 0; }
                _strike[i] = StrikeState(eff, i, _tick, c.Startle[i], _strike[i], S);
                bool shielded = tier == 2;   // the mass stays shielded while a puff shows danger (round 11d-2)
                if (_strike[i] == 2) tier = 1;   // danger wins over shield in the LOOK (locked design)

                // heart: the element before / after the molt midpoint, and the molt progress at both ends
                float mc = c.Molt[i], mp = _lastMolt[i];
                int from = c.Elem[i], to = mc > 0f ? c.MoltTo[i] : from;
                if (mc <= 0f && mp > 0f) { mc = 1f; }   // the molt finished this tick: Elem is already the new one
                else if (mc <= 0f) mp = 0f;

                var scale = new Vector3(h.Y * m, h.Z * m, h.X * m);
                inst.PrevPos = S.Centre + _lastPos[i] * S.UnitScale;
                inst.CurPos = S.Centre + cur * S.UnitScale;
                inst.PrevFace = _lastFace[i]; inst.CurFace = face;
                inst.PrevMolt = mp; inst.CurMolt = mc;
                inst.Scale = scale;
                inst.PrismZ = -(S.HeartWorldScale[eff] + S.HeartPrismGap + 0.5f * scale.Z);
                inst.BirthTick = _born[i];
                int ds = dom != null ? Math.Clamp(dom[i], 0, 2) : 0;
                inst.Flags = SwarmInstance.Pack(true, tier, from, to, ds, shielded);
                _bSpeed[i] = c.Vel[i].Length() * _toWorldSpeed;
                _bIndexPoint[i] = SwarmBodyPose.Body(inst, SwarmBodyPose.IndexAlpha);

                _hc[from]++;
                if (to != from) _hc[to]++;

                _lastAlive[i] = true;
                _lastPos[i] = cur; _lastFace[i] = face; _lastMolt[i] = c.Molt[i];
            }

            // heart lists: element-major, a molting heart in both of its elements
            int o = 0;
            for (int e = 0; e < 4; e++) { _bHeartStart[e] = o; _bHeartCount[e] = 0; o += _hc[e]; }
            for (int i = 0; i < _cap; i++)
            {
                ref var inst = ref _bInst[i];
                if (!inst.Alive) continue;
                int f = inst.HeartFrom, t = inst.HeartTo;
                _bHeart[_bHeartStart[f] + _bHeartCount[f]++] = (uint)i;
                if (t != f) _bHeart[_bHeartStart[t] + _bHeartCount[t]++] = (uint)i;
            }

            BuildEngaged();
            for (int e = 0; e < 4; e++) _bStomach[e] = c.Stomach[e];
            _bPlanIx = c.PlanIx; _bAlive = alive;
            _bAnchor = S.Centre + c.Anchor * S.UnitScale; _bBX = c.BX; _bBY = c.BY; _bBZ = c.BZ;
            _tick++;
        }

        /// <summary>Members within EngageRadius of a vessel, nearest first, at most MaxEngaged.</summary>
        void BuildEngaged()
        {
            int n = 0;
            float r = S.EngageRadius;
            if (PredCount > 0 && r > 0f && S.MaxEngaged > 0)
            {
                float r2 = r * r;
                for (int i = 0; i < _cap; i++)
                {
                    if ((_bInst[i].Flags & 1u) == 0) continue;
                    var p = _bInst[i].CurPos;
                    float best = float.MaxValue;
                    for (int k = 0; k < PredCount; k++)
                    {
                        // preds are in sim units; the engagement test is in world units
                        var v = S.Centre + Preds[k].C * S.UnitScale;
                        best = MathF.Min(best, Vector3.DistanceSquared(p, v));
                    }
                    if (best <= r2) { _engD[n] = best; _engI[n] = i; n++; }
                }
                if (n > S.MaxEngaged) { Array.Sort(_engD, _engI, 0, n); n = S.MaxEngaged; }
            }
            Array.Copy(_engI, _bEngaged, n);
            _bEngagedCount = n;
        }

        /// <summary>Interpolated world pose of slot i at display alpha (main thread, front buffers).</summary>
        public Vector3 PoseAt(int i, float alpha) => Vector3.Lerp(Instances[i].PrevPos, Instances[i].CurPos, alpha);

        public Vector3 FaceAt(int i, float alpha)
        {
            var f = Vector3.Lerp(Instances[i].PrevFace, Instances[i].CurFace, alpha);
            float l = f.Length();
            return l > 1e-5f ? f / l : Instances[i].CurFace;
        }

        /// <summary>World centre of slot i's BODY PRISM at display alpha - the point a weapon tests, exactly as the
        /// proxy's own body sits (local z = PrismZ under a pose facing FaceAt). Main thread, front buffers.</summary>
        public Vector3 BodyAt(int i, float alpha) => SwarmBodyPose.Body(Instances[i], alpha);
    }
}
