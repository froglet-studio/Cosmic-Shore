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
    /// <c>MassDepthPrisms</c>), whose material is on <c>OmniDepthPrismShader</c>. On the first frame after it is
    /// enabled it turns the crystal's models - every sibling under the crystal root that carries a renderer, itself
    /// included, never the root - to the crystal's own random pose (<see cref="CrystalPoseJitter"/>, seeded from
    /// the spawn position, lab round 10), so a field of crystals does not show one shared animation; the authored
    /// pose comes back on disable. Each frame it is visible it:
    ///  1. steps every column's ratchet against the GAMEPLAY camera (<see cref="ViewCamera"/>), in the crystal's
    ///     own space - the pilot turning round a crystal is what runs the ratchet. The crystal itself holds
    ///     still (the lab's free turn was dropped on 2026-10-12);
    ///     The seal sits at the camera's horizon (<see cref="CrystalDepthPrismProfileSO.SealAtHorizon"/>), so every
    ///     prism the camera can see is flush, and a prism may climb its column while its column top is hidden behind
    ///     the body (<see cref="CrystalDepthPrismProfileSO.RiseWhileHidden"/>) - Omni Motion Lab rounds 1 and 3;
    ///  2. writes the columns to the renderer's property block (<c>_DepthState[plate]</c> = place, fade,
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
        static readonly int PlateScaleStartID = Shader.PropertyToID("_PlateScaleStart");

        /// <summary>The shader's <c>_DepthState</c> array length. A mesh with more plates is refused.</summary>
        public const int MaxPlates = 60;

        /// <summary>A triangular prism has 6 corners - what the omni body loses to become holes.</summary>
        const int TrianglePlateCorners = 6;

        [Header("Motion")]
        [Tooltip("Depths and ratchet. CrystalDepthPrismProfile.asset is generated from the lab's pick; the omni and the Mass crystal share it.")]
        [SerializeField] CrystalDepthPrismProfileSO profile;

        [Header("Holes")]
        [Tooltip("The omni: cut the body's 20 triangular plates out, so the prisms fill real holes. The body is the one SkinnedMeshRenderer among this object's siblings. Off for the Mass crystal, which has no body.")]
        [SerializeField] bool cutBodyHoles;

        MeshRenderer prisms;
        // The crystal's models (sibling prefab instances, so resolved, not serialized) and their authored poses.
        readonly List<Transform> models = new();
        readonly List<Pose> authored = new();
        bool needsPose;
        Vector3[] plateDirections;
        Vector3[] plateCentres;      // seated, mesh space (TEXCOORD2)
        float[] topHeights;          // each prism's top face above its centre, along its direction
        float plateRadius;           // the plates' mean centre radius: the horizon and the occluder are measured by it
        DepthRatchet.Column[] columns;
        readonly Vector4[] state = new Vector4[MaxPlates];
        MaterialPropertyBlock block;
        bool fresh;
        float lastStep;

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
            if (!PlateDirections.TryGet(mesh, out plateDirections, out var problem)
                || !PlateDirections.TryGetShape(mesh, out plateCentres, out topHeights, out plateRadius, out problem))
            {
                CSDebug.LogError($"CrystalDepthPrisms on '{name}': {problem}. Re-run the generator that bakes this mesh. The prisms will sit sealed and still.", this);
                enabled = false;
                return;
            }
            columns = new DepthRatchet.Column[plateDirections.Length];
            block = new MaterialPropertyBlock();
            WidenBounds(mesh);

            // Taken once, so nothing parented to the crystal later (a capture effect) turns with it.
            var root = transform.parent;
            for (int i = 0; root && i < root.childCount; i++)
            {
                var child = root.GetChild(i);
                if (!child.GetComponentInChildren<Renderer>(true)) continue;
                models.Add(child);
                authored.Add(new Pose(child.localPosition, child.localRotation));
            }
        }

        void Start()
        {
            if (!cutBodyHoles || !enabled) return;
            // Start, not Awake: CrystalFlipWave (on the crystal root) dresses the body in its skin twin in its own
            // Awake, and the cut keeps that twin's bones by cutting the mesh the body ends up drawing.
            SkinnedMeshRenderer body = null;
            int found = 0;
            foreach (var child in models)
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

        /// <summary>A fresh (or re-pooled) crystal seats every column where its depth puts it, and takes the pose of
        /// wherever it now is.</summary>
        void OnEnable()
        {
            fresh = true;
            needsPose = true;
        }

        void OnDisable()
        {
            // Hand the authored pose back, so a pooled crystal or a capture copy starts from rest.
            for (int i = 0; i < models.Count; i++)
                if (models[i]) models[i].SetLocalPositionAndRotation(authored[i].position, authored[i].rotation);
        }

        /// <summary>
        /// Turns every model about the crystal root's origin to this crystal's pose. Run on the first frame after
        /// enable rather than in OnEnable, because a pooled crystal is enabled before it is moved to its spawn.
        /// </summary>
        void ApplyPose()
        {
            needsPose = false;
            if (models.Count == 0 || profile.OrientationJitter <= 0f) return;
            var root = transform.parent;
            var pose = CrystalPoseJitter.Pose(CrystalPoseJitter.SeedOf(root ? root.position : transform.position), profile.OrientationJitter);
            for (int i = 0; i < models.Count; i++)
                if (models[i]) models[i].SetLocalPositionAndRotation(pose * authored[i].position, pose * authored[i].rotation);
        }

        void LateUpdate()
        {
            using var _ = UpdateMarker.Auto();
            if (needsPose) ApplyPose();
            // A crystal nobody can see does no work. Its ratchet follows depth, so it is right again the frame
            // it comes back into view.
            if (!prisms.isVisible) return;

            var cam = ViewCamera();
            if (!cam) return;
            Vector3 eye = transform.InverseTransformPoint(cam.transform.position);
            float distance = eye.magnitude;
            if (distance < 1e-4f) return;
            Vector3 toCamera = eye / distance;

            float now = Time.time, dt = fresh ? 0f : Mathf.Max(0f, now - lastStep);
            lastStep = now;
            float seal = profile.SealFor(plateRadius, distance);
            bool rise = profile.RiseWhileHidden && !fresh;
            var material = rise ? prisms.sharedMaterial : null;
            float reach = material && material.HasProperty(ReachID) ? material.GetFloat(ReachID) : 1f;
            float sizeAtTop = material && material.HasProperty(PlateScaleStartID) ? material.GetFloat(PlateScaleStartID) : 1f;
            float occluder = profile.OccluderRadius * plateRadius;
            for (int j = 0; j < columns.Length; j++)
            {
                float depth = Vector3.Dot(plateDirections[j], toCamera);
                float seat = profile.Seat(depth, seal);
                if (fresh)
                {
                    columns[j] = DepthRatchet.Column.At(seat);
                }
                else
                {
                    bool hidden = false;
                    float target = 0f;
                    if (rise && seat < columns[j].Place && columns[j].Fade >= 1f)
                    {
                        target = DepthRatchet.RiseTarget(columns[j], seat, dt, profile.RiseRate);
                        hidden = DepthRatchet.HiddenBehindBody(
                            DepthRatchet.ColumnTop(plateCentres[j], plateDirections[j], topHeights[j], target, reach, sizeAtTop), eye, occluder);
                    }
                    columns[j] = DepthRatchet.Step(columns[j], depth, seat, dt, profile.ReseatDepth, profile.ReseatFadeSeconds, hidden, target);
                }
                state[j] = new Vector4(columns[j].Place, columns[j].Fade, columns[j].LeavingPlace, 0f);
            }
            fresh = false;

            prisms.GetPropertyBlock(block);
            block.SetVectorArray(DepthStateID, state);
            block.SetFloat(DepthDrivenID, 1f);
            prisms.SetPropertyBlock(block);
        }

        /// <summary>The gameplay camera (<see cref="CameraManager.ViewCamera"/>), NOT <c>Camera.main</c>: a ratchet run
        /// against Bootstrap's menu camera held every prism still (round 9).</summary>
        static Camera ViewCamera() => CameraManager.ViewCamera;

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
            static readonly Dictionary<Mesh, (Vector3[] centres, float[] tops, float radius)> s_shape = new();

            [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
            static void ResetCache()
            {
                s_cache.Clear();
                s_shape.Clear();
            }

            /// <summary>
            /// Each plate's seated centre, its prism's top face height above that centre (along the plate's direction),
            /// and the plates' mean centre radius - what the horizon seal and the hidden-rise test measure with. Read
            /// once per mesh; call after <see cref="TryGet"/> succeeded.
            /// </summary>
            public static bool TryGetShape(Mesh mesh, out Vector3[] centres, out float[] tops, out float radius, out string problem)
            {
                if (s_shape.TryGetValue(mesh, out var shape))
                {
                    (centres, tops, radius) = shape;
                    problem = null;
                    return true;
                }
                var directions = s_cache[mesh];
                var plates = new List<Vector4>();
                mesh.GetUVs(2, plates);
                var verts = mesh.vertices;
                int count = directions.Length;
                centres = new Vector3[count];
                tops = new float[count];
                for (int j = 0; j < count; j++) tops[j] = float.NegativeInfinity;
                for (int i = 0; i < plates.Count; i++)
                {
                    int j = Mathf.RoundToInt(plates[i].w);
                    var centre = new Vector3(plates[i].x, plates[i].y, plates[i].z);
                    centres[j] = centre;
                    tops[j] = Mathf.Max(tops[j], Vector3.Dot(verts[i] - centre, directions[j]));
                }
                radius = 0f;
                foreach (var c in centres) radius += c.magnitude;
                radius /= count;
                if (radius <= 0f)
                {
                    problem = $"mesh '{mesh.name}' has its plate centres at the origin";
                    return false;
                }
                s_shape[mesh] = (centres, tops, radius);
                problem = null;
                return true;
            }

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
