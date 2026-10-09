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

        [Test]
        public void StellaDistance_FloatKernelMatchesTheVectorForm()
        {
            // The shell search's exact test is written out in floats for the editor's Mono JIT. It must
            // return what the Vector3 form returned, bit for bit, on every runtime - the editor's Mono
            // computes inside an expression in double precision, so the float form must round where the
            // Vector3 form does: inside (0), beside faces and edges, past spike tips, far off, and on
            // degenerate (clamped) extents.
            var rng = new System.Random(31);
            var halves = new[] { Plate, new Vector3(0.5f, 6f, 2f), new Vector3(20f, 0.2f, 12f), Vector3.zero, new Vector3(1e-4f, 3f, 0f) };
            for (int q = 0; q < 20000; q++)
            {
                Vector3 h = halves[q % halves.Length];
                Vector3 p = q % 4 == 0
                    ? Vector3.Scale(h, new Vector3(Range(rng, -1.1f, 1.1f), Range(rng, -1.1f, 1.1f), Range(rng, -1.1f, 1.1f)))
                    : new Vector3(Range(rng, -40f, 40f), Range(rng, -15f, 15f), Range(rng, -40f, 40f));
                float want = ReferenceStella(p, h);
                float got = SkimRaceShell.StellaDistance(p.x, p.y, p.z, h);
                Assert.IsTrue(SameBits(want, got), $"stella {got:R} != {want:R} at p=({p.x:R}, {p.y:R}, {p.z:R}) h={h}");
                Assert.IsTrue(SameBits(want, SkimRaceShell.StellaDistance(p, h)), "the Vector3 entry point");
            }
        }

        [Test]
        public void ObstacleLocalFrame_MatchesInverseRotationTimesOffset()
        {
            // The laid-mass guard measures each box through a precomputed SkimRaceObstacle.LocalFrame.
            // It must equal the form it replaced, Inverse(rotation) * (p - center), bit for bit - in the
            // editor's Mono too, which computes inside an expression in double precision (pre-rounded
            // 3x3 matrix terms passed every single-precision runtime and failed there).
            var rng = new System.Random(47);
            for (int q = 0; q < 20000; q++)
            {
                var box = new SkimRaceObstacle
                {
                    Center = new Vector3(Range(rng, -900f, 900f), Range(rng, -200f, 200f), Range(rng, -900f, 900f)),
                    Rotation = Quaternion.Euler(Range(rng, -180f, 180f), Range(rng, -180f, 180f), Range(rng, -180f, 180f)),
                    Half = new Vector3(Range(rng, 0f, 8f), Range(rng, 0f, 8f), Range(rng, 0f, 8f)),
                };
                Vector3 p = box.Center + new Vector3(Range(rng, -20f, 20f), Range(rng, -20f, 20f), Range(rng, -20f, 20f));
                Quaternion inv = Quaternion.Inverse(box.Rotation);
                Vector3 lp = inv * (p - box.Center);
                float x = Mathf.Max(Mathf.Abs(lp.x) - box.Half.x, 0f);
                float y = Mathf.Max(Mathf.Abs(lp.y) - box.Half.y, 0f);
                float z = Mathf.Max(Mathf.Abs(lp.z) - box.Half.z, 0f);
                float want = Mathf.Sqrt(x * x + y * y + z * z);
                float dx = (float)(p.x - box.Center.x), dy = (float)(p.y - box.Center.y), dz = (float)(p.z - box.Center.z); // as p - center's fields
                float got = new SkimRaceObstacle.LocalFrame(inv).Distance(dx, dy, dz, box.Half);
                Assert.IsTrue(SameBits(want, got), $"box {got:R} != {want:R} (case {q})");
                Assert.IsTrue(SameBits(want, box.Distance(p)), $"SkimRaceObstacle.Distance (case {q})");
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

        /// <summary>The stella distance as the Vector3 form computed it before the float kernel, verbatim.</summary>
        static float ReferenceStella(Vector3 local, Vector3 half)
        {
            half = new Vector3(Mathf.Max(half.x, 1e-3f), Mathf.Max(half.y, 1e-3f), Mathf.Max(half.z, 1e-3f));
            return Mathf.Min(ReferenceTetra(local, half, TetraA), ReferenceTetra(local, half, TetraB));
        }

        static readonly Vector3[] TetraA = { new(1, 1, 1), new(1, -1, -1), new(-1, 1, -1), new(-1, -1, 1) };
        static readonly Vector3[] TetraB = { new(-1, -1, -1), new(-1, 1, 1), new(1, -1, 1), new(1, 1, -1) };

        static float ReferenceTetra(Vector3 p, Vector3 h, Vector3[] v)
        {
            Vector3 u = new(p.x / h.x, p.y / h.y, p.z / h.z);
            bool inside = true;
            for (int k = 0; k < 4 && inside; k++)
                if (Vector3.Dot(v[k], u) < -1f) inside = false;
            if (inside) return 0f;
            Vector3 w0 = Vector3.Scale(v[0], h), w1 = Vector3.Scale(v[1], h), w2 = Vector3.Scale(v[2], h), w3 = Vector3.Scale(v[3], h);
            float best = (ReferenceClosest(p, w0, w1, w2) - p).sqrMagnitude;
            best = Mathf.Min(best, (ReferenceClosest(p, w0, w1, w3) - p).sqrMagnitude);
            best = Mathf.Min(best, (ReferenceClosest(p, w0, w2, w3) - p).sqrMagnitude);
            best = Mathf.Min(best, (ReferenceClosest(p, w1, w2, w3) - p).sqrMagnitude);
            return Mathf.Sqrt(best);
        }

        static Vector3 ReferenceClosest(Vector3 p, Vector3 a, Vector3 b, Vector3 c)
        {
            Vector3 ab = b - a, ac = c - a, ap = p - a;
            float d1 = Vector3.Dot(ab, ap), d2 = Vector3.Dot(ac, ap);
            if (d1 <= 0f && d2 <= 0f) return a;
            Vector3 bp = p - b;
            float d3 = Vector3.Dot(ab, bp), d4 = Vector3.Dot(ac, bp);
            if (d3 >= 0f && d4 <= d3) return b;
            float vc = d1 * d4 - d3 * d2;
            if (vc <= 0f && d1 >= 0f && d3 <= 0f) return a + d1 / (d1 - d3) * ab;
            Vector3 cp = p - c;
            float d5 = Vector3.Dot(ab, cp), d6 = Vector3.Dot(ac, cp);
            if (d6 >= 0f && d5 <= d6) return c;
            float vb = d5 * d2 - d1 * d6;
            if (vb <= 0f && d2 >= 0f && d6 <= 0f) return a + d2 / (d2 - d6) * ac;
            float va = d3 * d6 - d5 * d4;
            if (va <= 0f && d4 - d3 >= 0f && d5 - d6 >= 0f)
                return b + (d4 - d3) / ((d4 - d3) + (d5 - d6)) * (c - b);
            float denom = 1f / (va + vb + vc);
            return a + ab * (vb * denom) + ac * (vc * denom);
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
