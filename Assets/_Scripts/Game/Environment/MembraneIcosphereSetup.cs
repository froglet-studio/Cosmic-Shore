using UnityEngine;

namespace CosmicShore.Game.Environment
{
    /// <summary>
    /// Generates a subdivided icosphere mesh for the cell membrane at runtime.
    /// The mesh is generated once and cached statically across all membrane instances.
    /// If a mesh is already assigned to the MeshFilter (e.g., from an editor-generated asset),
    /// this component does nothing.
    /// </summary>
    [RequireComponent(typeof(MeshFilter))]
    [RequireComponent(typeof(MeshRenderer))]
    public class MembraneIcosphereSetup : MonoBehaviour
    {
        [Header("Icosphere")]
        [Tooltip("Subdivision level. 3 = 1280 tris (smooth enough, cheap). 4 = 5120 tris (very smooth).")]
        [Range(1, 5)]
        [SerializeField] int subdivisions = 4;

        static Mesh cachedMesh;
        static int cachedSubdivisions = -1;

        void Awake()
        {
            var meshFilter = GetComponent<MeshFilter>();
            if (meshFilter.sharedMesh != null)
                return;

            if (cachedMesh == null || cachedSubdivisions != subdivisions)
            {
                cachedMesh = GenerateIcosphere(subdivisions);
                cachedMesh.name = $"MembraneIcosphere_Sub{subdivisions}";
                cachedSubdivisions = subdivisions;
            }

            meshFilter.sharedMesh = cachedMesh;
        }

        static Mesh GenerateIcosphere(int subdivisionLevel)
        {
            float phi = (1f + Mathf.Sqrt(5f)) * 0.5f;
            float a = 1f;
            float b = 1f / phi;

            var vertices = new System.Collections.Generic.List<Vector3>
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

            var triangles = new System.Collections.Generic.List<int>
            {
                2, 1, 0,   1, 2, 3,   5, 2, 3,   3, 8, 1,   3, 4, 8,
                3, 5, 4,   0, 1, 7,   7, 1, 8,   6, 7, 8,   6, 8,10,
                8, 4,10,   4,11,10,   4, 5,11,  11, 5, 9,   9, 5, 2,
                9, 2, 0,   6, 0, 7,   9, 0, 6,  11, 9, 6,  10,11, 6,
            };

            var midpointCache = new System.Collections.Generic.Dictionary<(int, int), int>();

            for (int level = 0; level < subdivisionLevel; level++)
            {
                var newTriangles = new System.Collections.Generic.List<int>();
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

            // Flip winding order so normals point inward (player is inside)
            for (int i = 0; i < triangles.Count; i += 3)
                (triangles[i], triangles[i + 1]) = (triangles[i + 1], triangles[i]);

            var mesh = new Mesh();
            if (vertices.Count > 65535)
                mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();

            return mesh;
        }

        static int GetMidpoint(int a, int b,
            System.Collections.Generic.List<Vector3> vertices,
            System.Collections.Generic.Dictionary<(int, int), int> cache)
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
    }
}
