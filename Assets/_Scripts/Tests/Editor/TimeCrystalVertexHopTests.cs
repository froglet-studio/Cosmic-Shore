#if UNITY_EDITOR
using System.Collections.Generic;
using CosmicShore.Gameplay;
using CosmicShore.Utility;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The Time crystal's loop-wrap snap is invisible only if every rotation it can snap to is a
    /// symmetry of the shape. These tests pin the group itself (60 rotations, closed, preserving the
    /// right one of the icosahedron's two coordinate-aligned orientations) and then run the real
    /// resolver against the real imported rig - the one fact no offline check can reach, because it
    /// depends on how Unity's FBX importer signed the axes.
    ///
    /// The mesh-side facts (the shell is symmetric, the loop seam is the bind pose, the first ring's
    /// bones name the start vertex) are asserted from the FBX by
    /// <c>Tools/Build/measure_time_crystal_wave.py</c>.
    /// </summary>
    public class TimeCrystalVertexHopTests
    {
        const string PrefabPath = "Assets/_Prefabs/Environment/CrystalTime.prefab";
        const float Phi = 1.6180339887f;

        static readonly Vector3 AxisA = new Vector3(0f, 1f, Phi).normalized;   // (0, ±1, ±φ) orientation
        static readonly Vector3 AxisB = new Vector3(0f, Phi, 1f).normalized;   // (0, ±φ, ±1) orientation

        static List<Vector3> FiveFoldAxes(Vector3 seed)
        {
            var axes = new List<Vector3>();
            for (int sy = -1; sy <= 1; sy += 2)
            for (int sz = -1; sz <= 1; sz += 2)
            {
                var v = new Vector3(0f, sy * Mathf.Abs(seed.y), sz * Mathf.Abs(seed.z));
                axes.Add(v);
                axes.Add(new Vector3(v.z, v.x, v.y));
                axes.Add(new Vector3(v.y, v.z, v.x));
            }
            return axes;
        }

        static bool OnAnyAxis(Vector3 v, List<Vector3> axes)
        {
            foreach (var axis in axes)
                if (Vector3.Dot(v, axis) > 0.9999f) return true;
            return false;
        }

        static bool InGroup(Quaternion q, Quaternion[] group)
        {
            foreach (var g in group)
                if (Mathf.Abs(Quaternion.Dot(g, q)) > 0.9999f) return true;
            return false;
        }

        [Test]
        public void Group_IsSixtyDistinctRotations_ClosedUnderComposition([Values(0, 1)] int orientation)
        {
            var group = IcosahedralSymmetry.BuildRotationGroup(orientation == 0 ? AxisA : AxisB);
            Assert.IsNotNull(group, "a genuine five-fold axis must close to the icosahedral group");
            Assert.AreEqual(IcosahedralSymmetry.RotationCount, group.Length);

            for (int i = 0; i < group.Length; i++)
            for (int j = 0; j < group.Length; j++)
            {
                if (i != j)
                    Assert.Less(Mathf.Abs(Quaternion.Dot(group[i], group[j])), 0.9999f, $"elements {i} and {j} are the same rotation");
                Assert.IsTrue(InGroup(group[i] * group[j], group), $"element {i} * element {j} left the group");
            }
        }

        [Test]
        public void Group_PreservesItsOwnOrientation_AndNotTheOther()
        {
            var group = IcosahedralSymmetry.BuildRotationGroup(AxisA);
            var own = FiveFoldAxes(AxisA);
            var other = FiveFoldAxes(AxisB);

            bool movesOther = false;
            foreach (var g in group)
            {
                foreach (var axis in own)
                    Assert.IsTrue(OnAnyAxis(g * axis, own), "a group element moved a vertex off the shape - the snap would be visible");
                foreach (var axis in other)
                    movesOther |= !OnAnyAxis(g * axis, other);
            }
            Assert.IsTrue(movesOther,
                "the group built for one orientation also preserved the other - it is no longer being built from the measured axis");
        }

        [Test]
        public void EveryVertex_IsReachedByExactlyFiveRotations()
        {
            var group = IcosahedralSymmetry.BuildRotationGroup(AxisA);
            var axes = FiveFoldAxes(AxisA);
            Assert.AreEqual(IcosahedralSymmetry.FiveFoldAxisCount, axes.Count);
            foreach (var axis in axes)
            {
                int hits = 0;
                foreach (var g in group)
                    if (Vector3.Dot(g * AxisA, axis) > 0.9999f) hits++;
                Assert.AreEqual(5, hits, $"vertex {axis} is not reached uniformly");
            }
        }

        [Test]
        public void Snap_AcceptsAnAxis_AndRejectsADirectionBetweenTheOrientations()
        {
            Assert.IsTrue(IcosahedralSymmetry.TrySnapToFiveFoldAxis(AxisA * 3f, 2f, out var snapped));
            Assert.Greater(Vector3.Dot(snapped, AxisA), 0.99999f);

            Assert.IsTrue(IcosahedralSymmetry.TrySnapToFiveFoldAxis(AxisB, 2f, out snapped));
            Assert.Greater(Vector3.Dot(snapped, AxisB), 0.99999f, "an axis of the other orientation must snap to itself, not to its neighbour");

            var between = (AxisA + AxisB).normalized;   // 13.3° from both
            Assert.IsFalse(IcosahedralSymmetry.TrySnapToFiveFoldAxis(between, 2f, out _));
            Assert.IsFalse(IcosahedralSymmetry.TrySnapToFiveFoldAxis(Vector3.zero, 2f, out _));
        }

        [Test]
        public void Pick_AlwaysMovesTheWaveToAnotherVertex_AndReachesAllEleven()
        {
            var group = IcosahedralSymmetry.BuildRotationGroup(AxisA);
            var rng = new System.Random(1234);
            var seen = new HashSet<int>();
            var axes = FiveFoldAxes(AxisA);
            int current = 0;
            for (int i = 0; i < 2000; i++)
            {
                var before = group[current] * AxisA;
                int next = IcosahedralSymmetry.PickElementMovingAxis(group, AxisA, current, rng);
                var after = group[next] * AxisA;
                Assert.Less(Vector3.Dot(before, after), 0.9f, "the wave restarted from the vertex it just used");
                for (int a = 0; a < axes.Count; a++)
                    if (Vector3.Dot(after, axes[a]) > 0.9999f) seen.Add(a);
                current = next;
            }
            Assert.AreEqual(IcosahedralSymmetry.FiveFoldAxisCount, seen.Count, "some vertex is never chosen");
        }

        [Test]
        public void CrystalTimePrefab_ResolvesItsWaveAxis_FromTheImportedRig()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Assert.IsNotNull(prefab, $"{PrefabPath} not found");

            Assert.IsFalse(prefab.GetComponentInChildren<CosmicShore.UI.JustRotate>(true),
                "the Time crystal tumbles again - the vertex hop only reads as stillness if nothing else turns it");
            Assert.IsTrue(prefab.TryGetComponent(out TimeCrystalVertexHop hop), "CrystalTime has no TimeCrystalVertexHop");

            var serialized = new SerializedObject(hop);
            var model = serialized.FindProperty("model").objectReferenceValue as Transform;
            Assert.IsTrue(model, "TimeCrystalVertexHop.model is unassigned");
            Assert.IsTrue(model.GetComponentInChildren<Animator>(), "the model carries no Animator to hop on");

            var bonesProperty = serialized.FindProperty("leadBlockBones");
            var bones = new List<string>();
            for (int i = 0; i < bonesProperty.arraySize; i++) bones.Add(bonesProperty.GetArrayElementAtIndex(i).stringValue);

            Assert.IsTrue(TimeCrystalVertexHop.TryResolveLeadAxis(model, bones, out var axis, out var problem), problem);
            Assert.IsNotNull(IcosahedralSymmetry.BuildRotationGroup(axis), $"the resolved axis {axis} does not generate the group");
        }
    }
}
#endif
