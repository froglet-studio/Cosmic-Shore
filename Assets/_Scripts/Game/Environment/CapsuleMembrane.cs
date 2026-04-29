using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace CosmicShore.Game
{
    /// <summary>
    /// Renders an icospheric arrangement of capsules as the cell membrane.
    /// All capsules are drawn in a single instanced draw call via Graphics.RenderMeshInstanced.
    /// Radial pulsing is computed CPU-side via Perlin noise and baked into the per-instance
    /// transform matrices each frame. This allows any material/shader to be used (e.g. SpindleMaterial).
    ///
    /// Setup: Attach to a GameObject, assign any material (SpindleMaterial works directly).
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

        public float Radius => radius;

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

        [Header("Radial Pulse")]
        [Tooltip("Spatial frequency of the Perlin noise driving the radial pulse.")]
        [SerializeField] float noiseFrequency = 0.6f;

        [Tooltip("Maximum radial displacement in world units.")]
        [SerializeField] float noiseAmplitude = 40f;

        [Tooltip("How fast the pulse pattern drifts over time.")]
        [SerializeField] float pulseSpeed = 0.4f;

        [Header("Rendering")]
        [SerializeField] Material membraneMaterial;

        [Tooltip("Rendering layer mask for the instanced draw.")]
        [SerializeField] uint renderingLayerMask = 1;

        Matrix4x4[] matrices;
        RenderParams renderParams;
        Mesh meshToRender;

        // Per-capsule data computed once at startup, reused every frame
        Vector3[] baseDirections;
        float[] baseRadii;
        Quaternion[] rotations;
        // Noise sampling coordinates (one per capsule, derived from jittered position)
        Vector3[] noiseCoords;

        void Awake()
        {
            meshToRender = capsuleMesh;
            if (meshToRender == null)
                meshToRender = GetBuiltinCapsuleMesh();

            BuildBaseData();
            matrices = new Matrix4x4[baseDirections.Length];
            UpdateMatrices();

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

            UpdateMatrices();
            renderParams.worldBounds = new Bounds(transform.position, Vector3.one * (radius * 2.5f));
            Graphics.RenderMeshInstanced(renderParams, meshToRender, 0, matrices);
        }

        void BuildBaseData()
        {
            var vertices = GenerateIcosphereVertices(subdivisions);
            int count = vertices.Count;
            baseDirections = new Vector3[count];
            baseRadii = new float[count];
            rotations = new Quaternion[count];
            noiseCoords = new Vector3[count];
            var rng = new System.Random(seed);

            for (int i = 0; i < count; i++)
            {
                Vector3 radialDir = vertices[i];

                Vector3 tangent1 = Vector3.Cross(radialDir, Vector3.up).sqrMagnitude > 0.001f
                    ? Vector3.Cross(radialDir, Vector3.up).normalized
                    : Vector3.Cross(radialDir, Vector3.right).normalized;
                Vector3 tangent2 = Vector3.Cross(radialDir, tangent1).normalized;

                float jitterU = ((float)rng.NextDouble() * 2f - 1f) * placementJitter;
                float jitterV = ((float)rng.NextDouble() * 2f - 1f) * placementJitter;
                float jitterR = ((float)rng.NextDouble() * 2f - 1f) * radialJitter;

                Vector3 jitteredDir = (radialDir + tangent1 * jitterU + tangent2 * jitterV).normalized;

                baseDirections[i] = jitteredDir;
                baseRadii[i] = radius * (1f + jitterR);
                rotations[i] = Quaternion.LookRotation(tangent1, jitteredDir);

                // Pre-scale the noise coordinate so we only multiply by time in the hot loop
                noiseCoords[i] = jitteredDir * noiseFrequency;
            }
        }

        void UpdateMatrices()
        {
            Vector3 center = transform.position;
            float time = Time.time * pulseSpeed;
            int count = baseDirections.Length;

            for (int i = 0; i < count; i++)
            {
                Vector3 nc = noiseCoords[i];
                // Sample Perlin noise at the capsule's sphere-surface coordinate + animated time offset
                // Unity's PerlinNoise is 2D, so we take two samples for a pseudo-3D effect
                // then we convert this to a vector3 with three different noise samples for more interesting variation between capsules
                float noise = Mathf.PerlinNoise(nc.x + time, nc.y + time * 0.7f) * 2f - 1f;
                noise += (Mathf.PerlinNoise(nc.y + time * 0.3f, nc.z + time * 0.5f) * 2f - 1f) * 0.5f;
                noise *= 0.667f; // normalize back to roughly -1..1
                Vector3 noiseVec = new Vector3(
                                       Mathf.PerlinNoise(nc.y + time * 0.3f, nc.z + time * 0.5f) * 2f - 1f,
                                                          Mathf.PerlinNoise(nc.z + time * 0.8f, nc.x + time * 0.2f) * 2f - 1f,
                                                                             Mathf.PerlinNoise(nc.x + time * 0.6f, nc.y + time * 0.4f) * 2f - 1f
                                                                                            );





                float r = baseRadii[i];// + noise * noiseAmplitude;
                Quaternion eulerNoise = Quaternion.Euler(noiseAmplitude * noiseVec);
                rotations[i] = Quaternion.LookRotation(eulerNoise * baseDirections[i], eulerNoise * Vector3.Cross(baseDirections[i], Vector3.up).normalized);
                Vector3 position = center + baseDirections[i] * r;
                matrices[i] = Matrix4x4.TRS(position, rotations[i], capsuleScale);
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
