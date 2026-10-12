using System.Collections.Generic;
using CosmicShore.Utility;
using Unity.Profiling;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Drives a crystal's triangular prisms "sealed by depth" (Omni Shepard Lab round 7, the Depth ratchet;
    /// <c>Docs/Studios/OMNI_SHEPARD_LAB.md</c>): every prism rides a column over its own hole, its place set by
    /// how far it is from the camera, so the far side of the crystal stands open at the tops of its columns
    /// and the near side is sealed, whichever way the crystal faces. In view a prism only ever moves down;
    /// it is replaced at the top of its column only once it is behind the crystal (<see cref="DepthRatchet"/>).
    ///
    /// Lives on the prisms' own renderer (the omni's <c>OmniCrystalTriangles</c>, the Mass crystal's
    /// <c>MassDepthPrisms</c>), whose material is on <c>OmniDepthPrismShader</c>. Each frame it is visible it:
    ///  1. turns the whole crystal - every sibling model under the crystal root, itself included - about the
    ///     profile's axis. The crystal ROOT is never turned: its rotation belongs to the capture flourish.
    ///  2. steps every column's ratchet against the main camera, in the crystal's own space;
    ///  3. writes the columns to the renderer's property block (<c>_DepthState[plate]</c> = place, fade,
    ///     leaving place) with <c>_DepthDriven</c> 1, composing with any other writer's block
    ///     (<see cref="Crystal"/>'s tint and dissolve).
    /// Without this component the shader draws every prism sealed and solid, so a husk, a codex model or a
    /// team material that loses the driver still reads as the whole crystal.
    ///
    /// On the omni (<see cref="cutBodyHoles"/>) it also swaps the body onto a twin with its 20 triangular
    /// plates removed (<see cref="RhombusSkinBaker.TryCutPlates"/>), so the holes the prisms fill are real.
    ///
    /// Per-plate data comes from the prism mesh itself: TEXCOORD2 = (plate centre xyz, plate index), baked by
    /// <c>Tools/Build/author_omni_crystal_triangles.py</c> (omni) and <c>author_mass_crystal_look.py</c> (Mass).
    /// </summary>
    [RequireComponent(typeof(MeshRenderer))]
    public class CrystalDepthPrisms : MonoBehaviour
    {
        static readonly ProfilerMarker UpdateMarker = new("CrystalDepthPrisms.LateUpdate");
        static readonly int DepthStateID = Shader.PropertyToID("_DepthState");
        static readonly int DepthDrivenID = Shader.PropertyToID("_DepthDriven");
        static readonly int ReachID = Shader.PropertyToID("_Reach");
        static readonly int FaceSplitID = Shader.PropertyToID("_FaceSplit");
        static readonly int SplitUnitID = Shader.PropertyToID("_SplitUnit");

        /// <summary>The shader's <c>_DepthState</c> array length. A mesh with more plates is refused.</summary>
        public const int MaxPlates = 60;

        /// <summary>A triangular prism has 6 corners - what the omni body loses to become holes.</summary>
        const int TrianglePlateCorners = 6;

        [Header("Motion")]
        [Tooltip("Depths, ratchet and free turn. CrystalDepthPrismProfile.asset is generated from the lab's pick; the omni and the Mass crystal share it.")]
        [SerializeField] CrystalDepthPrismProfileSO profile;

        [Header("Holes")]
        [Tooltip("The omni: cut the body's 20 triangular plates out, so the prisms fill real holes. The body is the one SkinnedMeshRenderer among this object's siblings. Off for the Mass crystal, which has no body.")]
        [SerializeField] bool cutBodyHoles;

        MeshRenderer prisms;
        Vector3[] plateDirections;
        DepthRatchet.Column[] columns;
        readonly Vector4[] state = new Vector4[MaxPlates];
        MaterialPropertyBlock block;
        bool fresh;
        float lastStep;

        // The free turn: every sibling model, with the local pose it was authored at.
        readonly List<Transform> turned = new();
        readonly List<Pose> authored = new();
        float turnPhase;

        void Awake()
        {
            prisms = GetComponent<MeshRenderer>();
            if (!profile)
            {
                CSDebug.LogError($"CrystalDepthPrisms on '{name}': no profile assigned. The prisms will sit sealed and still.", this);
                enabled = false;
                return;
            }
            var mesh = TryGetComponent<MeshFilter>(out var filter) ? filter.sharedMesh : null;
            if (!PlateDirections.TryGet(mesh, out plateDirections, out var problem))
            {
                CSDebug.LogError($"CrystalDepthPrisms on '{name}': {problem}. Re-run the generator that bakes this mesh. The prisms will sit sealed and still.", this);
                enabled = false;
                return;
            }
            columns = new DepthRatchet.Column[plateDirections.Length];
            block = new MaterialPropertyBlock();
            WidenBounds(mesh);

            // Resolved, not serialized: the crystal's models are sibling prefab instances, which a reference
            // inside this prefab cannot name. Taken once, so nothing parented to the crystal later turns with it.
            var root = transform.parent;
            if (root)
            {
                for (int i = 0; i < root.childCount; i++)
                {
                    var child = root.GetChild(i);
                    if (!child.GetComponentInChildren<Renderer>(true)) continue;
                    turned.Add(child);
                    authored.Add(new Pose(child.localPosition, child.localRotation));
                }
            }
            // A per-instance phase, so a field of crystals does not turn in lockstep. Its own stream: the global
            // UnityEngine.Random is seeded for deterministic content.
            turnPhase = (float)new System.Random(GetInstanceID()).NextDouble() * 360f;
        }

        void Start()
        {
            if (!cutBodyHoles || !enabled) return;
            // Start, not Awake: CrystalFlipWave (on the crystal root) dresses the body in its skin twin in its own
            // Awake, and the cut keeps that twin's bones by cutting the mesh the body ends up drawing.
            SkinnedMeshRenderer body = null;
            int found = 0;
            foreach (var child in turned)
            {
                if (child == transform) continue;
                foreach (var skinned in child.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    body = skinned;
                    found++;
                }
            }
            string problem = null;
            if (found != 1 || !RhombusSkinBaker.TryCutPlates(body, TrianglePlateCorners, out problem))
            {
                string why = found != 1 ? $"expected exactly one SkinnedMeshRenderer among the crystal's models, found {found}" : problem;
                CSDebug.LogError($"CrystalDepthPrisms on '{name}': cannot cut the body's triangle holes ({why}). The prisms will fill solid plates.", this);
            }
        }

        /// <summary>A fresh (or re-pooled) crystal seats every column where its depth puts it.</summary>
        void OnEnable() => fresh = true;

        void OnDisable()
        {
            // Hand the authored pose back, so a pooled crystal or a capture copy starts from rest.
            for (int i = 0; i < turned.Count; i++)
                if (turned[i]) turned[i].SetLocalPositionAndRotation(authored[i].position, authored[i].rotation);
        }

        void LateUpdate()
        {
            using var _ = UpdateMarker.Auto();
            // A crystal nobody can see does no work. Its turn is a pure function of the clock and its ratchet
            // follows depth, so it is right again the frame it comes back into view.
            if (!prisms.isVisible) return;

            Turn(Time.time);

            var cam = Camera.main;
            if (!cam) return;
            Vector3 toCamera = transform.InverseTransformPoint(cam.transform.position);
            if (toCamera.sqrMagnitude < 1e-8f) return;
            toCamera.Normalize();

            float now = Time.time, dt = fresh ? 0f : Mathf.Max(0f, now - lastStep);
            lastStep = now;
            for (int j = 0; j < columns.Length; j++)
            {
                float depth = Vector3.Dot(plateDirections[j], toCamera);
                float seat = profile.Seat(depth);
                columns[j] = fresh
                    ? DepthRatchet.Column.At(seat)
                    : DepthRatchet.Step(columns[j], depth, seat, dt, profile.ReseatDepth, profile.ReseatFadeSeconds);
                state[j] = new Vector4(columns[j].Place, columns[j].Fade, columns[j].LeavingPlace, 0f);
            }
            fresh = false;

            prisms.GetPropertyBlock(block);
            block.SetVectorArray(DepthStateID, state);
            block.SetFloat(DepthDrivenID, 1f);
            prisms.SetPropertyBlock(block);
        }

        /// <summary>Turns every model of the crystal about the profile's axis, which itself swings round up.</summary>
        void Turn(float time)
        {
            if (turned.Count == 0) return;
            float tilt = profile.TurnTiltDegrees * Mathf.Deg2Rad;
            var axis = new Vector3(Mathf.Sin(tilt), Mathf.Cos(tilt), 0f);
            var turn = Quaternion.AngleAxis(profile.AxisWobbleDegreesPerSecond * time, Vector3.up)
                     * Quaternion.AngleAxis(turnPhase + profile.TurnDegreesPerSecond * time, axis);
            for (int i = 0; i < turned.Count; i++)
                if (turned[i]) turned[i].SetLocalPositionAndRotation(turn * authored[i].position, turn * authored[i].rotation);
        }

        /// <summary>
        /// The mesh sits seated (place 1); the shader carries a prism out to <c>_Reach</c> times its radius at the
        /// top of its column, plus its face split. Widen the culling bounds by that, or a crystal whose body is off
        /// screen culls the prisms standing out from it.
        /// </summary>
        void WidenBounds(Mesh mesh)
        {
            var material = prisms.sharedMaterial;
            float reach = material && material.HasProperty(ReachID) ? Mathf.Max(1f, material.GetFloat(ReachID)) : 1f;
            float split = material && material.HasProperty(FaceSplitID) && material.HasProperty(SplitUnitID)
                ? material.GetFloat(FaceSplitID) * material.GetFloat(SplitUnitID) : 0f;
            var b = mesh.bounds;
            float radius = b.extents.magnitude + b.center.magnitude;
            prisms.localBounds = new Bounds(Vector3.zero, Vector3.one * 2f * (radius * reach + split * reach));
        }

        /// <summary>Each plate's outward direction, read once per mesh from TEXCOORD2 (centre xyz, plate index w).</summary>
        static class PlateDirections
        {
            static readonly Dictionary<Mesh, Vector3[]> s_cache = new();

            [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
            static void ResetCache() => s_cache.Clear();

            public static bool TryGet(Mesh mesh, out Vector3[] directions, out string problem)
            {
                directions = null;
                if (!mesh)
                {
                    problem = "no MeshFilter mesh";
                    return false;
                }
                if (s_cache.TryGetValue(mesh, out directions))
                {
                    problem = null;
                    return true;
                }
                if (!mesh.isReadable)
                {
                    problem = $"mesh '{mesh.name}' is not Read/Write enabled, so its plate centres cannot be read";
                    return false;
                }
                var plates = new List<Vector4>();
                mesh.GetUVs(2, plates);
                if (plates.Count != mesh.vertexCount)
                {
                    problem = $"mesh '{mesh.name}' carries no plate data in TEXCOORD2";
                    return false;
                }
                int count = 0;
                foreach (var p in plates) count = Mathf.Max(count, Mathf.RoundToInt(p.w) + 1);
                if (count < 1 || count > MaxPlates)
                {
                    problem = $"mesh '{mesh.name}' has {count} plates (1..{MaxPlates} supported)";
                    return false;
                }
                directions = new Vector3[count];
                foreach (var p in plates)
                {
                    var centre = new Vector3(p.x, p.y, p.z);
                    if (centre.sqrMagnitude > 0f) directions[Mathf.RoundToInt(p.w)] = centre.normalized;
                }
                for (int j = 0; j < count; j++)
                {
                    if (directions[j] != Vector3.zero) continue;
                    problem = $"mesh '{mesh.name}' plate {j} has no centre";
                    directions = null;
                    return false;
                }
                s_cache[mesh] = directions;
                problem = null;
                return true;
            }
        }
    }
}
