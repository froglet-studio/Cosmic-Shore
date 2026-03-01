using CosmicShore.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// Minimal freestyle HUD for Menu_Main's lava-lamp mode.
    /// Lives under "Game UI" so it fades in/out with the freestyle toggle
    /// controlled by <see cref="MenuCrystalClickHandler"/>.
    ///
    /// Phase 1: Provides a vessel-change button that opens the
    /// <see cref="MenuVesselSelectionPanelController"/> panel.
    /// Future phases will add shape drawing (Phase 2) and scoring (Phase 3).
    /// </summary>
    public sealed class MenuFreestyleHUD : MonoBehaviour
    {
        [Header("Vessel Selection")]
        [SerializeField] Button vesselChangeButton;
        [SerializeField] MenuVesselSelectionPanelController vesselSelectionPanel;

        void Awake()
        {
            if (vesselChangeButton)
                vesselChangeButton.onClick.AddListener(OnVesselChangeClicked);
        }

        void OnDestroy()
        {
            if (vesselChangeButton)
                vesselChangeButton.onClick.RemoveListener(OnVesselChangeClicked);
        }

        void OnVesselChangeClicked()
        {
            vesselSelectionPanel.Open();
        }
    }
}
