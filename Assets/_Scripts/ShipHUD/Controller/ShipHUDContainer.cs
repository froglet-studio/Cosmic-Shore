using System.Linq;
using UnityEngine;

namespace CosmicShore.Game
{
    public class ShipHUDContainer : MonoBehaviour
    {
        [System.Serializable]
        public struct HUDPrefabVariant
        {
            public ShipClassType shipType;
            public GameObject prefab;
        }

        [SerializeField] HUDPrefabVariant[] hudVariants;
        [SerializeField] RectTransform contentTransform;

        IShipHUDView _activeHUDView;

        public IShipHUDView InitializeView(IShipHUDController controller, IShipStatus shipStatus)
        {
            foreach (Transform content in contentTransform.GetComponentInChildren<Transform>())
            {
                if (content == null) continue;
                if (shipStatus.AutoPilotEnabled)
                {
                    content.gameObject.SetActive(false);
                    return null;
                }
                
                content.gameObject.SetActive(true);
                content.gameObject.TryGetComponent(out _activeHUDView);
                return _activeHUDView;
            }

            return null;
            //
            // var entry = hudVariants.FirstOrDefault(v => v.shipType == type);
            // if (entry.prefab == null)
            // {
            //     Debug.LogWarning($"[ShipHUDContainer] No prefab assigned for {type}");
            //     return null;
            // }
            //
            // var go = Instantiate(entry.prefab, contentTransform);
            // go.name = $"{type}HUD";
            //
            // if (!go.TryGetComponent(out _activeHUDView))
            // {
            //     Debug.LogError($"[ShipHUDContainer] Prefab {go.name} has no IShipHUDView!");
            //     return null;
            // }
            //
            // _activeHUDView.Initialize(controller);
        }
    }
}