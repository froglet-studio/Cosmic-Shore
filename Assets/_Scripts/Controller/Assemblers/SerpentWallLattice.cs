using System.Collections.Generic;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Pure geometry of the Serpent's seed wall (design: R_VesselActions/SERPENT_SEED_WALL.md).
    /// No Unity objects, no state: every number the runtime assembler lays comes from here, so
    /// the pattern can be tested headless and previewed without spawning anything.
    ///
    /// <para><b>The pattern.</b> A square lattice of bricks with an aspect of 2 (long = 2 x short),
    /// alternating orientation like a checkerboard: site (i, j) lies long-axis-UP when i + j is even
    /// and long-axis-RIGHT when it is odd. It reads as a herringbone, but it is symmetric rather
    /// than a strict tiling: a square hole is left at every lattice cell.</para>
    ///
    /// <para><b>The spacing is set by the SHIELDED shape.</b> A shielded prism draws as the
    /// octahedron that CIRCUMSCRIBES its box, at <see cref="ShieldReach"/> (3) times the box's
    /// half-extents (<c>OctahedronMeshGenerator.CIRCUMSCRIBING_SCALE</c>), so its cross-section in
    /// the wall plane is a rhombus whose vertices sit 3 x half the long side and 3 x half the short
    /// side from the brick's centre. Each brick's LONG-axis vertex points at the neighbouring
    /// brick's SHORT-axis vertex, and the pitch leaves <see cref="ShieldGap"/> of air between them:
    /// <c>pitch = 3 x (long/2 + short/2) + gap</c>, which is <c>5 x short</c> at aspect 2. So the
    /// wall is a lattice of diamonds that nearly touch once an omni crystal shields it, and widely
    /// spaced plain bricks before. (The first cut used the BOX's half-extents and packed the bricks
    /// three times too tight: shielded, they interpenetrated into one clump. The second made the
    /// vertices meet exactly, and in play shielded bricks read as touching, and the super-shielded
    /// seed, whose stellation reaches out to the corners of the 3x box, cut into its neighbours
    /// once the Lockdown twist turned it.)</para>
    ///
    /// <para><b>The Mass-5 twist.</b> Turning every brick clockwise about its own centre by the
    /// same angle keeps each cell square (every cell has four-fold symmetry) but changes its size,
    /// and the two kinds of cell, which are mirror images, go opposite ways: one parity closes and
    /// the other opens. That is the checkerboard. The open cells are sealed with flat danger panels
    /// sized here by <see cref="LargestClearSquare"/>. The runtime turns bricks and panels ONLY
    /// through <see cref="BrickRotation"/> and <see cref="PanelRotation"/>, which are where this
    /// file's 2D angles meet Unity's quaternions: the first build signed them independently in the
    /// assembler, twisted the bricks the opposite way to these maths, and so laid its panels in
    /// the cells that had CLOSED.</para>
    ///
    /// Coordinates are wall-local: x along the seed's right, y along its up, in world units.
    /// Angles are in degrees; a positive twist is CLOCKWISE seen from the Serpent's seat (looking
    /// along the seed's forward), which is a NEGATIVE angle both in this file's counter-clockwise
    /// 2D maths and for <c>Quaternion.AngleAxis</c> about forward: with x right and y up, a positive
    /// AngleAxis about forward turns right toward up, which from the seat is counter-clockwise.
    /// </summary>
    public static class SerpentWallLattice
    {
        /// <summary>The fixed brick aspect (long / short). The pitch formula assumes it.</summary>
        public const float Aspect = 2f;

        /// <summary>How far a shield reaches past its box, as a multiple of the box's
        /// half-extents: the shield octahedron's semi-axes are this times the brick's.</summary>
        public const float ShieldReach = OctahedronMeshGenerator.CIRCUMSCRIBING_SCALE;

        /// <summary>Air left between neighbouring shields, long vertex to short vertex, as a
        /// multiple of the short side. Clears the super-shielded seed's stellation through the
        /// whole <see cref="MaxTwistDegrees"/> range as well.</summary>
        public const float ShieldGap = 0.5f;

        /// <summary>Largest Lockdown twist the gap is sized for (the twist's authoring range).</summary>
        public const float MaxTwistDegrees = 25f;

        /// <summary>Lattice pitch: the shield's reach along half the long axis plus half the short
        /// axis (where the two shields' vertices would meet), plus <see cref="ShieldGap"/>.</summary>
        public static float Pitch(float shortSide) =>
            (ShieldReach * (Aspect + 1f) * 0.5f + ShieldGap) * shortSide;

        /// <summary>Brick extents (x = short, y = long, z = depth) for a short side and depth.</summary>
        public static Vector3 BrickScale(float shortSide, float depth) =>
            new Vector3(shortSide, shortSide * Aspect, depth);

        /// <summary>The seed is site (0, 0) and keeps its own long axis up, so even sites are
        /// long-axis-up and odd sites are long-axis-right.</summary>
        public static bool IsLongAxisUp(int i, int j) => ((i + j) & 1) == 0;

        public static Vector2 SiteCenter(int i, int j, float pitch) => new Vector2(i * pitch, j * pitch);

        /// <summary>
        /// World rotation of brick (i, j) in a wall whose seed sits at <paramref name="frame"/>,
        /// twisted CLOCKWISE (seen from the seat) by <paramref name="twistDegrees"/>. Local y is the
        /// brick's long axis, so a long-axis-RIGHT site turns a quarter about forward. Agrees with
        /// <see cref="Rhombus"/> by construction (SerpentWallLatticeTests pins it).
        /// </summary>
        public static Quaternion BrickRotation(Quaternion frame, int i, int j, float twistDegrees)
        {
            Quaternion local = IsLongAxisUp(i, j) ? Quaternion.identity : Quaternion.AngleAxis(90f, Vector3.forward);
            return Quaternion.AngleAxis(-twistDegrees, frame * Vector3.forward) * (frame * local);
        }

        /// <summary>World rotation of a cell's panel at the in-plane angle
        /// <see cref="LargestClearSquare"/> returned (counter-clockwise, this file's 2D maths).</summary>
        public static Quaternion PanelRotation(Quaternion frame, float angleDegrees) =>
            Quaternion.AngleAxis(angleDegrees, frame * Vector3.forward) * frame;

        /// <summary>A cell is the square hole whose lower-left brick is site (i, j).</summary>
        public static Vector2 CellCenter(int i, int j, float pitch) =>
            new Vector2((i + 0.5f) * pitch, (j + 0.5f) * pitch);

        /// <summary>Cells come in two mirror-image kinds; the twist opens one and closes the other.</summary>
        public static int CellParity(int i, int j) => (i + j) & 1;

        /// <summary>
        /// The order the wall claims sites in: outward from the seed by distance, then by angle,
        /// so it grows as a disc rather than a line. The seed (0, 0) is not included.
        ///
        /// <para>Its prefix is STABLE: <c>GrowthOrder(n)</c> is the first n entries of
        /// <c>GrowthOrder(m)</c> for any m &gt; n, which is what lets the wall extend its order
        /// forever without reshuffling sites it has already claimed. Only sites inside the disc
        /// the search square fully contains are ranked; the square's corners, which a larger
        /// square would out-rank with nearer sites outside it, wait for that larger square.</para>
        /// </summary>
        public static List<Vector2Int> GrowthOrder(int count)
        {
            var result = new List<Vector2Int>(Mathf.Max(0, count));
            if (count <= 0) return result;

            int radius = 1;
            while (SitesWithin(radius) < count) radius++;

            int r2 = radius * radius;
            var all = new List<Vector2Int>((2 * radius + 1) * (2 * radius + 1));
            for (int i = -radius; i <= radius; i++)
            for (int j = -radius; j <= radius; j++)
                if ((i != 0 || j != 0) && i * i + j * j <= r2) all.Add(new Vector2Int(i, j));

            all.Sort((a, b) =>
            {
                int da = a.x * a.x + a.y * a.y, db = b.x * b.x + b.y * b.y;
                if (da != db) return da.CompareTo(db);
                return Mathf.Atan2(a.y, a.x).CompareTo(Mathf.Atan2(b.y, b.x));
            });

            for (int k = 0; k < count && k < all.Count; k++) result.Add(all[k]);
            return result;
        }

        /// <summary>
        /// The in-plane footprint of brick (i, j) SUPER-shielded: the stellation's spike tips sit on
        /// the corners of the 3x box, so seen face-on it covers the whole 3x rectangle - a
        /// conservative outline (at the wall plane itself the stellation is only the rhombus).
        /// </summary>
        public static void StellatedFootprint(int i, int j, float shortSide, float pitch, float twistDegrees,
            Vector2[] into)
        {
            float halfLong = ShieldReach * shortSide * Aspect * 0.5f;
            float halfShort = ShieldReach * shortSide * 0.5f;
            bool up = IsLongAxisUp(i, j);
            float hx = up ? halfShort : halfLong, hy = up ? halfLong : halfShort;
            Vector2 c = SiteCenter(i, j, pitch);
            float rad = -twistDegrees * Mathf.Deg2Rad;
            into[0] = c + Rotate(new Vector2(hx, hy), rad);
            into[1] = c + Rotate(new Vector2(-hx, hy), rad);
            into[2] = c + Rotate(new Vector2(-hx, -hy), rad);
            into[3] = c + Rotate(new Vector2(hx, -hy), rad);
        }

        /// <summary>Sites other than the seed within Euclidean distance <paramref name="radius"/>.</summary>
        static int SitesWithin(int radius)
        {
            int n = 0, r2 = radius * radius;
            for (int i = -radius; i <= radius; i++)
            for (int j = -radius; j <= radius; j++)
                if ((i != 0 || j != 0) && i * i + j * j <= r2) n++;
            return n;
        }

        /// <summary>Ring index used to stage the shield ripple: Chebyshev distance from the seed.</summary>
        public static int Ring(Vector2Int site) => Mathf.Max(Mathf.Abs(site.x), Mathf.Abs(site.y));

        /// <summary>
        /// The shielded cross-section of brick (i, j): a rhombus with vertices on the long and short
        /// axes at the shield's reach, turned clockwise by <paramref name="twistDegrees"/> about its
        /// own centre.
        /// </summary>
        public static void Rhombus(int i, int j, float shortSide, float pitch, float twistDegrees,
            Vector2[] into)
        {
            float halfLong = ShieldReach * shortSide * Aspect * 0.5f;
            float halfShort = ShieldReach * shortSide * 0.5f;
            bool up = IsLongAxisUp(i, j);
            Vector2 a = up ? new Vector2(0f, halfLong) : new Vector2(halfLong, 0f);
            Vector2 b = up ? new Vector2(halfShort, 0f) : new Vector2(0f, halfShort);
            Vector2 c = SiteCenter(i, j, pitch);
            float rad = -twistDegrees * Mathf.Deg2Rad;
            into[0] = c + Rotate(a, rad);
            into[1] = c + Rotate(b, rad);
            into[2] = c + Rotate(-a, rad);
            into[3] = c + Rotate(-b, rad);
        }

        /// <summary>
        /// The largest square that fits in cell (i, j) between its four twisted shielded bricks,
        /// centred on the cell: its side and its in-plane angle (degrees, counter-clockwise in this
        /// file's 2D maths). Searched over angle in <paramref name="angleStepDegrees"/> steps and
        /// bisected on size; the cell has four-fold symmetry so 0..90 degrees covers every angle.
        /// </summary>
        public static (float side, float angleDegrees) LargestClearSquare(
            int i, int j, float shortSide, float twistDegrees, float angleStepDegrees = 1f)
        {
            float pitch = Pitch(shortSide);
            var bricks = new Vector2[4][];
            int k = 0;
            foreach (var s in new[] { (i, j), (i + 1, j), (i + 1, j + 1), (i, j + 1) })
            {
                bricks[k] = new Vector2[4];
                Rhombus(s.Item1, s.Item2, shortSide, pitch, twistDegrees, bricks[k]);
                k++;
            }

            Vector2 center = CellCenter(i, j, pitch);
            var square = new Vector2[4];
            float bestSide = 0f, bestAngle = 0f;
            float step = Mathf.Max(0.1f, angleStepDegrees);
            for (float deg = 0f; deg < 90f; deg += step)
            {
                float lo = 0f, hi = 3f * ShieldReach * shortSide;
                for (int iter = 0; iter < 32; iter++)
                {
                    float mid = 0.5f * (lo + hi);
                    Square(center, mid, deg, square);
                    bool hit = false;
                    for (int b = 0; b < 4 && !hit; b++) hit = Overlaps(square, bricks[b]);
                    if (hit) hi = mid; else lo = mid;
                }
                if (lo > bestSide) { bestSide = lo; bestAngle = deg; }
            }
            return (bestSide, bestAngle);
        }

        /// <summary>
        /// Which cell parity the twist OPENS (its clear square is larger than at rest), or -1 when
        /// the twist is too small to open either.
        /// </summary>
        public static int OpeningParity(float shortSide, float twistDegrees)
        {
            float rest = LargestClearSquare(0, 0, shortSide, 0f).side;
            float even = LargestClearSquare(0, 0, shortSide, twistDegrees).side;
            float odd = LargestClearSquare(1, 0, shortSide, twistDegrees).side;
            const float eps = 1e-3f;
            if (even > rest + eps && even >= odd) return 0;
            if (odd > rest + eps) return 1;
            return -1;
        }

        static Vector2 Rotate(Vector2 v, float rad)
        {
            float c = Mathf.Cos(rad), s = Mathf.Sin(rad);
            return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c);
        }

        static void Square(Vector2 center, float side, float degrees, Vector2[] into)
        {
            float h = side * 0.5f, rad = degrees * Mathf.Deg2Rad;
            into[0] = center + Rotate(new Vector2(h, h), rad);
            into[1] = center + Rotate(new Vector2(-h, h), rad);
            into[2] = center + Rotate(new Vector2(-h, -h), rad);
            into[3] = center + Rotate(new Vector2(h, -h), rad);
        }

        /// <summary>Separating-axis test for two convex quads. Touching counts as clear.</summary>
        static bool Overlaps(Vector2[] a, Vector2[] b) => !HasSeparatingAxis(a, a, b) && !HasSeparatingAxis(b, a, b);

        static bool HasSeparatingAxis(Vector2[] edges, Vector2[] a, Vector2[] b)
        {
            const float eps = 1e-5f;
            for (int e = 0; e < edges.Length; e++)
            {
                Vector2 d = edges[(e + 1) % edges.Length] - edges[e];
                var axis = new Vector2(-d.y, d.x);
                Project(a, axis, out float aMin, out float aMax);
                Project(b, axis, out float bMin, out float bMax);
                if (aMax <= bMin + eps || bMax <= aMin + eps) return true;
            }
            return false;
        }

        static void Project(Vector2[] poly, Vector2 axis, out float min, out float max)
        {
            min = max = Vector2.Dot(poly[0], axis);
            for (int p = 1; p < poly.Length; p++)
            {
                float v = Vector2.Dot(poly[p], axis);
                if (v < min) min = v;
                if (v > max) max = v;
            }
        }
    }
}
