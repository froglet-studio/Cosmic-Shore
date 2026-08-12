using System.Collections.Generic;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The court geometry the Astro League ball bounces off of.
    ///
    /// The original arena was a SPHERE: the ball reflected about the sphere's radial normal, and
    /// every surface normal on a sphere points at the center - so every bounce carried the ball back
    /// toward the middle (a whispering-gallery focusing effect), never a tangential "bank". Billiards,
    /// air hockey and Rocket League are fun *because* of FLAT walls: a flat plane preserves the
    /// wall-parallel velocity component and only flips the perpendicular one, so a glancing shot banks
    /// along the boards instead of being thrown back at center.
    ///
    /// Every ricochet-friendly polytope (box, octagonal/hexagonal prism, beveled box, octahedron) is
    /// a CONVEX POLYTOPE - an intersection of flat half-spaces, i.e. a list of inward-facing face
    /// planes. So one generic bounce (<see cref="Contain"/>) handles all of them: for the ball center,
    /// reflect off every plane it has poked through and clamp it to kiss the wall. Sphere and Cylinder
    /// keep an analytic curved branch (Sphere is the legacy baseline; Cylinder has flat goal caps + a
    /// curved barrel). <see cref="NotchedRing"/> layers a central torus OBSTACLE (the ball bounces off
    /// its OUTSIDE) inside a chosen outer court, with an angular notch - a center choke point that adds
    /// bounces while leaving the central hole + the notch open as shooting lanes. The same geometry
    /// drives the visual: <see cref="BuildVisualMesh"/> builds the outer hull + the notched torus, so
    /// the nucleus "cage" silhouette is the wall the ball actually hits.
    /// </summary>
    public enum AstroLeagueBoundaryShape
    {
        /// <summary>Legacy curved boundary - radial reflect, focuses toward center. Kept as the A/B baseline.</summary>
        Sphere = 0,
        /// <summary>Rectangular court - pure pool/air-hockey banking. Flat everything, 90° corners.</summary>
        Box = 1,
        /// <summary>Box with its 4 goal-axis edges chamfered → octagon cross-section. "Cage" arena, 135° corners, flat goal caps.</summary>
        OctagonalPrism = 2,
        /// <summary>Elongated hexagon cross-section extruded along the goal axis. Tighter 6-wall arena, flat goal caps.</summary>
        HexagonalPrism = 3,
        /// <summary>Box with every edge + corner chamfered - Rocket-League-style flat faces + angled ramps that redirect instead of trap.</summary>
        BeveledBox = 4,
        /// <summary>Diamond - 8 angled faces. Every wall banks, very different feel.</summary>
        Octahedron = 5,
        /// <summary>Flat goal caps + a curved barrel along the goal axis. Banks lengthwise, focuses across.</summary>
        Cylinder = 6,
        /// <summary>An outer court (default Cylinder) with a central NOTCHED TORUS ring obstacle - a choke
        /// point in the center that adds bounces; the central hole + the notch stay open as shooting lanes.</summary>
        NotchedRing = 7,
    }

    /// <summary>
    /// Immutable-after-construction court boundary for one intensity. Built on every peer by
    /// <see cref="AstroLeagueArena.Build"/> at the scaled arena dimensions; the ball contains itself
    /// against it each server tick (<see cref="AstroLeagueBall.ContainWithinBoundary"/>).
    /// </summary>
    public sealed class AstroLeagueBoundary
    {
        /// <summary>One court wall: <c>dot(x - Center, Normal) == Offset</c> is the wall plane; interior is &lt; Offset.</summary>
        readonly struct FacePlane
        {
            public readonly Vector3 Normal;  // unit, points OUTWARD
            public readonly float Offset;     // distance from center to the wall along Normal
            public FacePlane(Vector3 normal, float offset) { Normal = normal; Offset = offset; }
        }

        public AstroLeagueBoundaryShape Shape { get; }
        public Vector3 Center { get; }

        // Half-extents along the court axes: X = width/2, Y = height/2, Z = length/2 (the goal axis).
        public Vector3 HalfExtents { get; }

        // Curved-shape parameters (Sphere/Cylinder only).
        readonly float _sphereRadius;
        readonly float _cylinderRadius; // barrel radius in the XY cross-section
        readonly float _capHalfLength;  // ±Z cap distance for the cylinder

        // Effective OUTER shape the ball is contained within - equals Shape, except for NotchedRing,
        // whose outer is a separate court shape (default Cylinder) wrapped around the central ring.
        readonly AstroLeagueBoundaryShape _outerShape;

        // Central torus ring obstacle (NotchedRing only): axis = Z (the goal axis), so the ring lies in
        // the mid-plane and the ball can shoot straight down the center hole or through the angular notch.
        readonly bool _hasRing;
        readonly float _ringMajor;          // ring (major) radius - distance from center to the tube centerline
        readonly float _ringTube;           // tube (minor) radius - half the ring thickness
        readonly float _notchCenter;        // notch center angle (radians, atan2(y,x))
        readonly float _notchHalfWidth;     // notch half-width (radians); 0 = solid ring, no gap

        readonly List<FacePlane> _planes = new();

        /// <summary>
        /// Largest distance from center to the boundary surface - used to size the cell sense radius
        /// margin and gizmos. For polytopes this is the farthest vertex; for curved shapes the radius.
        /// </summary>
        public float MaxExtent { get; private set; }

        /// <param name="shape">Court geometry.</param>
        /// <param name="center">World center of the court.</param>
        /// <param name="halfExtents">(width/2, height/2, length/2) at the current intensity scale. Length is the goal (Z) axis.</param>
        /// <param name="sphereRadius">Radius for <see cref="AstroLeagueBoundaryShape.Sphere"/> (already intensity-scaled).</param>
        /// <param name="octagonBevelFraction">0..1: how far the octagon/beveled chamfers cut from face toward corner. ~0.5 reads as a clean octagon.</param>
        /// <param name="beveledBoxBevelFraction">0..1: chamfer depth for <see cref="AstroLeagueBoundaryShape.BeveledBox"/>.</param>
        /// <param name="ringOuterShape">For <see cref="AstroLeagueBoundaryShape.NotchedRing"/>: the outer court the ring sits inside (default Cylinder).</param>
        /// <param name="ringMajorFraction">0..1: ring radius as a fraction of the cross-section radius (min(width,height)/2). The central hole = (major − tube) must clear the ball.</param>
        /// <param name="ringTubeFraction">0..1: ring thickness as a fraction of the cross-section radius.</param>
        /// <param name="notchCenterRadians">Angle (atan2(y,x)) of the notch center - the gap in the ring.</param>
        /// <param name="notchHalfWidthRadians">Half-angle of the notch gap; 0 = a solid ring (no gap).</param>
        public AstroLeagueBoundary(
            AstroLeagueBoundaryShape shape,
            Vector3 center,
            Vector3 halfExtents,
            float sphereRadius,
            float octagonBevelFraction = 0.5f,
            float beveledBoxBevelFraction = 0.45f,
            AstroLeagueBoundaryShape ringOuterShape = AstroLeagueBoundaryShape.Cylinder,
            float ringMajorFraction = 0.5f,
            float ringTubeFraction = 0.18f,
            float notchCenterRadians = 0f,
            float notchHalfWidthRadians = 0.5f)
        {
            Shape = shape;
            Center = center;
            HalfExtents = new Vector3(
                Mathf.Max(0.01f, halfExtents.x),
                Mathf.Max(0.01f, halfExtents.y),
                Mathf.Max(0.01f, halfExtents.z));

            float hx = HalfExtents.x, hy = HalfExtents.y, hz = HalfExtents.z;
            _sphereRadius = Mathf.Max(0.01f, sphereRadius);
            _cylinderRadius = Mathf.Min(hx, hy);
            _capHalfLength = hz;

            // NotchedRing wraps a real outer court (never itself) around the central ring.
            _outerShape = shape == AstroLeagueBoundaryShape.NotchedRing
                ? (ringOuterShape == AstroLeagueBoundaryShape.NotchedRing ? AstroLeagueBoundaryShape.Cylinder : ringOuterShape)
                : shape;

            BuildOuterShape(_outerShape, hx, hy, hz, octagonBevelFraction, beveledBoxBevelFraction);

            if (shape == AstroLeagueBoundaryShape.NotchedRing)
            {
                float crossRadius = Mathf.Min(hx, hy);
                _ringMajor = Mathf.Clamp01(ringMajorFraction) * crossRadius;
                _ringTube = Mathf.Clamp01(ringTubeFraction) * crossRadius;
                _notchCenter = notchCenterRadians;
                _notchHalfWidth = Mathf.Max(0f, notchHalfWidthRadians);
                _hasRing = _ringMajor > 1e-3f && _ringTube > 1e-3f;
            }
        }

        /// <summary>Build the planes / analytic params + MaxExtent for one (outer) court shape.</summary>
        void BuildOuterShape(AstroLeagueBoundaryShape shape, float hx, float hy, float hz,
            float octagonBevelFraction, float beveledBoxBevelFraction)
        {
            switch (shape)
            {
                case AstroLeagueBoundaryShape.Sphere:
                    MaxExtent = _sphereRadius;
                    break;

                case AstroLeagueBoundaryShape.Cylinder:
                    AddCapPlanes(hz);
                    MaxExtent = Mathf.Sqrt(_cylinderRadius * _cylinderRadius + hz * hz);
                    break;

                case AstroLeagueBoundaryShape.Box:
                    BuildPrism(RectCrossSection(hx, hy), hz);
                    MaxExtent = HalfExtents.magnitude;
                    break;

                case AstroLeagueBoundaryShape.OctagonalPrism:
                    BuildPrism(OctagonCrossSection(hx, hy, octagonBevelFraction), hz);
                    MaxExtent = HalfExtents.magnitude;
                    break;

                case AstroLeagueBoundaryShape.HexagonalPrism:
                    BuildPrism(HexagonCrossSection(hx, hy), hz);
                    MaxExtent = HalfExtents.magnitude;
                    break;

                case AstroLeagueBoundaryShape.BeveledBox:
                    BuildBeveledBox(hx, hy, hz, beveledBoxBevelFraction);
                    MaxExtent = HalfExtents.magnitude;
                    break;

                case AstroLeagueBoundaryShape.Octahedron:
                    BuildOctahedron(hx, hy, hz);
                    MaxExtent = Mathf.Max(hx, Mathf.Max(hy, hz));
                    break;
            }
        }

        // ── Cross-section polygons (XY, centered at origin, CCW) ──────────────

        static List<Vector2> RectCrossSection(float hx, float hy) => new()
        {
            new Vector2(hx, hy), new Vector2(-hx, hy), new Vector2(-hx, -hy), new Vector2(hx, -hy)
        };

        /// <summary>Rectangle with its 4 corners chamfered → a (generally irregular) octagon that fits the hx×hy court.</summary>
        static List<Vector2> OctagonCrossSection(float hx, float hy, float bevelFraction)
        {
            // Chamfer depth: the cut planes sit between the farther face-center projection (don't eat a
            // face) and the corner projection (actually cut the corner). c is the inset along each axis.
            float f = Mathf.Clamp01(bevelFraction);
            float cx = Mathf.Lerp(0f, hx, f * 0.6f);
            float cy = Mathf.Lerp(0f, hy, f * 0.6f);
            return new List<Vector2>
            {
                new Vector2(hx, hy - cy), new Vector2(hx - cx, hy),     // +X/+Y corner chamfer
                new Vector2(-hx + cx, hy), new Vector2(-hx, hy - cy),   // -X/+Y
                new Vector2(-hx, -hy + cy), new Vector2(-hx + cx, -hy), // -X/-Y
                new Vector2(hx - cx, -hy), new Vector2(hx, -hy + cy),   // +X/-Y
            };
        }

        /// <summary>Elongated hexagon: flat top/bottom (±Y) spanning the middle, pointing to ±X.</summary>
        static List<Vector2> HexagonCrossSection(float hx, float hy)
        {
            float wx = hx * 0.5f; // half-width of the flat top/bottom edges
            return new List<Vector2>
            {
                new Vector2(hx, 0f), new Vector2(wx, hy), new Vector2(-wx, hy),
                new Vector2(-hx, 0f), new Vector2(-wx, -hy), new Vector2(wx, -hy),
            };
        }

        // ── Plane builders ───────────────────────────────────────────────────

        void AddCapPlanes(float hz)
        {
            _planes.Add(new FacePlane(Vector3.forward, hz));
            _planes.Add(new FacePlane(Vector3.back, hz));
        }

        /// <summary>Extrude a 2D cross-section polygon along Z into ±hz: one side plane per edge + two caps.</summary>
        void BuildPrism(List<Vector2> crossSection, float hz)
        {
            EnsureCounterClockwise(crossSection);
            int n = crossSection.Count;
            for (int i = 0; i < n; i++)
            {
                Vector2 a = crossSection[i];
                Vector2 b = crossSection[(i + 1) % n];
                Vector2 edge = b - a;
                // For a CCW polygon the interior is to the LEFT of each directed edge, so the OUTWARD
                // normal is the right-hand perpendicular (dy, -dx).
                Vector2 outward2 = new Vector2(edge.y, -edge.x);
                float len = outward2.magnitude;
                if (len < 1e-5f) continue;
                outward2 /= len;
                Vector3 normal = new Vector3(outward2.x, outward2.y, 0f);
                float offset = Vector2.Dot(a, outward2); // a lies on the wall line; center is origin
                _planes.Add(new FacePlane(normal, offset));
            }
            AddCapPlanes(hz);
        }

        void BuildBeveledBox(float hx, float hy, float hz, float bevelFraction)
        {
            // 6 faces.
            _planes.Add(new FacePlane(Vector3.right, hx));
            _planes.Add(new FacePlane(Vector3.left, hx));
            _planes.Add(new FacePlane(Vector3.up, hy));
            _planes.Add(new FacePlane(Vector3.down, hy));
            _planes.Add(new FacePlane(Vector3.forward, hz));
            _planes.Add(new FacePlane(Vector3.back, hz));

            float f = Mathf.Clamp01(bevelFraction);

            // 12 edge bevels: each pairs two axes; the chamfer plane sits between the farther
            // face-center projection (don't eat a face) and the edge projection (actually cut the edge).
            AddEdgeBevels(0, 1, hx, hy, f); // X-Y edges
            AddEdgeBevels(0, 2, hx, hz, f); // X-Z edges
            AddEdgeBevels(1, 2, hy, hz, f); // Y-Z edges

            // 8 corner bevels: (±1,±1,±1) chamfers.
            float invSqrt3 = 1f / Mathf.Sqrt(3f);
            float cornerProj = (hx + hy + hz) * invSqrt3;             // toward the true corner
            float faceProj = Mathf.Max(hx, Mathf.Max(hy, hz)) * invSqrt3; // keep faces intact
            float cornerOffset = Mathf.Lerp(faceProj, cornerProj, 1f - f * 0.7f);
            for (int sx = -1; sx <= 1; sx += 2)
                for (int sy = -1; sy <= 1; sy += 2)
                    for (int sz = -1; sz <= 1; sz += 2)
                        _planes.Add(new FacePlane(new Vector3(sx, sy, sz) * invSqrt3, cornerOffset));
        }

        void AddEdgeBevels(int axisA, int axisB, float ha, float hb, float f)
        {
            float invSqrt2 = 1f / Mathf.Sqrt(2f);
            float edgeProj = (ha + hb) * invSqrt2;            // toward the shared edge
            float faceProj = Mathf.Max(ha, hb) * invSqrt2;   // keep both faces intact
            float offset = Mathf.Lerp(faceProj, edgeProj, 1f - f * 0.7f);
            for (int sa = -1; sa <= 1; sa += 2)
                for (int sb = -1; sb <= 1; sb += 2)
                {
                    Vector3 normal = Vector3.zero;
                    normal[axisA] = sa * invSqrt2;
                    normal[axisB] = sb * invSqrt2;
                    _planes.Add(new FacePlane(normal, offset));
                }
        }

        void BuildOctahedron(float hx, float hy, float hz)
        {
            // Faces of the octahedron with vertices (±hx,0,0),(0,±hy,0),(0,0,±hz): plane |x|/hx+|y|/hy+|z|/hz = 1.
            Vector3 raw = new Vector3(1f / hx, 1f / hy, 1f / hz);
            for (int sx = -1; sx <= 1; sx += 2)
                for (int sy = -1; sy <= 1; sy += 2)
                    for (int sz = -1; sz <= 1; sz += 2)
                    {
                        Vector3 nRaw = new Vector3(sx * raw.x, sy * raw.y, sz * raw.z);
                        float len = nRaw.magnitude;
                        _planes.Add(new FacePlane(nRaw / len, 1f / len));
                    }
        }

        static void EnsureCounterClockwise(List<Vector2> poly)
        {
            float area = 0f;
            for (int i = 0; i < poly.Count; i++)
            {
                Vector2 a = poly[i];
                Vector2 b = poly[(i + 1) % poly.Count];
                area += a.x * b.y - b.x * a.y;
            }
            if (area < 0f) poly.Reverse(); // was clockwise → flip to CCW
        }

        // ── Containment (the bounce) ─────────────────────────────────────────

        /// <summary>
        /// If the ball (center <paramref name="position"/>, radius <paramref name="ballRadius"/>) has
        /// reached or passed any wall, depenetrate it to kiss the wall and reflect the wall-perpendicular
        /// component of <paramref name="velocity"/> with restitution <paramref name="restitution"/>
        /// (the wall-PARALLEL component is preserved - that is the bank). Returns true if it bounced,
        /// with the deepest contact reported for juice. Handles corners (multiple violated planes) by
        /// resolving each, so a box corner reverses both axes cleanly.
        /// </summary>
        public bool Contain(
            ref Vector3 position,
            ref Vector3 velocity,
            float ballRadius,
            float restitution,
            out Vector3 contactPoint,
            out Vector3 contactNormal)
        {
            contactPoint = position;
            contactNormal = Vector3.zero;
            float e = Mathf.Clamp01(restitution);

            // Outer containment (the ball stays INSIDE the court), dispatched on the effective outer shape.
            bool bounced;
            switch (_outerShape)
            {
                case AstroLeagueBoundaryShape.Sphere:
                    bounced = ContainSphere(ref position, ref velocity, ballRadius, e, out contactPoint, out contactNormal);
                    break;
                case AstroLeagueBoundaryShape.Cylinder:
                    bounced = ContainCylinder(ref position, ref velocity, ballRadius, e, out contactPoint, out contactNormal);
                    break;
                default:
                    bounced = ContainPolytope(ref position, ref velocity, ballRadius, e, out contactPoint, out contactNormal);
                    break;
            }

            // Central ring OBSTACLE (the ball stays OUTSIDE the torus tube) - runs after the outer wall.
            if (_hasRing && ContainRing(ref position, ref velocity, ballRadius, e, out Vector3 rcp, out Vector3 rcn))
            {
                bounced = true;
                contactPoint = rcp;
                contactNormal = rcn;
            }
            return bounced;
        }

        /// <summary>
        /// Central torus ring obstacle (axis Z): keep the ball OUTSIDE the tube. The ball can pass freely
        /// through the central hole (radius major−tube) and through the angular notch. When it would enter
        /// the tube, push it back to the tube surface and reflect the inward (toward-the-tube) velocity
        /// component - extra bounces + a center choke point, leaving the hole and notch open as lanes.
        /// </summary>
        bool ContainRing(ref Vector3 pos, ref Vector3 vel, float r, float e, out Vector3 cp, out Vector3 cn)
        {
            cp = pos; cn = Vector3.zero;

            Vector3 rel = pos - Center;
            float rho = Mathf.Sqrt(rel.x * rel.x + rel.y * rel.y); // distance from the Z (goal) axis
            if (rho < 1e-4f) return false;                          // dead center - inside the hole, far from the tube

            // The notch is an angular gap where the ring is absent (a shooting lane through the ring).
            if (_notchHalfWidth > 1e-4f)
            {
                float ang = Mathf.Atan2(rel.y, rel.x);
                float delta = Mathf.Abs(Mathf.DeltaAngle(ang * Mathf.Rad2Deg, _notchCenter * Mathf.Rad2Deg)) * Mathf.Deg2Rad;
                if (delta <= _notchHalfWidth) return false;
            }

            // 2D distance from the tube centerline circle (radius _ringMajor) in the (radial, z) plane.
            Vector2 q = new Vector2(rho - _ringMajor, rel.z);
            float d = q.magnitude;
            float minD = _ringTube + r;          // ball SURFACE kisses the tube
            if (d >= minD || d < 1e-4f) return false;

            // Outward normal: in the (radial, z) plane it's q/|q|; lift back to 3D via the XY radial dir.
            Vector2 qn = q / d;
            Vector2 radial = new Vector2(rel.x, rel.y) / rho;
            Vector3 normal = new Vector3(radial.x * qn.x, radial.y * qn.x, qn.y); // unit (qn, radial both unit)

            float vn = Vector3.Dot(vel, normal);
            if (vn < 0f) vel -= (1f + e) * vn * normal; // ball moving INTO the tube → reflect the inward part
            rel += normal * (minD - d);                  // push out to the tube surface
            pos = Center + rel;

            cn = normal;
            cp = pos - normal * r; // the ball-surface contact point
            return true;
        }

        bool ContainSphere(ref Vector3 pos, ref Vector3 vel, float r, float e, out Vector3 cp, out Vector3 cn)
        {
            cp = pos; cn = Vector3.zero;
            Vector3 fromCenter = pos - Center;
            float maxDist = _sphereRadius - r; // ball SURFACE kisses the wall
            if (maxDist <= 0f) return false;

            float dist = fromCenter.magnitude;
            if (dist <= maxDist || dist < 1e-4f) return false;

            Vector3 outward = fromCenter / dist;
            float vn = Vector3.Dot(vel, outward);
            if (vn > 0f) vel -= (1f + e) * vn * outward; // reflect only the outward (radial) component
            pos = Center + outward * maxDist;
            cn = -outward;
            cp = Center + outward * _sphereRadius;
            return true;
        }

        bool ContainCylinder(ref Vector3 pos, ref Vector3 vel, float r, float e, out Vector3 cp, out Vector3 cn)
        {
            cp = pos; cn = Vector3.zero;
            bool bounced = false;
            float deepest = 0f;

            // Curved barrel in the XY cross-section.
            Vector3 rel = pos - Center;
            Vector2 radial = new Vector2(rel.x, rel.y);
            float maxRadial = _cylinderRadius - r;
            float radDist = radial.magnitude;
            if (maxRadial > 0f && radDist > maxRadial && radDist > 1e-4f)
            {
                Vector2 outward2 = radial / radDist;
                Vector3 outward = new Vector3(outward2.x, outward2.y, 0f);
                float vn = Vector3.Dot(vel, outward);
                if (vn > 0f) vel -= (1f + e) * vn * outward;
                rel.x = outward2.x * maxRadial;
                rel.y = outward2.y * maxRadial;
                pos = Center + rel;
                cn = -outward;
                cp = Center + outward * _cylinderRadius;
                deepest = radDist - maxRadial;
                bounced = true;
            }

            // Flat goal caps ±Z.
            float maxZ = _capHalfLength - r;
            float over = Mathf.Abs(rel.z) - maxZ;
            if (maxZ > 0f && over > 0f)
            {
                float sz = Mathf.Sign(rel.z);
                Vector3 outward = new Vector3(0f, 0f, sz);
                float vn = Vector3.Dot(vel, outward);
                if (vn > 0f) vel -= (1f + e) * vn * outward;
                rel.z = sz * maxZ;
                pos = Center + rel;
                if (over > deepest) { cn = -outward; cp = Center + outward * _capHalfLength; }
                bounced = true;
            }
            return bounced;
        }

        bool ContainPolytope(ref Vector3 pos, ref Vector3 vel, float r, float e, out Vector3 cp, out Vector3 cn)
        {
            cp = pos; cn = Vector3.zero;
            Vector3 rel = pos - Center;
            bool bounced = false;
            float deepest = 0f;
            Vector3 deepestNormal = Vector3.zero;
            float deepestOffset = 0f;

            // Up to 4 passes: each resolves the violated faces; later passes catch a residual poke that
            // the prior depenetration nudged across a neighbouring (non-orthogonal) plane in a corner.
            // Box/prisms settle in 1-2 passes (orthogonal/well-separated normals → the early-out below
            // fires immediately); only a deep multi-plane vertex (e.g. an Octahedron corner, where 4
            // non-orthogonal planes meet) actually consumes passes 3-4, so the extra cap is free.
            for (int pass = 0; pass < 4; pass++)
            {
                bool anyThisPass = false;
                for (int i = 0; i < _planes.Count; i++)
                {
                    FacePlane plane = _planes[i];
                    float limit = plane.Offset - r;        // ball CENTER must stay within this
                    float over = Vector3.Dot(rel, plane.Normal) - limit;
                    if (over <= 1e-4f) continue;

                    rel -= over * plane.Normal;             // depenetrate to kiss the wall
                    float vn = Vector3.Dot(vel, plane.Normal);
                    if (vn > 0f) vel -= (1f + e) * vn * plane.Normal; // bank: only the perpendicular flips
                    anyThisPass = true;
                    bounced = true;

                    if (over > deepest)
                    {
                        deepest = over;
                        deepestNormal = plane.Normal;
                        deepestOffset = plane.Offset;
                    }
                }
                if (!anyThisPass) break;
            }

            if (bounced)
            {
                pos = Center + rel;
                cn = -deepestNormal;
                cp = Center + deepestNormal * deepestOffset;
            }
            return bounced;
        }

        // ── Visual cage mesh (convex hull of the face planes) ────────────────

        /// <summary>
        /// Build the boundary's visual mesh, centered on the origin in world units (the nucleus
        /// GameObject sits at <see cref="Center"/> with unit scale). The outer court is the convex hull
        /// of its face planes (polytopes) or a barrel + caps (cylinder), so the same geometry drives
        /// physics and visuals - the glowing cage silhouette IS the wall the ball hits. NotchedRing also
        /// appends the central notched torus. Returns null for the Sphere (it keeps its prefab mesh).
        /// </summary>
        public Mesh BuildVisualMesh()
        {
            var meshVerts = new List<Vector3>();
            var tris = new List<int>();
            var normals = new List<Vector3>();

            if (_outerShape == AstroLeagueBoundaryShape.Sphere)
                AppendSphereMesh(meshVerts, tris, normals);
            else if (_outerShape == AstroLeagueBoundaryShape.Cylinder)
                AppendCylinderMesh(meshVerts, tris, normals);
            else if (_planes.Count >= 4)
                AppendPolytopeMesh(meshVerts, tris, normals);

            if (_hasRing)
                AppendTorusMesh(meshVerts, tris, normals);

            if (tris.Count < 3) return null;
            return FinalizeMesh(meshVerts, tris, normals);
        }

        /// <summary>
        /// Solve the polytope hull shared by the visual mesh and the edge-path collector: the unique
        /// hull vertices (every plane-triple intersection lying inside all planes) and, per face, the
        /// indices of its vertices ordered CCW about the outward normal. One geometry source means the
        /// cage mesh, the edge lining and the physics walls can never drift apart. False when degenerate.
        /// </summary>
        bool SolvePolytopeHull(List<Vector3> verts, List<(int planeIndex, List<int> loop)> faces)
        {
            // Tolerances scale with arena size (50..600u half-extent across intensities) so a small
            // feature isn't merged away and a vertex on a large arena still registers on its face.
            float eps = Mathf.Max(0.1f, MaxExtent * 2e-3f);
            float epsSqr = eps * eps;

            // 1. Candidate vertices = intersection of every plane triple that lies inside all planes.
            int p = _planes.Count;
            for (int i = 0; i < p; i++)
                for (int j = i + 1; j < p; j++)
                    for (int k = j + 1; k < p; k++)
                    {
                        if (TrySolveTriple(_planes[i], _planes[j], _planes[k], out Vector3 v)
                            && InsideAllPlanes(v, eps))
                            AddUniqueVertex(verts, v, epsSqr);
                    }
            if (verts.Count < 4) return false;

            // 2. Each face = the vertices lying on its plane, ordered CCW about the outward normal.
            for (int f = 0; f < p; f++)
            {
                FacePlane plane = _planes[f];
                var onPlane = new List<int>();
                for (int vi = 0; vi < verts.Count; vi++)
                    if (Mathf.Abs(Vector3.Dot(verts[vi], plane.Normal) - plane.Offset) < eps)
                        onPlane.Add(vi);
                if (onPlane.Count < 3) continue;

                Vector3 centroid = Vector3.zero;
                for (int o = 0; o < onPlane.Count; o++) centroid += verts[onPlane[o]];
                centroid /= onPlane.Count;

                Vector3 tangent = BuildTangent(plane.Normal);
                Vector3 bitangent = Vector3.Cross(plane.Normal, tangent);
                onPlane.Sort((x, y) =>
                {
                    Vector3 dx = verts[x] - centroid, dy = verts[y] - centroid;
                    float ax = Mathf.Atan2(Vector3.Dot(dx, bitangent), Vector3.Dot(dx, tangent));
                    float ay = Mathf.Atan2(Vector3.Dot(dy, bitangent), Vector3.Dot(dy, tangent));
                    return ax.CompareTo(ay);
                });

                faces.Add((f, onPlane));
            }
            return faces.Count > 0;
        }

        /// <summary>Convex hull of the face planes, fan-triangulated per face, appended to the lists.</summary>
        void AppendPolytopeMesh(List<Vector3> meshVerts, List<int> tris, List<Vector3> normals)
        {
            var verts = new List<Vector3>();
            var faces = new List<(int planeIndex, List<int> loop)>();
            if (!SolvePolytopeHull(verts, faces)) return;

            foreach (var (planeIndex, loop) in faces)
            {
                FacePlane plane = _planes[planeIndex];
                int baseIndex = meshVerts.Count;
                for (int o = 0; o < loop.Count; o++)
                {
                    meshVerts.Add(verts[loop[o]]);
                    normals.Add(plane.Normal);
                }
                for (int o = 1; o < loop.Count - 1; o++)
                {
                    tris.Add(baseIndex);
                    tris.Add(baseIndex + o);
                    tris.Add(baseIndex + o + 1);
                }
            }
        }

        // ── Edge paths (the super-shielded lining) ───────────────────────────

        /// <summary>
        /// Collect the court's EDGE polylines, CENTER-RELATIVE (add <see cref="Center"/> for world
        /// space) - the geometry the arena dresses with its super-shielded prism lining. Polytope
        /// courts yield one 2-point segment per hull edge (a vertex pair shared by two faces); the
        /// cylinder yields its two cap rims; the sphere - which has no true edges - yields three
        /// latitude rings around the goal (Z) axis. NotchedRing yields its OUTER court's edges (the
        /// central torus is an obstacle, not an edge).
        /// </summary>
        public void CollectEdgePaths(List<Vector3[]> paths)
        {
            paths.Clear();
            const int ringSegments = 48;

            switch (_outerShape)
            {
                case AstroLeagueBoundaryShape.Sphere:
                {
                    float r = _sphereRadius;
                    float lat = r * 0.70710678f; // ±45° latitude
                    paths.Add(BuildLatitudeRing(r, 0f, ringSegments));
                    paths.Add(BuildLatitudeRing(lat, lat, ringSegments));
                    paths.Add(BuildLatitudeRing(lat, -lat, ringSegments));
                    break;
                }
                case AstroLeagueBoundaryShape.Cylinder:
                {
                    paths.Add(BuildLatitudeRing(_cylinderRadius, _capHalfLength, ringSegments));
                    paths.Add(BuildLatitudeRing(_cylinderRadius, -_capHalfLength, ringSegments));
                    break;
                }
                default:
                {
                    var verts = new List<Vector3>();
                    var faces = new List<(int planeIndex, List<int> loop)>();
                    if (!SolvePolytopeHull(verts, faces)) return;

                    // Every hull edge is a consecutive pair in exactly two face loops - a
                    // min/max-keyed set emits each once.
                    var seen = new HashSet<(int, int)>();
                    foreach (var (_, loop) in faces)
                        for (int o = 0; o < loop.Count; o++)
                        {
                            int a = loop[o];
                            int b = loop[(o + 1) % loop.Count];
                            var key = a < b ? (a, b) : (b, a);
                            if (seen.Add(key))
                                paths.Add(new[] { verts[key.Item1], verts[key.Item2] });
                        }
                    break;
                }
            }
        }

        /// <summary>A closed circle of constant Z around the goal axis (center-relative).</summary>
        static Vector3[] BuildLatitudeRing(float radius, float z, int segments)
        {
            var pts = new Vector3[segments + 1];
            for (int i = 0; i <= segments; i++)
            {
                float a = i / (float)segments * Mathf.PI * 2f;
                pts[i] = new Vector3(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius, z);
            }
            return pts;
        }

        /// <summary>Barrel (N segments) + flat ±Z caps, matching <see cref="ContainCylinder"/>'s walls.</summary>
        /// <summary>
        /// Flat-shaded icosphere hull at the sphere court's radius.
        ///
        /// The Sphere shape used to contribute NO hull mesh - the nucleus was merely resized
        /// (<c>Cell.SetNucleusWorldRadius</c>) and kept the prefab's plain sphere. That is why the
        /// faceted glowing "arena cover" every other court wears was missing at intensity 4: it is
        /// the generated cage mesh rendered through the nucleus' CageMaterial, and the sphere never
        /// generated one. Building it here means every shape gets the cover from ONE source, and -
        /// as with the polytopes - the surface you see is the surface the ball reflects off.
        ///
        /// Reuses <see cref="IcosphereMeshGenerator"/> (the ball's mesh source) flat-shaded, so the
        /// facets catch the cage material's fresnel exactly like the polytope faces do.
        /// </summary>
        void AppendSphereMesh(List<Vector3> meshVerts, List<int> tris, List<Vector3> normals)
        {
            // 3 subdivisions = 1280 tris - dense enough to read as a dome at arena scale, coarse
            // enough that the facets stay legible (the whole point of the look). FinalizeMesh
            // doubles it for the inside faces, as it does for every other shape.
            var sphere = IcosphereMeshGenerator.Generate(3, _sphereRadius, flatShaded: true);
            if (sphere == null) return;

            var sv = sphere.vertices;
            var st = sphere.triangles;
            var sn = sphere.normals;
            int baseIndex = meshVerts.Count;
            for (int i = 0; i < sv.Length; i++)
            {
                meshVerts.Add(sv[i]);
                normals.Add(sn != null && sn.Length == sv.Length ? sn[i] : sv[i].normalized);
            }
            for (int i = 0; i < st.Length; i++) tris.Add(baseIndex + st[i]);

            // The generator hands back a fresh Mesh each call; this one only ever existed to be
            // copied into the shared cage mesh. (Play-mode guard: Destroy is an error in edit mode,
            // and a boundary is only ever built at runtime.)
            if (Application.isPlaying) UnityEngine.Object.Destroy(sphere);
        }

        void AppendCylinderMesh(List<Vector3> meshVerts, List<int> tris, List<Vector3> normals)
        {
            const int seg = 32;
            float r = _cylinderRadius, hz = _capHalfLength;

            // Barrel: a ring of quads with outward radial normals.
            for (int i = 0; i < seg; i++)
            {
                float a0 = i / (float)seg * Mathf.PI * 2f;
                float a1 = (i + 1) / (float)seg * Mathf.PI * 2f;
                Vector3 d0 = new Vector3(Mathf.Cos(a0), Mathf.Sin(a0), 0f);
                Vector3 d1 = new Vector3(Mathf.Cos(a1), Mathf.Sin(a1), 0f);
                int b = meshVerts.Count;
                meshVerts.Add(d0 * r + Vector3.forward * hz); normals.Add(d0);
                meshVerts.Add(d1 * r + Vector3.forward * hz); normals.Add(d1);
                meshVerts.Add(d1 * r + Vector3.back * hz);    normals.Add(d1);
                meshVerts.Add(d0 * r + Vector3.back * hz);    normals.Add(d0);
                tris.Add(b); tris.Add(b + 1); tris.Add(b + 2);
                tris.Add(b); tris.Add(b + 2); tris.Add(b + 3);
            }

            // Caps: a fan at each ±Z end.
            for (int s = -1; s <= 1; s += 2)
            {
                Vector3 capN = new Vector3(0f, 0f, s);
                int center = meshVerts.Count;
                meshVerts.Add(capN * hz); normals.Add(capN);
                int ring = meshVerts.Count;
                for (int i = 0; i < seg; i++)
                {
                    float a = i / (float)seg * Mathf.PI * 2f;
                    meshVerts.Add(new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r, s * hz));
                    normals.Add(capN);
                }
                for (int i = 0; i < seg; i++)
                {
                    int v0 = ring + i, v1 = ring + (i + 1) % seg;
                    tris.Add(center); tris.Add(v0); tris.Add(v1);
                }
            }
        }

        /// <summary>
        /// The central torus ring (axis Z), matching <see cref="ContainRing"/>: sweep a tube circle
        /// (radius _ringTube) around the major circle (radius _ringMajor), SKIPPING the angular notch so
        /// the gap is visible. Outward normals point away from the tube centerline (the bounce surface).
        /// </summary>
        void AppendTorusMesh(List<Vector3> meshVerts, List<int> tris, List<Vector3> normals)
        {
            const int majorSeg = 48; // around the ring
            const int minorSeg = 12; // around the tube

            // Build the ring of section-rings, marking which major steps fall inside the notch (skipped).
            int[] ringStart = new int[majorSeg + 1];
            bool[] skip = new bool[majorSeg + 1];
            for (int i = 0; i <= majorSeg; i++)
            {
                float a = i / (float)majorSeg * Mathf.PI * 2f;
                if (_notchHalfWidth > 1e-4f)
                {
                    float delta = Mathf.Abs(Mathf.DeltaAngle(a * Mathf.Rad2Deg, _notchCenter * Mathf.Rad2Deg)) * Mathf.Deg2Rad;
                    skip[i] = delta <= _notchHalfWidth;
                }
                Vector3 ringDir = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);  // outward radial (XY)
                Vector3 centerlinePt = ringDir * _ringMajor;
                ringStart[i] = meshVerts.Count;
                for (int j = 0; j < minorSeg; j++)
                {
                    float b = j / (float)minorSeg * Mathf.PI * 2f;
                    // Tube cross-section spans the (radial, z) plane: cos→outward radial, sin→±Z.
                    Vector3 n = ringDir * Mathf.Cos(b) + Vector3.forward * Mathf.Sin(b);
                    meshVerts.Add(centerlinePt + n * _ringTube);
                    normals.Add(n);
                }
            }

            // Quad strips between adjacent (non-skipped) section-rings.
            for (int i = 0; i < majorSeg; i++)
            {
                if (skip[i] || skip[i + 1]) continue;
                int a0 = ringStart[i], a1 = ringStart[i + 1];
                for (int j = 0; j < minorSeg; j++)
                {
                    int j1 = (j + 1) % minorSeg;
                    int v00 = a0 + j, v01 = a0 + j1, v10 = a1 + j, v11 = a1 + j1;
                    tris.Add(v00); tris.Add(v10); tris.Add(v11);
                    tris.Add(v00); tris.Add(v11); tris.Add(v01);
                }
            }
        }

        /// <summary>
        /// Double-side the geometry (players fly INSIDE the boundary, so the cage must render no matter
        /// what the nucleus material's cull mode is - mirror every triangle with a flipped-normal copy)
        /// and assemble the Mesh.
        /// </summary>
        Mesh FinalizeMesh(List<Vector3> meshVerts, List<int> tris, List<Vector3> normals)
        {
            int single = meshVerts.Count;
            for (int i = 0; i < single; i++) { meshVerts.Add(meshVerts[i]); normals.Add(-normals[i]); }
            int triCount = tris.Count;
            for (int i = 0; i < triCount; i += 3)
            {
                tris.Add(single + tris[i]);
                tris.Add(single + tris[i + 2]);
                tris.Add(single + tris[i + 1]);
            }

            var mesh = new Mesh { name = $"AstroLeagueBoundary_{Shape}" };
            mesh.indexFormat = meshVerts.Count > 65000
                ? UnityEngine.Rendering.IndexFormat.UInt32
                : UnityEngine.Rendering.IndexFormat.UInt16;
            mesh.SetVertices(meshVerts);
            mesh.SetTriangles(tris, 0);
            mesh.SetNormals(normals);
            mesh.RecalculateBounds();
            return mesh;
        }

        static Vector3 BuildTangent(Vector3 normal)
        {
            Vector3 t = Mathf.Abs(normal.x) < 0.9f ? Vector3.right : Vector3.up;
            return Vector3.Normalize(t - normal * Vector3.Dot(t, normal));
        }

        bool InsideAllPlanes(Vector3 v, float eps)
        {
            for (int i = 0; i < _planes.Count; i++)
                if (Vector3.Dot(v, _planes[i].Normal) - _planes[i].Offset > eps)
                    return false;
            return true;
        }

        static void AddUniqueVertex(List<Vector3> verts, Vector3 v, float epsSqr)
        {
            for (int i = 0; i < verts.Count; i++)
                if ((verts[i] - v).sqrMagnitude < epsSqr)
                    return;
            verts.Add(v);
        }

        /// <summary>Solve the 3 planes' intersection point (relative to center). False if near-parallel.</summary>
        static bool TrySolveTriple(FacePlane a, FacePlane b, FacePlane c, out Vector3 point)
        {
            point = Vector3.zero;
            Vector3 bc = Vector3.Cross(b.Normal, c.Normal);
            float det = Vector3.Dot(a.Normal, bc);
            if (Mathf.Abs(det) < 1e-6f) return false;

            Vector3 ca = Vector3.Cross(c.Normal, a.Normal);
            Vector3 ab = Vector3.Cross(a.Normal, b.Normal);
            point = (a.Offset * bc + b.Offset * ca + c.Offset * ab) / det;
            return true;
        }
    }
}
