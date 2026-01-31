using UnityEngine;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class BlendShapeBatchProcessor : MonoBehaviour
{
    [Header("Batch Processing")]
    public List<SkinnedMeshRenderer> sourceRenderers = new List<SkinnedMeshRenderer>();
    public Material sharedMaterial;
    public bool shareBlendShapeData = true;
    
    private Texture2D sharedBlendShapeTexture;
    
    [ContextMenu("Process All Objects")]
    public void ProcessBatch()
    {
        if (sourceRenderers.Count == 0)
        {
            Debug.LogError("No source renderers specified!");
            return;
        }
        
        // Process first object to create shared data
        if (shareBlendShapeData && sourceRenderers[0] != null)
        {
            var firstExtractor = sourceRenderers[0].gameObject.AddComponent<BlendShapeExtractor>();
            firstExtractor.sourceRenderer = sourceRenderers[0];
            firstExtractor.targetMaterial = sharedMaterial;
            firstExtractor.ExtractAndSetup();
            
            // Get the created texture
            sharedBlendShapeTexture = sharedMaterial.GetTexture("_BlendShapeData") as Texture2D;
        }
        
        // Process remaining objects
        for (int i = shareBlendShapeData ? 1 : 0; i < sourceRenderers.Count; i++)
        {
            if (sourceRenderers[i] == null) continue;
            
            GameObject obj = sourceRenderers[i].gameObject;
            
            if (shareBlendShapeData)
            {
                // Just setup renderer with shared data
                SetupWithSharedData(obj);
            }
            else
            {
                // Full extraction for each
                var extractor = obj.AddComponent<BlendShapeExtractor>();
                extractor.sourceRenderer = sourceRenderers[i];
                extractor.targetMaterial = Instantiate(sharedMaterial);
                extractor.ExtractAndSetup();
            }
        }
        
        Debug.Log($"Processed {sourceRenderers.Count} objects successfully!");
    }
    
    private void SetupWithSharedData(GameObject obj)
    {
        // Disable skinned mesh renderer
        var skinnedRenderer = obj.GetComponent<SkinnedMeshRenderer>();
        if (skinnedRenderer != null)
        {
            skinnedRenderer.enabled = false;
        }
        
        // Add or get components
        var meshRenderer = obj.GetComponent<MeshRenderer>();
        if (meshRenderer == null)
        {
            meshRenderer = obj.AddComponent<MeshRenderer>();
        }
        
        var meshFilter = obj.GetComponent<MeshFilter>();
        if (meshFilter == null)
        {
            meshFilter = obj.AddComponent<MeshFilter>();
        }
        
        // Create instance mesh without blend shapes
        if (skinnedRenderer != null && skinnedRenderer.sharedMesh != null)
        {
            var instanceMesh = Instantiate(skinnedRenderer.sharedMesh);
            instanceMesh.name = skinnedRenderer.sharedMesh.name + "_Instance";
            instanceMesh.ClearBlendShapes();
            meshFilter.sharedMesh = instanceMesh;
        }
        
        // Use shared material
        meshRenderer.sharedMaterial = sharedMaterial;
    }
}
