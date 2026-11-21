using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Utility.AITraining
{
    /// <summary>
    /// The people's recorded lines of the pair being trained: the paths (world positions, resampled) of their fastest
    /// editor races, as Froglet Compute's <c>people_seed.py</c> writes them into the run's seed file and the trainer
    /// entry copies onto <see cref="TrainingSessionStateSO"/> (<c>PeopleLinePoints</c> / <c>PeopleLineStarts</c>,
    /// published here by the runner's Configure). <see cref="PeopleLineFitness"/> reads <see cref="Distance"/>.
    /// Pure: a static store and a nearest-point search over polylines; <c>PeopleLineTests</c> pins it.
    /// </summary>
    public static class PeopleLine
    {
        static readonly List<Vector3[]> s_lines = new();

        public static bool HasLines => s_lines.Count > 0;
        public static int LineCount => s_lines.Count;

        /// <summary>Polylines from a flat point list and the index each line starts at (the seed file's shape).</summary>
        public static void Set(IReadOnlyList<Vector3> points, IReadOnlyList<int> starts)
        {
            s_lines.Clear();
            if (points == null || points.Count == 0) return;
            var bounds = new List<int>(starts ?? new int[0]);
            if (bounds.Count == 0 || bounds[0] != 0) bounds.Insert(0, 0);
            for (int i = 0; i < bounds.Count; i++)
            {
                int from = Mathf.Clamp(bounds[i], 0, points.Count);
                int to = i + 1 < bounds.Count ? Mathf.Clamp(bounds[i + 1], from, points.Count) : points.Count;
                if (to - from < 2) continue;
                var line = new Vector3[to - from];
                for (int k = from; k < to; k++) line[k - from] = points[k];
                s_lines.Add(line);
            }
        }

        public static void Clear() => s_lines.Clear();

        /// <summary>The distance from <paramref name="p"/> to the nearest segment of any line; 0 with no line.</summary>
        public static float Distance(Vector3 p)
        {
            if (s_lines.Count == 0) return 0f;
            float best = float.PositiveInfinity;
            for (int l = 0; l < s_lines.Count; l++)
            {
                var line = s_lines[l];
                for (int i = 0; i + 1 < line.Length; i++)
                {
                    float d = SegmentDistance(p, line[i], line[i + 1]);
                    if (d < best) best = d;
                }
            }
            return best;
        }

        public static float SegmentDistance(Vector3 p, Vector3 a, Vector3 b)
        {
            var ab = b - a;
            float len2 = ab.sqrMagnitude;
            float t = len2 > 1e-6f ? Mathf.Clamp01(Vector3.Dot(p - a, ab) / len2) : 0f;
            return Vector3.Distance(p, a + ab * t);
        }
    }
}
