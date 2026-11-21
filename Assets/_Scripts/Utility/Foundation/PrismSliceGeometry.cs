using UnityEngine;

namespace CosmicShore.Utility
{
    /// <summary>
    /// The CPU half of the Rhino sword's slice (Docs/PRISM_ANIMATION.md §4.10): turns "this
    /// prism was cut by this plane at this speed" into the two halves' stamps. Pure — no scene,
    /// no ECS, no Unity lifetime — so every invariant the shader relies on is asserted by an
    /// edit-mode test against exactly the numbers the game stamps (<c>PrismSliceGeometryTests</c>).
    ///
    /// The stamps and the promise each one keeps:
    /// <list type="bullet">
    /// <item><b>Plane</b> — the cut in the half's OBJECT space as (m, d) with m = Mᵀ·n and
    /// d = n·(Q − t), deliberately unnormalised, so <c>dot(m, x) − d</c> is the WORLD signed
    /// distance under any non-uniform scale. The half keeps <c>dot(m, x) ≤ d</c>.</item>
    /// <item><b>Centre</b> — the central projection's centre: the mean of the half's polytope
    /// vertices (its kept cube corners plus the cut's edge crossings). The mean of all the
    /// vertices of a full-dimensional convex polytope is strictly INSIDE it, which is the whole
    /// precondition for the projection being a bijection onto the cut face. w = the half's
    /// deepest point from the cut, world units — the dissolve's full depth.</item>
    /// <item><b>Pivot</b> — the hinge the half opens about: its most-TRAILING point along the
    /// blade's travel, dropped onto the cut. Every point of the half is then at or ahead of the
    /// hinge, so an opening of up to 90° can only carry points AWAY from the other half — the
    /// no-interpenetration guarantee. w = how far the half parts.</item>
    /// <item><b>Axis</b> — cross(outward normal, travel): in the cut plane, perpendicular to the
    /// travel, signed so a positive angle opens the half away from its twin. w = the angle.</item>
    /// <item><b>Drift</b> — the share of the impact velocity both halves are carried with.</item>
    /// <item><b>Bounds</b> — an object-space AABB covering every pose the half takes, because the
    /// entity's matrix never moves and Entities Graphics culls by it.</item>
    /// </list>
    /// </summary>
    public static class PrismSliceGeometry
    {
        /// <summary>The numbers a slice needs that are not about this particular prism.</summary>
        public struct Settings
        {
            public float MaxCutOffsetFraction;
            public float SeparationFraction;
            public float MinSeparation;
            public float OpenAngleMin;          // radians
            public float OpenAngleMax;          // radians
            public float OpenAngleFullSpeed;
            public float DriftFraction;
            public float MaxDriftSpeed;
            public float DriftDragSeconds;
        }

        /// <summary>One half's stamps (see the class summary).</summary>
        public struct Half
        {
            public Vector4 Plane;
            public Vector4 Centre;
            public Vector4 Pivot;
            public Vector4 Axis;
            public Vector4 Drift;
            public Vector3 BoundsCenter;
            public Vector3 BoundsExtents;
        }

        /// <summary>Object-space half-extent of both prism mesh families.</summary>
        const float H = 0.5f;

        static readonly Vector3[] s_corners =
        {
            new(-H, -H, -H), new(H, -H, -H), new(-H, H, -H), new(H, H, -H),
            new(-H, -H, H), new(H, -H, H), new(-H, H, H), new(H, H, H),
        };

        // The cube's twelve edges as corner-index pairs (corners differ in exactly one bit).
        static readonly int[] s_edges =
        {
            0, 1, 2, 3, 4, 5, 6, 7,     // along x
            0, 2, 1, 3, 4, 6, 5, 7,     // along y
            0, 4, 1, 5, 2, 6, 3, 7,     // along z
        };

        // Scratch — main-thread only, like every caller.
        static readonly Vector3[] s_points = new Vector3[8 + 12];

        /// <summary>
        /// Builds both halves. False when the cut is degenerate (a zero normal, a zero-thickness
        /// prism, or a half with no volume) — the caller then explodes the prism instead.
        /// Half A is the side the normal points AWAY from (its outward normal is +n); half B is
        /// the side the normal points into.
        /// </summary>
        public static bool TryBuild(Vector3 position, Quaternion rotation, Vector3 scale,
            Vector3 cutPoint, Vector3 cutNormal, Vector3 velocity, in Settings settings,
            out Half a, out Half b)
        {
            a = default;
            b = default;

            float nLen = cutNormal.magnitude;
            if (!(nLen > 1e-6f) || float.IsNaN(nLen) || float.IsInfinity(nLen)) return false;
            Vector3 n = cutNormal / nLen;

            Vector3 ax = rotation * Vector3.right;
            Vector3 ay = rotation * Vector3.up;
            Vector3 az = rotation * Vector3.forward;
            Vector3 s = new(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
            if (!(s.x > 1e-6f && s.y > 1e-6f && s.z > 1e-6f)) return false;

            // The prism's half-thickness ACROSS the cut: the support of the box along n.
            float halfThickness = H * (Mathf.Abs(Vector3.Dot(n, ax)) * s.x +
                                       Mathf.Abs(Vector3.Dot(n, ay)) * s.y +
                                       Mathf.Abs(Vector3.Dot(n, az)) * s.z);
            if (!(halfThickness > 1e-5f)) return false;

            // Keep the blade's own plane, but never let a grazing tip shave a sliver off a
            // corner: the prism's centre may sit at most this far from the cut.
            float centreOffset = Vector3.Dot(n, position - cutPoint);
            float maxOffset = settings.MaxCutOffsetFraction * halfThickness;
            centreOffset = Mathf.Clamp(centreOffset, -maxOffset, maxOffset);
            Vector3 q = position - n * centreOffset;           // a point on the (clamped) cut

            // Object-space plane, unnormalised: m = Mᵀ·n, d = n·(Q − t). The SIGNED scale, to
            // agree with the TRS the entity renders with.
            Vector3 m = new(Vector3.Dot(ax, n) * scale.x, Vector3.Dot(ay, n) * scale.y, Vector3.Dot(az, n) * scale.z);
            float d = Vector3.Dot(n, q - position);

            // The blade's travel, in the cut plane. The plane CONTAINS the swing, so this is the
            // velocity itself up to float noise; a stab (velocity along n) falls back to a stable
            // perpendicular.
            Vector3 travel = velocity - n * Vector3.Dot(n, velocity);
            if (travel.sqrMagnitude < 1e-8f)
            {
                travel = Vector3.Cross(n, ay);
                if (travel.sqrMagnitude < 1e-8f) travel = Vector3.Cross(n, ax);
            }
            travel.Normalize();

            float speed = velocity.magnitude;
            float angle = Mathf.Lerp(settings.OpenAngleMin, settings.OpenAngleMax,
                Mathf.Clamp01(speed / Mathf.Max(settings.OpenAngleFullSpeed, 1e-3f)));
            angle = Mathf.Clamp(angle, 0f, Mathf.PI * 0.5f);
            float separation = Mathf.Max(settings.MinSeparation, settings.SeparationFraction * halfThickness);
            Vector3 drift = speed > 1e-6f
                ? velocity * Mathf.Min(settings.DriftFraction, settings.MaxDriftSpeed / speed)
                : Vector3.zero;

            var model = Matrix4x4.TRS(position, rotation, scale);
            var inverse = model.inverse;

            return TryBuildHalf(model, inverse, m, d, n, q, travel, angle, separation, drift,
                                settings.DriftDragSeconds, out a)
                && TryBuildHalf(model, inverse, -m, -d, -n, q, travel, angle, separation, drift,
                                settings.DriftDragSeconds, out b);
        }

        /// <summary>One half, keeping dot(m, x) ≤ d in object space; <paramref name="outward"/> is
        /// the unit world normal pointing out of it (toward its twin).</summary>
        static bool TryBuildHalf(in Matrix4x4 model, in Matrix4x4 inverse, Vector3 m, float d,
            Vector3 outward, Vector3 cutPoint, Vector3 travel, float angle, float separation,
            Vector3 drift, float dragSeconds, out Half half)
        {
            half = default;

            // The half's polytope vertices: the kept corners, plus every edge's crossing.
            int count = 0;
            for (int i = 0; i < 8; i++)
                if (Vector3.Dot(m, s_corners[i]) - d <= 0f)
                    s_points[count++] = s_corners[i];
            for (int e = 0; e < 12; e++)
            {
                Vector3 p0 = s_corners[s_edges[2 * e]];
                Vector3 p1 = s_corners[s_edges[2 * e + 1]];
                float d0 = Vector3.Dot(m, p0) - d;
                float d1 = Vector3.Dot(m, p1) - d;
                if ((d0 < 0f && d1 > 0f) || (d0 > 0f && d1 < 0f))
                    s_points[count++] = p0 + (p1 - p0) * (d0 / (d0 - d1));
            }
            if (count < 4) return false;

            Vector3 centre = Vector3.zero;
            float deepest = 0f;
            float trailing = float.PositiveInfinity;
            Vector3 trailingPoint = Vector3.zero;
            for (int i = 0; i < count; i++)
            {
                Vector3 p = s_points[i];
                centre += p;
                deepest = Mathf.Max(deepest, d - Vector3.Dot(m, p));   // world units
                Vector3 w = model.MultiplyPoint3x4(p);
                float along = Vector3.Dot(travel, w);
                if (along < trailing)
                {
                    trailing = along;
                    trailingPoint = w;
                }
            }
            centre /= count;

            // The projection's precondition, checked rather than assumed: C strictly inside.
            if (!(Vector3.Dot(m, centre) - d < -1e-5f) || !(deepest > 1e-5f)) return false;

            // The hinge: the trailing point dropped onto the cut. T ⊥ n, so this keeps its
            // travel coordinate — every point of the half stays at or ahead of it.
            Vector3 pivot = trailingPoint - outward * Vector3.Dot(outward, trailingPoint - cutPoint);
            Vector3 axis = Vector3.Cross(outward, travel);
            if (axis.sqrMagnitude < 1e-10f) return false;
            axis.Normalize();

            // The culling envelope: the half lives inside a sphere about the hinge (rotation never
            // leaves it), carried along two monotone translations — the parting and the drift.
            float radius = 0f;
            for (int i = 0; i < count; i++)
                radius = Mathf.Max(radius, (model.MultiplyPoint3x4(s_points[i]) - pivot).magnitude);
            Vector3 part = -outward * separation;
            Vector3 carry = drift * dragSeconds;
            Vector3 lo = pivot, hi = pivot;
            Encapsulate(ref lo, ref hi, pivot + part);
            Encapsulate(ref lo, ref hi, pivot + carry);
            Encapsulate(ref lo, ref hi, pivot + part + carry);
            lo -= Vector3.one * radius;
            hi += Vector3.one * radius;

            Vector3 oLo = Vector3.positiveInfinity, oHi = Vector3.negativeInfinity;
            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = new((i & 1) != 0 ? hi.x : lo.x, (i & 2) != 0 ? hi.y : lo.y, (i & 4) != 0 ? hi.z : lo.z);
                Vector3 o = inverse.MultiplyPoint3x4(corner);
                oLo = Vector3.Min(oLo, o);
                oHi = Vector3.Max(oHi, o);
            }

            half = new Half
            {
                Plane = new Vector4(m.x, m.y, m.z, d),
                Centre = new Vector4(centre.x, centre.y, centre.z, deepest),
                Pivot = new Vector4(pivot.x, pivot.y, pivot.z, separation),
                Axis = new Vector4(axis.x, axis.y, axis.z, angle),
                Drift = new Vector4(drift.x, drift.y, drift.z, 0f),
                BoundsCenter = (oLo + oHi) * 0.5f,
                BoundsExtents = (oHi - oLo) * 0.5f,
            };
            return true;
        }

        static void Encapsulate(ref Vector3 lo, ref Vector3 hi, Vector3 p)
        {
            lo = Vector3.Min(lo, p);
            hi = Vector3.Max(hi, p);
        }
    }
}
