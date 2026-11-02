using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The AI lane fan, batched for every AI at once off the main thread. Each AI writes its pose during its Update
    /// (<see cref="Write"/>); at LateUpdate <see cref="Schedule"/> runs a Burst job that builds <see cref="Fan"/> sphere casts
    /// per AI (straight down the velocity, and short taps of <see cref="Angles"/> left, right, up and down), one
    /// <see cref="SpherecastCommand"/> batch, and a Burst job that picks per AI the smallest clear tap toward its goal.
    /// The next frame's first <see cref="Read"/> completes it (the workers ran during rendering), so the answer is one frame
    /// old. No managed allocation after construction.
    /// </summary>
    public sealed class SwitchbackSenseBatch : IDisposable
    {
        public const int Fan = 11, Taps = 9, MaxHits = 4;
        /// <summary>Reach (u) of the two flank casts (pure right and left) that catch a trail running alongside.</summary>
        public const float FlankReach = 6f, FlankNear = 4.5f;
        /// <summary>Tap angles (degrees): the fan's inner and outer ring. Small on purpose - a lane change, not a swerve.</summary>
        public static readonly float2 Angles = new(8f, 16f);

        readonly int _cap;
        NativeArray<float3> _pos, _dir, _up, _goal;
        NativeArray<float2> _radReach;   // x radius, y reach (0 = idle slot)
        NativeArray<SpherecastCommand> _cmds;
        NativeArray<RaycastHit> _hits;
        NativeArray<float4> _out, _ready; // xyz steer dir, w: <0 lane clear, else distance to the hit straight ahead
        NativeArray<byte> _clear;          // 1: some tap was clear
        NativeArray<byte> _readyClear;
        JobHandle _handle;
        bool _scheduled, _disposed;
        int _mask;

        public SwitchbackSenseBatch(int capacity, int layerMask)
        {
            _cap = capacity; _mask = layerMask;
            _pos = new NativeArray<float3>(capacity, Allocator.Persistent);
            _dir = new NativeArray<float3>(capacity, Allocator.Persistent);
            _up = new NativeArray<float3>(capacity, Allocator.Persistent);
            _goal = new NativeArray<float3>(capacity, Allocator.Persistent);
            _radReach = new NativeArray<float2>(capacity, Allocator.Persistent);
            _cmds = new NativeArray<SpherecastCommand>(capacity * Fan, Allocator.Persistent);
            _hits = new NativeArray<RaycastHit>(capacity * Fan * MaxHits, Allocator.Persistent);
            _out = new NativeArray<float4>(capacity, Allocator.Persistent);
            _ready = new NativeArray<float4>(capacity, Allocator.Persistent);
            _clear = new NativeArray<byte>(capacity, Allocator.Persistent);
            _readyClear = new NativeArray<byte>(capacity, Allocator.Persistent);
            for (int i = 0; i < capacity; i++) _ready[i] = new float4(0, 0, 0, -1f);
        }

        public int Capacity => _cap;

        /// <summary>This frame's pose for slot <paramref name="i"/>; reach 0 leaves the slot idle.</summary>
        public void Write(int i, Vector3 pos, Vector3 dir, Vector3 up, Vector3 goalDir, float radius, float reach)
        {
            if (_disposed || i < 0 || i >= _cap) return;
            Complete();
            _pos[i] = pos; _dir[i] = dir; _up[i] = up; _goal[i] = goalDir; _radReach[i] = new float2(radius, reach);
        }

        /// <summary>Last frame's answer for slot <paramref name="i"/>: false when the lane ahead was clear.</summary>
        /// <summary>Whether the farthest blocked tap reaches at least 1.5x the straight line's hit (last frame).</summary>
        public bool FarthestHelps(int i) => !_disposed && i >= 0 && i < _cap && _readyClear[i] == 2;

        public bool Read(int i, out Vector3 steerDir, out float hitDist, out bool tapClear)
        {
            steerDir = default; hitDist = -1f; tapClear = false;
            if (_disposed || i < 0 || i >= _cap) return false;
            Complete();
            float4 o = _ready[i];
            if (o.w < 0f) return false;
            steerDir = new Vector3(o.x, o.y, o.z); hitDist = o.w; tapClear = _readyClear[i] == 1;
            return true;
        }

        void Complete()
        {
            if (!_scheduled) return;
            _handle.Complete();
            _scheduled = false;
            _ready.CopyFrom(_out);
            _readyClear.CopyFrom(_clear);
            for (int i = 0; i < _cap; i++) _radReach[i] = float2.zero; // a slot not written this frame stays idle
        }

        /// <summary>LateUpdate: cast this frame's fans on the job workers.</summary>
        public void Schedule()
        {
            if (_disposed) return;
            Complete();
            var qp = new QueryParameters(_mask, false, QueryTriggerInteraction.Collide, false);
            var build = new BuildJob { Pos = _pos, Dir = _dir, Up = _up, RadReach = _radReach, Query = qp, Angles = Angles, Cmds = _cmds }
                .Schedule(_cap * Fan, 8);
            var cast = SpherecastCommand.ScheduleBatch(_cmds, _hits, 4, MaxHits, build);
            _handle = new PickJob { Dir = _dir, Up = _up, Goal = _goal, RadReach = _radReach, Angles = Angles, Hits = _hits, Out = _out, Clear = _clear }
                .Schedule(_cap, 4, cast);
            _scheduled = true;
            JobHandle.ScheduleBatchedJobs();
        }

        public void Dispose()
        {
            if (_disposed) return;
            _handle.Complete();
            _disposed = true;
            _pos.Dispose(); _dir.Dispose(); _up.Dispose(); _goal.Dispose(); _radReach.Dispose();
            _cmds.Dispose(); _hits.Dispose(); _out.Dispose(); _ready.Dispose(); _clear.Dispose(); _readyClear.Dispose();
        }

        static float3 TapDir(float3 dir, float3 up, float2 angles, int k)
        {
            if (k == 0) return dir;
            float3 right = math.normalizesafe(math.cross(up, dir), new float3(1, 0, 0));
            float3 u = math.cross(dir, right);
            if (k >= Taps) return k == Taps ? right : -right;
            float a = math.radians(k <= 4 ? angles.x : angles.y);
            int side = (k - 1) & 3; // 0 right, 1 left, 2 up, 3 down
            float3 lat = side == 0 ? right : side == 1 ? -right : side == 2 ? u : -u;
            return math.normalizesafe(dir * math.cos(a) + lat * math.sin(a), dir);
        }

        [BurstCompile]
        struct BuildJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<float3> Pos, Dir, Up;
            [ReadOnly] public NativeArray<float2> RadReach;
            public QueryParameters Query;
            public float2 Angles;
            [WriteOnly] public NativeArray<SpherecastCommand> Cmds;

            public void Execute(int c)
            {
                int i = c / Fan, k = c % Fan;
                float2 rr = RadReach[i];
                float3 d = rr.y > 0f ? TapDir(Dir[i], Up[i], Angles, k) : new float3(0, 0, 1);
                // An idle slot casts a zero-length sweep (no hits).
                float reach = rr.y > 0f ? (k >= Taps ? FlankReach : rr.y) : 0f;
                Cmds[c] = new SpherecastCommand(Pos[i], math.max(rr.x, 0.1f), d, Query, reach);
            }
        }

        [BurstCompile]
        struct PickJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<float3> Dir, Up, Goal;
            [ReadOnly] public NativeArray<float2> RadReach;
            [ReadOnly] public NativeArray<RaycastHit> Hits;
            public float2 Angles;
            [WriteOnly] public NativeArray<float4> Out;
            [WriteOnly] public NativeArray<byte> Clear;

            float Nearest(int i, int k)
            {
                float best = float.MaxValue;
                int b = (i * Fan + k) * MaxHits;
                for (int h = 0; h < MaxHits; h++)
                {
                    RaycastHit hit = Hits[b + h];
                    float d = hit.distance;
                    // Unfilled slots are zeroed; start-overlaps (distance 0) are the hull's own fresh trail.
                    if (d > 0.01f && d < best && math.lengthsq((float3)hit.normal) > 0f) best = d;
                }
                return best;
            }

            public void Execute(int i)
            {
                float2 rr = RadReach[i];
                Clear[i] = 0;
                if (rr.y <= 0f) { Out[i] = new float4(0, 0, 0, -1f); return; }
                float ahead = Nearest(i, 0);
                float flankR = Nearest(i, Taps), flankL = Nearest(i, Taps + 1);
                if (ahead == float.MaxValue)
                {
                    // Clear ahead, but a trail alongside: a short tap away from it (if that tap is clear and the other flank is not closer).
                    bool nearR = flankR < FlankNear, nearL = flankL < FlankNear;
                    int away = nearR && (!nearL || flankR < flankL) ? 2 : nearL ? 1 : 0; // tap index 1 = right, 2 = left
                    if (away != 0 && Nearest(i, away) == float.MaxValue)
                    {
                        Clear[i] = 1;
                        Out[i] = new float4(TapDir(Dir[i], Up[i], Angles, away), math.min(flankR, flankL));
                        return;
                    }
                    Out[i] = new float4(0, 0, 0, -1f); return;
                }
                // Blocked: the clear tap with the best score (inner taps first, toward the goal, away from a near flank);
                // else the farthest one.
                int pick = 0; float pickScore = float.MinValue, far = ahead; int farK = 0;
                for (int k = 1; k < Taps; k++)
                {
                    float d = Nearest(i, k);
                    float3 td = TapDir(Dir[i], Up[i], Angles, k);
                    if (d == float.MaxValue)
                    {
                        int side = (k - 1) & 3;
                        float flank = side == 0 ? flankR : side == 1 ? flankL : float.MaxValue;
                        float s = (k <= 4 ? 1f : 0f) + math.dot(td, Goal[i]) - (flank < FlankNear ? 2f : 0f);
                        if (s > pickScore) { pickScore = s; pick = k; }
                    }
                    else if (d > far) { far = d; farK = k; }
                }
                bool clear = pick != 0;
                Clear[i] = (byte)(clear ? 1 : far > ahead * 1.5f ? 2 : 0);
                float3 steer = TapDir(Dir[i], Up[i], Angles, clear ? pick : farK);
                Out[i] = new float4(steer, ahead);
            }
        }
    }
}
