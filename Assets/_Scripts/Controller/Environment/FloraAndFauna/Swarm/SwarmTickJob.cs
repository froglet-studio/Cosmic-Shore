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
    /// bits 5-6 after it. Scale is the body prism's local scale (x wide, y thin, z long) and PrismZ its
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

        public static uint Pack(bool alive, int tier, int from, int to) =>
            (alive ? 1u : 0u) | ((uint)(tier & 3) << 1) | ((uint)(from & 3) << 3) | ((uint)(to & 3) << 5);
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
        /// <summary>World radius around a vessel inside which a member becomes a real proxy.</summary>
        public float EngageRadius = 160f;
        /// <summary>Most proxies this swarm may hold (nearest first).</summary>
        public int MaxEngaged = 160;
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

        // ── worker-private state carried tick to tick ──
        readonly Vector3[] _lastPos, _lastFace;
        readonly float[] _lastMolt, _born;
        readonly bool[] _lastAlive, _danger;
        readonly float[] _engD;
        readonly int[] _engI;
        readonly int[] _hc = new int[4];
        long _tick;
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

        /// <summary>Bank eaten volume (main thread, any time); paid into the core's stomach next tick.</summary>
        public void QueueDeposit(int element, float volume)
        {
            if (element < 0 || element > 3 || !(volume > 0f)) return;
            lock (_inLock) _deposit[element] += volume;
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

        static void RunOnWorker(object o)
        {
            var job = (SwarmTickJob)o;
            try { job.Run(); }
            catch (Exception e) { job.Error = e; }
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
            Tick = _tick - 1;
        }

        /// <summary>The frame out of the core's state: one instance per slot, the heart lists, the engaged set.</summary>
        void Build()
        {
            var c = Core;
            float m = 2f * S.UnitScale * S.PrismScale;
            int alive = 0;
            Array.Clear(_hc, 0, 4);
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
                inst.Flags = SwarmInstance.Pack(true, tier, from, to);
                _bSpeed[i] = c.Vel[i].Length() * _toWorldSpeed;

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
    }
}
