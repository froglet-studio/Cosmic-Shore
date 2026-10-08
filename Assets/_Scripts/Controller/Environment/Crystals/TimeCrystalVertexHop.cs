using System.Collections.Generic;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Time crystal holds still and its flip wave starts from a different vertex every loop.
    ///
    /// <c>TimeCrystalExport.fbx</c> is 30 blocks on an icosidodecahedral shell, and its one take
    /// (<c>TimeSequenceAnimFinal.001</c>, 2 s) flips each block 180° in a wave that runs from one
    /// five-fold vertex to the opposite one. At both ends of the loop every block sits exactly on its
    /// bind pose, so the shape there is fully icosahedrally symmetric - congruent under all 60
    /// rotations of the group. Snapping the model to a random one of those rotations on the frame
    /// the loop wraps is therefore invisible: the crystal never appears to turn, only the next wave
    /// appears from another of the 12 vertices. No block moves more than 1° for the first 0.24 s of
    /// the loop, so the snap has a whole still window to land in, not one frame.
    ///
    /// This replaced the crystal's continuous <c>JustRotate</c> tumble. The rotation is written to
    /// the MODEL, never the crystal root: the root's rotation belongs to the capture flourish
    /// (<see cref="ElementalCrystalImpactor"/>) and to the spent-husk spawn.
    ///
    /// The symmetry frame is read from Unity's OWN import of the rig rather than assumed: the FBX
    /// declares a Z-up axis system, and which way Unity's conversion signs each axis decides which of
    /// the icosahedron's two coordinate-aligned orientations the model ends up in. The five bones of
    /// the blocks that flip first sit on the pentagon around the wave's start vertex, so their rest
    /// centroid IS that vertex (exactly, in the source file) - and snapping it onto a five-fold axis
    /// fixes the orientation too. Measured from the FBX by
    /// <c>Tools/Build/measure_time_crystal_wave.py</c>.
    /// </summary>
    public class TimeCrystalVertexHop : MonoBehaviour
    {
        /// <summary>
        /// How far the lead bones' centroid may sit from a five-fold axis. Measured 0.000°; the nearest
        /// axis of the WRONG orientation is 26.6° away, so this both tolerates float noise and refuses
        /// a model whose frame is not what this component assumes.
        /// </summary>
        const float AxisSnapToleranceDegrees = 2f;

        [Header("Model")]
        [Tooltip("The imported TimeCrystalExport instance. Its Animator plays the flip wave, and it is the transform this component re-orients - never the crystal root, whose rotation belongs to the capture flourish.")]
        [SerializeField] Transform model;

        [Header("Wave Start")]
        [Tooltip("Bones of the five blocks that flip FIRST in the take (Tools/Build/measure_time_crystal_wave.py). Their rest-pose centroid is the vertex the wave starts from, in Unity's own imported frame.")]
        [SerializeField] string[] leadBlockBones = { "Bone.019", "Bone.020", "Bone.029", "Bone.030", "Bone.031" };

        static readonly Dictionary<Vector3, Quaternion[]> GroupByAxis = new();

        Animator animator;
        Quaternion baseRotation;
        Quaternion[] group;
        Vector3 leadAxis;
        int currentElement;
        int lastCycle = -1;
        System.Random rng;

        void Awake()
        {
            if (!model)
            {
                CSDebug.LogError($"TimeCrystalVertexHop on '{name}': no model assigned. The crystal will animate but never change vertex.", this);
                enabled = false;
                return;
            }

            animator = model.GetComponentInChildren<Animator>();
            if (!animator)
            {
                CSDebug.LogError($"TimeCrystalVertexHop on '{name}': '{model.name}' has no Animator, so there is no loop to hop on.", this);
                enabled = false;
                return;
            }

            if (!TryResolveLeadAxis(model, leadBlockBones, out leadAxis, out string problem) ||
                !TryGetGroup(leadAxis, out group))
            {
                CSDebug.LogError($"TimeCrystalVertexHop on '{name}': {problem ?? "the measured axis does not generate the icosahedral group"}. " +
                                 "Was TimeCrystalExport.fbx re-exported? Re-run Tools/Build/measure_time_crystal_wave.py.", this);
                enabled = false;
                return;
            }

            baseRotation = model.localRotation;
            rng = new System.Random(GetInstanceID());
        }

        /// <summary>A fresh crystal starts its first wave from a random vertex too.</summary>
        void OnEnable()
        {
            if (group == null) return;
            Apply(rng.Next(group.Length));
            lastCycle = -1;
        }

        // LateUpdate runs after the Animator has evaluated this frame, so the snap lands on the same
        // frame the loop wraps.
        void LateUpdate()
        {
            if (!animator.isActiveAndEnabled) return;

            int cycle = Mathf.FloorToInt(animator.GetCurrentAnimatorStateInfo(0).normalizedTime);
            if (lastCycle >= 0 && cycle != lastCycle)
                Apply(IcosahedralSymmetry.PickElementMovingAxis(group, leadAxis, currentElement, rng));
            lastCycle = cycle;
        }

        void Apply(int element)
        {
            currentElement = element;
            // The group acts in the model's own local frame - the frame the lead axis was measured in.
            model.localRotation = baseRotation * group[element];
        }

        static bool TryGetGroup(Vector3 axis, out Quaternion[] rotations)
        {
            if (!GroupByAxis.TryGetValue(axis, out rotations))
            {
                rotations = IcosahedralSymmetry.BuildRotationGroup(axis);
                if (rotations != null) GroupByAxis[axis] = rotations;
            }
            return rotations != null;
        }

        /// <summary>
        /// The five-fold axis the wave starts from, in <paramref name="modelRoot"/>'s local space:
        /// the rest-pose centroid of the lead bones, snapped. Must run while the rig is at rest
        /// (before the Animator first evaluates, or on the prefab asset).
        /// </summary>
        public static bool TryResolveLeadAxis(Transform modelRoot, IReadOnlyList<string> boneNames, out Vector3 axis, out string problem)
        {
            axis = default;
            if (boneNames == null || boneNames.Count == 0)
            {
                problem = "no lead bones listed";
                return false;
            }

            var sum = Vector3.zero;
            foreach (var boneName in boneNames)
            {
                var bone = FindDeep(modelRoot, boneName);
                if (!bone)
                {
                    problem = $"lead bone '{boneName}' not found under '{modelRoot.name}'";
                    return false;
                }
                sum += modelRoot.InverseTransformPoint(bone.position);
            }

            if (!IcosahedralSymmetry.TrySnapToFiveFoldAxis(sum, AxisSnapToleranceDegrees, out axis))
            {
                problem = $"lead bones' centroid {sum / boneNames.Count} is not within {AxisSnapToleranceDegrees}° of a five-fold axis";
                return false;
            }

            problem = null;
            return true;
        }

        static Transform FindDeep(Transform parent, string childName)
        {
            foreach (Transform child in parent)
            {
                if (child.name == childName) return child;
                var found = FindDeep(child, childName);
                if (found) return found;
            }
            return null;
        }
    }
}
