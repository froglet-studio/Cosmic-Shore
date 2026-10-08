using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Utility
{
    /// <summary>
    /// The 60 rotations of the icosahedral group, for a model whose two-fold axes lie along its
    /// local coordinate axes (true of anything built on the standard icosahedron / dodecahedron /
    /// icosidodecahedron coordinates, and preserved by Unity's FBX axis conversion, which only
    /// permutes and negates axes).
    ///
    /// A shape with that symmetry is CONGRUENT under every one of these rotations, so a wave that
    /// runs across it from one five-fold vertex looks the same started from any of the twelve. That is
    /// what <see cref="FlipWaveRig"/> resolves the frame for: it finds the twelve start vertices of a
    /// crystal whose plates sit on the two-fold axes (<see cref="TryResolveFromTwoFoldDirections"/>).
    ///
    /// Two orientations of the icosahedron share coordinate two-fold axes: one with its 12 five-fold
    /// axes at the cyclic permutations of (0, ±1, ±φ), the other at (0, ±φ, ±1). They differ by a 90°
    /// turn, and the rotations of one are not symmetries of the other, so the group is always built
    /// from a five-fold axis the caller MEASURED - never from an assumed orientation.
    /// </summary>
    public static class IcosahedralSymmetry
    {
        public const int RotationCount = 60;

        /// <summary>Five-fold axes per orientation (the 12 vertices of the icosahedron).</summary>
        public const int FiveFoldAxisCount = 12;

        const float Phi = 1.6180339887f;

        // Two group elements are the same rotation when their quaternions agree up to sign. The
        // smallest rotation in the group is 72°, i.e. |dot| = cos 36° = 0.81 between DISTINCT
        // elements, so this threshold sits far from both the real duplicates and the real neighbours.
        const float SameRotationDot = 0.9999f;

        /// <summary>
        /// The angle between a two-fold axis and each of its two nearest five-fold axes: acos(φ / √(1 + φ²)).
        /// A plate centred on a two-fold axis therefore sits this far from the two vertices it lies between.
        /// </summary>
        public const float TwoFoldToFiveFoldDegrees = 31.7175f;

        // Five-fold axes of ONE orientation meet at 63.43° (|dot| 1/√5) or are antipodal (|dot| 1); the other
        // orientation's nearest axes sit 26.57° away (|dot| 0.894), so this tolerance separates the families.
        const float SameFamilyDotTolerance = 0.01f;
        static readonly float InverseSqrt5 = 1f / Mathf.Sqrt(5f);

        static readonly Vector3[] FiveFoldCandidates = BuildFiveFoldCandidates();

        /// <summary>
        /// The 24 unit directions that are a five-fold axis of EITHER orientation: the cyclic
        /// permutations of (0, ±1, ±φ) and of (0, ±φ, ±1).
        /// </summary>
        static Vector3[] BuildFiveFoldCandidates()
        {
            var candidates = new List<Vector3>(2 * FiveFoldAxisCount);
            foreach (var (a, b) in new[] { (1f, Phi), (Phi, 1f) })
            for (int sa = -1; sa <= 1; sa += 2)
            for (int sb = -1; sb <= 1; sb += 2)
            {
                var v = new Vector3(0f, sa * a, sb * b).normalized;
                candidates.Add(v);
                candidates.Add(new Vector3(v.z, v.x, v.y));
                candidates.Add(new Vector3(v.y, v.z, v.x));
            }
            return candidates.ToArray();
        }

        /// <summary>
        /// Snaps a measured direction onto the nearest five-fold axis of either orientation.
        /// Returns false when nothing lies within <paramref name="toleranceDegrees"/> - i.e. the
        /// measurement does not describe an icosahedral frame aligned to the coordinate axes.
        /// The two orientations' nearest axes are 26.6° apart, so a tolerance well under 13° also
        /// decides WHICH orientation the frame is.
        /// </summary>
        public static bool TrySnapToFiveFoldAxis(Vector3 direction, float toleranceDegrees, out Vector3 axis)
        {
            axis = default;
            if (direction.sqrMagnitude < 1e-12f) return false;

            var dir = direction.normalized;
            float best = -2f;
            foreach (var candidate in FiveFoldCandidates)
            {
                float dot = Vector3.Dot(dir, candidate);
                if (dot <= best) continue;
                best = dot;
                axis = candidate;
            }
            return best >= Mathf.Cos(toleranceDegrees * Mathf.Deg2Rad);
        }

        /// <summary>
        /// All 60 rotations of the icosahedral group containing a 72° turn about
        /// <paramref name="fiveFoldAxis"/> (which must be one of the snapped candidates), identity
        /// first. Returns null if the closure is not exactly 60 rotations - which only happens for an
        /// axis that is not a five-fold axis of a coordinate-aligned frame.
        /// </summary>
        public static Quaternion[] BuildRotationGroup(Vector3 fiveFoldAxis)
        {
            var a = fiveFoldAxis.normalized;
            // A cyclic permutation of a five-fold axis is another five-fold axis of the SAME
            // orientation, and never ±a. Two five-fold turns about distinct axes generate the group.
            var b = new Vector3(a.z, a.x, a.y);
            var generators = new[] { Quaternion.AngleAxis(72f, a), Quaternion.AngleAxis(72f, b) };

            var group = new List<Quaternion>(RotationCount) { Quaternion.identity };
            for (int i = 0; i < group.Count && group.Count <= RotationCount; i++)
            foreach (var generator in generators)
            {
                var q = Quaternion.Normalize(generator * group[i]);
                if (!Contains(group, q)) group.Add(q);
            }
            return group.Count == RotationCount ? group.ToArray() : null;
        }

        static bool Contains(List<Quaternion> group, Quaternion q)
        {
            foreach (var element in group)
                if (Mathf.Abs(Quaternion.Dot(element, q)) > SameRotationDot) return true;
            return false;
        }

        /// <summary>
        /// The twelve five-fold axes of the orientation that contains <paramref name="fiveFoldAxis"/> (which
        /// must be one of the snapped candidates).
        /// </summary>
        public static Vector3[] FiveFoldAxesOf(Vector3 fiveFoldAxis)
        {
            var a = fiveFoldAxis.normalized;
            var axes = new List<Vector3>(FiveFoldAxisCount);
            foreach (var candidate in FiveFoldCandidates)
            {
                float dot = Mathf.Abs(Vector3.Dot(a, candidate));
                if (Mathf.Abs(dot - 1f) < SameFamilyDotTolerance || Mathf.Abs(dot - InverseSqrt5) < SameFamilyDotTolerance)
                    axes.Add(candidate);
            }
            return axes.ToArray();
        }

        /// <summary>
        /// The twelve five-fold axes of the coordinate-aligned icosahedral frame whose TWO-fold axes the
        /// given directions lie on - e.g. the outward directions of a crystal's 30 plates. Each direction must
        /// sit <see cref="TwoFoldToFiveFoldDegrees"/> from exactly two five-fold axes of the chosen orientation
        /// (within <paramref name="toleranceDegrees"/>), and exactly one of the two orientations may qualify.
        /// That decides the orientation from the geometry itself, with no assumption about how an importer
        /// signed the axes. Returns false, with the reason, when the directions do not describe such a frame.
        /// </summary>
        public static bool TryResolveFromTwoFoldDirections(IReadOnlyList<Vector3> directions, float toleranceDegrees,
                                                           out Vector3[] fiveFoldAxes, out string problem)
        {
            fiveFoldAxes = null;
            if (directions == null || directions.Count == 0)
            {
                problem = "no directions to resolve a frame from";
                return false;
            }

            float lo = Mathf.Cos((TwoFoldToFiveFoldDegrees + toleranceDegrees) * Mathf.Deg2Rad);
            float hi = Mathf.Cos((TwoFoldToFiveFoldDegrees - toleranceDegrees) * Mathf.Deg2Rad);
            Vector3[] found = null;
            int qualifying = 0;
            for (int family = 0; family < 2; family++)
            {
                var axes = FiveFoldAxesOf(FiveFoldCandidates[family * FiveFoldAxisCount]);
                bool fits = true;
                foreach (var direction in directions)
                {
                    var d = direction.normalized;
                    int near = 0;
                    foreach (var axis in axes)
                    {
                        float dot = Vector3.Dot(d, axis);
                        if (dot >= lo && dot <= hi) near++;
                    }
                    if (near != 2) { fits = false; break; }
                }
                if (!fits) continue;
                qualifying++;
                found = axes;
            }

            if (qualifying != 1)
            {
                problem = qualifying == 0
                    ? $"the {directions.Count} directions do not lie on the two-fold axes of a coordinate-aligned icosahedral frame (within {toleranceDegrees}°)"
                    : "the directions fit BOTH icosahedral orientations - they cannot decide the frame";
                return false;
            }
            fiveFoldAxes = found;
            problem = null;
            return true;
        }
    }
}
