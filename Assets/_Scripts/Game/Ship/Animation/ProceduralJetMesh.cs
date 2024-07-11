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
    private Vector3[] vertices;
    private Vector2[] uv;
    private int[] triangles;

    void Start()
    {
        GenerateMesh();
    }

    void Update()
    {
        UpdateUVs();
    }

    void GenerateMesh()
    {
        mesh = new Mesh();
        GetComponent<MeshFilter>().mesh = mesh;

        vertices = new Vector3[(segments + 1) * 2];
        uv = new Vector2[(segments + 1) * 2];
        triangles = new int[segments * 6];

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

    void UpdateUVs()
    {
        float offset = Time.time * uvScrollSpeed;
        for (int i = 0; i <= segments; i++)
        {
            float t = (float)i / segments;
            uv[i * 2].x = (t * uvScale) + offset;
            uv[i * 2 + 1].x = (t * uvScale) + offset;
        }
        mesh.uv = uv;
    }
}