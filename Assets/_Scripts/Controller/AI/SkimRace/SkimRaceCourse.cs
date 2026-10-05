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
        readonly float[] _shellRadius;      // |_shellHalf|: the sphere through the shell box's corners
        readonly float[] _cumulative; // _cumulative[i] = arc length from point 0 to point i
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
            _shellRadius = new float[n];
            HasShells = rotations != null && shellHalfExtents != null
                        && rotations.Count == n && shellHalfExtents.Count == n;
            for (int i = 0; i < n; i++)
            {
                _rotations[i] = HasShells ? rotations[i] : Quaternion.identity;
                _invRotations[i] = Quaternion.Inverse(_rotations[i]);
                _shellHalf[i] = HasShells ? shellHalfExtents[i] : Vector3.zero;
                _shellRadius[i] = _shellHalf[i].magnitude;
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
                // The window is walked with a wrap-around index, not a remainder per segment: the same
                // segments in the same order (so the same answer, ties included) without ~100 integer
                // divisions per call - this runs at every MPC rollout step.
                int i = ((hint - window) % n + n) % n;
                for (int k = -window; k <= window; k++, i = i + 1 == n ? 0 : i + 1)
                    TestSegment(i, i + 1 == n ? 0 : i + 1, position, ref bestSeg, ref bestSqr, ref bestT);
                // Accept the windowed answer only if it is genuinely close to the course.
                if (bestSqr > 150f * 150f) bestSeg = -1;
            }

            if (bestSeg < 0)
            {
                bestSqr = float.MaxValue;
                for (int i = 0; i < n; i++)
                    TestSegment(i, i + 1 == n ? 0 : i + 1, position, ref bestSeg, ref bestSqr, ref bestT);
            }

            hint = bestSeg;
            Vector3 a = _points[bestSeg];
            Vector3 b = _points[(bestSeg + 1) % n];
            closest = Vector3.Lerp(a, b, bestT);
            distance = Mathf.Sqrt(bestSqr);
            return _cumulative[bestSeg] + bestT * (_cumulative[bestSeg + 1] - _cumulative[bestSeg]);
        }

        void TestSegment(int i, int next, Vector3 p, ref int bestSeg, ref float bestSqr, ref float bestT)
        {
            Vector3 a = _points[i];
            Vector3 b = _points[next];
            Vector3 ab = b - a;
            float len2 = ab.sqrMagnitude;
            float t = len2 > 1e-6f ? Mathf.Clamp01(Vector3.Dot(p - a, ab) / len2) : 0f;
            float d2 = (a + ab * t - p).sqrMagnitude;
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
        /// </summary>
        public float ShellClearance(Vector3 position, int hint, int window, out int nearest)
        {
            nearest = -1;
            if (!HasShells || hint < 0) return float.PositiveInfinity;
            int n = _points.Length;
            float best = float.PositiveInfinity;
            // Wrap-around index, as in Project (the same prisms in the same order).
            int i = ((hint - window) % n + n) % n;
            for (int k = -window; k <= window; k++, i = i + 1 == n ? 0 : i + 1)
            {
                Vector3 d = position - _points[i];
                float d2 = d.sqrMagnitude;
                if (d2 > 60f * 60f) continue;
                // The stella lies inside the sphere through its box corners, so |d| - that radius is a
                // lower bound on its distance: a prism that cannot beat the best so far is skipped
                // without the exact (8-triangle) test. Same answer, a fraction of the cost - this runs
                // inside every MPC rollout step, and its cost is paid in the game's frame time.
                float lower = Mathf.Sqrt(d2) - _shellRadius[i];
                if (lower >= best) continue;
                float c = SkimRaceShell.StellaDistance(_invRotations[i] * d, _shellHalf[i]);
                if (c < best) { best = c; nearest = i; }
            }
            return best;
        }

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
