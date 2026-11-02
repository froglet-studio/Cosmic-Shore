using System.Collections.Generic;
using Unity.Profiling;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// A tube of <see cref="Radius"/> round a polyline (a race course's gate-to-gate path) that a cell's fauna keep out
    /// of: no creature is born in it, every goal is pushed out of it, and mass inside it is not prey. The same contract
    /// as the cell's other pens (<see cref="Cell.FaunaExclusionRadius"/>, Docs/ECOSYSTEM.md section 22): a spatial DIET +
    /// STEERING rule, never a wall - nothing is teleported or culled. Switchback sets one so the path stays free for the
    /// prism obstacles and the fauna's body sync and animators stop costing frame time where the pilots fly
    /// (Docs/perf/SWITCHBACK_PRISM_BUDGET.md). Pure geometry, so EditMode tests cover it without a scene.
    /// </summary>
    public sealed class FaunaCorridor
    {
        static readonly ProfilerMarker s_pushMarker = new("Fauna.Corridor.PushOut");

        readonly List<Vector3> _points = new(32);
        Vector3 _boundsMin, _boundsMax;

        public float Radius { get; private set; }

        /// <summary>How far past the wall <see cref="PushOut"/> parks a point (0 = on the wall). A goal parked ON the wall is
        /// crossed by every creature that overshoots it between steering ticks, which is how the 2026-11-02 PC playtest
        /// found light fauna 139-190 u inside a 220 u corridor; a standoff keeps the overshoot outside.</summary>
        public float GoalStandoff { get; private set; }
        public int PointCount => _points.Count;
        public bool IsActive => Radius > 0f && _points.Count > 0;

        public void Set(IReadOnlyList<Vector3> path, float radius, bool closed, float goalStandoff = 0f)
        {
            _points.Clear();
            Radius = Mathf.Max(0f, radius);
            GoalStandoff = Mathf.Max(0f, goalStandoff);
            if (path == null || path.Count == 0 || Radius <= 0f) { Radius = 0f; return; }
            for (int i = 0; i < path.Count; i++) _points.Add(path[i]);
            if (closed && path.Count > 2) _points.Add(path[0]);
            _boundsMin = _boundsMax = _points[0];
            for (int i = 1; i < _points.Count; i++)
            {
                _boundsMin = Vector3.Min(_boundsMin, _points[i]);
                _boundsMax = Vector3.Max(_boundsMax, _points[i]);
            }
            var pad = Vector3.one * Radius;
            _boundsMin -= pad;
            _boundsMax += pad;
        }

        public void Clear()
        {
            _points.Clear();
            Radius = 0f;
            GoalStandoff = 0f;
        }

        bool InBounds(Vector3 p) =>
            p.x >= _boundsMin.x && p.y >= _boundsMin.y && p.z >= _boundsMin.z &&
            p.x <= _boundsMax.x && p.y <= _boundsMax.y && p.z <= _boundsMax.z;

        /// <summary>The closest point of the path to <paramref name="p"/>, and its squared distance.</summary>
        public float ClosestSqr(Vector3 p, out Vector3 closest)
        {
            closest = _points.Count > 0 ? _points[0] : p;
            float best = (p - closest).sqrMagnitude;
            for (int i = 0; i + 1 < _points.Count; i++)
            {
                Vector3 a = _points[i], ab = _points[i + 1] - a;
                float len2 = ab.sqrMagnitude;
                float t = len2 > 1e-6f ? Mathf.Clamp01(Vector3.Dot(p - a, ab) / len2) : 0f;
                Vector3 c = a + ab * t;
                float d = (p - c).sqrMagnitude;
                if (d < best) { best = d; closest = c; }
            }
            return best;
        }

        public bool Contains(Vector3 p)
        {
            if (!IsActive || !InBounds(p)) return false;
            return ClosestSqr(p, out _) < Radius * Radius;
        }

        /// <summary>
        /// <paramref name="p"/> unchanged when it is outside the tube; otherwise pushed radially out to the tube's wall
        /// (a hair past it), plus <see cref="GoalStandoff"/>. A point ON the path has no radial, so <paramref name="selfPosition"/> (the creature) gives the
        /// side, else a fixed perpendicular. Up to four passes, for a point pushed out of one leg into a neighbour.
        /// </summary>
        public Vector3 PushOut(Vector3 p, Vector3? selfPosition = null)
        {
            if (!Contains(p)) return p;
            using (s_pushMarker.Auto())
            {
                float wall = Radius * 1.001f + 0.01f + GoalStandoff;
                for (int pass = 0; pass < 4; pass++)
                {
                    float d2 = ClosestSqr(p, out var c);
                    if (d2 >= Radius * Radius) return p;
                    Vector3 off = p - c;
                    if (off.sqrMagnitude < 1e-6f)
                    {
                        off = selfPosition.HasValue ? selfPosition.Value - c : Vector3.zero;
                        if (off.sqrMagnitude < 1e-6f) off = Vector3.up;
                    }
                    p = c + off.normalized * wall;
                }
                return p;
            }
        }
    }
}
