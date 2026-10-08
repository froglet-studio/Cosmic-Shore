#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using CosmicShore.Gameplay;
using CosmicShore.Utility;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The Time crystal's procedural flip wave (<see cref="CrystalFlipWave"/>) has to look exactly like the
    /// artist's take it replaced, from whichever of the twelve vertices it starts. These tests pin the frame
    /// math, the wave plan, the prefab wiring, and - the one that matters - PARITY: the shipped rig and profile
    /// posing the real imported model, against Unity's own import of the take, frame by frame.
    ///
    /// The source-side facts (the take's rings, cadence, axes and the fitted profile) are asserted from the FBX
    /// by <c>Tools/Build/author_time_crystal_flip_wave.py</c>, which also generates the profile asset.
    /// </summary>
    public class CrystalFlipWaveTests
    {
        const string PrefabPath = "Assets/_Prefabs/Environment/CrystalTime.prefab";
        const string FbxPath = "Assets/_Models/TimeCrystalExport.fbx";
        const string ProfilePath = "Assets/_SO_Assets/Environment/TimeCrystalFlipWaveProfile.asset";
        const string OmniPrefabPath = "Assets/_Prefabs/Environment/Crystal.prefab";
        const string TakeName = "TimeCrystalArmature|TimeSequenceAnimFinal.001";
        const int TakeFrames = 51;
        const float TakeFps = 25f;

        /// <summary>
        /// Worst vertex error, as a fraction of the shell radius, the procedural wave may show against the
        /// imported take. Measured offline against the raw FBX at 0.42% (the source's own Euler-key noise);
        /// Unity's keyframe reduction (0.5° rotation error on this model's importer) can add about that again.
        /// </summary>
        const float ParityTolerance = 0.015f;

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

        /// <summary>The 30 two-fold axes of an orientation: the midpoints of its 30 pairs of neighbouring vertices.</summary>
        static List<Vector3> TwoFoldAxes(Vector3 seed)
        {
            var five = FiveFoldAxes(seed);
            var two = new List<Vector3>();
            for (int i = 0; i < five.Count; i++)
            for (int j = i + 1; j < five.Count; j++)
                if (Vector3.Dot(five[i], five[j]) > 0.4f) two.Add((five[i] + five[j]).normalized);
            return two;
        }

        /// <summary>A rhombic triacontahedron's faces as flip plates: long diagonal between the two vertices a face spans.</summary>
        static List<FlipPlate> TriacontahedronPlates(Vector3 seed)
        {
            var five = FiveFoldAxes(seed);
            var plates = new List<FlipPlate>();
            for (int i = 0; i < five.Count; i++)
            for (int j = i + 1; j < five.Count; j++)
            {
                if (Vector3.Dot(five[i], five[j]) < 0.4f) continue;
                var radial = (five[i] + five[j]).normalized;
                var along = five[i] - five[j];
                var longDiagonal = (along - Vector3.Dot(along, radial) * radial).normalized;
                plates.Add(new FlipPlate(radial, radial, longDiagonal, Vector3.Cross(radial, longDiagonal), 0.3f));
            }
            return plates;
        }

        static bool OnAnyAxis(Vector3 v, IEnumerable<Vector3> axes) => axes.Any(axis => Vector3.Dot(v, axis) > 0.9999f);

        static bool InGroup(Quaternion q, Quaternion[] group) => group.Any(g => Mathf.Abs(Quaternion.Dot(g, q)) > 0.9999f);

        // ------------------------------------------------------------------ the frame

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
        public void FiveFoldAxesOf_ReturnsExactlyItsOwnOrientationsTwelve([Values(0, 1)] int orientation)
        {
            var seed = orientation == 0 ? AxisA : AxisB;
            var axes = IcosahedralSymmetry.FiveFoldAxesOf(seed);
            Assert.AreEqual(IcosahedralSymmetry.FiveFoldAxisCount, axes.Length);
            var own = FiveFoldAxes(seed);
            foreach (var axis in axes) Assert.IsTrue(OnAnyAxis(axis, own), $"{axis} belongs to the other orientation");
        }

        [Test]
        public void Frame_ResolvesFromTwoFoldDirections_AndDecidesTheOrientation([Values(0, 1)] int orientation)
        {
            var seed = orientation == 0 ? AxisA : AxisB;
            var directions = TwoFoldAxes(seed);
            Assert.AreEqual(30, directions.Count);
            Assert.IsTrue(IcosahedralSymmetry.TryResolveFromTwoFoldDirections(directions, 2f, out var axes, out var problem), problem);
            var own = FiveFoldAxes(seed);
            Assert.AreEqual(12, axes.Length);
            foreach (var axis in axes) Assert.IsTrue(OnAnyAxis(axis, own), "the frame resolved to the wrong orientation");
        }

        [Test]
        public void Frame_RefusesDirectionsOffTheCoordinateFrame()
        {
            var tilt = Quaternion.AngleAxis(5f, new Vector3(0.3f, 1f, 0.2f));
            var tilted = TwoFoldAxes(AxisA).Select(d => tilt * d).ToList();
            Assert.IsFalse(IcosahedralSymmetry.TryResolveFromTwoFoldDirections(tilted, 2f, out _, out var problem));
            Assert.IsNotNull(problem);
        }

        // ------------------------------------------------------------------ the plan

        [Test]
        public void Plan_FromEveryVertex_IsRingsOfFiveFiveTenFiveFive_RollingWithTheWave()
        {
            var plates = TriacontahedronPlates(AxisA);
            foreach (var start in FiveFoldAxes(AxisA))
            {
                var steps = FlipWave.Plan(plates, start, out int rings);
                Assert.AreEqual(5, rings);
                var counts = Enumerable.Range(0, rings).Select(r => steps.Count(s => s.Ring == r)).ToArray();
                CollectionAssert.AreEqual(new[] { 5, 5, 10, 5, 5 }, counts, $"rings from {start}");

                for (int i = 0; i < plates.Count; i++)
                {
                    var plate = plates[i];
                    var step = steps[i];
                    Assert.AreEqual(1f, step.Axis.magnitude, 1e-4f);
                    Assert.AreEqual(0f, Vector3.Dot(step.Axis, plate.Radial), 1e-4f, "a plate must turn about an in-plane diagonal");
                    Assert.Less(Vector3.Dot(Vector3.Cross(step.Axis, plate.Radial), step.Toward), 0f,
                        "a plate's outer face must roll WITH the wave, away from the start vertex");
                    float chosen = Mathf.Abs(Vector3.Dot(step.Axis, step.Toward));
                    float other = Mathf.Abs(Vector3.Dot(Vector3.Cross(plate.Radial, step.Axis), step.Toward));
                    Assert.LessOrEqual(chosen, other + 1e-4f, "a plate must turn about its diagonal MORE perpendicular to the wave");
                    Assert.AreEqual(0f, Vector3.Dot(step.Toward, plate.Radial), 1e-4f, "Toward must be tangent to the shell");
                    Assert.Greater(Vector3.Dot(step.Toward, start), 0f, "Toward must point at the start vertex");
                }
                Assert.Greater(Vector3.Dot(plates[steps.ToList().FindIndex(s => s.Ring == 0)].Radial, start), 0.8f, "ring 0 must sit round the start vertex");
            }
        }

        [Test]
        public void Pose_IsRigid_AndAFullFlipIsTheSameSlab()
        {
            var plate = TriacontahedronPlates(AxisA)[0];
            var step = FlipWave.Plan(TriacontahedronPlates(AxisA), AxisA, out _)[0];
            var corners = new[]
            {
                plate.Centroid + 0.3f * plate.LongDiagonal, plate.Centroid - 0.3f * plate.LongDiagonal,
                plate.Centroid + 0.18f * plate.ShortDiagonal, plate.Centroid - 0.18f * plate.ShortDiagonal,
            };
            var half = new FlipSample(0.5f, 1f, 0f, 0f);
            var posed = corners.Select(c => FlipWave.PosePoint(plate, step, half, c)).ToArray();
            for (int i = 0; i < corners.Length; i++)
            for (int j = i + 1; j < corners.Length; j++)
                Assert.AreEqual((corners[i] - corners[j]).magnitude, (posed[i] - posed[j]).magnitude, 1e-5f, "a pure flip must be rigid");

            var full = new FlipSample(1f, 1f, 0f, 0f);
            foreach (var c in corners)
            {
                var flipped = FlipWave.PosePoint(plate, step, full, c);
                Assert.IsTrue(corners.Any(o => (o - flipped).magnitude < 1e-5f), "a 180° flip about a diagonal must land on the plate's own corners");
            }
        }

        // ------------------------------------------------------------------ the profile

        [Test]
        public void Profile_EveryRingStartsAtRest_AndEndsTheLoopFlipped()
        {
            var profile = AssetDatabase.LoadAssetAtPath<FlipWaveProfileSO>(ProfilePath);
            Assert.IsTrue(profile, $"{ProfilePath} not found");
            for (int ring = 0; ring < 5; ring++)
            {
                var first = profile.Sample(ring, 0f);
                Assert.AreEqual(0f, first.Flip, 1e-5f, $"ring {ring} is not at rest when the wave starts");
                Assert.AreEqual(1f, first.Scale, 1e-5f);
                var last = profile.Sample(ring, profile.LoopSeconds);
                Assert.AreEqual(1f, last.Flip, 1e-5f, $"ring {ring} has not finished its flip when the loop wraps - the reset to rest would show");
                Assert.AreEqual(1f, last.Scale, 1e-4f);
                Assert.AreEqual(0f, last.Dip, 1e-4f);
                Assert.AreEqual(0f, last.Slide, 1e-4f);
            }
        }

        // ------------------------------------------------------------------ the prefab

        [Test]
        public void CrystalTimePrefab_IsWiredForTheProceduralWave()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Assert.IsNotNull(prefab, $"{PrefabPath} not found");

            Assert.IsFalse(prefab.GetComponentInChildren<CosmicShore.UI.JustRotate>(true),
                "the Time crystal tumbles again - a turning shell makes every wave start look like a jump");
            Assert.IsTrue(prefab.TryGetComponent(out CrystalFlipWave wave), "CrystalTime has no CrystalFlipWave");

            var serialized = new SerializedObject(wave);
            var model = serialized.FindProperty("model").objectReferenceValue as Transform;
            Assert.IsTrue(model, "CrystalFlipWave.model is unassigned");
            Assert.IsTrue(serialized.FindProperty("profile").objectReferenceValue as FlipWaveProfileSO, "CrystalFlipWave.profile is unassigned");
            Assert.IsFalse(model.GetComponentInChildren<Animator>(true),
                "the model still carries an Animator - it would fight the procedural wave for the bones every frame");

            var skinned = model.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            Assert.AreEqual(1, skinned.Length, "the model must carry exactly one skinned mesh");
            Assert.IsTrue(skinned[0].sharedMesh.isReadable, $"{FbxPath} must be Read/Write enabled - a player build cannot measure the plates otherwise");

            var instance = Object.Instantiate(prefab);
            try
            {
                var instanceModel = instance.GetComponent<CrystalFlipWave>();
                var instanceSkinned = instance.GetComponentInChildren<SkinnedMeshRenderer>(true);
                var frame = new SerializedObject(instanceModel).FindProperty("model").objectReferenceValue as Transform;
                Assert.IsTrue(FlipWaveRig.TryBuild(instanceSkinned, frame, out var rig, out var problem), problem);
                Assert.AreEqual(30, rig.PlateCount);
                Assert.AreEqual(12, rig.StartCount);
                Assert.AreEqual(5, rig.RingCount);
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        // ------------------------------------------------------------------ the omni crystal's rhombi

        /// <summary>A slab: the 2D outline at <paramref name="centre"/> in the XY plane, extruded 0.1 along Z.</summary>
        static void AddSlab(List<Vector3> verts, List<int> tris, Vector3 centre, params Vector2[] outline)
        {
            int k = outline.Length, b = verts.Count;
            foreach (var o in outline) verts.Add(centre + new Vector3(o.x, o.y, 0.05f));
            foreach (var o in outline) verts.Add(centre + new Vector3(o.x, o.y, -0.05f));
            for (int i = 1; i < k - 1; i++)
            {
                tris.AddRange(new[] { b, b + i, b + i + 1 });
                tris.AddRange(new[] { b + k, b + k + i + 1, b + k + i });
            }
            for (int i = 0; i < k; i++)
            {
                int j = (i + 1) % k;
                tris.AddRange(new[] { b + i, b + k + i, b + k + j, b + i, b + k + j, b + j });
            }
        }

        [Test]
        public void RhombusSkin_BonesOnlyTheRhombi_AndDrawsTheSourceAtRest()
        {
            var verts = new List<Vector3>();
            var tris = new List<int>();
            AddSlab(verts, tris, new Vector3(0f, 0f, 1f), new Vector2(0.5f, 0f), new Vector2(0f, 0.3f), new Vector2(-0.5f, 0f), new Vector2(0f, -0.3f)); // rhombus
            AddSlab(verts, tris, new Vector3(2f, 0f, 1f), new Vector2(0.3f, 0.3f), new Vector2(-0.3f, 0.3f), new Vector2(-0.3f, -0.3f), new Vector2(0.3f, -0.3f)); // square
            AddSlab(verts, tris, new Vector3(-2f, 0f, 1f), new Vector2(0.3f, 0f), new Vector2(-0.2f, 0.25f), new Vector2(-0.2f, -0.25f)); // triangle
            var source = new Mesh { name = "Plates" };
            source.SetVertices(verts);
            source.SetTriangles(tris, 0);

            var skin = RhombusSkinBaker.Bake(source, out var problem);
            try
            {
                Assert.IsNotNull(skin, problem);
                Assert.AreEqual(1, skin.PlateCentroids.Length, "exactly the one rhombus gets a bone - not the square, not the triangle");
                Assert.AreEqual(0f, (skin.PlateCentroids[0] - new Vector3(0f, 0f, 1f)).magnitude, 1e-5f);
                var weights = skin.Mesh.boneWeights;
                var bindposes = skin.Mesh.bindposes;
                Assert.AreEqual(2, bindposes.Length);
                for (int v = 0; v < verts.Count; v++)
                {
                    Assert.AreEqual(v < 8 ? 1 : 0, weights[v].boneIndex0, $"vertex {v} is on the wrong bone");
                    Assert.AreEqual(1f, weights[v].weight0, 1e-6f);
                    // At rest bone 0 is the renderer and bone 1 sits at the centroid, so bindpose then bone
                    // puts every vertex back exactly where the source draws it.
                    var bone = weights[v].boneIndex0 == 0 ? Matrix4x4.identity : Matrix4x4.Translate(skin.PlateCentroids[0]);
                    Assert.AreEqual(0f, ((bone * bindposes[weights[v].boneIndex0]).MultiplyPoint3x4(verts[v]) - verts[v]).magnitude, 1e-5f);
                }
                CollectionAssert.AreEqual(source.vertices, skin.Mesh.vertices, "the twin must keep the source's vertex order");
            }
            finally
            {
                if (skin != null) Object.DestroyImmediate(skin.Mesh);
                Object.DestroyImmediate(source);
            }
        }

        [Test]
        public void OmniCrystalPrefab_TurnsItsThirtyRhombi_AndLeavesTheBodyStill()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(OmniPrefabPath);
            Assert.IsNotNull(prefab, $"{OmniPrefabPath} not found");
            Assert.IsTrue(prefab.TryGetComponent(out CrystalFlipWave wave), "the omni Crystal has no CrystalFlipWave");
            var serialized = new SerializedObject(wave);
            Assert.IsTrue(serialized.FindProperty("skinRhombi").boolValue, "CrystalFlipWave.skinRhombi is off on the omni");
            Assert.IsTrue(serialized.FindProperty("profile").objectReferenceValue as FlipWaveProfileSO, "the omni's CrystalFlipWave has no profile");

            var instance = Object.Instantiate(prefab);
            try
            {
                var frame = new SerializedObject(instance.GetComponent<CrystalFlipWave>()).FindProperty("model").objectReferenceValue as Transform;
                Assert.IsTrue(frame, "CrystalFlipWave.model is unassigned");
                Assert.IsTrue(frame.TryGetComponent(out SkinnedMeshRenderer body), "the omni body is not a SkinnedMeshRenderer - its rhombi cannot turn");
                Assert.AreSame(instance.GetComponent<Crystal>().CrystalModels[0].model, frame.gameObject, "the flip wave must drive crystalModels slot 0, the body");
                var source = body.sharedMesh;

                Assert.IsTrue(RhombusSkinBaker.TryDress(body, out var problem), problem);
                Assert.AreSame(source, RhombusSkinBaker.SourceOf(body.sharedMesh), "the twin must resolve back to the FBX mesh it was skinned from");
                Assert.AreEqual(31, body.bones.Length, "the still body plus one bone per rhombus");
                Assert.IsTrue(FlipWaveRig.TryBuild(body, frame, out var rig, out problem, body.transform), problem);
                Assert.AreEqual(30, rig.PlateCount);
                Assert.AreEqual(12, rig.StartCount);
                Assert.AreEqual(5, rig.RingCount);
                for (int s = 0; s < rig.StartCount; s++)
                    CollectionAssert.AreEqual(new[] { 5, 5, 10, 5, 5 },
                        Enumerable.Range(0, 5).Select(r => Enumerable.Range(0, 30).Count(p => rig.RingOf(s, p) == r)).ToArray(), $"rings from vertex {s}");

                Assert.IsTrue(RhombusSkinBaker.TryDress(body, out problem), "dressing twice must be a no-op: " + problem);
                Assert.AreEqual(31, body.bones.Length, "a second dress grew more bones");
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        // ------------------------------------------------------------------ parity

        static Vector3[] SkinInFrame(SkinnedMeshRenderer renderer, Transform frame, Mesh mesh, Vector3[] vertices, BoneWeight[] weights, Matrix4x4[] bindposes)
        {
            var bones = renderer.bones;
            var toFrame = frame.worldToLocalMatrix;
            var posed = new Vector3[vertices.Length];
            for (int v = 0; v < vertices.Length; v++)
            {
                int b = weights[v].boneIndex0;
                posed[v] = toFrame.MultiplyPoint3x4(bones[b].localToWorldMatrix.MultiplyPoint3x4(bindposes[b].MultiplyPoint3x4(vertices[v])));
            }
            return posed;
        }

        [Test]
        public void ProceduralWave_MatchesTheImportedTake_FrameByFrame()
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(FbxPath);
            var take = AssetDatabase.LoadAllAssetsAtPath(FbxPath).OfType<AnimationClip>().FirstOrDefault(c => c.name == TakeName);
            var profile = AssetDatabase.LoadAssetAtPath<FlipWaveProfileSO>(ProfilePath);
            Assert.IsTrue(source, $"{FbxPath} not found");
            Assert.IsTrue(take, $"take '{TakeName}' not found in {FbxPath}");
            Assert.IsTrue(profile, $"{ProfilePath} not found");

            var model = Object.Instantiate(source);
            try
            {
                var renderer = model.GetComponentInChildren<SkinnedMeshRenderer>(true);
                var mesh = renderer.sharedMesh;
                var vertices = mesh.vertices;
                var weights = mesh.boneWeights;
                var bindposes = mesh.bindposes;
                var frame = model.transform;

                // The take's frame 0 is the bind pose as a SHAPE, but most of its bones start turned 180° about a
                // diagonal; pose it there first so the rig's rest has the take's vertex correspondence.
                take.SampleAnimation(model, 0f);
                var rest = SkinInFrame(renderer, frame, mesh, vertices, weights, bindposes);
                take.SampleAnimation(model, 0.6f);
                var moved = SkinInFrame(renderer, frame, mesh, vertices, weights, bindposes);
                Assert.Greater(Enumerable.Range(0, rest.Length).Max(v => (rest[v] - moved[v]).magnitude), 0.01f,
                    "SampleAnimation did not pose the model - the parity below would compare the rest pose with itself");
                take.SampleAnimation(model, 0f);

                Assert.IsTrue(FlipWaveRig.TryBuild(renderer, frame, out var rig, out var problem), problem);

                // The take's start vertex: the wave whose ring 0 is exactly the plates the take moves first.
                var restRotations = Enumerable.Range(0, rig.PlateCount).Select(p => rig.Bone(p).localRotation).ToArray();
                take.SampleAnimation(model, 0.3f);
                var leading = new HashSet<int>(Enumerable.Range(0, rig.PlateCount)
                    .Where(p => Quaternion.Angle(rig.Bone(p).localRotation, restRotations[p]) > 1f));
                int start = Enumerable.Range(0, rig.StartCount).FirstOrDefault(s =>
                    leading.SetEquals(Enumerable.Range(0, rig.PlateCount).Where(p => rig.RingOf(s, p) == 0)));
                Assert.AreEqual(5, leading.Count, "expected the take to move five plates first");
                Assert.IsTrue(leading.SetEquals(Enumerable.Range(0, rig.PlateCount).Where(p => rig.RingOf(start, p) == 0)),
                    "no planned wave starts with the plates the take moves first");

                var centre = rest.Aggregate(Vector3.zero, (a, b) => a + b) / rest.Length;
                float radius = rest.Max(p => (p - centre).magnitude);
                var samples = new FlipSample[rig.RingCount];
                float worst = 0f, worstAt = 0f;
                for (int i = 0; i < 2 * TakeFrames - 1; i++)
                {
                    float t = i * 0.5f / TakeFps;   // every frame and every midpoint
                    take.SampleAnimation(model, t);
                    var expected = SkinInFrame(renderer, frame, mesh, vertices, weights, bindposes);

                    rig.ResetToRest();
                    for (int r = 0; r < samples.Length; r++) samples[r] = profile.Sample(r, t);
                    rig.Apply(start, samples);
                    var actual = SkinInFrame(renderer, frame, mesh, vertices, weights, bindposes);

                    for (int v = 0; v < actual.Length; v++)
                    {
                        float error = (actual[v] - expected[v]).magnitude / radius;
                        if (error > worst) { worst = error; worstAt = t; }
                    }
                }

                TestContext.WriteLine($"procedural wave vs imported take: worst {100f * worst:F3}% of the radius (at {worstAt:F2} s)");
                Assert.Less(worst, ParityTolerance,
                    $"the procedural wave drifts {100f * worst:F2}% of the radius from the take at {worstAt:F2} s - re-run Tools/Build/author_time_crystal_flip_wave.py");
            }
            finally
            {
                Object.DestroyImmediate(model);
            }
        }
    }
}
#endif
