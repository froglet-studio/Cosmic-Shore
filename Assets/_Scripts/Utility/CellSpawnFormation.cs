using UnityEngine;

namespace CosmicShore.Utility
{
    /// <summary>
    /// Builds player spawn poses arranged symmetrically on a sphere around a cell centre, every
    /// vessel facing the centre. Pure math, no Unity scene state - unit tested in
    /// <c>CellSpawnFormationTests</c>.
    ///
    /// The ring radius is measured off the CELL'S NUCLEUS (see <c>Cell.NucleusWorldRadius</c>), so
    /// each intensity's cell places its players the same distance outside its own core rather than
    /// at authored world positions that only suit one cell size.
    ///
    /// Symmetry by player count (the arrangement is the point - everyone gets an identical approach
    /// to the core, and no one starts closer to another player than to the centre):
    ///   1 - a single point on +Z.
    ///   2 - antipodal, i.e. both on the same axis through the centre.
    ///   3 - an equilateral triangle (a great circle, 120 deg apart).
    ///   4 - tetrahedral symmetry (the 4 alternating cube corners; every pair equidistant).
    ///   5+ - a Fibonacci sphere, the natural generalisation of "spread evenly over a sphere".
    ///
    /// <see cref="Formation.EquatorialRing"/> is the opt-in alternative: everyone on ONE horizontal
    /// great circle, evenly spaced, like Joust's authored spawn points. Use it when the arena has a
    /// meaningful "up" or a pole feature the sphere formation would drop players on top of - Ribcage
    /// wants it because its cage is densest at the poles, so a tetrahedral spread hands two of four
    /// players a much harder approach than the other two.
    /// </summary>
    public static class CellSpawnFormation
    {
        /// <summary>How spawn slots are distributed around the cell.</summary>
        public enum Formation
        {
            /// <summary>Maximally symmetric ON A SPHERE (tetrahedron / triangle / axis / Fibonacci).</summary>
            Symmetric = 0,

            /// <summary>Evenly spaced on the horizontal great circle through the centre.</summary>
            EquatorialRing = 1,
        }

        /// <summary>The four tetrahedron vertices (alternating corners of a cube), normalized.</summary>
        static readonly Vector3[] TetrahedronDirections =
        {
            new Vector3( 1f,  1f,  1f).normalized,
            new Vector3( 1f, -1f, -1f).normalized,
            new Vector3(-1f,  1f, -1f).normalized,
            new Vector3(-1f, -1f,  1f).normalized,
        };

        /// <summary>
        /// Spawn poses for <paramref name="count"/> players on a sphere of <paramref name="radius"/>
        /// around <paramref name="center"/>, each rotated to face the centre.
        /// </summary>
        public static Pose[] Build(int count, Vector3 center, float radius,
            Formation formation = Formation.Symmetric)
        {
            count = Mathf.Max(1, count);
            var poses = new Pose[count];

            for (int i = 0; i < count; i++)
            {
                Vector3 dir = Direction(i, count, formation);
                Vector3 position = center + dir * Mathf.Max(0f, radius);
                poses[i] = new Pose(position, FacingCenter(dir));
            }

            return poses;
        }

        /// <summary>The outward unit direction of slot <paramref name="index"/> in a formation of
        /// <paramref name="count"/>. Deterministic - the same index always yields the same slot.</summary>
        public static Vector3 Direction(int index, int count,
            Formation formation = Formation.Symmetric)
        {
            count = Mathf.Max(1, count);

            if (formation == Formation.EquatorialRing)
                return EquatorialDirection(index, count);

            switch (count)
            {
                case 1:
                    return Vector3.forward;

                // Both players on ONE axis through the centre: they start nose-to-nose.
                case 2:
                    return index == 0 ? Vector3.forward : Vector3.back;

                // Equilateral triangle on the XZ great circle.
                case 3:
                {
                    float theta = index * (2f * Mathf.PI / 3f);
                    return new Vector3(Mathf.Sin(theta), 0f, Mathf.Cos(theta));
                }

                case 4:
                    return TetrahedronDirections[index % TetrahedronDirections.Length];

                default:
                    return FibonacciDirection(index, count);
            }
        }

        /// <summary>
        /// Evenly spaced on the horizontal great circle (y = 0), so every player starts level with
        /// the arena's equator and has an identical approach - no one is handed the poles. Slot 0
        /// is on +Z and the ring walks anticlockwise, so a 4-player game is the 90-degree cross
        /// Joust authors by hand.
        /// </summary>
        static Vector3 EquatorialDirection(int index, int count)
        {
            float theta = index * (2f * Mathf.PI / count);
            return new Vector3(Mathf.Sin(theta), 0f, Mathf.Cos(theta));
        }

        /// <summary>
        /// Evenly-spread direction #<paramref name="index"/> of <paramref name="count"/> on a sphere
        /// (golden-angle spiral). Used for counts with no small-N regular polytope.
        /// </summary>
        static Vector3 FibonacciDirection(int index, int count)
        {
            // y walks the [-1, 1] band in equal steps (equal-area rings), the azimuth advances by
            // the golden angle so successive points never line up.
            float y = 1f - (index + 0.5f) * (2f / count);
            float ringRadius = Mathf.Sqrt(Mathf.Max(0f, 1f - y * y));
            float goldenAngle = Mathf.PI * (3f - Mathf.Sqrt(5f));
            float theta = goldenAngle * index;

            return new Vector3(Mathf.Cos(theta) * ringRadius, y, Mathf.Sin(theta) * ringRadius).normalized;
        }

        /// <summary>Rotation that aims a vessel at the cell centre from an outward direction.</summary>
        static Quaternion FacingCenter(Vector3 outward)
        {
            Vector3 forward = -outward;

            // World up is degenerate as a reference when the vessel is looking straight up/down;
            // fall back to a world axis that is guaranteed non-parallel.
            Vector3 up = Mathf.Abs(Vector3.Dot(forward, Vector3.up)) > 0.999f
                ? Vector3.forward
                : Vector3.up;

            return Quaternion.LookRotation(forward, up);
        }
    }
}
