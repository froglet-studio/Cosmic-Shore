using System;
using Unity.Mathematics;

namespace CosmicShore.ECS
{
    /// <summary>
    /// The ONE due-queue of settled-bloom grow-stamp clears (Docs/PRISM_ANIMATION.md C23).
    ///
    /// A bloom's entity stamp keeps _GrowRate &gt; 0, and while it does every vertex of the prism runs
    /// PrismGrowScale's exp() and its three per-instance loads, for a factor that has long been exactly 1.
    /// <see cref="PrismRenderService.StampGrow"/> pushes one entry per stamp, fired at the first moment the
    /// shader's float32 factor <c>max(1 - (1 - s0)·exp(-k·t), 0)</c> IS exactly 1.0f on every axis, so the
    /// clear is bit-invisible, not a snap. The render service's LateUpdate flush host drains it.
    ///
    /// A binary min-heap on fire time over a managed struct array: no per-entry allocation, growth doubles
    /// (amortised) up to <see cref="MaxCapacity"/>. Per frame: O(1) when nothing is due, otherwise
    /// O(log n) per due entry popped, at most a budget per frame. Entries are never cancelled: each pop is
    /// re-validated against the entity's CURRENT stamp (<see cref="Decide"/>), so a retarget, a restore or a
    /// pool reuse turns an older entry into a no-op. The lay order is not monotone in fire time (the delay
    /// spans ~5-18 s with s0 and k), which is why this is a heap and not a FIFO.
    /// </summary>
    internal sealed class PrismGrowStampClearQueue
    {
        internal struct Entry
        {
            public float FireTime;   // PrismClock.Now at which the stamp is exactly final
            public float StampTime;  // the stamp's start time: the guard against a newer stamp
            public PrismRenderHandle Handle;
        }

        internal enum Verdict : byte
        {
            Skip = 0,   // stale: a newer stamp owns the entity, or it is already cleared
            Clear = 1,  // this stamp is exactly final now
            Refire = 2, // this stamp's start time, but a later end (a same-instant re-stamp): push again
        }

        // ------------------------------------------------------------------
        // The exact time (pure math, shared by the push and the guard).
        // ------------------------------------------------------------------

        /// <summary>
        /// ln of the largest per-axis remainder <c>|1 - s0|·exp(-k·t)</c> that still rounds to exactly 1.0f is
        /// -25·ln2 (half an ulp below 1; above 1 it is -24·ln2). 27·ln2 = 18.715 keeps a 4x (8x for shrinks)
        /// margin for a GPU exp that is not correctly rounded and a clock read a frame apart; it costs ~0.3-0.6 s
        /// more bloom evaluation per lay. Verified in float32 for k in [1.25, 2.5] (the ClockRateK range) and
        /// s0 in [0, 60], also with exp perturbed by ±2^-19 offline (PrismGrowStampClearTests holds the exact-exp grid).
        /// </summary>
        internal const float ExactLnMargin = 18.72f;

        /// <summary>Seconds after the stamp at which PrismGrowScale_float (PrismClockAnimation.hlsl) returns
        /// exactly 1.0f on every axis: <c>(ln max|1 - s0| + ExactLnMargin) / k</c>, clamped at 0.
        /// 0 when the stamp is already its own end state (s0 = 1) or unstamped (k = 0).</summary>
        internal static float ExactDelay(float rate, in float3 startFrac)
        {
            float deviation = math.cmax(math.abs(1f - startFrac));
            if (rate <= 0f || deviation <= 0f) return 0f;
            return math.max(0f, (math.log(deviation) + ExactLnMargin) / rate);
        }

        /// <summary>What a popped entry does, given the entity's CURRENT grow stamp. Pure, so the stale-entry
        /// rule is testable without an ECS world.</summary>
        internal static Verdict Decide(float entryStampTime, float currentStart, float currentRate,
            in float3 currentStartFrac, float now, out float refireTime)
        {
            refireTime = 0f;
            if (currentRate <= 0f) return Verdict.Skip;               // already cleared (or never stamped)
            if (currentStart != entryStampTime) return Verdict.Skip;  // a newer stamp: it has its own entry
            float fire = currentStart + ExactDelay(currentRate, in currentStartFrac);
            if (now >= fire) return Verdict.Clear;
            refireTime = fire;
            return Verdict.Refire;
        }

        // ------------------------------------------------------------------
        // The heap.
        // ------------------------------------------------------------------

        /// <summary>Hard ceiling: 2^18 entries (5 MB at 20 bytes). The largest arena build lays ~70k prisms
        /// within one 18 s window, so this is ~4x headroom; past it a push is refused and that prism keeps
        /// its stamp (the pre-C23 cost, never a wrong picture).</summary>
        internal const int MaxCapacity = 1 << 18;
        internal const int InitialCapacity = 1024;

        Entry[] _heap;
        int _count;
        readonly int _maxCapacity;

        internal PrismGrowStampClearQueue(int initialCapacity = InitialCapacity, int maxCapacity = MaxCapacity)
        {
            _maxCapacity = math.max(1, maxCapacity);
            _heap = new Entry[math.clamp(initialCapacity, 1, _maxCapacity)];
        }

        internal int Count => _count;
        internal int Capacity => _heap.Length;

        /// <summary>Pushes the stamp written at <paramref name="stampTime"/>, due at
        /// <paramref name="fireTime"/>. False only when the queue is at <see cref="MaxCapacity"/>.</summary>
        internal bool TryPush(float fireTime, float stampTime, in PrismRenderHandle handle)
        {
            if (_count == _heap.Length)
            {
                if (_heap.Length >= _maxCapacity) return false;
                Array.Resize(ref _heap, math.min(_heap.Length * 2, _maxCapacity));
            }
            int i = _count++;
            var e = new Entry { FireTime = fireTime, StampTime = stampTime, Handle = handle };
            while (i > 0)
            {
                int parent = (i - 1) >> 1;
                if (_heap[parent].FireTime <= fireTime) break;
                _heap[i] = _heap[parent];
                i = parent;
            }
            _heap[i] = e;
            return true;
        }

        /// <summary>Pops, in fire-time order, up to <paramref name="max"/> entries whose fire time is
        /// &lt;= <paramref name="now"/> into <paramref name="into"/>; returns how many. Touches only the root
        /// when nothing is due.</summary>
        internal int PopDue(float now, Entry[] into, int max)
        {
            max = math.min(max, into.Length);
            int n = 0;
            while (n < max && _count > 0 && _heap[0].FireTime <= now)
            {
                into[n++] = _heap[0];
                var last = _heap[--_count];
                _heap[_count] = default;
                if (_count > 0) SiftDown(last);
            }
            return n;
        }

        void SiftDown(in Entry e)
        {
            int i = 0;
            int half = _count >> 1;
            while (i < half)
            {
                int child = (i << 1) + 1;
                int right = child + 1;
                if (right < _count && _heap[right].FireTime < _heap[child].FireTime) child = right;
                if (e.FireTime <= _heap[child].FireTime) break;
                _heap[i] = _heap[child];
                i = child;
            }
            _heap[i] = e;
        }

        /// <summary>Drops every entry (a new play session, domain reload disabled). Keeps the array.</summary>
        internal void Clear()
        {
            Array.Clear(_heap, 0, _count);
            _count = 0;
        }
    }
}
