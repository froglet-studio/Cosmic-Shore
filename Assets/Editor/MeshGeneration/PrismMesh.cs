using UnityEngine;
using UnityEditor;

public static class PrismMeshGenerator
{
    [MenuItem("Assets/Create/Generate Geometry/Prism Mesh")]
    public static void CreatePrismMesh()
    {
        Mesh mesh = GenerateMesh();

        string path = EditorUtility.SaveFilePanelInProject(
            "Generate Prism Mesh",
            "Prism",
            "asset",
            "Select a location to save the cube mesh."
        );

        if (string.IsNullOrEmpty(path))
            return;

        AssetDatabase.CreateAsset(mesh, path);
        AssetDatabase.SaveAssets();
    }

    private static Mesh GenerateMesh()
    {
        Mesh mesh = new Mesh();
        mesh.name = "CenteredCube_SeparatedVerts";

        float s = 0.5f;

        var faces = new (Vector3 normal, Vector3 A, Vector3 B, Vector3 C, Vector3 D)[]
        {
            (Vector3.forward,  new Vector3(-s,-s, s), new Vector3(s,-s, s), new Vector3(s, s, s), new Vector3(-s, s, s)),
            (Vector3.back,     new Vector3(s,-s,-s), new Vector3(-s,-s,-s), new Vector3(-s, s,-s), new Vector3(s, s,-s)),
            (Vector3.left,     new Vector3(-s,-s,-s), new Vector3(-s,-s, s), new Vector3(-s, s, s), new Vector3(-s, s,-s)),
            (Vector3.right,    new Vector3(s,-s, s), new Vector3(s,-s,-s), new Vector3(s, s,-s), new Vector3(s, s, s)),
            (Vector3.up,       new Vector3(-s, s, s), new Vector3(s, s, s), new Vector3(s, s,-s), new Vector3(-s, s,-s)),
            (Vector3.down,     new Vector3(-s,-s,-s), new Vector3(s,-s,-s), new Vector3(s,-s, s), new Vector3(-s,-s, s))
        };

        // 6 faces * 4 triangles * 3 verts = 72 verts
        Vector3[] vertices = new Vector3[72];
        Vector3[] normals = new Vector3[72];
        Vector2[] uvs = new Vector2[72];
        Vector4[] tangents = new Vector4[72];
        int[] triangles = new int[72];

        int vi = 0; // vertex index
        int ti = 0; // triangle index

        foreach (var face in faces)
        {
            Vector3 faceCenter = (face.A + face.B + face.C + face.D) * 0.25f;
            var E = faceCenter;

            var tris = new (Vector3 v0, Vector3 v1, Vector3 v2)[]
            {
            (face.A, face.B, E),
            (face.B, face.C, E),
            (face.C, face.D, E),
            (face.D, face.A, E),
            };

            foreach (var tri in tris)
            {
                vertices[vi + 0] = tri.v0;
                vertices[vi + 1] = tri.v1;
                vertices[vi + 2] = tri.v2;
                
                normals[vi + 0] = face.normal;
                normals[vi + 1] = face.normal;
                normals[vi + 2] = face.normal;

                uvs[vi + 0] = new Vector2(0, 0);
                uvs[vi + 1] = new Vector2(1, 0);
                uvs[vi + 2] = new Vector2(0.5f, 0.5f);

                Vector3 triCenter = (tri.v0 + tri.v1 + tri.v2) / 3f;
                Vector3 tangentDir = (triCenter - faceCenter);
                tangentDir = Vector3.ProjectOnPlane(tangentDir, face.normal).normalized;
                Vector4 tangent = new Vector4(tangentDir.x, tangentDir.y, tangentDir.z, 1f);

                tangents[vi + 0] = tangent;
                tangents[vi + 1] = tangent;
                tangents[vi + 2] = tangent;

                triangles[ti + 0] = vi + 0;
                triangles[ti + 1] = vi + 1;
                triangles[ti + 2] = vi + 2;

                vi += 3;
                ti += 3;
            }
        }

        mesh.vertices = vertices;
        mesh.normals = normals;
        mesh.uv = uvs;
        mesh.tangents = tangents;
        mesh.triangles = triangles;

        mesh.RecalculateBounds();

        return mesh;
    }

}
