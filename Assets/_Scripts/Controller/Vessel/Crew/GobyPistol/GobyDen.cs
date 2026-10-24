using CosmicShore.Data;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Pistol's dens (Garrett, 2026-10-22): "once the shrimp gets to a perch it spends a moment resting and building a
    /// den of prisms and shielded prisms ... the pistol can choose to travel backward to an old den and it will add prisms
    /// to a den based on the distance traveled between perches and mass the pair has collected, L5 mass produces danger
    /// prisms and super shielded prisms". Pure: how many prisms a rest adds, which kind each one is, and where it sits.
    /// </summary>
    public static class GobyDen
    {
        /// <summary>The prisms a rest adds: one per <paramref name="unitsPerPrism"/> travelled since the last den, scaled by
        /// the pair's Mass (<paramref name="massScale"/>), never past <paramref name="max"/> in one den.</summary>
        public static int PrismsFor(float travelled, float unitsPerPrism, float massScale, int already, int max)
        {
            if (travelled <= 0f || already >= max) return 0;
            int n = Mathf.FloorToInt(travelled / Mathf.Max(1f, unitsPerPrism) * Mathf.Max(0f, massScale));
            return Mathf.Clamp(n, 0, max - already);
        }

        /// <summary>
        /// The kind of a den's <paramref name="index"/>-th prism. A golden-ratio sequence spreads the shares evenly however
        /// many prisms the den holds. Below the Mass upgrade a den is plain and shielded prisms only; with it, the den also
        /// gets danger and super-shielded prisms.
        /// </summary>
        public static PrismKind KindFor(int index, bool massUpgrade, float shieldedShare, float dangerShare, float superShare)
        {
            float u = Frac((index + 1) * 0.6180339887f);
            if (massUpgrade)
            {
                if (u < superShare) return PrismKind.SuperShielded;
                u -= superShare;
                if (u < dangerShare) return PrismKind.Danger;
                u -= dangerShare;
            }
            return u < shieldedShare ? PrismKind.Shielded : PrismKind.Plain;
        }

        static float Frac(float x) => x - Mathf.Floor(x);

        /// <summary>The least distance two den prisms keep: a block's bounding diameter (s√3) times the clearance, so no two
        /// cubes can intersect whatever their orientation (the Shrike's nest rule).</summary>
        public static float Spacing(float blockSize, float clearance) => blockSize * Mathf.Sqrt(3f) * Mathf.Max(1f, clearance);

        /// <summary>How many prisms one shell of radius <paramref name="radius"/> holds. A Fibonacci shell of n points keeps
        /// every pair at least 2.686 r / √n apart (n ≥ 2, measured), so capping n at (2.6 r / spacing)² keeps the spacing.</summary>
        public static int ShellCapacity(float radius, float spacing)
        {
            float k = 2.6f * radius / Mathf.Max(1e-3f, spacing);
            return Mathf.Max(1, Mathf.FloorToInt(k * k));
        }

        /// <summary>
        /// Where a den's <paramref name="index"/>-th prism sits, about the perch, with +y the perch's up: shells of radius
        /// <paramref name="firstRadius"/>, then one spacing further out each, every shell filled top-down along its own
        /// Fibonacci spiral. A prism never moves when more are added, and no two are closer than the spacing.
        /// </summary>
        public static Vector3 Local(int index, float firstRadius, float spacing)
        {
            float r = Mathf.Max(spacing, firstRadius);
            int start = 0;
            for (int guard = 0; guard < 64; guard++)
            {
                int cap = ShellCapacity(r, spacing);
                if (index < start + cap) return Fibonacci(index - start, cap) * r;
                start += cap;
                r += spacing;
            }
            return Vector3.up * r;
        }

        static Vector3 Fibonacci(int i, int n)
        {
            if (n <= 1) return Vector3.up;
            float y = 1f - 2f * (i + 0.5f) / n, rr = Mathf.Sqrt(Mathf.Max(0f, 1f - y * y)), a = i * Mathf.PI * (3f - Mathf.Sqrt(5f));
            return new Vector3(rr * Mathf.Cos(a), y, rr * Mathf.Sin(a));
        }
    }
}
