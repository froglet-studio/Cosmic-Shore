using CosmicShore;
using UnityEngine;
using UnityEngine.UI;

public class ShipHUDView : MonoBehaviour, IShipHUDView
{
    public ShipTypes ShipHUDType => hudType;

    [SerializeField] private GameObject _trailBlock;
    [SerializeField] private ShipTypes hudType;

    // --- Serpent Variant ---
    [SerializeField] private Button serpentBoostButton;
    [SerializeField] private Button serpentWallDisplayButton;

    // --- Dolphin Variant ---
    [SerializeField] private Button dolphinBoostButton;

    // --- Manta Variant ---
    [SerializeField] private Button mantaBoostButton;
    [SerializeField] private Button mantaBoost2Button;


    // --- Rhino Variant ---
    [SerializeField] private Button rhinoBoostButton;

    // --- Squirrel Variant ---
    [SerializeField] private Button squirrelBoostButton;

    // --- Sparrow Variant ---
    [SerializeField] private Button sparrowBoostButton;


    public void Initialize(IShipHUDController controller)
    {
        // Remove previous listeners if re-initializing
        RemoveAllButtonListeners();

        switch (hudType)
        {
            case ShipTypes.Serpent:
                if (serpentBoostButton != null)
                    serpentBoostButton.onClick.AddListener(() => controller.OnButtonPressed(1));
                if (serpentWallDisplayButton != null)
                    serpentWallDisplayButton.onClick.AddListener(() => controller.OnButtonPressed(2));
                break;
            case ShipTypes.Dolphin:
                if (dolphinBoostButton != null)
                    dolphinBoostButton.onClick.AddListener(() => controller.OnButtonPressed(1));
                break;
            case ShipTypes.Manta:
                if (mantaBoostButton != null)
                    mantaBoostButton.onClick.AddListener(() => controller.OnButtonPressed(1));
                break;
            case ShipTypes.Rhino:
                if (rhinoBoostButton != null)
                    rhinoBoostButton.onClick.AddListener(() => controller.OnButtonPressed(1));
                break;
            case ShipTypes.Squirrel:
                if (squirrelBoostButton != null)
                    squirrelBoostButton.onClick.AddListener(() => controller.OnButtonPressed(1));
                break;
            case ShipTypes.Sparrow:
                if (sparrowBoostButton != null)
                    sparrowBoostButton.onClick.AddListener(() => controller.OnButtonPressed(1));
                break;
        }
    }

    private void RemoveAllButtonListeners()
    {
        // Clean up ALL listeners for ALL buttons (optional but safest)
        if (serpentBoostButton != null) serpentBoostButton.onClick.RemoveAllListeners();
        if (serpentWallDisplayButton != null) serpentWallDisplayButton.onClick.RemoveAllListeners();
        if (dolphinBoostButton != null) dolphinBoostButton.onClick.RemoveAllListeners();
        // ...repeat for all variant buttons
    }
}

