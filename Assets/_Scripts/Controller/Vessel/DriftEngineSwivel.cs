using UnityEngine;
using CosmicShore.Utility;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Squirrel's drift animation: while the hull aims away from where it is going, its engine
    /// pods swing onto COURSE, parallel with the <see cref="DriftJet"/> plumes they carry. The same
    /// idea as the Dolphin's drift cage (<see cref="RiptideAnimation"/>), where the wings and engines
    /// go on flying straight down Course while the fuselage aims through them.
    ///
    /// <para><b>Why LateUpdate, and why a component rather than puppetry.</b> The Squirrel's model is
    /// moved by an Animator (<see cref="MantaAnimationContoller"/>), which rewrites every bone between
    /// Update and LateUpdate. So the swivel is applied AFTER it, on top of the animated pose, and the
    /// engines keep every pitch / yaw / roll take while they swing. A bone the Animator did not write
    /// this frame still holds the swivel written last frame; it is put back to its animated pose
    /// first, so the swing can never compound.</para>
    ///
    /// <para><b>Why the jets stay in the back of the engines.</b> Each jet is a child of an engine
    /// bone, and each pod turns rigidly about its own centre, so a jet rides along with the pod it is
    /// mounted in. Its direction is still <see cref="DriftJet"/>'s (Course while drifting), and the pod
    /// turns until its jet's flight axis (<see cref="DriftJet.FlightAxis"/>) lies on that same Course,
    /// so plume and pod line up.</para>
    ///
    /// <para><b>The swing is two parts.</b> First the hull's shortest arc onto Course (the Dolphin's
    /// appendage frame: no roll of its own, held near the antipode where that arc is undefined, and
    /// slew-limited). Then a small per-pod correction from the pod's own cant onto Course, which is
    /// what makes it exactly parallel to its jet. The correction is never degenerate: after the first
    /// part the pod is already within its cant (a few degrees) of Course.</para>
    ///
    /// Cosmetic only: it reads Course and IsDrifting, which every peer has, and changes no gameplay.
    /// </summary>
    public class DriftEngineSwivel : MonoBehaviour
    {
        [System.Serializable]
        public class Engine
        {
            [Tooltip("For the inspector only.")]
            public string label;

            [Tooltip("The model's bones that make up this pod, by name. They turn together, rigidly, " +
                     "about the centre of their heads. A bone whose ancestor is also listed rides " +
                     "that ancestor and is only counted for the centre.")]
            public string[] boneNames;

            [Tooltip("How far the pod slides while drifting, world units in the VESSEL's frame (x right, " +
                     "y up, z forward), eased in with the swing. 0 = it turns in place. A swung pod " +
                     "passes within ~0.01 u of the wing slats beyond ~30 degrees of drift aim (measured " +
                     "on the shipped FBX); a slide is the dial for that if it reads as clipping.")]
            public Vector3 driftOffset;
        }

        [Header("Wiring")]
        [Tooltip("The vessel's status: IsDrifting and Course. The same one the DriftJets read.")]
        [SerializeField] VesselStatus vesselStatus;

        [Tooltip("Where the bones and jets are looked up. Empty = this object.")]
        [SerializeField] Transform modelRoot;

        [Header("Engine pods")]
        [Tooltip("The engine pods that swing onto Course while drifting.")]
        [SerializeField] Engine[] engines =
        {
            new() { label = "Front engine L", boneNames = new[] { "bbone_FrontEngineBase.L", "bbone_FrontEngineTrim.L", "bbone_FrontEngineTip.L" } },
            new() { label = "Front engine R", boneNames = new[] { "bbone_FrontEngineBase.R", "bbone_FrontEngineTrim.R", "bbone_FrontEngineTip.R" } },
            new() { label = "Back engine L", boneNames = new[] { "bbone_BackEngineFrame.L", "bbone_BackEngineInner.L" } },
            new() { label = "Back engine R", boneNames = new[] { "bbone_BackEngineFrame.R", "bbone_BackEngineInner.R" } },
        };

        [Header("Feel")]
        [Tooltip("How fast the pods ease onto Course and back, per second (exponential). 3.7 is the " +
                 "DriftJet plumes' own ease (0.06 per frame) at 60 fps, so pods and plumes turn together.")]
        [SerializeField] float swivelRate = 3.7f;

        [Tooltip("The swing may not turn faster than this, degrees per second. 360 never engages in " +
                 "ordinary drifting; it keeps a full-reverse aim (where the hull's arc onto Course " +
                 "flips) a fast swing instead of a one-frame snap. The Dolphin's cage uses the same.")]
        [SerializeField] float maxSwivelDegreesPerSecond = 360f;

        // Per pod, resolved once.
        struct Pod
        {
            public Transform[] Rotated;   // top-level bones: written
            public Transform[] All;       // every listed bone: averaged for the centre
            public DriftJet Jet;          // the jet mounted in this pod, or null
            public Quaternion LocalSwing; // the swing in the VESSEL's frame, held on drift exit
            public Vector3 Offset;        // driftOffset, vessel frame
        }

        Pod[] _pods = System.Array.Empty<Pod>();

        // Per written bone: the animated pose it was given and the pose this component wrote.
        Transform[] _bones = System.Array.Empty<Transform>();
        Vector3[] _basePos = System.Array.Empty<Vector3>();
        Quaternion[] _baseRot = System.Array.Empty<Quaternion>();
        Vector3[] _writtenPos = System.Array.Empty<Vector3>();
        Quaternion[] _writtenRot = System.Array.Empty<Quaternion>();
        bool[] _written = System.Array.Empty<bool>();

        // Per pod, this frame: the centre and the flight axis, read off the animated pose before any write.
        Vector3[] _centres = System.Array.Empty<Vector3>();
        Vector3[] _axes = System.Array.Empty<Vector3>();

        float _weight;
        bool _resolved;

        void Awake() => Resolve();

        void OnDisable()
        {
            RestoreUnanimatedBones();
            _weight = 0f;
            for (int p = 0; p < _pods.Length; p++) _pods[p].LocalSwing = Quaternion.identity;
        }

        void Resolve()
        {
            if (_resolved) return;
            _resolved = true;

            var root = modelRoot ? modelRoot : transform;
            var byName = new System.Collections.Generic.Dictionary<string, Transform>(System.StringComparer.Ordinal);
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                byName.TryAdd(t.name, t);
            var jets = root.GetComponentsInChildren<DriftJet>(true);

            var pods = new System.Collections.Generic.List<Pod>();
            var bones = new System.Collections.Generic.List<Transform>();
            var missing = new System.Collections.Generic.List<string>();

            foreach (var engine in engines)
            {
                if (engine?.boneNames == null) continue;

                var all = new System.Collections.Generic.List<Transform>();
                foreach (var boneName in engine.boneNames)
                {
                    if (byName.TryGetValue(boneName, out var bone)) all.Add(bone);
                    else missing.Add(boneName);
                }
                if (all.Count == 0) continue;

                var rotated = new System.Collections.Generic.List<Transform>();
                foreach (var bone in all)
                    if (!HasListedAncestor(bone, all)) rotated.Add(bone);

                DriftJet jet = null;
                foreach (var candidate in jets)
                {
                    var host = candidate.transform.parent;
                    if (host && IsUnderAny(host, all)) { jet = candidate; break; }
                }

                pods.Add(new Pod
                {
                    Rotated = rotated.ToArray(),
                    All = all.ToArray(),
                    Jet = jet,
                    LocalSwing = Quaternion.identity,
                    Offset = engine.driftOffset,
                });
                bones.AddRange(rotated);
            }

            if (missing.Count > 0)
                CSDebug.LogWarning($"[{nameof(DriftEngineSwivel)}] '{name}' could not find engine bone(s): " +
                                   $"{string.Join(", ", missing)}. Those pods will not swing while drifting - " +
                                   "check the bone names against the model.");

            _pods = pods.ToArray();
            _bones = bones.ToArray();
            _basePos = new Vector3[_bones.Length];
            _baseRot = new Quaternion[_bones.Length];
            _writtenPos = new Vector3[_bones.Length];
            _writtenRot = new Quaternion[_bones.Length];
            _written = new bool[_bones.Length];
            _centres = new Vector3[_pods.Length];
            _axes = new Vector3[_pods.Length];
        }

        static bool HasListedAncestor(Transform bone, System.Collections.Generic.List<Transform> listed)
        {
            for (var t = bone.parent; t; t = t.parent)
                if (listed.Contains(t)) return true;
            return false;
        }

        static bool IsUnderAny(Transform t, System.Collections.Generic.List<Transform> roots)
        {
            for (; t; t = t.parent)
                if (roots.Contains(t)) return true;
            return false;
        }

        void LateUpdate()
        {
            if (!vesselStatus) return;
            Resolve();

            RestoreUnanimatedBones();

            bool drifting = vesselStatus.IsDrifting;
            float ease = 1f - Mathf.Exp(-swivelRate * Time.deltaTime);
            _weight = Mathf.Lerp(_weight, drifting ? 1f : 0f, ease);
            if (!drifting && _weight < 1e-3f)
            {
                _weight = 0f;
                for (int p = 0; p < _pods.Length; p++) _pods[p].LocalSwing = Quaternion.identity;
                return;
            }

            Quaternion hull = transform.rotation;
            Quaternion toVessel = Quaternion.Inverse(hull);

            // Read every pod off the ANIMATED pose before writing any of them.
            for (int p = 0; p < _pods.Length; p++)
            {
                var all = _pods[p].All;
                Vector3 sum = Vector3.zero;
                for (int i = 0; i < all.Length; i++) sum += all[i].position;
                _centres[p] = sum / all.Length;
                _axes[p] = _pods[p].Jet ? _pods[p].Jet.FlightAxis : transform.forward;
            }

            Vector3 course = vesselStatus.Course;
            bool aimable = drifting && course.sqrMagnitude > 1e-6f;
            if (aimable)
            {
                course.Normalize();
                // Inside FromToRotation's degenerate cone (aiming fully backwards) the arc's axis is
                // arbitrary and flips frame to frame: hold the last swing, as the Dolphin's cage does.
                aimable = Vector3.Dot(transform.forward, course) > -0.999f;
            }

            float maxStep = maxSwivelDegreesPerSecond * Time.deltaTime;
            Quaternion hullArc = aimable ? Quaternion.FromToRotation(transform.forward, course) : Quaternion.identity;

            int b = 0;
            for (int p = 0; p < _pods.Length; p++)
            {
                ref var pod = ref _pods[p];

                if (aimable)
                {
                    Vector3 swungAxis = hullArc * _axes[p];
                    Quaternion swing = Quaternion.FromToRotation(swungAxis, course) * hullArc;
                    Quaternion target = toVessel * swing * hull;

                    float step = Quaternion.Angle(pod.LocalSwing, target);
                    pod.LocalSwing = step > maxStep
                        ? Quaternion.Slerp(pod.LocalSwing, target, maxStep / step)
                        : target;
                }

                Quaternion worldSwing = hull * Quaternion.Slerp(Quaternion.identity, pod.LocalSwing, _weight) * toVessel;
                Vector3 centre = _centres[p];
                Vector3 slide = hull * (pod.Offset * _weight);

                var rotated = pod.Rotated;
                for (int i = 0; i < rotated.Length; i++, b++)
                {
                    var bone = rotated[i];
                    _basePos[b] = bone.localPosition;
                    _baseRot[b] = bone.localRotation;

                    bone.SetPositionAndRotation(centre + slide + worldSwing * (bone.position - centre),
                                                worldSwing * bone.rotation);

                    _writtenPos[b] = bone.localPosition;
                    _writtenRot[b] = bone.localRotation;
                    _written[b] = true;
                }
            }
        }

        // A bone still holding exactly the pose this component wrote was not rewritten by the
        // Animator since: give it back its animated pose, or the next swing lands on top of this one.
        void RestoreUnanimatedBones()
        {
            for (int b = 0; b < _bones.Length; b++)
            {
                if (!_written[b]) continue;
                _written[b] = false;
                var bone = _bones[b];
                if (!bone) continue;
                if (bone.localPosition.Equals(_writtenPos[b]) && bone.localRotation.Equals(_writtenRot[b]))
                {
                    bone.localPosition = _basePos[b];
                    bone.localRotation = _baseRot[b];
                }
            }
        }
    }
}
