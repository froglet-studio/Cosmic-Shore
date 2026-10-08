using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Utility
{
    /// <summary>
    /// One rigid plate of a crystal shell: a thin rhombic slab lying tangent to the shell, described by
    /// its centroid, its outward radial, its two in-plane diagonals and its reach (half the long one).
    /// All in one space - whichever space the caller measured it in.
    /// </summary>
    public readonly struct FlipPlate
    {
        public readonly Vector3 Centroid;
        public readonly Vector3 Radial;
        public readonly Vector3 LongDiagonal;
        public readonly Vector3 ShortDiagonal;
        public readonly float Reach;

        public FlipPlate(Vector3 centroid, Vector3 radial, Vector3 longDiagonal, Vector3 shortDiagonal, float reach)
        {
            Centroid = centroid;
            Radial = radial;
            LongDiagonal = longDiagonal;
            ShortDiagonal = shortDiagonal;
            Reach = reach;
        }
    }

    /// <summary>A plate's part in one wave: its ring, the signed axis it turns about, and the unit direction toward the start vertex.</summary>
    public readonly struct FlipStep
    {
        public readonly int Ring;
        public readonly Vector3 Axis;
        public readonly Vector3 Toward;

        public FlipStep(int ring, Vector3 axis, Vector3 toward)
        {
            Ring = ring;
            Axis = axis;
            Toward = toward;
        }
    }

    /// <summary>
    /// Where one ring is in its flip at one instant. <see cref="Flip"/> is the fraction of the 180° turn,
    /// <see cref="Scale"/> a uniform squash about the plate's centroid, and <see cref="Dip"/> (inward) and
    /// <see cref="Slide"/> (toward the start vertex) displace the centroid in units of the plate's reach.
    /// </summary>
    public readonly struct FlipSample : System.IEquatable<FlipSample>
    {
        public static readonly FlipSample Rest = new(0f, 1f, 0f, 0f);

        /// <summary>Equal to no sample, NaN included - marks a plate whose pose must be rewritten.</summary>
        public static readonly FlipSample Unwritten = new(float.NaN, float.NaN, float.NaN, float.NaN);

        public readonly float Flip;
        public readonly float Scale;
        public readonly float Dip;
        public readonly float Slide;

        public FlipSample(float flip, float scale, float dip, float slide)
        {
            Flip = flip;
            Scale = scale;
            Dip = dip;
            Slide = slide;
        }

        public bool Equals(FlipSample other) =>
            Flip == other.Flip && Scale == other.Scale && Dip == other.Dip && Slide == other.Slide;

        public override bool Equals(object obj) => obj is FlipSample other && Equals(other);

        public override int GetHashCode() => System.HashCode.Combine(Flip, Scale, Dip, Slide);
    }

    /// <summary>
    /// The flip wave, as math. A wave starts at a vertex of the crystal and runs to the opposite one; every
    /// plate turns 180° about one of its own diagonals, ring by ring. Three rules, each measured from the
    /// Time crystal's authored take by <c>Tools/Build/author_time_crystal_flip_wave.py</c>:
    /// <list type="number">
    /// <item>A plate's RING is the rank of its height along the start axis (the Time crystal's 30 plates
    /// fall into rings of 5 / 5 / 10 / 5 / 5).</item>
    /// <item>It turns about whichever of its two diagonals is more perpendicular to the wave's direction
    /// across it.</item>
    /// <item>It turns so its outer face rolls WITH the wave - away from the start vertex.</item>
    /// </list>
    /// A plate flipped 180° about a diagonal is the same plate, so a finished wave can snap back to rest
    /// and the next one can start from any vertex. Pure: no Unity objects, so tests and offline harnesses
    /// run exactly this code.
    /// </summary>
    public static class FlipWave
    {
        /// <summary>Two plates whose heights along the start axis differ by less than this share a ring.</summary>
        public const float RingHeightTolerance = 0.05f;

        /// <summary>
        /// Measures a plate from its vertices (duplicates allowed - an importer splits vertices along normal and
        /// UV seams, so corners are de-duplicated before the centroid is taken). Refuses a point set that is not
        /// a thin slab tangent to the shell.
        /// </summary>
        public static bool TryMeasurePlate(IReadOnlyList<Vector3> points, Vector3 shellCentre, out FlipPlate plate, out string problem)
        {
            plate = default;
            var corners = Distinct(points);
            if (corners.Count < 4)
            {
                problem = $"a plate has {corners.Count} distinct corners";
                return false;
            }

            var centroid = Vector3.zero;
            foreach (var c in corners) centroid += c;
            centroid /= corners.Count;

            var outward = centroid - shellCentre;
            if (outward.sqrMagnitude < 1e-12f)
            {
                problem = "a plate sits on the shell centre";
                return false;
            }
            var radial = outward.normalized;

            Vector3 far = Vector3.zero;
            float thickness = 0f;
            foreach (var c in corners)
            {
                var rel = c - centroid;
                float along = Vector3.Dot(rel, radial);
                thickness = Mathf.Max(thickness, Mathf.Abs(along));
                var flat = rel - along * radial;
                if (flat.sqrMagnitude > far.sqrMagnitude) far = flat;
            }

            float reach = far.magnitude;
            var longDiagonal = far / reach;
            var shortDiagonal = Vector3.Cross(radial, longDiagonal);
            float halfShort = 0f;
            foreach (var c in corners) halfShort = Mathf.Max(halfShort, Mathf.Abs(Vector3.Dot(c - centroid, shortDiagonal)));

            if (thickness > 0.25f * reach || halfShort < 0.1f * reach)
            {
                problem = $"a plate is not a thin slab tangent to the shell (reach {reach}, half-thickness {thickness}, half-short {halfShort})";
                return false;
            }

            plate = new FlipPlate(centroid, radial, longDiagonal, shortDiagonal, reach);
            problem = null;
            return true;
        }

        static List<Vector3> Distinct(IReadOnlyList<Vector3> points)
        {
            float scale = 0f;
            foreach (var p in points) scale = Mathf.Max(scale, Mathf.Abs(p.x), Mathf.Abs(p.y), Mathf.Abs(p.z));
            float epsilonSqr = (1e-5f * Mathf.Max(scale, 1e-6f)) * (1e-5f * Mathf.Max(scale, 1e-6f));

            var distinct = new List<Vector3>(8);
            foreach (var p in points)
            {
                bool seen = false;
                foreach (var d in distinct)
                    if ((d - p).sqrMagnitude <= epsilonSqr) { seen = true; break; }
                if (!seen) distinct.Add(p);
            }
            return distinct;
        }

        /// <summary>
        /// Each plate's ring, axis and toward-the-start direction for a wave from <paramref name="startAxis"/>
        /// (a unit vector from the shell centre through the start vertex). Rings are numbered from the start.
        /// </summary>
        public static FlipStep[] Plan(IReadOnlyList<FlipPlate> plates, Vector3 startAxis, out int ringCount)
        {
            var start = startAxis.normalized;
            var heights = new float[plates.Count];
            for (int i = 0; i < plates.Count; i++) heights[i] = Vector3.Dot(plates[i].Radial, start);

            var sorted = (float[])heights.Clone();
            System.Array.Sort(sorted);
            var levels = new List<float>();
            for (int i = sorted.Length - 1; i >= 0; i--)
                if (levels.Count == 0 || levels[levels.Count - 1] - sorted[i] > RingHeightTolerance)
                    levels.Add(sorted[i]);
            ringCount = levels.Count;

            var steps = new FlipStep[plates.Count];
            for (int i = 0; i < plates.Count; i++)
            {
                int ring = 0;
                for (int k = 1; k < levels.Count; k++)
                    if (Mathf.Abs(levels[k] - heights[i]) < Mathf.Abs(levels[ring] - heights[i])) ring = k;

                var plate = plates[i];
                var toward = (start - heights[i] * plate.Radial).normalized;
                var axis = Mathf.Abs(Vector3.Dot(plate.LongDiagonal, toward)) < Mathf.Abs(Vector3.Dot(plate.ShortDiagonal, toward))
                    ? plate.LongDiagonal
                    : plate.ShortDiagonal;
                // Turning by +angle about `axis` moves the outer face along cross(axis, radial); that must
                // point AWAY from the start vertex.
                if (Vector3.Dot(Vector3.Cross(axis, plate.Radial), toward) > 0f) axis = -axis;

                steps[i] = new FlipStep(ring, axis, toward);
            }
            return steps;
        }

        /// <summary>The rotation a sample applies to a plate.</summary>
        public static Quaternion Turn(in FlipStep step, in FlipSample sample) =>
            Quaternion.AngleAxis(180f * sample.Flip, step.Axis);

        /// <summary>Where a point of the plate (in the plate's space, at rest) is carried by a sample.</summary>
        public static Vector3 PosePoint(in FlipPlate plate, in FlipStep step, in FlipSample sample, Vector3 restPoint) =>
            Displacement(plate, step, sample) + plate.Centroid + sample.Scale * (Turn(step, sample) * (restPoint - plate.Centroid));

        /// <summary>How far a sample moves the plate's centroid: a dip inward plus a slide toward the start vertex.</summary>
        public static Vector3 Displacement(in FlipPlate plate, in FlipStep step, in FlipSample sample) =>
            plate.Reach * (sample.Slide * step.Toward - sample.Dip * plate.Radial);
    }
}
