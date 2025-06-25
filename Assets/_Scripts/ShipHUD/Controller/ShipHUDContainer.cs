using CosmicShore;
using System.Collections.Generic;
using UnityEngine;

public class ShipHUDContainer : MonoBehaviour
{
    [System.Serializable]
    public struct HUDPrefabVariant
    {
        public ShipTypes shipType;
        public GameObject prefab;
    }

    [SerializeField]
    private List<HUDPrefabVariant> hudVariants;

    [Header("Where to spawn the HUDs")]
    [SerializeField]
    private RectTransform contentTransform;
    public RectTransform ContentTransform => contentTransform;

    /// <summary>
    /// Finds & instantiates the prefab for this type directly under contentTransform,
    /// then returns its IShipHUDView component.
    /// </summary>
    public IShipHUDView Show(ShipTypes type)
    {
        var entry = hudVariants.Find(v => v.shipType == type);
        if (entry.prefab == null)
        {
            Debug.LogWarning($"[ShipHUDContainer] No prefab assigned for {type}");
            return null;
        }

        // Instantiate as a child of contentTransform
        var go = Instantiate(entry.prefab, contentTransform);
        go.name = $"{type}HUD";

        var view = go.GetComponent<IShipHUDView>();
        if (view == null)
            Debug.LogError($"[ShipHUDContainer] Prefab {go.name} has no IShipHUDView!");
        return view;
    }
}
