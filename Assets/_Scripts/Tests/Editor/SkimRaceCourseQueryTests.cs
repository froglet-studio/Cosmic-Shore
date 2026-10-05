#if UNITY_EDITOR
using CosmicShore.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// <see cref="SkimRaceCourse.ShellClearance"/> and <see cref="SkimRaceCourse.Project"/> run inside every
    /// rollout step of the Skim Race pilot's planners, so both are written for speed: the shell search
    /// visits the hint prism first and rules prisms out with two lower bounds, and the projection reads
    /// precomputed segment vectors. Neither may change an answer. Each is checked here, bit for bit,
    /// against the plain definition it replaced - every prism in the window measured exactly, a tie going
    /// to the prism furthest behind the hint; every segment in the window tested with its vector built on
    /// the spot - over thousands of seeded points: around the ribbon, inside overlapping shells (exact
    /// 0-distance ties), on spike tips, and on a course short enough for the window to wrap onto itself.
    /// </summary>
    public class SkimRaceCourseQueryTests
    {
        static readonly Vector3 Plate = new(15f, 1.5f, 4.5f);

        [Test]
        public void ShellClearance_MatchesExhaustiveSearch()
        {
            var rng = new System.Random(20261005);
            foreach (int n in new[] { 60, 9 })
            {
                var course = BuildCourse(rng, n, out var points, out var rotations, out var halves);
                foreach (int window in new[] { 4, 6, 8, 10 })
                    for (int q = 0; q < 1500; q++)
                    {
                        Vector3 p = QueryPoint(rng, points, rotations, halves);
                        int hint = rng.Next(-1, n);
                        float got = course.ShellClearance(p, hint, window, out int gotNearest);
                        float want = ReferenceShellClearance(p, hint, window, points, rotations, halves, out int wantNearest);
                        string at = $"n={n} window={window} hint={hint} p=({p.x:R}, {p.y:R}, {p.z:R})";
                        Assert.IsTrue(SameBits(want, got), $"clearance {got:R} != {want:R} at {at}");
                        Assert.AreEqual(wantNearest, gotNearest, $"nearest at {at}");
                    }
            }
        }

        [Test]
        public void ShellClearance_TieInsideOverlappingShells_GoesToThePrismFurthestBehind()
        {
            // Twelve aligned plates 2 u apart along their 30 u long axis: prism 6's centre is inside
            // every shell within 7 prisms of it, so all seven in a window of 3 measure exactly 0.
            const int n = 12;
            var points = new Vector3[n];
            var normals = new Vector3[n];
            var rotations = new Quaternion[n];
            var halves = new Vector3[n];
            for (int i = 0; i < n; i++)
            {
                points[i] = new Vector3(2f * i, 0f, 0f);
                normals[i] = Vector3.up;
                rotations[i] = Quaternion.identity;
                halves[i] = Plate;
            }
            var course = new SkimRaceCourse(points, normals, rotations, halves);
            float c = course.ShellClearance(points[6], 6, 3, out int nearest);
            Assert.AreEqual(0f, c);
            Assert.AreEqual(3, nearest, "the -window..+window scan kept the first 0 it met: hint - window");
        }

        [Test]
        public void Project_MatchesThePlainWindowedSearch()
        {
            var rng = new System.Random(5);
            foreach (int n in new[] { 60, 9 })
            {
                var course = BuildCourse(rng, n, out var points, out var rotations, out var halves);
                for (int q = 0; q < 3000; q++)
                {
                    Vector3 p = rng.NextDouble() < 0.1
                        ? new Vector3(Range(rng, -900f, 900f), Range(rng, -900f, 900f), Range(rng, -900f, 900f)) // forces the full scan
                        : QueryPoint(rng, points, rotations, halves);
                    int hint = rng.Next(-1, n);
                    int gotHint = hint, wantHint = hint;
                    float got = course.Project(p, ref gotHint, out Vector3 gotClosest, out float gotDistance);
                    float want = ReferenceProject(points, p, ref wantHint, out Vector3 wantClosest, out float wantDistance);
                    string at = $"n={n} hint={hint} p=({p.x:R}, {p.y:R}, {p.z:R})";
                    Assert.IsTrue(SameBits(want, got), $"s {got:R} != {want:R} at {at}");
                    Assert.AreEqual(wantHint, gotHint, $"hint at {at}");
                    Assert.IsTrue(SameBits(wantDistance, gotDistance), $"distance at {at}");
                    Assert.IsTrue(SameBits(wantClosest.x, gotClosest.x) && SameBits(wantClosest.y, gotClosest.y)
                                  && SameBits(wantClosest.z, gotClosest.z), $"closest at {at}");
                }
            }
        }

        // ── The definitions ─────────────────────────────────────────────────────────

        static float ReferenceShellClearance(Vector3 position, int hint, int window, Vector3[] points,
            Quaternion[] rotations, Vector3[] halves, out int nearest)
        {
            nearest = -1;
            if (hint < 0) return float.PositiveInfinity;
            int n = points.Length;
            float best = float.PositiveInfinity;
            for (int k = -window; k <= window; k++)
            {
                int i = ((hint + k) % n + n) % n;
                Vector3 d = position - points[i];
                if (d.sqrMagnitude > 60f * 60f) continue;
                float c = SkimRaceShell.StellaDistance(Quaternion.Inverse(rotations[i]) * d, halves[i]);
                if (c < best) { best = c; nearest = i; }
            }
            return best;
        }

        static float ReferenceProject(Vector3[] points, Vector3 position, ref int hint,
            out Vector3 closest, out float distance)
        {
            int n = points.Length;
            int bestSeg = -1;
            float bestSqr = float.MaxValue, bestT = 0f;
            if (hint >= 0 && hint < n)
            {
                for (int k = -24; k <= 24; k++)
                    TestSegment(points, ((hint + k) % n + n) % n, position, ref bestSeg, ref bestSqr, ref bestT);
                if (bestSqr > 150f * 150f) bestSeg = -1;
            }
            if (bestSeg < 0)
            {
                bestSqr = float.MaxValue;
                for (int i = 0; i < n; i++)
                    TestSegment(points, i, position, ref bestSeg, ref bestSqr, ref bestT);
            }
            hint = bestSeg;
            closest = Vector3.Lerp(points[bestSeg], points[(bestSeg + 1) % n], bestT);
            distance = Mathf.Sqrt(bestSqr);
            // Arc length from the same expression the course's constructor tabulates.
            var cumulative = new float[n + 1];
            for (int i = 0; i < n; i++)
                cumulative[i + 1] = cumulative[i] + Vector3.Distance(points[i], points[(i + 1) % n]);
            return cumulative[bestSeg] + bestT * (cumulative[bestSeg + 1] - cumulative[bestSeg]);
        }

        static void TestSegment(Vector3[] points, int i, Vector3 p, ref int bestSeg, ref float bestSqr, ref float bestT)
        {
            Vector3 a = points[i];
            Vector3 b = points[(i + 1) % points.Length];
            Vector3 ab = b - a;
            float len2 = ab.sqrMagnitude;
            float t = len2 > 1e-6f ? Mathf.Clamp01(Vector3.Dot(p - a, ab) / len2) : 0f;
            float d2 = (a + ab * t - p).sqrMagnitude;
            if (d2 < bestSqr) { bestSqr = d2; bestSeg = i; bestT = t; }
        }

        // ── Fixtures ────────────────────────────────────────────────────────────────

        /// <summary>A wavy closed ribbon of plates ~10 u apart, so neighbouring 30 u shells overlap.
        /// One plate in seven gets random extents, and one is degenerate (zero extents).</summary>
        static SkimRaceCourse BuildCourse(System.Random rng, int n, out Vector3[] points, out Quaternion[] rotations,
            out Vector3[] halves)
        {
            points = new Vector3[n];
            rotations = new Quaternion[n];
            halves = new Vector3[n];
            var normals = new Vector3[n];
            float radius = n * 10f / (2f * Mathf.PI);
            for (int i = 0; i < n; i++)
            {
                float a = 2f * Mathf.PI * i / n;
                points[i] = new Vector3(radius * Mathf.Cos(a), 12f * Mathf.Sin(3f * a), radius * Mathf.Sin(a))
                            + new Vector3(Range(rng, -2f, 2f), Range(rng, -2f, 2f), Range(rng, -2f, 2f));
                rotations[i] = Quaternion.Euler(Range(rng, -40f, 40f), Range(rng, 0f, 360f), Range(rng, -40f, 40f));
                halves[i] = i % 7 == 3 ? new Vector3(Range(rng, 0.5f, 20f), Range(rng, 0.2f, 6f), Range(rng, 0.5f, 12f)) : Plate;
                normals[i] = rotations[i] * Vector3.up;
            }
            halves[n / 2] = Vector3.zero;
            return new SkimRaceCourse(points, normals, rotations, halves);
        }

        /// <summary>Near the ribbon (a hull's working range), on a prism's centre, or just past a spike tip.</summary>
        static Vector3 QueryPoint(System.Random rng, Vector3[] points, Quaternion[] rotations, Vector3[] halves)
        {
            int i = rng.Next(points.Length);
            double kind = rng.NextDouble();
            if (kind < 0.15) return points[i];
            if (kind < 0.30)
            {
                Vector3 corner = new(rng.Next(2) == 0 ? -halves[i].x : halves[i].x,
                                     rng.Next(2) == 0 ? -halves[i].y : halves[i].y,
                                     rng.Next(2) == 0 ? -halves[i].z : halves[i].z);
                return points[i] + rotations[i] * (corner * Range(rng, 0.98f, 1.05f));
            }
            return points[i] + new Vector3(Range(rng, -30f, 30f), Range(rng, -12f, 12f), Range(rng, -30f, 30f));
        }

        static float Range(System.Random rng, float lo, float hi) => lo + (float)rng.NextDouble() * (hi - lo);

        static bool SameBits(float a, float b) => a == b ? a != 0f || (1f / a == 1f / b) : float.IsNaN(a) && float.IsNaN(b);
    }
}
#endif
