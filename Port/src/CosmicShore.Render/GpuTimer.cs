using System;
using Silk.NET.OpenGL;

namespace CosmicShore.Render
{
    /// <summary>
    /// Which frame's queries are safe to read. A GL timer query's result arrives a few frames after
    /// it was issued, so each frame writes its own slot of a ring and, before reusing a slot, reads
    /// what that slot measured <see cref="Depth"/> frames ago. A slot whose result is still not
    /// back is dropped (counted), never waited on: waiting would stall the CPU on the GPU, which is
    /// the cost the timer exists to measure. Pure bookkeeping, so tests drive it with no GPU.
    /// </summary>
    public sealed class GpuTimerRing
    {
        readonly bool[] _issued;
        int _frame;

        public GpuTimerRing(int depth) => _issued = new bool[Math.Max(2, depth)];

        public int Depth => _issued.Length;

        /// <summary>The slot this frame writes.</summary>
        public int Slot => _frame % _issued.Length;

        /// <summary>True when <see cref="Slot"/> still holds an unread frame (read it before reusing).</summary>
        public bool PendingInSlot => _issued[Slot];

        /// <summary>Frames whose results were not back when their slot came round again.</summary>
        public int Dropped { get; private set; }

        /// <summary>The pending frame in <see cref="Slot"/> was read (or <paramref name="available"/> = false: dropped).</summary>
        public void Harvested(bool available)
        {
            if (!available) Dropped++;
            _issued[Slot] = false;
        }

        /// <summary>This frame's queries were issued into <see cref="Slot"/>; the next frame takes the next slot.</summary>
        public void Issued()
        {
            _issued[Slot] = true;
            _frame++;
        }
    }

    /// <summary>
    /// GPU time per render pass from <c>GL_TIME_ELAPSED</c> queries (desktop GL 3.3 core; GL ES 3.0
    /// has no timer query in core, so on a phone this reports unsupported and costs nothing).
    /// Passes run one after another: a time-elapsed query cannot nest.
    /// </summary>
    public sealed class GpuTimer : IDisposable
    {
        readonly GL _gl;
        readonly uint[,] _queries;
        readonly bool[,] _used;
        readonly double[] _ms;
        readonly GpuTimerRing _ring = new(4);
        int _open = -1;

        public static bool Supported => !GlCaps.IsEs;

        public string[] Passes { get; }

        public int Dropped => _ring.Dropped;

        public GpuTimer(GL gl, params string[] passes)
        {
            _gl = gl;
            Passes = passes;
            _ms = new double[passes.Length];
            _queries = new uint[_ring.Depth, passes.Length];
            _used = new bool[_ring.Depth, passes.Length];
            for (int s = 0; s < _ring.Depth; s++)
                for (int p = 0; p < passes.Length; p++)
                    _queries[s, p] = gl.GenQuery();
        }

        /// <summary>
        /// Starts a frame. Returns the per-pass milliseconds of the frame measured
        /// <see cref="GpuTimerRing.Depth"/> frames ago when its results are back (a pass that did
        /// not run reads 0), else null. The array is reused: copy what you keep.
        /// </summary>
        public double[] BeginFrame()
        {
            if (!_ring.PendingInSlot) return null;
            int slot = _ring.Slot;
            bool available = true;
            for (int p = 0; p < Passes.Length && available; p++)
                if (_used[slot, p])
                {
                    _gl.GetQueryObject(_queries[slot, p], QueryObjectParameterName.ResultAvailable, out int ready);
                    available = ready != 0;
                }
            if (available)
                for (int p = 0; p < Passes.Length; p++)
                {
                    _ms[p] = 0;
                    if (!_used[slot, p]) continue;
                    _gl.GetQueryObject(_queries[slot, p], QueryObjectParameterName.Result, out ulong ns);
                    _ms[p] = ns / 1e6;
                }
            for (int p = 0; p < Passes.Length; p++) _used[slot, p] = false;
            _ring.Harvested(available);
            return available ? _ms : null;
        }

        public void Begin(int pass)
        {
            if (_open >= 0) End();
            _gl.BeginQuery(QueryTarget.TimeElapsed, _queries[_ring.Slot, pass]);
            _used[_ring.Slot, pass] = true;
            _open = pass;
        }

        public void End()
        {
            if (_open < 0) return;
            _gl.EndQuery(QueryTarget.TimeElapsed);
            _open = -1;
        }

        public void EndFrame()
        {
            End();
            _ring.Issued();
        }

        public void Dispose()
        {
            foreach (var q in _queries) _gl.DeleteQuery(q);
        }
    }
}
