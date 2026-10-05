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
    /// swarm's controlling domain; only a MultiDomain swarm uses 1 and 2). Scale is the body prism's local scale (x wide, y thin, z long) and PrismZ its
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

        public static uint Pack(bool alive, int tier, int from, int to, int domainSlot = 0) =>
            (alive ? 1u : 0u) | ((uint)(tier & 3) << 1) | ((uint)(from & 3) << 3) | ((uint)(to & 3) << 5)
            | ((uint)(domainSlot & 3) << 7);
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
        /// <summary>Ticks a locust shimmer holds before the dangerous quarter of the cloud moves on.</summary>
        public int LocustPhaseTicks = 20;
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
        public bool WantStarvationVictim;

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
        readonly bool[] _lastAlive, _danger;
        readonly float[] _engD;
        readonly int[] _engI;
        readonly int[] _hc = new int[4];
        long _tick;

        /// <summary>
        /// Round 10 bestiary strike for a non-Charge member: a danger plate is a hostile danger prism, so an
        /// opposing-domain pilot who hits it BURNS petals (ELEMENTAL_ECONOMY.md §4); the swarm's own domain is stung only.
        /// Mass = lurker (bristles while only half-startled: creep up on it and it bites, rush it and it bolts), Space = locust (a rotating quarter of the cloud),
        /// Time = pack hunter (turns on a vessel at a lower startle than the pufferfish, with hysteresis).
        /// </summary>
        bool BestiaryStrike(int eff, int i, float st, bool was)
        {
            switch (eff)
            {
                case 1: return st > S.LurkCalm && st < S.DangerEnter;   // bristles when first noticed, safe once it bolts
                case 2: return ((i * 7919L + _tick / Math.Max(1, S.LocustPhaseTicks)) & 3L) == 0L;
                case 3: return was ? st >= S.DangerExit : st > S.HuntEnter;
                default: return false;
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
            _lastAlive = new bool[_cap]; _danger = new bool[_cap];
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
            int kills;
            lock (_inLock)
            {
                kills = _killCount;
                Array.Copy(_kills, _killsRun, kills);
                _killCount = 0;
                for (int e = 0; e < 4; e++) { _depositRun[e] = _deposit[e]; _deposit[e] = 0f; }
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
                    _danger[i] = false;
                    continue;
                }
                alive++;
                var cur = c.Pos[i]; var face = c.Facing[i];
                bool newborn = !_lastAlive[i];
                if (newborn) { _lastPos[i] = cur; _lastFace[i] = face; _lastMolt[i] = 0f; _born[i] = tickNow; _danger[i] = false; }

                int eff = c.EffectiveElement(i);
                var h = S.DefaultHalf[eff];
                int tier = 0;
                if (c.TryGetLook(i, eff, out var look, out int lt)) { h = look; if (eff == 0 && lt == 2) tier = 2; }
                if (eff == 0)
                {
                    float st = c.Startle[i];
                    if (_danger[i]) { if (st < S.DangerExit) _danger[i] = false; }
                    else if (st > S.DangerEnter) _danger[i] = true;
                    if (_danger[i]) tier = 1;   // danger wins over shield (locked design)
                }
                else if (S.Bestiary) { _danger[i] = BestiaryStrike(eff, i, c.Startle[i], _danger[i]); if (_danger[i]) tier = 1; }
                else _danger[i] = false;

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
                inst.Flags = SwarmInstance.Pack(true, tier, from, to, ds);
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
