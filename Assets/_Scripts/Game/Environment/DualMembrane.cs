using UnityEngine;

namespace CosmicShore.Game
{
    /// <summary>
    /// Generates a hex/pentagon dual membrane mesh at runtime from a source mesh.
    ///
    /// Attach to a GameObject with MeshFilter + MeshRenderer. Assign a triangulated
    /// source mesh (e.g., icosphere FBX) and this component will compute the
    /// topological dual at Awake — producing a Goldberg polyhedron with hexagons
    /// and pentagons that edge-detection shaders can pick up cleanly.
    ///
    /// Supports optional radial undulation driven by Perlin noise, animated per-frame
    /// for an organic breathing membrane effect.
    ///
    /// Drop-in replacement for the static MembraneBase prefab — assign to
    /// CellConfigDataSO.MembranePrefab.
    /// </summary>
    [RequireComponent(typeof(MeshFilter))]
    [RequireComponent(typeof(MeshRenderer))]
    public class DualMembrane : MonoBehaviour
    {
        [Header("Source Mesh")]
        [Tooltip("Triangulated source mesh to dualize. Must have Read/Write enabled in import settings.")]
        [SerializeField] Mesh sourceMesh;

        [Header("Dual Mesh Settings")]
        [Tooltip("Project dual vertices onto the average-radius sphere for a clean spherical shape.")]
        [SerializeField] bool projectToSphere = true;

        [Tooltip("Use per-face normals so polygon edges are visible to edge-detection shaders.")]
        [SerializeField] bool flatShading = true;

        [Header("Undulation")]
        [Tooltip("Enable animated radial undulation for organic breathing effect.")]
        [SerializeField] bool enableUndulation;

        [Tooltip("Amplitude of radial displacement.")]
        [SerializeField] float undulationAmplitude = 0.02f;

        [Tooltip("Spatial frequency of the noise pattern across the sphere.")]
        [SerializeField] float undulationFrequency = 4f;

        [Tooltip("Speed at which the undulation pattern drifts over time.")]
        [SerializeField] float undulationSpeed = 0.3f;

        // Cached topology — computed once, reused every frame for undulation
        DualMeshUtility.DualResult? _topology;
        Vector3[] _basePositions;
        Mesh _mesh;
        MeshFilter _meshFilter;

        void Awake()
        {
            _meshFilter = GetComponent<MeshFilter>();
            GenerateDualMesh();
        }

        void Update()
        {
            if (!enableUndulation || !_topology.HasValue || _mesh == null) return;
            AnimateUndulation();
        }

        /// <summary>
        /// Compute the dual topology and build the initial mesh.
        /// </summary>
        void GenerateDualMesh()
        {
            if (sourceMesh == null) return;

            _topology = DualMeshUtility.ComputeDualTopology(sourceMesh, projectToSphere);
            if (!_topology.HasValue) return;

            var topo = _topology.Value;

            // Snapshot base positions for undulation animation
            _basePositions = new Vector3[topo.DualVertices.Length];
            System.Array.Copy(topo.DualVertices, _basePositions, topo.DualVertices.Length);

            _mesh = DualMeshUtility.BuildMesh(gameObject.name + "_DualMembrane", topo, flatShading);
            _meshFilter.mesh = _mesh;
        }

        /// <summary>
        /// Per-frame: displace dual vertices radially with drifting Perlin noise,
        /// then rebuild the mesh positions and normals.
        /// </summary>
        void AnimateUndulation()
        {
            var topo = _topology.Value;
            float phase = Time.time * undulationSpeed;

            // Displace each dual vertex radially
            var vertices = _mesh.vertices;
            int dualVertCount = _basePositions.Length;

            // The mesh has more vertices than the dual (fan-triangulation duplicates them).
            // We need to recompute dual positions, then rebuild the full mesh.
            // For performance, we update dual vertex positions and rebuild only the vertex buffer.

            for (int i = 0; i < dualVertCount; i++)
            {
                Vector3 n = _basePositions[i].normalized;
                float baseR = _basePositions[i].magnitude;
                float undulation = DualMeshUtility.SampleUndulation(n, undulationFrequency, phase) * undulationAmplitude;
                topo.DualVertices[i] = n * (baseR + undulation);
            }

            // Rebuild the full mesh from displaced topology
            var rebuilt = DualMeshUtility.BuildMesh(
                _mesh.name, topo, flatShading);

            // Swap buffers rather than creating a new Mesh object each frame
            _mesh.SetVertices(rebuilt.vertices);
            _mesh.SetNormals(rebuilt.normals);
            _mesh.RecalculateBounds();

            // Clean up the temp mesh
            Destroy(rebuilt);
        }

        /// <summary>
        /// Regenerate the dual mesh at runtime (e.g., after changing source mesh or settings).
        /// </summary>
        public void Rebuild()
        {
            if (_mesh != null) Destroy(_mesh);
            GenerateDualMesh();
        }

        void OnDestroy()
        {
            if (_mesh != null) Destroy(_mesh);
        }
    }
}
