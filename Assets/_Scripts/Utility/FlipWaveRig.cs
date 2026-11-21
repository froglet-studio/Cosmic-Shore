using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Utility
{
    /// <summary>
    /// Drives a skinned crystal's plates through a <see cref="FlipWave"/>. Each bone of the
    /// <see cref="SkinnedMeshRenderer"/> rigidly carries one plate; the rig measures every plate from the
    /// mesh itself, resolves the shell's icosahedral frame from where the plates sit, plans a wave from each
    /// of the twelve vertices, and then poses bones from per-ring <see cref="FlipSample"/>s.
    ///
    /// Everything is measured in Unity's OWN import, at runtime: the FBX declares a Z-up axis system and
    /// which way the importer signs the axes decides which of the icosahedron's two coordinate-aligned
    /// orientations the model lands in, so nothing about the frame is authored. The plates' outward
    /// directions are the shell's two-fold axes, and exactly one orientation puts each of them 31.7° from
    /// two vertices (<see cref="IcosahedralSymmetry.TryResolveFromTwoFoldDirections"/>).
    ///
    /// The mesh must be Read/Write enabled - a player build has no vertex data otherwise. The measurement
    /// and the twelve plans are cached per mesh: every instance of one prefab shares them.
    /// </summary>
    public sealed class FlipWaveRig
    {
        /// <summary>How far a plate's outward direction may sit off a two-fold axis.</summary>
        const float FrameToleranceDegrees = 2f;

        /// <summary>Shared per mesh: the plates and the twelve plans, in each bone's PARENT space.</summary>
        sealed class Shape
        {
            public int[] Bones;            // the renderer bone that carries each plate
            public FlipPlate[] Plates;     // parent space
            public FlipStep[][] Steps;     // [start][plate], parent space
            public Vector3[] StartAxes;    // frame space - for callers that need to name a vertex
            public int RingCount;
        }

        static readonly Dictionary<Mesh, Shape> ShapeByMesh = new();

        // Enter Play Mode keeps statics across sessions (domain reload is off), so a model re-exported mid-session
        // must be measured afresh rather than served from the last session's cache.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetCache() => ShapeByMesh.Clear();

        readonly Shape shape;
        readonly Transform[] bones;
        readonly Vector3[] restPositions;
        readonly Quaternion[] restRotations;
        readonly Vector3[] restScales;
        readonly FlipSample[] written;
        int writtenStart = -1;

        FlipWaveRig(Shape shape, Transform[] rendererBones)
        {
            this.shape = shape;
            int count = shape.Plates.Length;
            bones = new Transform[count];
            restPositions = new Vector3[count];
            restRotations = new Quaternion[count];
            restScales = new Vector3[count];
            written = new FlipSample[count];
            for (int i = 0; i < count; i++)
            {
                var bone = rendererBones[shape.Bones[i]];
                bones[i] = bone;
                restPositions[i] = bone.localPosition;
                restRotations[i] = bone.localRotation;
                restScales[i] = bone.localScale;
                written[i] = FlipSample.Unwritten;
            }
        }

        public int PlateCount => shape.Plates.Length;
        public int StartCount => shape.Steps.Length;
        public int RingCount => shape.RingCount;

        /// <summary>The <paramref name="start"/>th wave's start axis, in the frame space the rig was built in.</summary>
        public Vector3 StartAxis(int start) => shape.StartAxes[start];

        /// <summary>Which ring plate <paramref name="plate"/> is in for the wave from <paramref name="start"/>.</summary>
        public int RingOf(int start, int plate) => shape.Steps[start][plate].Ring;

        /// <summary>The bone carrying plate <paramref name="plate"/>.</summary>
        public Transform Bone(int plate) => bones[plate];

        /// <summary>
        /// Builds a rig for <paramref name="renderer"/>, whose bones must be at rest. <paramref name="frame"/>
        /// is the model's root: the space Unity's FBX conversion leaves coordinate-aligned.
        /// <paramref name="stillBone"/>, when given, carries geometry that never moves (the omni crystal's
        /// body round its rhombi, <see cref="RhombusSkinBaker"/>); every OTHER bone must carry one plate.
        /// </summary>
        public static bool TryBuild(SkinnedMeshRenderer renderer, Transform frame, out FlipWaveRig rig, out string problem,
                                    Transform stillBone = null)
        {
            rig = null;
            if (!renderer || !frame)
            {
                problem = "no renderer or frame";
                return false;
            }
            var mesh = renderer.sharedMesh;
            var rendererBones = renderer.bones;
            if (!mesh || rendererBones == null || rendererBones.Length == 0)
            {
                problem = $"'{renderer.name}' has no skinned mesh or no bones";
                return false;
            }

            if (!ShapeByMesh.TryGetValue(mesh, out var shape))
            {
                if (!TryMeasureShape(mesh, rendererBones, frame, stillBone, out shape, out problem)) return false;
                ShapeByMesh[mesh] = shape;
            }
            foreach (int b in shape.Bones)
            {
                if (b < rendererBones.Length && rendererBones[b]) continue;
                problem = $"'{renderer.name}' is missing bone {b}";
                return false;
            }

            rig = new FlipWaveRig(shape, rendererBones);
            problem = null;
            return true;
        }

        static bool TryMeasureShape(Mesh mesh, Transform[] rendererBones, Transform frame, Transform stillBone,
                                    out Shape shape, out string problem)
        {
            shape = null;
            if (!mesh.isReadable)
            {
                problem = $"mesh '{mesh.name}' is not Read/Write enabled, so its plates cannot be measured in a player";
                return false;
            }

            var vertices = mesh.vertices;
            var weights = mesh.boneWeights;
            var bindposes = mesh.bindposes;
            if (weights.Length != vertices.Length || bindposes.Length != rendererBones.Length)
            {
                problem = $"mesh '{mesh.name}' has {weights.Length} bone weights for {vertices.Length} vertices and {bindposes.Length} bindposes for {rendererBones.Length} bones";
                return false;
            }

            // Every vertex in frame space at rest, grouped by the one bone that carries it.
            var toFrame = frame.worldToLocalMatrix;
            var pointsByBone = new Dictionary<int, List<Vector3>>();
            for (int v = 0; v < vertices.Length; v++)
            {
                var w = weights[v];
                if (w.weight0 < 0.999f)
                {
                    problem = $"mesh '{mesh.name}' vertex {v} is not rigidly skinned (weight {w.weight0}) - a plate must move as one piece";
                    return false;
                }
                int b = w.boneIndex0;
                if (stillBone && rendererBones[b] == stillBone) continue;
                if (!rendererBones[b])
                {
                    problem = $"mesh '{mesh.name}' skins vertex {v} to missing bone {b}";
                    return false;
                }
                var point = toFrame.MultiplyPoint3x4(rendererBones[b].localToWorldMatrix.MultiplyPoint3x4(bindposes[b].MultiplyPoint3x4(vertices[v])));
                if (!pointsByBone.TryGetValue(b, out var list)) pointsByBone[b] = list = new List<Vector3>(8);
                list.Add(point);
            }

            var boneOrder = new List<int>(pointsByBone.Keys);
            boneOrder.Sort();

            // The shell centre is the mean of the plates' corner centroids.
            var shellCentre = Vector3.zero;
            foreach (int b in boneOrder) shellCentre += Mean(pointsByBone[b]);
            shellCentre /= boneOrder.Count;

            var platesInFrame = new FlipPlate[boneOrder.Count];
            var directions = new Vector3[boneOrder.Count];
            for (int i = 0; i < boneOrder.Count; i++)
            {
                if (!FlipWave.TryMeasurePlate(pointsByBone[boneOrder[i]], shellCentre, out platesInFrame[i], out problem))
                {
                    problem = $"bone '{rendererBones[boneOrder[i]].name}': {problem}";
                    return false;
                }
                directions[i] = platesInFrame[i].Radial;
            }

            if (!IcosahedralSymmetry.TryResolveFromTwoFoldDirections(directions, FrameToleranceDegrees, out var startAxes, out problem))
                return false;

            var steps = new FlipStep[startAxes.Length][];
            int ringCount = 0;
            for (int s = 0; s < startAxes.Length; s++)
            {
                steps[s] = FlipWave.Plan(platesInFrame, startAxes[s], out int rings);
                if (s > 0 && rings != ringCount)
                {
                    problem = $"the wave from vertex {s} has {rings} rings, the one from vertex 0 has {ringCount}";
                    return false;
                }
                ringCount = rings;
            }

            // Re-express everything in each bone's parent space, where the bone's local pose is written.
            var plates = new FlipPlate[boneOrder.Count];
            var parentSteps = new FlipStep[startAxes.Length][];
            for (int s = 0; s < startAxes.Length; s++) parentSteps[s] = new FlipStep[boneOrder.Count];
            for (int i = 0; i < boneOrder.Count; i++)
            {
                var parent = rendererBones[boneOrder[i]].parent;
                var m = (parent ? parent.worldToLocalMatrix : Matrix4x4.identity) * frame.localToWorldMatrix;
                if (!TrySimilarity(m, out float scale, out float handedness))
                {
                    problem = $"bone '{rendererBones[boneOrder[i]].name}': the frame-to-parent transform is not a rotation and uniform scale";
                    return false;
                }

                var p = platesInFrame[i];
                plates[i] = new FlipPlate(m.MultiplyPoint3x4(p.Centroid), m.MultiplyVector(p.Radial).normalized,
                    m.MultiplyVector(p.LongDiagonal).normalized, m.MultiplyVector(p.ShortDiagonal).normalized, p.Reach * scale);
                for (int s = 0; s < startAxes.Length; s++)
                {
                    var step = steps[s][i];
                    // A reflection reverses the sense of every turn, so the axis flips with it.
                    parentSteps[s][i] = new FlipStep(step.Ring, handedness * m.MultiplyVector(step.Axis).normalized,
                        m.MultiplyVector(step.Toward).normalized);
                }
            }

            shape = new Shape
            {
                Bones = boneOrder.ToArray(),
                Plates = plates,
                Steps = parentSteps,
                StartAxes = startAxes,
                RingCount = ringCount,
            };
            problem = null;
            return true;
        }

        static Vector3 Mean(List<Vector3> points)
        {
            var sum = Vector3.zero;
            foreach (var p in points) sum += p;
            return sum / points.Count;
        }

        static bool TrySimilarity(Matrix4x4 m, out float scale, out float handedness)
        {
            Vector3 x = m.GetColumn(0), y = m.GetColumn(1), z = m.GetColumn(2);
            scale = x.magnitude;
            handedness = m.determinant < 0f ? -1f : 1f;
            float tolerance = 1e-3f * scale;
            return scale > 1e-8f
                   && Mathf.Abs(y.magnitude - scale) < tolerance && Mathf.Abs(z.magnitude - scale) < tolerance
                   && Mathf.Abs(Vector3.Dot(x, y)) < tolerance * scale && Mathf.Abs(Vector3.Dot(y, z)) < tolerance * scale
                   && Mathf.Abs(Vector3.Dot(z, x)) < tolerance * scale;
        }

        /// <summary>
        /// Poses every plate for the wave from <paramref name="start"/>, with <paramref name="ringSamples"/>
        /// indexed by ring. Only bones whose sample changed since the last write are touched - between a ring's
        /// flips its plates hold still and cost nothing.
        /// </summary>
        public void Apply(int start, FlipSample[] ringSamples)
        {
            if (start != writtenStart)
            {
                for (int i = 0; i < written.Length; i++) written[i] = FlipSample.Unwritten;
                writtenStart = start;
            }

            var steps = shape.Steps[start];
            for (int i = 0; i < bones.Length; i++)
            {
                var step = steps[i];
                var sample = ringSamples[step.Ring];
                if (sample.Equals(written[i])) continue;

                var plate = shape.Plates[i];
                var turn = FlipWave.Turn(step, sample);
                var position = FlipWave.Displacement(plate, step, sample) + plate.Centroid
                               + sample.Scale * (turn * (restPositions[i] - plate.Centroid));
                bones[i].SetLocalPositionAndRotation(position, turn * restRotations[i]);
                if (sample.Scale != written[i].Scale) bones[i].localScale = restScales[i] * sample.Scale;
                written[i] = sample;
            }
        }

        /// <summary>Puts every plate back on its rest pose.</summary>
        public void ResetToRest()
        {
            for (int i = 0; i < bones.Length; i++)
            {
                bones[i].SetLocalPositionAndRotation(restPositions[i], restRotations[i]);
                bones[i].localScale = restScales[i];
            }
            writtenStart = -1;   // the next Apply rewrites every plate
        }
    }
}
