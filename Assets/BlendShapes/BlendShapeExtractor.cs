using UnityEngine;

public class BlendShapeExtractor : MonoBehaviour
{
    [Header("Source & Target")]
    public SkinnedMeshRenderer sourceRenderer;
    public Material targetMaterial;
    public Texture2D blendShapeTexture;

    [ContextMenu("Extract and Setup")]
    public void ExtractAndSetup()
    {
        if (sourceRenderer == null || targetMaterial == null)
        {
            Debug.LogError("Missing source renderer or target material!");
            return;
        }

        Mesh mesh = sourceRenderer.sharedMesh;
        if (mesh == null)
        {
            Debug.LogError("Source renderer has no mesh!");
            return;
        }

        int vertexCount = mesh.vertexCount;
        int shapeCount = Mathf.Min(mesh.blendShapeCount, 4);

        blendShapeTexture = new Texture2D(vertexCount, shapeCount * 2, TextureFormat.RGBAHalf, false);
        blendShapeTexture.wrapMode = TextureWrapMode.Clamp;
        blendShapeTexture.filterMode = FilterMode.Point;

        Vector3[] deltaVertices = new Vector3[vertexCount];
        Vector3[] deltaNormals = new Vector3[vertexCount];
        Vector3[] deltaTangents = new Vector3[vertexCount];

        for (int s = 0; s < shapeCount; s++)
        {
            int frameIndex = mesh.GetBlendShapeFrameCount(s) - 1;
            mesh.GetBlendShapeFrameVertices(s, frameIndex, deltaVertices, deltaNormals, deltaTangents);

            for (int v = 0; v < vertexCount; v++)
            {
                Vector3 dv = deltaVertices[v];
                Vector3 dn = deltaNormals[v];
                blendShapeTexture.SetPixel(v, s * 2, new Color(dv.x, dv.y, dv.z, 1f));
                blendShapeTexture.SetPixel(v, s * 2 + 1, new Color(dn.x, dn.y, dn.z, 1f));
            }
        }

        blendShapeTexture.Apply();
        targetMaterial.SetTexture("_BlendShapeData", blendShapeTexture);

        // Create static mesh without blend shapes
        Mesh instanceMesh = Instantiate(mesh);
        instanceMesh.name = mesh.name + "_Instance";
        instanceMesh.ClearBlendShapes();

        MeshRenderer meshRenderer = GetComponent<MeshRenderer>();
        if (meshRenderer == null)
            meshRenderer = gameObject.AddComponent<MeshRenderer>();
        MeshFilter meshFilter = GetComponent<MeshFilter>();
        if (meshFilter == null)
            meshFilter = gameObject.AddComponent<MeshFilter>();

        meshRenderer.sharedMaterial = targetMaterial;
        meshFilter.sharedMesh = instanceMesh;

        sourceRenderer.enabled = false;
    }
}
