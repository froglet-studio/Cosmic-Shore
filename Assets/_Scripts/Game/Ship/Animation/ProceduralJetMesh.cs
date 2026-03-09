using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class ProceduralJetMesh : MonoBehaviour
{
    public int segments = 8;
    public float length = 5f;
    public AnimationCurve radiusCurve = AnimationCurve.Linear(0, 1, 1, 0);
    public float uvScale = 1f;
    public float uvScrollSpeed = 1f;

    private Mesh mesh;
    private Material _material;
    private static readonly int UVOffsetID = Shader.PropertyToID("_UVOffset");
    private bool _shaderSupportsOffset;

    void Start()
    {
        GenerateMesh();

        // Try shader-driven UV scroll first (zero CPU cost per frame)
        var renderer = GetComponent<MeshRenderer>();
        if (renderer) _material = renderer.material;
        _shaderSupportsOffset = _material != null && _material.HasProperty(UVOffsetID);
    }

    void Update()
    {
        if (_shaderSupportsOffset)
        {
            // GPU-driven UV scroll — no mesh upload, no CPU cost
            _material.SetFloat(UVOffsetID, Time.time * uvScrollSpeed);
        }
        else
        {
            // Fallback: update UVs on mesh but reuse the existing array (no allocation)
            UpdateUVs();
        }
    }

    void GenerateMesh()
    {
        mesh = new Mesh();
        GetComponent<MeshFilter>().mesh = mesh;

        var vertices = new Vector3[(segments + 1) * 2];
        var uv = new Vector2[(segments + 1) * 2];
        var triangles = new int[segments * 6];

        for (int i = 0; i <= segments; i++)
        {
            float t = (float)i / segments;
            float radius = radiusCurve.Evaluate(t);
            float z = t * length;

            vertices[i * 2] = new Vector3(radius, 0, z);
            vertices[i * 2 + 1] = new Vector3(-radius, 0, z);

            uv[i * 2] = new Vector2(t * uvScale, 0);
            uv[i * 2 + 1] = new Vector2(t * uvScale, 1);

            if (i < segments)
            {
                int baseIndex = i * 6;
                triangles[baseIndex] = i * 2;
                triangles[baseIndex + 1] = i * 2 + 1;
                triangles[baseIndex + 2] = (i + 1) * 2;
                triangles[baseIndex + 3] = i * 2 + 1;
                triangles[baseIndex + 4] = (i + 1) * 2 + 1;
                triangles[baseIndex + 5] = (i + 1) * 2;
            }
        }

        mesh.vertices = vertices;
        mesh.uv = uv;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
    }

    // Cached array — reuse to avoid allocation. Set once from mesh data.
    private Vector2[] _uvCache;

    void UpdateUVs()
    {
        if (_uvCache == null)
            _uvCache = mesh.uv;

        float offset = Time.time * uvScrollSpeed;
        for (int i = 0; i <= segments; i++)
        {
            float t = (float)i / segments;
            _uvCache[i * 2].x = (t * uvScale) + offset;
            _uvCache[i * 2 + 1].x = (t * uvScale) + offset;
        }
        mesh.uv = _uvCache;
    }
}
