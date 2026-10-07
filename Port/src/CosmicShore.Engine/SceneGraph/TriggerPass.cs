using System;
using System.Collections.Generic;

namespace CosmicShore.Engine
{
    /// <summary>
    /// Trigger physics: overlap detection between registered colliders with
    /// OnTriggerEnter/OnTriggerStay/OnTriggerExit dispatch.
    ///
    /// Timing — the pass is the physics step: it runs inside each fixed step, after
    /// FixedUpdate (the original engine's Physics.Simulate), at Time.fixedDeltaTime — the
    /// project's TimeManager step, 0.04 s here. A contact is therefore a SAMPLE taken at
    /// that rate, not once per rendered frame: a frame with no fixed step fires nothing,
    /// and a body fast enough to cross a trigger between two steps is never seen. The same
    /// step's shapes are the scene every physics query answers from until the next step
    /// (see TriggerPassSnapshot).
    ///
    /// Semantics (original-engine contract, minimal subset):
    ///   • A pair produces trigger events iff at least one collider has
    ///     <see cref="Collider.isTrigger"/> (no Rigidbody requirement — the port has no
    ///     rigidbodies). Two non-trigger colliders are ignored entirely.
    ///   • Both sides receive the callback: every MonoBehaviour on each collider's own
    ///     GameObject declaring `OnTriggerEnter(Collider)` / `OnTriggerExit(Collider)`
    ///     (any visibility, discovered reflectively via <see cref="LifecycleHooks"/>) is
    ///     invoked with the OTHER collider as the argument. Like the original engine,
    ///     delivery ignores the per-behaviour `enabled` flag (the documented quirk that
    ///     physics messages bypass it); the receiving GameObject must be alive and active.
    ///   • OnTriggerExit fires when an overlapping pair separates, AND when either side is
    ///     destroyed or disabled mid-overlap — the surviving side is notified with the
    ///     (possibly already destroyed) other collider as the argument.
    ///   • OnTriggerStay fires every physics step for every pair still touching, after the
    ///     step's exits and enters (a pair's first Stay lands in its Enter step).
    ///
    /// Shapes — exact sphere-sphere; boxes are treated as world-space AABBs (rotation is
    /// ignored: center transformed through the hierarchy, extents scaled by |lossyScale|).
    /// OBB support arrives with the full physics phase.
    ///
    /// Determinism — colliders are iterated in registration order (AddComponent order,
    /// which is creation order). Pairs are tested (i, j) with i &lt; j over that order;
    /// exit events fire first (in the order pairs were originally established), then enter
    /// events in discovery order. For a pair, the earlier-registered collider's GameObject
    /// is notified first. No RNG, no hashing of unstable values — identical scenes produce
    /// identical event streams.
    /// </summary>
    public sealed partial class TriggerPass
    {
        readonly OrderedColliderSet _colliders = new();                // registration order
        readonly List<(Collider a, Collider b)> _activePairs = new();  // established order
        readonly HashSet<(Collider a, Collider b)> _activeSet = new();

        // Per-frame scratch (reused to avoid steady-state allocation).
        readonly List<Collider> _live = new();
        readonly List<(Collider a, Collider b)> _discovered = new();
        readonly HashSet<(Collider a, Collider b)> _current = new();

        // Colliders whose own enabled flag is set: the only ones a frame can find live. A grown
        // arena registers ~50k colliders of which ~1.3k are live (a prism's collider stays off
        // until its creation completes), and walking all of them every frame to find those
        // was most of the pass. Registration order is kept by sequence number and restored by
        // sorting the (small) live set, so discovery order is unchanged.
        readonly HashSet<Collider> _enabled = new(ReferenceEqualityComparer.Instance);
        // Registration order lives on Component.TriggerSeq: Instantiate's field copy stops at the
        // engine base classes, so a clone keeps its OWN place in line (a field declared on
        // Collider would be copied from the source after the clone registered).
        long _nextSeq;

        public TriggerPass() { }

        internal void Register(Collider collider)
        {
            if (_colliders.Contains(collider)) return;
            collider.TriggerSeq = ++_nextSeq;
            _colliders.Add(collider);
            if (collider.enabled) { _enabled.Add(collider); NoteArrived(collider); }
        }

        internal void Unregister(Collider collider)
        {
            _colliders.Remove(collider);
            _enabled.Remove(collider);
            collider.TriggerSeq = 0;
        }

        // COSMIC_SHORE_VERIFY_TRIGGERS=1: every 30 frames, rebuild the live set by walking every
        // registered collider (the former way) and report any difference, in order.
        static readonly bool s_verify = Environment.GetEnvironmentVariable("COSMIC_SHORE_VERIFY_TRIGGERS") == "1";
        readonly List<Collider> _verifyLive = new();

        void VerifyLive()
        {
            _verifyLive.Clear();
            foreach (var c in _colliders) if (c.isActiveAndEnabled) _verifyLive.Add(c);
            bool same = _verifyLive.Count == _live.Count;
            for (int i = 0; same && i < _live.Count; i++) same = ReferenceEquals(_verifyLive[i], _live[i]);
            Console.WriteLine($"[verify-triggers] frame {Time.frameCount}: {_live.Count} live vs {_verifyLive.Count} walked, {(same ? "identical order" : "DIFFERENT")}");
        }

        /// <summary>A registered collider's enabled flag changed.</summary>
        internal void SetEnabled(Collider collider, bool enabled)
        {
            if (!_colliders.Contains(collider)) return;
            if (enabled) { _enabled.Add(collider); NoteArrived(collider); }
            else _enabled.Remove(collider);
        }



        /// <summary>
        /// Registration-ordered collider set with O(1) add/remove/contains. It was a plain
        /// list, whose Contains-before-Add and linear Remove made every prism a grown arena
        /// lays or retires an O(n) scan of every live collider — at ~20k prisms, a tenth of
        /// the frame. Enumeration order is still exactly registration order, which the pass
        /// relies on for a deterministic enter-event stream.
        /// </summary>
        sealed class OrderedColliderSet
        {
            readonly LinkedList<Collider> _order = new();
            readonly Dictionary<Collider, LinkedListNode<Collider>> _nodes = new(ReferenceEqualityComparer.Instance);

            public int Count => _order.Count;
            public bool Contains(Collider c) => _nodes.ContainsKey(c);
            public void Add(Collider c)
            {
                if (_nodes.ContainsKey(c)) return;
                _nodes[c] = _order.AddLast(c);
            }
            public bool Remove(Collider c)
            {
                if (!_nodes.Remove(c, out var node)) return false;
                _order.Remove(node);
                return true;
            }
            public void Clear() { _order.Clear(); _nodes.Clear(); }
            public LinkedList<Collider>.Enumerator GetEnumerator() => _order.GetEnumerator();
        }

        internal void RunFrame()
        {
            if (_enabled.Count == 0 && _activePairs.Count == 0) return;

            // 1. Snapshot live participants in registration order. Colliders added by
            //    callbacks during this pass join next frame.
            long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
            SnapshotLive();
            if (s_verify && Time.frameCount % 30 == 0) VerifyLive();
            long t1 = System.Diagnostics.Stopwatch.GetTimestamp();

            // 2. Current overlap set — (i, j), i < j, at least one trigger.
            //
            //    Broadphase: every live collider's shape is resolved to world space ONCE per
            //    frame (the hierarchy walk used to be repeated per pair), then a sort-and-sweep
            //    on x finds the pairs whose AABBs touch. Candidates are sorted back into
            //    (i, j) registration order before the exact test, so discovery order — and so
            //    the enter-event stream — is identical to the exhaustive i<j scan it replaces.
            //    Arenas with tens of thousands of trigger prisms stay near-linear.
            _discovered.Clear();
            _current.Clear();
            BuildShapes();
            long t2 = System.Diagnostics.Stopwatch.GetTimestamp();
            SweepCandidates();
            // This step's shapes become the scene every query answers from until the next step
            // (committed before dispatch: a trigger callback that queries sees this step).
            CommitQuerySnapshot(_sweptCount);
            long t3 = System.Diagnostics.Stopwatch.GetTimestamp();
            foreach (long key in _candidates)
            {
                int i = (int)(key >> 32), j = (int)(key & 0xFFFFFFFF);
                TestPair(i, j);
            }
            long t4 = System.Diagnostics.Stopwatch.GetTimestamp();
            GameLoop.AddPhase("  trig.live", t1 - t0);
            GameLoop.AddPhase("  trig.shapes", t2 - t1);
            GameLoop.AddPhase("  trig.sweep", t3 - t2);
            GameLoop.AddPhase("  trig.pairs", t4 - t3);
            if (GameLoop.PhaseTiming && Time.frameCount % 30 == 0)
                Console.WriteLine($"[triggers] {_colliders.Count} registered, {_enabled.Count} enabled, {_live.Count} live, {_candidates.Count} candidates, {_activePairs.Count} active pairs");

            // 3. Exits first: previously-active pairs that separated, disabled, or died.
            if (_activePairs.Count > 0)
            {
                var previous = _activePairs.ToArray();
                _activePairs.Clear();
                foreach (var pair in previous)
                {
                    if (_current.Contains(pair))
                    {
                        _activePairs.Add(pair); // still overlapping — keep established order
                        continue;
                    }
                    _activeSet.Remove(pair);
                    Dispatch(pair.a, pair.b, enter: false);
                }
            }

            // 4. Enters, in discovery order.
            foreach (var pair in _discovered)
            {
                if (!_activeSet.Add(pair)) continue;
                _activePairs.Add(pair);
                Dispatch(pair.a, pair.b, enter: true);
            }

            // 5. Stays: every pair still touching after this step, in established order —
            //    including a pair that entered this step (the original engine reports the
            //    first Stay in the same simulation step as the Enter). Only behaviours that
            //    declare OnTriggerStay are visited, and nothing is allocated per pair.
            int stays = _activePairs.Count;
            for (int p = 0; p < stays && p < _activePairs.Count; p++)
            {
                var (a, b) = _activePairs[p];
                if (!_current.Contains((a, b))) continue; // left during this step's callbacks
                DispatchStay(a, b);
                DispatchStay(b, a);
            }
        }

        /// <summary>Exact-test one (earlier, later) registration-order pair (≥1 side is a trigger).</summary>
        void TestPair(int i, int j)
        {
            var a = _live[i];
            var b = _live[j];
            if (ReferenceEquals(a.gameObject, b.gameObject)) return;
            if (!ShapesOverlap(in _shapes[i], in _shapes[j])) return;

            var pair = (a, b);
            _current.Add(pair);
            _discovered.Add(pair);
        }

        // ── Broadphase ───────────────────────────────────────────────

        enum ShapeKind : byte { None, Sphere, Box }

        /// <summary>A collider's world-space shape for this frame (mesh colliders are their bounds box).</summary>
        struct Shape
        {
            public ShapeKind Kind;
            public Vector3 Center, Extents; // Extents = radius on every axis for spheres
            public float Radius;
            public bool Trigger;
        }

        Shape[] _shapes = Array.Empty<Shape>();
        int[] _order = Array.Empty<int>();
        readonly List<int> _sweepActive = new();
        readonly List<long> _candidates = new();

        void BuildShapes()
        {
            int n = _live.Count;
            if (_shapes.Length < n) { _shapes = new Shape[Math.Max(n, _shapes.Length * 2)]; _order = new int[_shapes.Length]; }
            for (int i = 0; i < n; i++)
            {
                var c = _live[i];
                ref var sh = ref _shapes[i];
                sh.Trigger = c.isTrigger;
                switch (c)
                {
                    case SphereCollider sphere:
                        sh.Kind = ShapeKind.Sphere;
                        sh.Center = sphere.transform.TransformPoint(sphere.center);
                        sh.Radius = WorldRadius(sphere);
                        sh.Extents = new Vector3(sh.Radius, sh.Radius, sh.Radius);
                        break;
                    case BoxCollider box:
                        sh.Kind = ShapeKind.Box;
                        (sh.Center, sh.Extents) = BoxBounds(box);
                        break;
                    case MeshCollider mesh when TryMeshBounds(mesh, out var mc, out var me):
                        sh.Kind = ShapeKind.Box;
                        sh.Center = mc;
                        sh.Extents = me;
                        break;
                    default:
                        sh.Kind = ShapeKind.None; // no overlap test exists for this shape
                        break;
                }
            }
        }

        int _sweptCount;

        void SweepCandidates()
        {
            _candidates.Clear();
            int m = _sweptCount = SortShapesByMinX();

            _sweepActive.Clear();
            for (int k = 0; k < m; k++)
            {
                int e = _order[k];
                ref readonly var se = ref _shapes[e];
                float minX = se.Center.x - se.Extents.x;
                for (int t = _sweepActive.Count - 1; t >= 0; t--)
                {
                    int a = _sweepActive[t];
                    ref readonly var sa = ref _shapes[a];
                    if (sa.Center.x + sa.Extents.x < minX)
                    {
                        _sweepActive[t] = _sweepActive[^1];
                        _sweepActive.RemoveAt(_sweepActive.Count - 1);
                        continue;
                    }
                    if (!sa.Trigger && !se.Trigger) continue;
                    if (Mathf.Abs(sa.Center.y - se.Center.y) > sa.Extents.y + se.Extents.y) continue;
                    if (Mathf.Abs(sa.Center.z - se.Center.z) > sa.Extents.z + se.Extents.z) continue;
                    int lo = Math.Min(a, e), hi = Math.Max(a, e);
                    _candidates.Add(((long)lo << 32) | (uint)hi);
                }
                _sweepActive.Add(e);
            }
            _candidates.Sort();
        }

        /// <summary>The exact tests, over the frame's resolved shapes (same rules as <see cref="Overlaps"/>).</summary>
        static bool ShapesOverlap(in Shape a, in Shape b)
        {
            if (a.Kind == ShapeKind.Sphere && b.Kind == ShapeKind.Sphere)
            {
                float r = a.Radius + b.Radius;
                return (a.Center - b.Center).sqrMagnitude <= r * r;
            }
            if (a.Kind == ShapeKind.Box && b.Kind == ShapeKind.Box)
                return AabbAabb(a.Center, a.Extents, b.Center, b.Extents);
            ref readonly var s = ref a.Kind == ShapeKind.Sphere ? ref a : ref b;
            ref readonly var box = ref a.Kind == ShapeKind.Sphere ? ref b : ref a;
            var closest = new Vector3(
                Mathf.Clamp(s.Center.x, box.Center.x - box.Extents.x, box.Center.x + box.Extents.x),
                Mathf.Clamp(s.Center.y, box.Center.y - box.Extents.y, box.Center.y + box.Extents.y),
                Mathf.Clamp(s.Center.z, box.Center.z - box.Extents.z, box.Center.z + box.Extents.z));
            return (s.Center - closest).sqrMagnitude <= s.Radius * s.Radius;
        }

        // ── Dispatch ─────────────────────────────────────────────────

        /// <summary>
        /// Observer for every trigger contact that starts, raised once per pair before dispatch.
        /// The parity harness (Player/ParityRun) records contact events through it; null otherwise.
        /// </summary>
        public static Action<Collider, Collider> ContactStarted;

        static void Dispatch(Collider a, Collider b, bool enter)
        {
            if (enter) ContactStarted?.Invoke(a, b);
            DispatchTo(a, b, enter);
            DispatchTo(b, a, enter);
        }

        static void DispatchStay(Collider receiver, Collider other)
        {
            var go = receiver.gameObject;
            if (go is null || go.IsDestroyed || !go.activeInHierarchy) return;
            var components = go.Components;
            for (int i = 0; i < components.Count; i++)
                if (components[i] is MonoBehaviour { HasTriggerStay: true } mb && !mb.IsDestroyed)
                    mb.RunTriggerStay(other);
        }

        /// <summary>Notify every receiving behaviour on <paramref name="receiver"/>'s GameObject, passing <paramref name="other"/>.</summary>
        static void DispatchTo(Collider receiver, Collider other, bool enter)
        {
            var go = receiver.gameObject;
            if (go is null || go.IsDestroyed || !go.activeInHierarchy) return;

            // Snapshot — callbacks may add/remove components.
            var components = go.Components;
            var snapshot = new Component[components.Count];
            for (int i = 0; i < components.Count; i++) snapshot[i] = components[i];

            foreach (var component in snapshot)
            {
                if (component is not MonoBehaviour mb || mb.IsDestroyed) continue;
                if (enter) mb.RunTriggerEnter(other);
                else mb.RunTriggerExit(other);
            }
        }

        // ── Spatial queries ──────────────────────────────────────────

        /// <summary>
        /// Sphere query over the registered colliders — backs
        /// <see cref="Physics.OverlapSphereNonAlloc"/>. Results fill in registration
        /// order (deterministic), truncated at the buffer's capacity, exactly like the
        /// original engine's non-alloc contract. Includes trigger AND non-trigger
        /// colliders; inactive/disabled colliders are skipped.
        /// </summary>
        internal int OverlapSphereNonAlloc(Vector3 position, float radius, Collider[] results)
            => OverlapSphereNonAlloc(position, radius, results, ~0, QueryTriggerInteraction.Collide);

        /// <summary>
        /// Layer-masked non-alloc sphere query — backs
        /// <see cref="Physics.OverlapSphereNonAlloc(Vector3, float, Collider[], int)"/>.
        /// Same contract as the unmasked variant plus the original engine's layer
        /// filter: a collider qualifies when the bit for its GameObject's layer is
        /// set in <paramref name="layerMask"/>.
        /// </summary>
        internal int OverlapSphereNonAlloc(Vector3 position, float radius, Collider[] results, int layerMask)
            => OverlapSphereNonAlloc(position, radius, results, layerMask, QueryTriggerInteraction.Collide);

        /// <summary>
        /// Allocating sphere query with layer filtering — backs
        /// <see cref="Physics.OverlapSphere(Vector3, float, int)"/>. Same contract as
        /// the non-alloc variant (registration order, trigger AND non-trigger,
        /// inactive skipped) plus the original engine's layer-mask filter.
        /// </summary>
        internal Collider[] OverlapSphere(Vector3 position, float radius, int layerMask)
        {
            var buffer = new Collider[Math.Max(16, _enabled.Count)];
            int n = OverlapSphereNonAlloc(position, radius, buffer, layerMask, QueryTriggerInteraction.Collide);
            Array.Resize(ref buffer, n);
            return buffer;
        }

        /// <summary>
        /// Box occupancy query — backs <see cref="Physics.CheckBox"/>. True when any
        /// collider in the query scene overlaps the box. Like the trigger pass's box
        /// handling, the probe is a world-space AABB (orientation ignored — phase-2
        /// deviation, see class doc).
        /// </summary>
        internal bool CheckBox(Vector3 center, Vector3 halfExtents)
            => OverlapBox(center, halfExtents, ~0, QueryTriggerInteraction.Collide).Length > 0;

        static bool BoxOverlapsCollider(Vector3 center, Vector3 extents, Collider collider)
        {
            switch (collider)
            {
                case SphereCollider s:
                {
                    Vector3 sphereCenter = s.transform.TransformPoint(s.center);
                    float radius = WorldRadius(s);
                    var closest = new Vector3(
                        Mathf.Clamp(sphereCenter.x, center.x - extents.x, center.x + extents.x),
                        Mathf.Clamp(sphereCenter.y, center.y - extents.y, center.y + extents.y),
                        Mathf.Clamp(sphereCenter.z, center.z - extents.z, center.z + extents.z));
                    return (sphereCenter - closest).sqrMagnitude <= radius * radius;
                }
                case BoxCollider box:
                {
                    var (boxCenter, ext) = BoxBounds(box);
                    return Mathf.Abs(center.x - boxCenter.x) <= extents.x + ext.x
                        && Mathf.Abs(center.y - boxCenter.y) <= extents.y + ext.y
                        && Mathf.Abs(center.z - boxCenter.z) <= extents.z + ext.z;
                }
                case MeshCollider mesh:
                {
                    if (!TryMeshBounds(mesh, out var meshCenter, out var ext)) return false;
                    return Mathf.Abs(center.x - meshCenter.x) <= extents.x + ext.x
                        && Mathf.Abs(center.y - meshCenter.y) <= extents.y + ext.y
                        && Mathf.Abs(center.z - meshCenter.z) <= extents.z + ext.z;
                }
                default:
                    return false;
            }
        }

        static bool SphereOverlapsCollider(Vector3 center, float radius, Collider collider)
        {
            switch (collider)
            {
                case SphereCollider s:
                {
                    Vector3 otherCenter = s.transform.TransformPoint(s.center);
                    float radii = radius + WorldRadius(s);
                    return (center - otherCenter).sqrMagnitude <= radii * radii;
                }
                case BoxCollider box:
                {
                    var (boxCenter, ext) = BoxBounds(box);
                    var closest = new Vector3(
                        Mathf.Clamp(center.x, boxCenter.x - ext.x, boxCenter.x + ext.x),
                        Mathf.Clamp(center.y, boxCenter.y - ext.y, boxCenter.y + ext.y),
                        Mathf.Clamp(center.z, boxCenter.z - ext.z, boxCenter.z + ext.z));
                    return (center - closest).sqrMagnitude <= radius * radius;
                }
                case MeshCollider mesh:
                {
                    if (!TryMeshBounds(mesh, out var meshCenter, out var ext)) return false;
                    var closest = new Vector3(
                        Mathf.Clamp(center.x, meshCenter.x - ext.x, meshCenter.x + ext.x),
                        Mathf.Clamp(center.y, meshCenter.y - ext.y, meshCenter.y + ext.y),
                        Mathf.Clamp(center.z, meshCenter.z - ext.z, meshCenter.z + ext.z));
                    return (center - closest).sqrMagnitude <= radius * radius;
                }
                default:
                    return false;
            }
        }

        // ── Overlap math ─────────────────────────────────────────────
        //
        // MeshColliders participate as their mesh-bounds AABB (see TryMeshBounds) — the
        // same rotation-ignored world-AABB convention boxes use. Null-mesh colliders
        // never overlap. Full mesh collision arrives with the physics phase.

        static bool Overlaps(Collider a, Collider b) => (a, b) switch
        {
            (SphereCollider sa, SphereCollider sb) => SphereSphere(sa, sb),
            (SphereCollider s, BoxCollider box) => SphereBox(s, box),
            (BoxCollider box, SphereCollider s) => SphereBox(s, box),
            (BoxCollider ba, BoxCollider bb) => BoxBox(ba, bb),
            (MeshCollider m, _) => MeshOverlaps(m, b),
            (_, MeshCollider m) => MeshOverlaps(m, a),
            _ => false,
        };

        /// <summary>MeshCollider (as its mesh-bounds AABB) vs. any other supported shape.</summary>
        static bool MeshOverlaps(MeshCollider mesh, Collider other)
        {
            if (!TryMeshBounds(mesh, out var center, out var extents)) return false;
            switch (other)
            {
                case SphereCollider s:
                {
                    Vector3 sphereCenter = s.transform.TransformPoint(s.center);
                    float radius = WorldRadius(s);
                    var closest = new Vector3(
                        Mathf.Clamp(sphereCenter.x, center.x - extents.x, center.x + extents.x),
                        Mathf.Clamp(sphereCenter.y, center.y - extents.y, center.y + extents.y),
                        Mathf.Clamp(sphereCenter.z, center.z - extents.z, center.z + extents.z));
                    return (sphereCenter - closest).sqrMagnitude <= radius * radius;
                }
                case BoxCollider box:
                {
                    var (boxCenter, ext) = BoxBounds(box);
                    return AabbAabb(center, extents, boxCenter, ext);
                }
                case MeshCollider otherMesh:
                {
                    if (!TryMeshBounds(otherMesh, out var otherCenter, out var otherExtents)) return false;
                    return AabbAabb(center, extents, otherCenter, otherExtents);
                }
                default:
                    return false;
            }
        }

        static bool AabbAabb(Vector3 centerA, Vector3 extA, Vector3 centerB, Vector3 extB)
            => Mathf.Abs(centerA.x - centerB.x) <= extA.x + extB.x
            && Mathf.Abs(centerA.y - centerB.y) <= extA.y + extB.y
            && Mathf.Abs(centerA.z - centerB.z) <= extA.z + extB.z;

        /// <summary>
        /// World AABB of a mesh collider: mesh local-bounds center transformed through the
        /// hierarchy, extents scaled by |lossyScale|; rotation ignored (phase-2 deviation,
        /// see class doc). False when no live mesh is assigned.
        /// </summary>
        static bool TryMeshBounds(MeshCollider mesh, out Vector3 center, out Vector3 extents)
        {
            var shared = mesh.sharedMesh;
            if (shared is null || shared.IsDestroyed)
            {
                center = default;
                extents = default;
                return false;
            }

            Bounds local = shared.bounds;
            Vector3 s = mesh.transform.lossyScale;
            center = mesh.transform.TransformPoint(local.center);
            extents = new Vector3(
                Mathf.Abs(local.extents.x * s.x),
                Mathf.Abs(local.extents.y * s.y),
                Mathf.Abs(local.extents.z * s.z));
            return true;
        }

        static bool SphereSphere(SphereCollider a, SphereCollider b)
        {
            Vector3 centerA = a.transform.TransformPoint(a.center);
            Vector3 centerB = b.transform.TransformPoint(b.center);
            float radii = WorldRadius(a) + WorldRadius(b);
            return (centerA - centerB).sqrMagnitude <= radii * radii;
        }

        static bool SphereBox(SphereCollider sphere, BoxCollider box)
        {
            Vector3 center = sphere.transform.TransformPoint(sphere.center);
            float radius = WorldRadius(sphere);
            var (boxCenter, ext) = BoxBounds(box);

            // Closest point on the AABB to the sphere center.
            var closest = new Vector3(
                Mathf.Clamp(center.x, boxCenter.x - ext.x, boxCenter.x + ext.x),
                Mathf.Clamp(center.y, boxCenter.y - ext.y, boxCenter.y + ext.y),
                Mathf.Clamp(center.z, boxCenter.z - ext.z, boxCenter.z + ext.z));
            return (center - closest).sqrMagnitude <= radius * radius;
        }

        static bool BoxBox(BoxCollider a, BoxCollider b)
        {
            var (centerA, extA) = BoxBounds(a);
            var (centerB, extB) = BoxBounds(b);
            return Mathf.Abs(centerA.x - centerB.x) <= extA.x + extB.x
                && Mathf.Abs(centerA.y - centerB.y) <= extA.y + extB.y
                && Mathf.Abs(centerA.z - centerB.z) <= extA.z + extB.z;
        }

        /// <summary>Original-engine sphere scaling: radius × max |lossyScale| component.</summary>
        static float WorldRadius(SphereCollider sphere)
        {
            Vector3 s = sphere.transform.lossyScale;
            return sphere.radius * Mathf.Max(Mathf.Abs(s.x), Mathf.Max(Mathf.Abs(s.y), Mathf.Abs(s.z)));
        }

        /// <summary>World AABB of a box collider, rotation ignored (phase-2 deviation, see class doc).</summary>
        static (Vector3 center, Vector3 extents) BoxBounds(BoxCollider box)
        {
            Vector3 s = box.transform.lossyScale;
            var extents = new Vector3(
                Mathf.Abs(box.size.x * s.x),
                Mathf.Abs(box.size.y * s.y),
                Mathf.Abs(box.size.z * s.z)) * 0.5f;
            return (box.transform.TransformPoint(box.center), extents);
        }
    }
}
