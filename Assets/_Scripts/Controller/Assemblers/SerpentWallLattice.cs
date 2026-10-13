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
    /// wall is a lattice of diamonds that nearly touch once a crystal shields it, and widely spaced
    /// plain bricks before. (The first cut used the BOX's half-extents and packed the bricks three
    /// times too tight: shielded, they interpenetrated into one clump. The second made the vertices
    /// meet exactly, and in play shielded bricks read as touching.)</para>
    ///
    /// <para><b>The Mass-5 panels.</b> Every hole is the same square, tilted so its four sides lie
    /// parallel to the four shield edges around it (<see cref="CellPanel"/>): the even cells one
    /// way, the odd cells the mirror way. A panel a little smaller than the hole leaves an even
    /// crack of <c>clearance</c> along every side. (Lockdown first TWISTED the bricks so half the
    /// holes closed and panels went in the other half; Garrett dropped the twist on 2026-10-13 in
    /// favour of one panel size in every hole, with cracks between.) The runtime turns
    /// bricks and panels ONLY through <see cref="BrickRotation"/> and <see cref="PanelRotation"/>,
    /// which are where this file's 2D angles meet Unity's quaternions.</para>
    ///
    /// Coordinates are wall-local: x along the seed's right, y along its up, in world units.
    /// Angles are in degrees, counter-clockwise in the 2D maths, which is also
    /// <c>Quaternion.AngleAxis</c> about the seed's forward (it turns right toward up).
    /// </summary>
    public static class SerpentWallLattice
    {
        /// <summary>The fixed brick aspect (long / short). The pitch formula assumes it.</summary>
        public const float Aspect = 2f;

        /// <summary>How far a shield reaches past its box, as a multiple of the box's
        /// half-extents: the shield octahedron's semi-axes are this times the brick's.</summary>
        public const float ShieldReach = OctahedronMeshGenerator.CIRCUMSCRIBING_SCALE;

        /// <summary>Air left between neighbouring shields, long vertex to short vertex, as a
        /// multiple of the short side. Clears the super-shielded seed's stellation as well.</summary>
        public const float ShieldGap = 0.5f;

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
        /// World rotation of brick (i, j) in a wall whose seed sits at <paramref name="frame"/>.
        /// Local y is the brick's long axis, so a long-axis-RIGHT site turns a quarter about
        /// forward. Agrees with <see cref="Rhombus"/> (SerpentWallLatticeTests pins it).
        /// </summary>
        public static Quaternion BrickRotation(Quaternion frame, int i, int j) =>
            IsLongAxisUp(i, j) ? frame : frame * Quaternion.AngleAxis(90f, Vector3.forward);

        /// <summary>World rotation of a cell's panel at the in-plane angle
        /// <see cref="CellPanel"/> returned (counter-clockwise, this file's 2D maths).</summary>
        public static Quaternion PanelRotation(Quaternion frame, float angleDegrees) =>
            Quaternion.AngleAxis(angleDegrees, frame * Vector3.forward) * frame;

        /// <summary>A cell is the square hole whose lower-left brick is site (i, j).</summary>
        public static Vector2 CellCenter(int i, int j, float pitch) =>
            new Vector2((i + 0.5f) * pitch, (j + 0.5f) * pitch);

        /// <summary>
        /// The danger panel for cell (i, j): the square hole between its four shielded bricks,
        /// shrunk so an even crack of <paramref name="clearance"/> (world units) runs along every
        /// side. The hole's sides lie on the four shield edges facing the cell, which are parallel
        /// in pairs and meet at right angles, so the panel is tilted to match:
        /// <c>atan(1 / Aspect)</c> (26.6 degrees) in an even cell (i + j even) and the mirror angle in
        /// an odd one. Every cell's panel is the same size. Returns side 0 when the clearance
        /// leaves no room.
        /// </summary>
        public static (float side, float angleDegrees) CellPanel(int i, int j, float shortSide, float clearance)
        {
            // Distance from the cell centre to the edge of the lower-left brick's shield that faces
            // it, worked in the even cell (brick long-axis up, rhombus Aspect*x + y = Reach*Aspect*short/2):
            // ((Aspect + 1) * pitch/2 - Reach * Aspect * short/2) / sqrt(Aspect^2 + 1). The other
            // three edges are the same distance by the cell's symmetry.
            float pitch = Pitch(shortSide);
            float apothem = ((Aspect + 1f) * pitch - ShieldReach * Aspect * shortSide)
                            / (2f * Mathf.Sqrt(Aspect * Aspect + 1f));
            float side = Mathf.Max(0f, 2f * (apothem - clearance));
            float tilt = Mathf.Atan2(1f, Aspect) * Mathf.Rad2Deg;
            return (side, ((i + j) & 1) == 0 ? tilt : 90f - tilt);
        }

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
        public static void StellatedFootprint(int i, int j, float shortSide, float pitch, Vector2[] into)
        {
            float halfLong = ShieldReach * shortSide * Aspect * 0.5f;
            float halfShort = ShieldReach * shortSide * 0.5f;
            bool up = IsLongAxisUp(i, j);
            float hx = up ? halfShort : halfLong, hy = up ? halfLong : halfShort;
            Vector2 c = SiteCenter(i, j, pitch);
            into[0] = c + new Vector2(hx, hy);
            into[1] = c + new Vector2(-hx, hy);
            into[2] = c + new Vector2(-hx, -hy);
            into[3] = c + new Vector2(hx, -hy);
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
        /// axes at the shield's reach.
        /// </summary>
        public static void Rhombus(int i, int j, float shortSide, float pitch, Vector2[] into)
        {
            float halfLong = ShieldReach * shortSide * Aspect * 0.5f;
            float halfShort = ShieldReach * shortSide * 0.5f;
            bool up = IsLongAxisUp(i, j);
            Vector2 a = up ? new Vector2(0f, halfLong) : new Vector2(halfLong, 0f);
            Vector2 b = up ? new Vector2(halfShort, 0f) : new Vector2(0f, halfShort);
            Vector2 c = SiteCenter(i, j, pitch);
            into[0] = c + a;
            into[1] = c + b;
            into[2] = c - a;
            into[3] = c - b;
        }
    }
}
