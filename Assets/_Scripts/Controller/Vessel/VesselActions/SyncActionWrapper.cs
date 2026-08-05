using UnityEngine;
using CosmicShore.Utility;
using CosmicShore.Data;

namespace CosmicShore.Gameplay
{
    public class SyncActionWrapper : MonoBehaviour
    {
        [Header("Required components")]
        [Tooltip("Script that implements IScaleProvider (e.g. GrowActionBase)")]
        [SerializeField]
        private MonoBehaviour leadActionMono;

        [Tooltip("The camera-side action")]
        [SerializeField]
        private ZoomOutAction syncAction;

        private void Start()
        {
            if (leadActionMono == null || syncAction == null)
            {
                CSDebug.LogError("[SyncWrapper] Missing references"); 
                enabled = false;
                return;
            }

            if (!(leadActionMono is IScaleProvider provider))
            {
                CSDebug.LogError("[SyncWrapper] Lead action does not implement IScaleProvider");
                enabled = false;
                return;
            }

            syncAction.AddProvider(provider);
        }
    }
}
