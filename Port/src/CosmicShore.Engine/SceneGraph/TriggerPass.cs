using System;
using System.Collections.Generic;

namespace CosmicShore.Engine
{
    /// <summary>
    /// Phase-2 minimal trigger physics: per-frame overlap detection between registered
    /// colliders with OnTriggerEnter/OnTriggerExit dispatch on enter/exit transitions.
    ///
    /// Timing — the pass runs once per <see cref="GameLoop.Tick"/>, immediately AFTER the
    /// Update phase (and before coroutines/scheduler/LateUpdate). The original engine
    /// delivers contacts during its fixed-step physics phase before Update; this port runs
    /// headless at a fixed Tick delta, so a per-frame pass after Update gives the same
    /// fixed-cadence contact stream while seeing the transforms gameplay code (AIPilot →
    /// VesselTransformer) wrote this frame — contacts land on the frame the overlap is
    /// first visible, not one frame late.
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
    ///   • OnTriggerStay is NOT dispatched (out of phase-2 scope).
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
    public sealed class TriggerPass
    {
        readonly List<Collider> _colliders = new();                    // registration order
        readonly List<(Collider a, Collider b)> _activePairs = new();  // established order
        readonly HashSet<(Collider a, Collider b)> _activeSet = new();

        // Per-frame scratch (reused to avoid steady-state allocation).
        readonly List<Collider> _live = new();
        readonly List<(Collider a, Collider b)> _discovered = new();
        readonly HashSet<(Collider a, Collider b)> _current = new();

        internal void Register(Collider collider)
        {
            if (!_colliders.Contains(collider)) _colliders.Add(collider);
        }

        internal void Unregister(Collider collider) => _colliders.Remove(collider);

        internal void RunFrame()
        {
            if (_colliders.Count == 0 && _activePairs.Count == 0) return;

            // 1. Snapshot live participants in registration order. Colliders added by
            //    callbacks during this pass join next frame.
            _live.Clear();
            foreach (var collider in _colliders)
                if (collider.isActiveAndEnabled)
                    _live.Add(collider);

            // 2. Current overlap set — (i, j), i < j, at least one trigger.
            _discovered.Clear();
            _current.Clear();
            for (int i = 0; i < _live.Count; i++)
            {
                var a = _live[i];
                for (int j = i + 1; j < _live.Count; j++)
                {
                    var b = _live[j];
                    if (!a.isTrigger && !b.isTrigger) continue;
                    if (ReferenceEquals(a.gameObject, b.gameObject)) continue;
                    if (!Overlaps(a, b)) continue;

                    var pair = (a, b);
                    _current.Add(pair);
                    _discovered.Add(pair);
                }
            }

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
        }

        // ── Dispatch ─────────────────────────────────────────────────

        static void Dispatch(Collider a, Collider b, bool enter)
        {
            DispatchTo(a, b, enter);
            DispatchTo(b, a, enter);
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

        // ── Overlap math ─────────────────────────────────────────────

        static bool Overlaps(Collider a, Collider b) => (a, b) switch
        {
            (SphereCollider sa, SphereCollider sb) => SphereSphere(sa, sb),
            (SphereCollider s, BoxCollider box) => SphereBox(s, box),
            (BoxCollider box, SphereCollider s) => SphereBox(s, box),
            (BoxCollider ba, BoxCollider bb) => BoxBox(ba, bb),
            _ => false,
        };

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
