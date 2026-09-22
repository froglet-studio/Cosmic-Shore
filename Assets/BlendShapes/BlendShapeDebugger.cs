using UnityEngine;

[ExecuteAlways]
public class BlendShapeDebugger : MonoBehaviour
{
    [Header("Manual Testing")]
    public Material debugMaterial;
    
    [Header("Debug Controls")]
    [Range(0f, 1f)] public float manualShape1 = 0f;
    [Range(0f, 1f)] public float manualShape2 = 0f;
    [Range(0f, 1f)] public float manualShape3 = 0f;
    [Range(0f, 1f)] public float manualShape4 = 0f;
    
    [Header("Animation Override")]
    public bool overrideAnimation = false;
    public bool pauseAnimation = false;
    [Range(0f, 1f)] public float animationProgress = 0f;
    
    [Header("Visualization")]
    public bool showAnimationPhase = true;
    public bool showBlendWeights = true;
    
    private MaterialPropertyBlock propertyBlock;
    private MeshRenderer meshRenderer;
    private string currentPhase = "";
    private Vector4 currentWeights;

    void OnEnable()
    {
        propertyBlock = new MaterialPropertyBlock();
        meshRenderer = GetComponent<MeshRenderer>();
        if (meshRenderer != null && debugMaterial != null)
        {
            meshRenderer.sharedMaterial = debugMaterial;
        }
    }

    void Update()
    {
        if (meshRenderer == null) return;

        propertyBlock.Clear();

        if (overrideAnimation)
        {
            currentWeights = new Vector4(manualShape1, manualShape2, manualShape3, manualShape4);
            currentPhase = "Manual Override";
            propertyBlock.SetVector("_BlendWeights", currentWeights);
            propertyBlock.SetFloat("_UseManualWeights", 1f);
        }
        else
        {
            propertyBlock.SetFloat("_UseManualWeights", 0f);
            if (pauseAnimation)
            {
                propertyBlock.SetFloat("_PauseAtProgress", animationProgress);
                CalculateWeightsAtProgress(animationProgress);
            }
            else
            {
                propertyBlock.SetFloat("_PauseAtProgress", 0f);
            }
        }

        meshRenderer.SetPropertyBlock(propertyBlock);
        UpdateDebugDisplay();
    }
    
    private Vector4 CalculateWeightsAtProgress(float progress)
    {
        Vector4 weights = Vector4.zero;
        
        if (progress < 0.25f)
        {
            weights.x = progress * 4f;
            currentPhase = "Phase 1: 5-Point First Half";
        }
        else if (progress < 0.5f)
        {
            weights.x = 1f;
            weights.y = (progress - 0.25f) * 4f;
            currentPhase = "Phase 2: 5-Point Second Half";
        }
        else if (progress < 0.75f)
        {
            weights.z = (progress - 0.5f) * 4f;
            currentPhase = "Phase 3: 3-Point First Half";
        }
        else
        {
            weights.z = 1f;
            weights.w = (progress - 0.75f) * 4f;
            currentPhase = "Phase 4: 3-Point Second Half";
        }
        
        currentWeights = weights;
        return weights;
    }
    
    private void UpdateDebugDisplay()
    {
        if (showAnimationPhase && !string.IsNullOrEmpty(currentPhase))
        {
            Debug.Log($"Animation Phase: {currentPhase}");
        }
        
        if (showBlendWeights)
        {
            Debug.Log($"Blend Weights: Shape1={currentWeights.x:F2}, " +
                     $"Shape2={currentWeights.y:F2}, Shape3={currentWeights.z:F2}, " +
                     $"Shape4={currentWeights.w:F2}");
        }
    }
    
    void OnDrawGizmosSelected()
    {
        // Visualize animation progress
        if (showAnimationPhase)
        {
            Gizmos.color = GetPhaseColor(animationProgress);
            Gizmos.DrawWireSphere(transform.position, 0.5f);
        }
    }
    
    private Color GetPhaseColor(float progress)
    {
        if (progress < 0.25f) return Color.red;        // Phase 1
        if (progress < 0.5f) return Color.yellow;      // Phase 2
        if (progress < 0.75f) return Color.green;      // Phase 3
        return Color.blue;                             // Phase 4
    }
}
