using System.Collections.Generic;
using CosmicShore.Utility;
using Unity.Profiling;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Charge element's light (Omni Motion Lab round 5, <c>Docs/Studios/OMNI_MOTION_LAB.md</c>): bolts and lit plates
    /// drawn into one additive mesh in the crystal's own space, on <c>CrystalChargeArcsShader</c>.
    ///
    ///   SEAM WALK (the Charge crystal) - one bolt walks the seams between the twelve blooms forever (the dodecahedral net:
    ///   it never runs to a bloom's centre, never circles a petal, never stops), flickering like chain lightning and
    ///   fading behind itself; each petal lights by how near the bolt runs to its short sides. It REPLACES the edge crawl:
    ///   <see cref="silenceCrawlOn"/>'s discharge is held at zero while this runs (a property block, so the material - and
    ///   the hull fusion that copies it - keeps its crawl).
    ///   FLUX (the omni) - a pentagon charges, an arc leaps to a pentagon 55-125 degrees away and lands, both fade, and the
    ///   next discharge leaps on from the one it landed on. Only those two pentagons light. At each end the arc forks
    ///   smoothly onto the pentagon's five corners, and the five run on as one coaxial bundle (a braided cable) between - each
    ///   strand one ribbon the whole way, so there is no seam. Where the arc's path runs
    ///   through a raised Mass prism (the sibling <see cref="CrystalDepthPrisms"/>), that prism lights up while the arc
    ///   holds (<see cref="ChargeArcs.FluxStrikes"/>), so the bolt reads as conducting through it, not clipping.
    ///
    /// Every bolt is ONE continuous plasma ribbon (<see cref="ChargeArcMeshWriter.Plasma"/>), modelled on the Squirrel's
    /// skim crackle: a smooth curve bent by flowing noise, a Gaussian glow with a hot core, a sub-filament beside it.
    ///
    /// The sites are read once from <see cref="sourceMesh"/> (the crystal model this object overlays, in the same space)
    /// and shared by every crystal on that mesh (<see cref="ChargeSites"/>); the omni's pentagons are read at the
    /// thickness <see cref="OmniCrystalCadenceSO.PlateThickness"/> draws them. Lives on a child of the crystal root, so
    /// <see cref="CrystalDepthPrisms"/>' random pose turns it with the body. A crystal nobody can see only keeps its clock,
    /// and so does one too small on screen for a bolt to show
    /// (<see cref="CrystalChargeArcsProfileSO.SleepBelowScreenRadiusPixels"/>): it sleeps on its unlit faces.
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    [DisallowMultipleComponent]
    public class CrystalChargeArcs : MonoBehaviour
    {
        public enum Effect
        {
            SeamWalk = 1,
            Flux = 2,
        }

        static readonly ProfilerMarker UpdateMarker = new("CrystalChargeArcs.LateUpdate");
        static readonly int ArcIntensityId = Shader.PropertyToID("_ArcIntensity");
        static readonly int NodeIntensityId = Shader.PropertyToID("_NodeIntensity");
        static readonly Dictionary<(Mesh, float), ChargeSites> s_sites = new();
        static MaterialPropertyBlock s_block;

        [Header("Effect")]
        [Tooltip("Seam walk: the Charge crystal's (one bolt walking the seams between the blooms forever). Flux: the omni's (chained discharges between pentagons).")]
        [SerializeField] Effect effect = Effect.Flux;

        [Tooltip("Timing and brightness, shared by both crystals. CrystalChargeArcsProfile.asset is generated (Tools/Build/author_omni_crystal_triangles.py).")]
        [SerializeField] CrystalChargeArcsProfileSO profile;

        [Header("Geometry")]
        [Tooltip("The crystal model this light overlays (the FBX mesh its body draws), in this object's space: the Charge crystal's 60 petals or the omni's 12 pentagonal prisms are read from it.")]
        [SerializeField] Mesh sourceMesh;

        [Header("Replaces")]
        [Tooltip("A renderer whose edge crawl (_ArcIntensity, _NodeIntensity) this light replaces: held at zero through a property block while this runs, restored when it stops. The Charge crystal's shell. Empty on the omni, whose crawl overlay is gone.")]
        [SerializeField] Renderer silenceCrawlOn;

        MeshRenderer arcsRenderer;
        Mesh mesh;
        Bounds bounds;
        ChargeSites sites;
        ChargeArcSettings settings;
        ChargeSeamWalker walker;
        ChargeFluxChain chain;
        readonly ChargeArcMeshWriter writer = new();
        float[] faceLight;
        float clockStart;
        Crystal crystal;
        CrystalDepthPrisms depthPrisms;   // the omni's Mass columns a flux arc can pass through
        Vector3[] prismCentres;
        Vector3[] prismSeats;
        float[] prismRadii, prismStrike, prismPlaces, prismSeatRadii;
        bool asleep;   // too small on screen: the unlit faces are uploaded and nothing is rebuilt until it wakes

        void Awake()
        {
            arcsRenderer = GetComponent<MeshRenderer>();
            if (!profile || !sourceMesh)
            {
                CSDebug.LogError($"CrystalChargeArcs on '{name}': {(profile ? "no source mesh" : "no profile")} assigned. The Charge light is off.", this);
                enabled = false;
                return;
            }
            var cadence = GetComponentInParent<OmniCrystalCadence>();
            float thickness = cadence && cadence.Settings ? cadence.Settings.PlateThickness : 1f;
            if (!TryGetSites(sourceMesh, thickness, out sites, out var problem))
            {
                CSDebug.LogError($"CrystalChargeArcs on '{name}': mesh '{sourceMesh.name}': {problem}. The Charge light is off.", this);
                enabled = false;
                return;
            }
            settings = profile.Settings;
            faceLight = new float[sites.Faces.Length];
            // An arc bows out at most ~1.5 radii; the bounds are fixed so the renderer's visibility never depends on
            // what the light happens to be drawing.
            bounds = new Bounds(Vector3.zero, Vector3.one * (sites.Radius * 3.2f));
            mesh = new Mesh { name = sourceMesh.name + " (charge arcs)", hideFlags = HideFlags.DontSave };
            mesh.MarkDynamic();
            GetComponent<MeshFilter>().sharedMesh = mesh;
            crystal = GetComponentInParent<Crystal>();
            if (effect == Effect.Flux && transform.parent)
                depthPrisms = transform.parent.GetComponentInChildren<CrystalDepthPrisms>(true);
        }

        void OnEnable()
        {
            if (mesh == null) return;
            clockStart = Time.time;
            asleep = false;
            int seed = GetInstanceID();
            walker = new ChargeSeamWalker(sites, seed, 0f);
            chain = new ChargeFluxChain(sites, seed ^ 0x5bd1e995);
            SilenceCrawl();
            if (crystal) crystal.ModelMaterialSettled += SilenceCrawl;
            // The faces, unlit: the mesh is never empty, so the renderer can be seen (and start drawing) at once.
            System.Array.Clear(faceLight, 0, faceLight.Length);
            Upload();
        }

        void OnDisable()
        {
            if (crystal) crystal.ModelMaterialSettled -= SilenceCrawl;
            RestoreCrawl();
            if (depthPrisms && depthPrisms.Strike != null) System.Array.Clear(depthPrisms.Strike, 0, depthPrisms.Strike.Length);
        }

        void OnDestroy()
        {
            if (mesh) Destroy(mesh);
        }

        void LateUpdate()
        {
            using var _ = UpdateMarker.Auto();
            float seconds = Time.time - clockStart;
            if (effect == Effect.SeamWalk) walker.Advance(seconds, settings);
            if (!arcsRenderer.enabled || !arcsRenderer.isVisible) return;
            var view = CameraManager.ViewCamera;
            if (!view) return;
            float pixels = ScreenRadiusPixels(view);
            if (TooSmallToSee(pixels))
            {
                if (!asleep) Sleep();
                return;
            }
            bool waking = asleep;
            asleep = false;
            // Mid-distance: rebuild on alternate frames, staggered by instance so the crystals split the work.
            float half = profile.HalfRateBelowScreenRadiusPixels;
            if (!waking && half > 0f && pixels < half && ((Time.frameCount + GetInstanceID()) & 1) == 1) return;
            var eye = transform.InverseTransformPoint(view.transform.position);

            writer.Clear();
            if (effect == Effect.SeamWalk) ChargeArcs.SeamWalk(sites, walker, seconds, settings, writer, eye, faceLight);
            else
            {
                ChargeArcs.Flux(sites, chain, seconds, settings, writer, eye, faceLight);
                StrikePrisms(seconds);
            }
            Upload();
        }

        /// <summary>True when this crystal's radius on the view camera is under the profile's sleep size (1.25x that to
        /// wake, so one at the edge does not flicker between the two).</summary>
        bool TooSmallToSee(float pixels)
        {
            float limit = profile.SleepBelowScreenRadiusPixels;
            if (limit <= 0f) return false;
            if (asleep) limit *= 1.25f;
            return pixels < limit;
        }

        /// <summary>This crystal's radius on the view camera, in pixels.</summary>
        float ScreenRadiusPixels(Camera view)
        {
            float radius = sites.Radius * transform.lossyScale.x;
            float halfHeight = view.orthographic
                ? view.orthographicSize
                : Mathf.Max(1e-3f, Vector3.Distance(view.transform.position, transform.position))
                  * Mathf.Tan(view.fieldOfView * 0.5f * Mathf.Deg2Rad);
            return radius / halfHeight * view.pixelHeight * 0.5f;
        }

        /// <summary>Goes dark the way <see cref="OnEnable"/> starts: the faces unlit, no bolt, no Mass column struck.</summary>
        void Sleep()
        {
            asleep = true;
            writer.Clear();
            System.Array.Clear(faceLight, 0, faceLight.Length);
            Upload();
            if (depthPrisms && depthPrisms.Strike != null) System.Array.Clear(depthPrisms.Strike, 0, depthPrisms.Strike.Length);
        }

        /// <summary>Lights every protracted Mass prism a flux arc is passing through: each prism's body is carried from the
        /// prisms' space into this one and tested against the arcs' paths.</summary>
        void StrikePrisms(float seconds)
        {
            if (!depthPrisms || !depthPrisms.isActiveAndEnabled || depthPrisms.Strike == null) return;
            int count = depthPrisms.PrismCount;
            if (prismCentres == null || prismCentres.Length != count)
            {
                prismCentres = new Vector3[count];
                prismRadii = new float[count];
                prismStrike = new float[count];
                prismPlaces = new float[count];
                prismSeats = new Vector3[count];
                prismSeatRadii = new float[count];
            }
            var from = depthPrisms.transform;
            float scale = from.lossyScale.x / Mathf.Max(1e-6f, transform.lossyScale.x);
            for (int j = 0; j < count; j++)
            {
                depthPrisms.TryGetPrism(j, out var centre, out float radius);
                prismCentres[j] = transform.InverseTransformPoint(from.TransformPoint(centre));
                prismRadii[j] = radius * scale;
                prismPlaces[j] = depthPrisms.PlaceOf(j);
                depthPrisms.SeatOf(j, out var seat, out float seatRadius);
                prismSeats[j] = transform.InverseTransformPoint(from.TransformPoint(seat));
                prismSeatRadii[j] = seatRadius * scale;
            }
            // Only a prism standing out of the omni lights, when an arc crosses its column; a retracted one stays dark.
            ChargeArcs.FluxStrikes(sites, chain, seconds, settings, prismCentres, prismRadii, prismPlaces, prismSeats, prismSeatRadii,
                                   count, prismStrike);
            System.Array.Copy(prismStrike, depthPrisms.Strike, count);
        }

        /// <summary>The lit faces (after the bolts already in the writer), then the whole mesh to the GPU.</summary>
        void Upload()
        {
            // A petal beside the seam walk lights from its own centre; a discharging pentagon from its site's.
            bool own = effect == Effect.SeamWalk;
            for (int f = 0; f < sites.Faces.Length; f++) writer.Face(sites.Faces[f], faceLight[f], own);
            mesh.Clear();
            mesh.SetVertices(writer.Positions);
            mesh.SetUVs(0, writer.Data);
            mesh.SetTriangles(writer.Indices, 0, false);
            mesh.bounds = bounds;
            writer.Clear();
        }

        static bool TryGetSites(Mesh source, float thickness, out ChargeSites built, out string problem)
        {
            problem = null;
            if (s_sites.TryGetValue((source, thickness), out built)) return true;
            if (!source.isReadable)
            {
                problem = "not readable (enable Read/Write in its import settings)";
                return false;
            }
            if (!ChargeSites.TryBuild(source.vertices, source.triangles, thickness, out built, out problem)) return false;
            s_sites[(source, thickness)] = built;
            return true;
        }

        void SilenceCrawl()
        {
            if (!silenceCrawlOn) return;
            s_block ??= new MaterialPropertyBlock();
            silenceCrawlOn.GetPropertyBlock(s_block);
            s_block.SetFloat(ArcIntensityId, 0f);
            s_block.SetFloat(NodeIntensityId, 0f);
            silenceCrawlOn.SetPropertyBlock(s_block);
        }

        void RestoreCrawl()
        {
            if (!silenceCrawlOn) return;
            var material = silenceCrawlOn.sharedMaterial;
            if (!material) return;
            s_block ??= new MaterialPropertyBlock();
            silenceCrawlOn.GetPropertyBlock(s_block);
            if (material.HasProperty(ArcIntensityId)) s_block.SetFloat(ArcIntensityId, material.GetFloat(ArcIntensityId));
            if (material.HasProperty(NodeIntensityId)) s_block.SetFloat(NodeIntensityId, material.GetFloat(NodeIntensityId));
            silenceCrawlOn.SetPropertyBlock(s_block);
        }
    }
}
