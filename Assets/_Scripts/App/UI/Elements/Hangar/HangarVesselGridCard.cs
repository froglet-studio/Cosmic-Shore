using CosmicShore.App.Systems.VesselUnlock;
using CosmicShore.App.UI.Screens;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.App.UI.Elements.Hangar
{
    /// <summary>
    /// Grid card for the vessel selection grid in the Hangar.
    /// Displays vessel icon, name, and lock state.
    /// </summary>
    public class HangarVesselGridCard : MonoBehaviour
    {
        [Header("UI Components")]
        [SerializeField] private Image vesselIcon;
        [SerializeField] private TMP_Text vesselName;
        [SerializeField] private GameObject lockOverlay;
        [SerializeField] private Button cardButton;

        SO_Ship _ship;
        HangarScreen _hangarScreen;

        public SO_Ship Ship => _ship;

        public void Configure(SO_Ship ship, HangarScreen hangarScreen)
        {
            _ship = ship;
            _hangarScreen = hangarScreen;

            if (vesselIcon)
                vesselIcon.sprite = ship.IconActive;

            if (vesselName)
                vesselName.text = ship.Name.ToUpperInvariant();

            UpdateLockState();

            if (cardButton)
            {
                cardButton.onClick.RemoveAllListeners();
                cardButton.onClick.AddListener(OnCardClicked);
            }
        }

        public void UpdateLockState()
        {
            if (lockOverlay)
                lockOverlay.SetActive(_ship != null && _ship.IsLocked);
        }

        void OnCardClicked()
        {
            if (_hangarScreen && _ship)
                _hangarScreen.SelectVesselForDetail(_ship);
        }
    }
}
