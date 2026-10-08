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
    /// A shape with that symmetry is CONGRUENT under every one of these rotations, so snapping its
    /// transform to any of them in a single frame is invisible - only something riding on the shape
    /// that is NOT symmetric (an animation that starts at one vertex) visibly moves. That is what
    /// <see cref="CosmicShore.Gameplay.TimeCrystalVertexHop"/> uses it for.
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

        // Distinct five-fold axes meet at 63.4° (dot ±0.447) or are antipodal (dot -1).
        const float SameAxisDot = 0.9f;

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
        /// A uniformly random element of <paramref name="group"/> that carries
        /// <paramref name="axis"/> to a DIFFERENT five-fold axis than element
        /// <paramref name="current"/> does - i.e. one of the 11 other vertices, each equally likely.
        /// </summary>
        public static int PickElementMovingAxis(Quaternion[] group, Vector3 axis, int current, System.Random rng)
        {
            var currentPole = group[current] * axis;
            // 55 of the 60 elements qualify, so rejection sampling almost never needs a second try.
            for (int attempt = 0; attempt < 32; attempt++)
            {
                int candidate = rng.Next(group.Length);
                if (Vector3.Dot(group[candidate] * axis, currentPole) < SameAxisDot) return candidate;
            }
            for (int candidate = 0; candidate < group.Length; candidate++)
                if (Vector3.Dot(group[candidate] * axis, currentPole) < SameAxisDot) return candidate;
            return current;
        }
    }
}
