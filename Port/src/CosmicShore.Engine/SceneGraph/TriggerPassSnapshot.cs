using System;
using System.Collections.Generic;

namespace CosmicShore.Engine
{
    /// <summary>
    /// The physics scene a query sees (original contract: Physics.autoSyncTransforms = false,
    /// which is this project's setting). A collider's pose reaches the physics scene when the
    /// physics step syncs transforms, so between two steps every Overlap*/Raycast answers from
    /// the poses of the LAST step — moving a transform does not move its collider for queries
    /// until the next step (or an explicit <see cref="Physics.SyncTransforms"/>). A collider
    /// that ARRIVES between steps (added, enabled, or its GameObject activated) enters the
    /// scene at once with its current pose; one that leaves (disabled, deactivated, destroyed)
    /// is gone at once.
    ///
    /// The snapshot is the trigger pass's own per-step shapes, kept sorted by min-x, so a query
    /// is a binary search plus the shapes whose x-extent reaches it instead of a walk over
    /// every registered collider. Results are ordered by collider registration (deterministic),
    /// then truncated to the caller's buffer, as before.
    /// </summary>
    public sealed partial class TriggerPass
    {
        // A shape wider than this on x is kept out of the sorted scan (it would widen every
        // query's window) and tested on every query instead.
        const float BigHalfExtentX = 64f;

        int _snapCount;                       // _live[0.._snapCount) and _shapes are the snapshot
        int _snapSorted;                      // _order[0.._snapSorted) sorted by min-x (small shapes and big alike)
        float[] _snapMinX = Array.Empty<float>();
        float _snapMaxHalfX;                  // widest small shape's half-extent on x
        readonly List<int> _snapBig = new();
        readonly List<Collider> _arrived = new();
        readonly HashSet<Collider> _arrivedSet = new(ReferenceEqualityComparer.Instance);
        readonly List<int> _qLive = new();
        readonly List<Collider> _qHits = new();
        long[] _keys = Array.Empty<long>();

        /// <summary>A collider entered the physics scene between steps (registered, enabled, or activated).</summary>
        internal void NoteArrived(Collider collider)
        {
            if (_arrivedSet.Add(collider)) _arrived.Add(collider);
        }

        /// <summary>After a step's shapes are built and sorted: they ARE the physics scene until the next step.</summary>
        void CommitQuerySnapshot(int sortedCount)
        {
            _snapCount = _live.Count;
            _snapSorted = sortedCount;
            if (_snapMinX.Length < sortedCount) _snapMinX = new float[Math.Max(sortedCount, _snapMinX.Length * 2)];
            _snapBig.Clear();
            _snapMaxHalfX = 0f;
            for (int k = 0; k < sortedCount; k++)
            {
                ref readonly var sh = ref _shapes[_order[k]];
                _snapMinX[k] = sh.Center.x - sh.Extents.x;
                if (sh.Extents.x > BigHalfExtentX) _snapBig.Add(_order[k]);
                else if (sh.Extents.x > _snapMaxHalfX) _snapMaxHalfX = sh.Extents.x;
            }
            _arrived.Clear();
            _arrivedSet.Clear();
        }

        /// <summary>Physics.SyncTransforms: the scene takes every collider's current pose now (no trigger messages).</summary>
        internal void SyncQuerySnapshot()
        {
            SnapshotLive();
            BuildShapes();
            CommitQuerySnapshot(SortShapesByMinX());
        }

        /// <summary>Live participants in registration order.</summary>
        void SnapshotLive()
        {
            _live.Clear();
            int n = 0;
            if (_keys.Length < _enabled.Count) _keys = new long[Math.Max(_enabled.Count, _keys.Length * 2)];
            var scratch = _liveScratch;
            scratch.Clear();
            foreach (var collider in _enabled)
            {
                if (!collider.isActiveAndEnabled) continue;
                // Registration sequence in the high word, position in the low: a primitive
                // sort with no delegate and no dictionary lookup per comparison.
                _keys[n] = (collider.TriggerSeq << 32) | (uint)n;
                scratch.Add(collider);
                n++;
            }
            Array.Sort(_keys, 0, n);
            for (int k = 0; k < n; k++) _live.Add(scratch[(int)(_keys[k] & 0xFFFFFFFF)]);
        }
        readonly List<Collider> _liveScratch = new();

        /// <summary>
        /// _order[0..m) = the shaped live indices by (min-x, index) — the order the sweep and the
        /// query snapshot both use. Keys pack a sortable min-x above the index, so the sort is a
        /// primitive one with exactly the tie-break the comparer it replaced had.
        /// </summary>
        int SortShapesByMinX()
        {
            int n = _live.Count, m = 0;
            if (_keys.Length < n) _keys = new long[Math.Max(n, _keys.Length * 2)];
            for (int i = 0; i < n; i++)
            {
                ref readonly var sh = ref _shapes[i];
                if (sh.Kind == ShapeKind.None) continue;
                _keys[m++] = ((long)SortableBits(sh.Center.x - sh.Extents.x) << 32) | (uint)i;
            }
            Array.Sort(_keys, 0, m);
            for (int k = 0; k < m; k++) _order[k] = (int)(_keys[k] & 0xFFFFFFFF);
            return m;
        }

        static int SortableBits(float f)
        {
            int b = BitConverter.SingleToInt32Bits(f);
            return b >= 0 ? b : b ^ 0x7FFFFFFF;
        }

        void EnsureQueryScene()
        {
            if (Physics.autoSyncTransforms) SyncQuerySnapshot();
        }

        /// <summary>
        /// Snapshot shapes whose AABB touches [min, max], plus every collider that arrived
        /// since the step — in registration order. <paramref name="snapHits"/> gets live
        /// indices (tested against the step's pose); <paramref name="arrivals"/> the rest.
        /// </summary>
        void GatherCandidates(Vector3 min, Vector3 max, List<int> snapHits)
        {
            snapHits.Clear();
            // The sorted window: every small shape with minX in [min.x - 2*maxHalf, max.x].
            float lo = min.x - 2f * _snapMaxHalfX;
            int k = LowerBound(_snapMinX, _snapSorted, lo);
            for (; k < _snapSorted && _snapMinX[k] <= max.x; k++)
            {
                int i = _order[k];
                ref readonly var sh = ref _shapes[i];
                if (sh.Extents.x > BigHalfExtentX) continue; // tested below
                if (AabbTouches(in sh, min, max)) snapHits.Add(i);
            }
            foreach (int i in _snapBig)
                if (AabbTouches(in _shapes[i], min, max)) snapHits.Add(i);
            snapHits.Sort();
        }

        static bool AabbTouches(in PhysicsShape sh, Vector3 min, Vector3 max)
            => sh.Center.x + sh.Extents.x >= min.x && sh.Center.x - sh.Extents.x <= max.x
            && sh.Center.y + sh.Extents.y >= min.y && sh.Center.y - sh.Extents.y <= max.y
            && sh.Center.z + sh.Extents.z >= min.z && sh.Center.z - sh.Extents.z <= max.z;

        static int LowerBound(float[] a, int n, float value)
        {
            int lo = 0, hi = n;
            while (lo < hi)
            {
                int mid = (lo + hi) >> 1;
                if (a[mid] < value) lo = mid + 1; else hi = mid;
            }
            return lo;
        }

        /// <summary>Snapshot collider i still in the scene (it may have left since the step).</summary>
        Collider SnapCollider(int i, int layerMask, QueryTriggerInteraction qti)
        {
            var c = _live[i];
            if (c.destroyedFlag || !Accepts(c, layerMask, qti)) return null;
            return c;
        }

        /// <summary>Merges the snapshot and arrival hits into registration order, then fills the buffer.</summary>
        int EmitOrdered(List<Collider> hits, Collider[] results)
        {
            if (hits.Count > 1) hits.Sort(_bySeqOrLast);
            int n = Math.Min(hits.Count, results.Length);
            for (int i = 0; i < n; i++) results[i] = hits[i];
            return n;
        }

        Comparison<Collider> _bySeqOrLastCache;
        Comparison<Collider> _bySeqOrLast => _bySeqOrLastCache ??= (a, b) =>
            (a.TriggerSeq > 0 ? a.TriggerSeq : long.MaxValue).CompareTo(b.TriggerSeq > 0 ? b.TriggerSeq : long.MaxValue);

        // ── Sphere / capsule / box ─────────────────────────────────────

        internal int OverlapSphereNonAlloc(Vector3 position, float radius, Collider[] results, int layerMask, QueryTriggerInteraction qti)
        {
            EnsureQueryScene();
            var r = new Vector3(radius, radius, radius);
            GatherCandidates(position - r, position + r, _qLive);
            _qHits.Clear();
            foreach (int i in _qLive)
                if (SnapCollider(i, layerMask, qti) is { } c && ShapeMath.SphereOverlaps(in _shapes[i], position, radius)) _qHits.Add(c);
            foreach (var c in _arrived)
                if (!c.destroyedFlag && Accepts(c, layerMask, qti) && SphereOverlapsCollider(position, radius, c) && !InSnapshotHits(c)) _qHits.Add(c);
            return EmitOrdered(_qHits, results);
        }

        internal int OverlapCapsuleNonAlloc(Vector3 p0, Vector3 p1, float radius, Collider[] results, int layerMask, QueryTriggerInteraction qti)
        {
            EnsureQueryScene();
            var r = new Vector3(radius, radius, radius);
            GatherCandidates(Vector3.Min(p0, p1) - r, Vector3.Max(p0, p1) + r, _qLive);
            _qHits.Clear();
            PhysicsShape probe = default;
            ShapeMath.SetCapsule(ref probe, p0, p1, radius);
            foreach (int i in _qLive)
                if (SnapCollider(i, layerMask, qti) is { } c && ShapeMath.Overlap(in probe, in _shapes[i])) _qHits.Add(c);
            foreach (var c in _arrived)
            {
                if (c.destroyedFlag || !Accepts(c, layerMask, qti) || InSnapshotHits(c)) continue;
                if (ProbeOverlapsCollider(in probe, c)) _qHits.Add(c);
            }
            return EmitOrdered(_qHits, results);
        }

        internal Collider[] OverlapBox(Vector3 center, Vector3 halfExtents, Quaternion orientation, int layerMask, QueryTriggerInteraction qti)
        {
            EnsureQueryScene();
            PhysicsShape probe = default;
            ShapeMath.SetBox(ref probe, center, orientation, new Vector3(Mathf.Abs(halfExtents.x), Mathf.Abs(halfExtents.y), Mathf.Abs(halfExtents.z)));
            GatherCandidates(center - probe.Extents, center + probe.Extents, _qLive);
            _qHits.Clear();
            foreach (int i in _qLive)
                if (SnapCollider(i, layerMask, qti) is { } c && ShapeMath.Overlap(in probe, in _shapes[i])) _qHits.Add(c);
            foreach (var c in _arrived)
                if (!c.destroyedFlag && Accepts(c, layerMask, qti) && ProbeOverlapsCollider(in probe, c) && !InSnapshotHits(c)) _qHits.Add(c);
            var buffer = new Collider[_qHits.Count];
            EmitOrdered(_qHits, buffer);
            return buffer;
        }

        /// <summary>An arrival that was ALSO in the step's snapshot (left and came back) is reported once.</summary>
        bool InSnapshotHits(Collider c)
        {
            foreach (var h in _qHits) if (ReferenceEquals(h, c)) return true;
            return false;
        }

        // ── Rays ───────────────────────────────────────────────────────

        internal bool Raycast(Vector3 origin, Vector3 direction, out RaycastHit hit, float maxDistance, int layerMask, QueryTriggerInteraction qti)
        {
            hit = default;
            var all = RaycastHits(origin, direction, maxDistance, layerMask, qti);
            if (all.Count == 0) return false;
            hit = all[0];
            return true;
        }

        internal RaycastHit[] RaycastAll(Vector3 origin, Vector3 direction, float maxDistance, int layerMask, QueryTriggerInteraction qti)
            => RaycastHits(origin, direction, maxDistance, layerMask, qti).ToArray();

        readonly List<RaycastHit> _rayHits = new();

        List<RaycastHit> RaycastHits(Vector3 origin, Vector3 direction, float maxDistance, int layerMask, QueryTriggerInteraction qti)
        {
            EnsureQueryScene();
            _rayHits.Clear();
            bool bounded = !float.IsInfinity(maxDistance) && maxDistance < 1e7f;
            if (bounded)
            {
                var end = origin + direction * maxDistance;
                GatherCandidates(Vector3.Min(origin, end), Vector3.Max(origin, end), _qLive);
            }
            else
            {
                _qLive.Clear();
                for (int k = 0; k < _snapSorted; k++) _qLive.Add(_order[k]);
                _qLive.Sort();
            }
            foreach (int i in _qLive)
            {
                if (SnapCollider(i, layerMask, qti) is not { } c) continue;
                if (ShapeMath.Raycast(in _shapes[i], origin, direction, out float d, out Vector3 n) && d <= maxDistance)
                    _rayHits.Add(new RaycastHit { collider = c, distance = d, point = origin + direction * d, normal = n });
            }
            foreach (var c in _arrived)
            {
                if (c.destroyedFlag || !Accepts(c, layerMask, qti)) continue;
                bool dup = false;
                foreach (var h in _rayHits) if (ReferenceEquals(h.collider, c)) { dup = true; break; }
                if (dup) continue;
                if (RayCollider(origin, direction, c, out float d, out Vector3 n) && d <= maxDistance)
                    _rayHits.Add(new RaycastHit { collider = c, distance = d, point = origin + direction * d, normal = n });
            }
            // Nearest first; equal distances keep registration order (stable over a registration-ordered list).
            _rayHits.Sort((a, b) =>
            {
                int c = a.distance.CompareTo(b.distance);
                return c != 0 ? c : _bySeqOrLast(a.collider, b.collider);
            });
            return _rayHits;
        }
    }
}
