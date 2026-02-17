using UnityEngine;
using System.Collections.Generic;
using System.Linq;
#if UNITY_EDITOR
using UnityEditor;
#endif

[System.Serializable]
public class BlendShapeMapping
{
    public string originalName;
    public string displayName;
    public int index;
    public float maxWeight;
}

public class BlendShapeValidator : MonoBehaviour
{
    [Header("Validation")]
    public SkinnedMeshRenderer targetRenderer;
    public List<BlendShapeMapping> detectedShapes = new List<BlendShapeMapping>();
    
    [Header("Expected Blend Shapes")]
    [SerializeField] private string[] expectedNames = new string[]
    {
        "5PointRotate-1stHalfSpin",
        "5PointRotate-2ndHalfSpin",
        "3PointRotate-1stHalfSpin",
        "3PointRotate-2ndHalfSpin"
    };
    
    [ContextMenu("Validate Blend Shapes")]
    public void ValidateBlendShapes()
    {
        if (targetRenderer == null || targetRenderer.sharedMesh == null)
        {
            Debug.LogError("No SkinnedMeshRenderer or mesh found!");
            return;
        }
        
        Mesh mesh = targetRenderer.sharedMesh;
        detectedShapes.Clear();

        // Verify normals and tangents
        bool hasNormals = mesh.normals != null && mesh.normals.Length == mesh.vertexCount;
        bool hasTangents = mesh.tangents != null && mesh.tangents.Length == mesh.vertexCount;
        if (!hasNormals)
        {
            Debug.LogWarning("Mesh is missing normals or they do not match the vertex count.");
        }
        if (!hasTangents)
        {
            Debug.LogWarning("Mesh is missing tangents or they do not match the vertex count.");
        }

        Debug.Log($"Found {mesh.blendShapeCount} blend shapes in {mesh.name}:");
        
        for (int i = 0; i < mesh.blendShapeCount; i++)
        {
            string shapeName = mesh.GetBlendShapeName(i);
            int frameCount = mesh.GetBlendShapeFrameCount(i);
            
            var mapping = new BlendShapeMapping
            {
                originalName = shapeName,
                displayName = GetCleanName(shapeName),
                index = i,
                maxWeight = 100f
            };
            
            detectedShapes.Add(mapping);
            
            // Check if this matches expected shapes
            bool isExpected = expectedNames.Any(expected => 
                shapeName.Contains(expected) || 
                expected.Contains(shapeName) ||
                NormalizeShapeName(shapeName) == NormalizeShapeName(expected)
            );
            
            string status = isExpected ? "\u2713" : "?";
            Debug.Log($"  [{status}] Shape {i}: '{shapeName}' ({frameCount} frames)");
            
            // Analyze the blend shape
            AnalyzeBlendShape(mesh, i);
        }
        
        // Check for missing expected shapes
        foreach (string expected in expectedNames)
        {
            bool found = detectedShapes.Any(shape => 
                shape.originalName.Contains(expected) || 
                NormalizeShapeName(shape.originalName) == NormalizeShapeName(expected)
            );
            
            if (!found)
            {
                Debug.LogWarning($"Expected blend shape '{expected}' not found!");
            }
        }
    }
    
    private void AnalyzeBlendShape(Mesh mesh, int shapeIndex)
    {
        int frameCount = mesh.GetBlendShapeFrameCount(shapeIndex);
        Vector3[] deltaVertices = new Vector3[mesh.vertexCount];
        Vector3[] deltaNormals = new Vector3[mesh.vertexCount];
        Vector3[] deltaTangents = new Vector3[mesh.vertexCount];
        
        // Get the final frame
        mesh.GetBlendShapeFrameVertices(shapeIndex, frameCount - 1, 
            deltaVertices, deltaNormals, deltaTangents);
        
        // Count affected vertices
        int affectedVerts = 0;
        int affectedNormals = 0;
        float maxDisplacement = 0f;
        
        for (int i = 0; i < mesh.vertexCount; i++)
        {
            float vertMag = deltaVertices[i].magnitude;
            float normMag = deltaNormals[i].magnitude;
            
            if (vertMag > 0.0001f) affectedVerts++;
            if (normMag > 0.0001f) affectedNormals++;
            maxDisplacement = Mathf.Max(maxDisplacement, vertMag);
        }
        
        Debug.Log($"    \u2192 Affects {affectedVerts}/{mesh.vertexCount} vertices, " +
                  $"{affectedNormals} normals, max displacement: {maxDisplacement:F3}");
    }
    
    private string GetCleanName(string shapeName)
    {
        // Clean up common naming patterns from FBX
        return shapeName
            .Replace("_", " ")
            .Replace("-", " ")
            .Replace(".", " ")
            .Trim();
    }
    
    private string NormalizeShapeName(string name)
    {
        return name.ToLower()
            .Replace(" ", "")
            .Replace("_", "")
            .Replace("-", "")
            .Replace(".", "");
    }
}
