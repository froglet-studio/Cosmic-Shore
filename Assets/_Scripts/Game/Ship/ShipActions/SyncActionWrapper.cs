using UnityEngine;

namespace CosmicShore._Scripts.Game.Ship.ShipActions
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
                Debug.LogError("[SyncWrapper] Missing references"); 
                enabled = false;
                return;
            }

            if (!(leadActionMono is IScaleProvider provider))
            {
                Debug.LogError("[SyncWrapper] Lead action does not implement IScaleProvider");
                enabled = false;
                return;
            }

            syncAction.AddProvider(provider);
        }
    }
}
