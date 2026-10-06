using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Skim Race course as a closed polyline with arc-length parameterisation — the racing
    /// line the pilot follows between crystals.
    ///
    /// Built from the TRACK PRISMS THE GAME ACTUALLY LAID (<see cref="SkimRaceCourseSource.TryBuildFromScene"/>), in
    /// lay order, so it is the visible ribbon every pilot sees and skims, not a private copy of
    /// the authored waypoints that could drift from it. Each sample carries the ribbon's normal
    /// (the prism's up) so the line can be flown a fixed height above the ribbon: inside skimmer
    /// reach, clear of the hull.
    ///
    /// Pure geometry: no Unity object references are held after construction, so a destroyed
    /// track cannot leave a stale reference behind.
    /// </summary>
    public sealed class SkimRaceCourse
    {
        readonly Vector3[] _points;
        readonly Vector3[] _normals;
        readonly Quaternion[] _rotations;   // each prism's pose (identity frames when unknown)
        readonly Quaternion[] _invRotations; // cached inverses for ShellClearance (called per rollout step)
        readonly Vector3[] _shellHalf;      // each prism's contact-shell half-extents (zero = unknown)
        readonly Vector3[] _shellBox;       // _shellHalf clamped as StellaDistance clamps it: the box holding the stella
        readonly float[] _shellRadius;      // |_shellBox|: the sphere through the shell box's corners
        readonly float[] _cumulative; // _cumulative[i] = arc length from point 0 to point i
        readonly Vector3[] _segDir;   // _points[i + 1] - _points[i] (wrapping), for Project's segment test
        readonly float[] _segLen2;    // _segDir[i].sqrMagnitude
        readonly float _length;

        public int Count => _points.Length;
        public float Length => _length;
        public Vector3 PointAtIndex(int i) => _points[i];

        public bool HasShells { get; }

        public SkimRaceCourse(IReadOnlyList<Vector3> points, IReadOnlyList<Vector3> normals,
            IReadOnlyList<Quaternion> rotations = null, IReadOnlyList<Vector3> shellHalfExtents = null)
        {
            int n = points.Count;
            _points = new Vector3[n];
            _normals = new Vector3[n];
            _rotations = new Quaternion[n];
            _invRotations = new Quaternion[n];
            _shellHalf = new Vector3[n];
            _shellBox = new Vector3[n];
            _shellRadius = new float[n];
            HasShells = rotations != null && shellHalfExtents != null
                        && rotations.Count == n && shellHalfExtents.Count == n;
            for (int i = 0; i < n; i++)
            {
                _rotations[i] = HasShells ? rotations[i] : Quaternion.identity;
                _invRotations[i] = Quaternion.Inverse(_rotations[i]);
                _shellHalf[i] = HasShells ? shellHalfExtents[i] : Vector3.zero;
                Vector3 h = _shellHalf[i];
                _shellBox[i] = new Vector3(Mathf.Max(h.x, 1e-3f), Mathf.Max(h.y, 1e-3f), Mathf.Max(h.z, 1e-3f));
                _shellRadius[i] = _shellBox[i].magnitude;
            }
            _cumulative = new float[n + 1];
            for (int i = 0; i < n; i++)
            {
                _points[i] = points[i];
                Vector3 nrm = normals != null && i < normals.Count ? normals[i] : Vector3.up;
                _normals[i] = nrm.sqrMagnitude > 1e-6f ? nrm.normalized : Vector3.up;
            }
            for (int i = 0; i < n; i++)
                _cumulative[i + 1] = _cumulative[i] + Vector3.Distance(_points[i], _points[(i + 1) % n]);
            _segDir = new Vector3[n];
            _segLen2 = new float[n];
            for (int i = 0; i < n; i++)
            {
                _segDir[i] = _points[(i + 1) % n] - _points[i];
                _segLen2[i] = _segDir[i].sqrMagnitude;
            }
            _length = Mathf.Max(_cumulative[n], 1e-3f);
        }

        /// <summary>Wraps an arc length into [0, Length).</summary>
        public float Wrap(float s)
        {
            s %= _length;
            return s < 0f ? s + _length : s;
        }

        /// <summary>Forward arc distance from <paramref name="from"/> to <paramref name="to"/>, in [0, Length).</summary>
        public float Ahead(float from, float to) => Wrap(to - from);

        /// <summary>
        /// Closest point on the course. <paramref name="hint"/> is the segment found last time
        /// (-1 = search everything); the search widens to the full course if the windowed
        /// answer is not close, so a teleport-free but fast vessel can never lose the course.
        /// </summary>
        public float Project(Vector3 position, ref int hint, out Vector3 closest, out float distance)
        {
            int n = _points.Length;
            int bestSeg = -1;
            float bestSqr = float.MaxValue;
            float bestT = 0f;

            if (hint >= 0 && hint < n)
            {
                const int window = 24;
                // hint-window .. hint+window in order, wrapped; this runs every rollout step.
                int i = ((hint - window) % n + n) % n;
                for (int k = -window; k <= window; k++)
                {
                    TestSegment(i, position, ref bestSeg, ref bestSqr, ref bestT);
                    if (++i == n) i = 0;
                }
                // Accept the windowed answer only if it is genuinely close to the course.
                if (bestSqr > 150f * 150f) bestSeg = -1;
            }

            if (bestSeg < 0)
            {
                bestSqr = float.MaxValue;
                for (int i = 0; i < n; i++)
                    TestSegment(i, position, ref bestSeg, ref bestSqr, ref bestT);
            }

            hint = bestSeg;
            Vector3 a = _points[bestSeg];
            Vector3 b = _points[(bestSeg + 1) % n];
            closest = Vector3.Lerp(a, b, bestT);
            distance = Mathf.Sqrt(bestSqr);
            return _cumulative[bestSeg] + bestT * (_cumulative[bestSeg + 1] - _cumulative[bestSeg]);
        }

        // Written out in floats on purpose: ~49 of these run per projection, a projection runs every
        // rollout step, and the editor's Mono JIT pays for each Vector3 operator as a call and a
        // struct copy. The same float operations in the same order as the Vector3 form
        // t = Clamp01(Dot(p - a, ab) / len2), d2 = (a + ab * t - p).sqrMagnitude, so the same answer.
        void TestSegment(int i, Vector3 p, ref int bestSeg, ref float bestSqr, ref float bestT)
        {
            Vector3 a = _points[i];
            Vector3 ab = _segDir[i];   // b - a and its squared length, computed once at construction
            float len2 = _segLen2[i];
            float t = 0f;
            if (len2 > 1e-6f)
            {
                t = ((p.x - a.x) * ab.x + (p.y - a.y) * ab.y + (p.z - a.z) * ab.z) / len2;
                t = t < 0f ? 0f : t > 1f ? 1f : t; // Mathf.Clamp01
            }
            float dx = a.x + ab.x * t - p.x;
            float dy = a.y + ab.y * t - p.y;
            float dz = a.z + ab.z * t - p.z;
            float d2 = dx * dx + dy * dy + dz * dz;
            if (d2 < bestSqr) { bestSqr = d2; bestSeg = i; bestT = t; }
        }

        /// <summary>Point, unit tangent and ribbon normal at arc length <paramref name="s"/>.</summary>
        public Vector3 Sample(float s, out Vector3 tangent, out Vector3 normal)
        {
            s = Wrap(s);
            int n = _points.Length;
            int lo = 0, hi = n - 1;
            while (lo < hi)
            {
                int mid = (lo + hi + 1) >> 1;
                if (_cumulative[mid] <= s) lo = mid; else hi = mid - 1;
            }
            int i = lo;
            int j = (i + 1) % n;
            float segLen = _cumulative[i + 1] - _cumulative[i];
            float t = segLen > 1e-6f ? (s - _cumulative[i]) / segLen : 0f;
            Vector3 d = _points[j] - _points[i];
            tangent = d.sqrMagnitude > 1e-6f ? d.normalized : Vector3.forward;
            normal = Vector3.Slerp(_normals[i], _normals[j], t);
            return Vector3.Lerp(_points[i], _points[j], t);
        }

        /// <summary>
        /// Distance from <paramref name="position"/> to the nearest track prism's contact shell,
        /// searching <paramref name="window"/> prisms either side of segment <paramref name="hint"/>.
        /// Uses <see cref="SkimRaceShell.StellaDistance"/> (exact). Returns +inf without shells.
        /// On a tie <paramref name="nearest"/> is the prism furthest BEHIND the hint, as it always was.
        /// </summary>
        public float ShellClearance(Vector3 position, int hint, int window, out int nearest)
        {
            nearest = -1;
            if (!HasShells || hint < 0) return float.PositiveInfinity;
            int n = _points.Length;
            float best = float.PositiveInfinity;
            int bestK = 0;
            // This runs inside every rollout step of every planner, and its cost is paid in the game's
            // frame time, so the exact (8-triangle) stella test is spent only on a prism that could beat
            // the best so far. Two lower bounds rule a prism out first: the sphere through its box's
            // corners, then the box itself (the stella is inscribed in it, so the box is never further).
            // The hint segment is the nearest prism nearly always, so the search starts there and works
            // outward (0, -1, +1, -2, +2, ...) - the first exact test sets a best that the bounds then
            // prune the rest against. A prism is skipped only when a bound beats the best by
            // BoundSlack, far beyond the float rounding in either side, and ties are broken by offset
            // exactly as the old -window..+window scan broke them: the answer is unchanged.
            for (int j = 0; j <= 2 * window; j++)
            {
                int k = (j & 1) == 0 ? j >> 1 : -((j + 1) >> 1);
                int i = ((hint + k) % n + n) % n;
                // In floats, as TestSegment and for the same reason: d = position - point, its
                // sqrMagnitude, and local = inverseRotation * d by Unity's own Quaternion * Vector3.
                Vector3 pt = _points[i];
                float dx = position.x - pt.x, dy = position.y - pt.y, dz = position.z - pt.z;
                float d2 = dx * dx + dy * dy + dz * dz;
                if (d2 > 60f * 60f) continue;
                float cut = best + BoundSlack;
                if (Mathf.Sqrt(d2) - _shellRadius[i] > cut) continue;
                Quaternion q = _invRotations[i];
                float nx = q.x * 2f, ny = q.y * 2f, nz = q.z * 2f;
                float xx = q.x * nx, yy = q.y * ny, zz = q.z * nz;
                float xy = q.x * ny, xz = q.x * nz, yz = q.y * nz;
                float wx = q.w * nx, wy = q.w * ny, wz = q.w * nz;
                float lx = (1f - (yy + zz)) * dx + (xy - wz) * dy + (xz + wy) * dz;
                float ly = (xy + wz) * dx + (1f - (xx + zz)) * dy + (yz - wx) * dz;
                float lz = (xz - wy) * dx + (yz + wx) * dy + (1f - (xx + yy)) * dz;
                Vector3 box = _shellBox[i];
                float bx = Mathf.Max(Mathf.Abs(lx) - box.x, 0f);
                float by = Mathf.Max(Mathf.Abs(ly) - box.y, 0f);
                float bz = Mathf.Max(Mathf.Abs(lz) - box.z, 0f);
                if (bx * bx + by * by + bz * bz > cut * cut) continue;
                float c = SkimRaceShell.StellaDistance(lx, ly, lz, _shellHalf[i]);
                if (c < best || (c == best && k < bestK)) { best = c; bestK = k; nearest = i; }
            }
            return best;
        }

        // World units. The bounds and the exact distance each carry ~1e-5 of float rounding at
        // track scale; a prism must lose by this much before it is skipped.
        const float BoundSlack = 1e-2f;

        public Vector3 PrismUp(int i) => _rotations[i] * Vector3.up;
        public Vector3 PrismRight(int i) => _rotations[i] * Vector3.right;

        /// <summary>
        /// The largest heading change (degrees) the course makes between arc lengths
        /// <paramref name="from"/> and <paramref name="from"/> + <paramref name="span"/>, measured
        /// against the tangent at <paramref name="from"/>. The pilot reads this to brake for a
        /// corner it is about to enter.
        /// </summary>
        public float MaxTurnAhead(float from, float span, int samples = 8)
        {
            Sample(from, out Vector3 t0, out _);
            float worst = 0f;
            for (int k = 1; k <= samples; k++)
            {
                Sample(from + span * k / samples, out Vector3 tk, out _);
                worst = Mathf.Max(worst, Vector3.Angle(t0, tk));
            }
            return worst;
        }
    }
}
