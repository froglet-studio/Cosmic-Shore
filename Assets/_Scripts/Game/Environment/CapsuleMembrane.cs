using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace CosmicShore.Game
{
    /// <summary>
    /// Renders an icospheric arrangement of capsules as the cell membrane.
    /// All capsules are drawn in a single instanced draw call via Graphics.RenderMeshInstanced.
    /// Perlin noise radial pulsing is computed entirely in the shader — no per-frame CPU cost.
    ///
    /// Setup: Attach to a GameObject, assign a material using CosmicShore/CapsuleMembrane shader.
    /// The component generates icosphere vertex positions at the configured subdivision level,
    /// places one capsule at each vertex oriented radially, and renders them every frame.
    /// </summary>
    public class CapsuleMembrane : MonoBehaviour
    {
        [Header("Icosphere Layout")]
        [Tooltip("Subdivision level. 0=12, 1=42, 2=162, 3=642 capsules.")]
        [Range(0, 4)]
        [SerializeField] int subdivisions = 2;

        [Tooltip("Radius of the membrane sphere.")]
        [SerializeField] float radius = 500f;

        [Header("Capsule Shape")]
        [Tooltip("Mesh to instance. Leave null to use Unity's built-in capsule.")]
        [SerializeField] Mesh capsuleMesh;

        [Tooltip("Scale of each capsule in local space (X=width, Y=height/length, Z=depth).")]
        [SerializeField] Vector3 capsuleScale = new(12f, 30f, 12f);

        [Header("Placement Noise")]
        [Tooltip("How much each capsule's position is jittered tangentially on the sphere surface.")]
        [SerializeField] float placementJitter = 0.15f;

        [Tooltip("How much each capsule's radial distance varies from the base radius.")]
        [SerializeField] float radialJitter = 0.05f;

        [Tooltip("Seed for deterministic placement noise.")]
        [SerializeField] int seed = 42;

        [Header("Rendering")]
        [SerializeField] Material membraneMaterial;

        [Tooltip("Rendering layer mask for the instanced draw.")]
        [SerializeField] uint renderingLayerMask = 1;

        Matrix4x4[] matrices;
        RenderParams renderParams;
        Mesh meshToRender;

        void Awake()
        {
            meshToRender = capsuleMesh;
            if (meshToRender == null)
                meshToRender = GetBuiltinCapsuleMesh();

            BuildMatrices();

            renderParams = new RenderParams(membraneMaterial)
            {
                worldBounds = new Bounds(transform.position, Vector3.one * (radius * 2.5f)),
                shadowCastingMode = ShadowCastingMode.Off,
                receiveShadows = false,
                renderingLayerMask = renderingLayerMask,
            };
        }

        void Update()
        {
            if (meshToRender == null || membraneMaterial == null) return;

            // Update bounds to follow the membrane if it moves
            renderParams.worldBounds = new Bounds(transform.position, Vector3.one * (radius * 2.5f));
            Graphics.RenderMeshInstanced(renderParams, meshToRender, 0, matrices);
        }

        void BuildMatrices()
        {
            var vertices = GenerateIcosphereVertices(subdivisions);
            matrices = new Matrix4x4[vertices.Count];
            var worldPos = transform.position;
            var rng = new System.Random(seed);

            for (int i = 0; i < vertices.Count; i++)
            {
                Vector3 radialDir = vertices[i]; // already normalized

                // Tangential jitter: pick two perpendicular tangent vectors, offset along them
                Vector3 tangent1 = Vector3.Cross(radialDir, Vector3.up).sqrMagnitude > 0.001f
                    ? Vector3.Cross(radialDir, Vector3.up).normalized
                    : Vector3.Cross(radialDir, Vector3.right).normalized;
                Vector3 tangent2 = Vector3.Cross(radialDir, tangent1).normalized;

                float jitterU = ((float)rng.NextDouble() * 2f - 1f) * placementJitter;
                float jitterV = ((float)rng.NextDouble() * 2f - 1f) * placementJitter;
                float jitterR = ((float)rng.NextDouble() * 2f - 1f) * radialJitter;

                // Jitter the direction on the sphere surface, then re-normalize
                Vector3 jitteredDir = (radialDir + tangent1 * jitterU + tangent2 * jitterV).normalized;
                float jitteredRadius = radius * (1f + jitterR);
                Vector3 position = worldPos + jitteredDir * jitteredRadius;

                // Orient capsule so local Y points radially outward
                Quaternion rotation = Quaternion.LookRotation(tangent1, jitteredDir);

                matrices[i] = Matrix4x4.TRS(position, rotation, capsuleScale);
            }
        }

        static Mesh GetBuiltinCapsuleMesh()
        {
            var temp = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            var mesh = temp.GetComponent<MeshFilter>().sharedMesh;
            DestroyImmediate(temp);
            return mesh;
        }

        // ---- Icosphere generation (matches SpawnableSpherene algorithm) ----

        static List<Vector3> GenerateIcosphereVertices(int subdivisionLevel)
        {
            float phi = (1f + Mathf.Sqrt(5f)) * 0.5f;
            float a = 1f;
            float b = 1f / phi;

            var vertices = new List<Vector3>
            {
                new( 0,  b, -a),
                new( b,  a,  0),
                new(-b,  a,  0),
                new( 0,  b,  a),
                new( 0, -b,  a),
                new(-a,  0,  b),
                new( 0, -b, -a),
                new( a,  0, -b),
                new( a,  0,  b),
                new(-a,  0, -b),
                new( b, -a,  0),
                new(-b, -a,  0),
            };

            for (int i = 0; i < vertices.Count; i++)
                vertices[i] = vertices[i].normalized;

            var triangles = new List<int>
            {
                2, 1, 0,   1, 2, 3,   5, 2, 3,   3, 8, 1,   3, 4, 8,
                3, 5, 4,   0, 1, 7,   7, 1, 8,   6, 7, 8,   6, 8,10,
                8, 4,10,   4,11,10,   4, 5,11,  11, 5, 9,   9, 5, 2,
                9, 2, 0,   6, 0, 7,   9, 0, 6,  11, 9, 6,  10,11, 6,
            };

            var midpointCache = new Dictionary<(int, int), int>();

            for (int level = 0; level < subdivisionLevel; level++)
            {
                var newTriangles = new List<int>();
                midpointCache.Clear();

                for (int i = 0; i < triangles.Count; i += 3)
                {
                    int v0 = triangles[i];
                    int v1 = triangles[i + 1];
                    int v2 = triangles[i + 2];

                    int m01 = GetMidpoint(v0, v1, vertices, midpointCache);
                    int m12 = GetMidpoint(v1, v2, vertices, midpointCache);
                    int m20 = GetMidpoint(v2, v0, vertices, midpointCache);

                    newTriangles.AddRange(new[] { v0, m01, m20 });
                    newTriangles.AddRange(new[] { v1, m12, m01 });
                    newTriangles.AddRange(new[] { v2, m20, m12 });
                    newTriangles.AddRange(new[] { m01, m12, m20 });
                }

                triangles = newTriangles;
            }

            return vertices;
        }

        static int GetMidpoint(int a, int b, List<Vector3> vertices, Dictionary<(int, int), int> cache)
        {
            var key = a < b ? (a, b) : (b, a);
            if (cache.TryGetValue(key, out int index))
                return index;

            Vector3 midpoint = ((vertices[a] + vertices[b]) * 0.5f).normalized;
            index = vertices.Count;
            vertices.Add(midpoint);
            cache[key] = index;
            return index;
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, radius);
        }
    }
}
