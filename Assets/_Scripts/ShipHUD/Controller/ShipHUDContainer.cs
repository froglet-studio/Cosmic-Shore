using System.Collections.Generic;
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

        [SerializeField] private List<HUDPrefabVariant> hudVariants;

        [Header("Where to spawn the HUDs")] [SerializeField]
        private RectTransform contentTransform;

        public RectTransform ContentTransform => contentTransform;

        private IShipHUDView _activeHUDView;
        public IShipHUDView ActiveHUDView => _activeHUDView;

        /// <summary>
        /// Finds & instantiates the prefab for this type directly under contentTransform,
        /// then returns its IShipHUDView component.
        /// </summary>
        public IShipHUDView InitializeView(IShipHUDController controller, ShipClassType type)
        {
            var entry = hudVariants.Find(v => v.shipType == type);
            if (entry.prefab == null)
            {
                Debug.LogWarning($"[ShipHUDContainer] No prefab assigned for {type}");
                return null;
            }

            foreach (Transform content in contentTransform.GetComponentInChildren<Transform>())
            {
                if (content != null)
                {
                    content.gameObject.SetActive(true);
                    content.gameObject.TryGetComponent<IShipHUDView>(out _activeHUDView);
                }
                else
                {
                    var go = Instantiate(entry.prefab, contentTransform);
                    go.name = $"{type}HUD";

                    if (!go.TryGetComponent<IShipHUDView>(out _activeHUDView))
                        Debug.LogError($"[ShipHUDContainer] Prefab {go.name} has no IShipHUDView!");
                }

                _activeHUDView.Initialize(controller);
                return _activeHUDView;
            }

            return null;
        }
    }

}
