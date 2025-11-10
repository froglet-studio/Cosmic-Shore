using CosmicShore;
using UnityEngine;

public class OverheatTrailVisualBridge : MonoBehaviour
{
    [SerializeField] private OverheatingActionExecutor overheating;
    [SerializeField] private CosmicShore.Silhouette    silhouette;
    [SerializeField] private SilhouetteConfigSO        config;

    void Awake()
    {
        if (!overheating) overheating = GetComponent<OverheatingActionExecutor>();
        if (!silhouette)  silhouette  = GetComponentInChildren<Silhouette>(true);
    }

    void OnEnable()
    {
        if (!overheating || !silhouette) return;

        overheating.OnOverheated        += EnableDanger;
        overheating.OnHeatDecayStarted  += DisableDanger; // you can also wait for Completed if you prefer
        overheating.OnHeatDecayCompleted+= DisableDanger;
    }

    void OnDisable()
    {
        if (!overheating || !silhouette) return;

        overheating.OnOverheated        -= EnableDanger;
        overheating.OnHeatDecayStarted  -= DisableDanger;
        overheating.OnHeatDecayCompleted-= DisableDanger;
    }

    void EnableDanger()  { if (config == null || !config.enableDangerVisual) return; silhouette.SetDangerVisual(true);  }
    void DisableDanger() { if (config == null || !config.enableDangerVisual) return; silhouette.SetDangerVisual(false); }
}