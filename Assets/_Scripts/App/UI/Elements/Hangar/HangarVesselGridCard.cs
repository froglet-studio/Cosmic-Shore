using CosmicShore.App.UI.Screens;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.App.UI.Elements.Hangar
{
    /// <summary>
    /// Grid card for the vessel selection grid in the Hangar.
    /// Displays vessel icon, name, and lock state.
    /// Requires a CanvasGroup on the same GameObject for fade animations.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class HangarVesselGridCard : MonoBehaviour
    {
        [Header("UI Components")]
        [SerializeField] private Image vesselIcon;
        [SerializeField] private TMP_Text vesselName;
        [SerializeField] private GameObject lockOverlay;
        [SerializeField] private Button cardButton;

        SO_Vessel _ship;
        HangarScreen _hangarScreen;
        CanvasGroup _canvasGroup;

        public SO_Vessel Ship => _ship;

        void Awake()
        {
            _canvasGroup = GetComponent<CanvasGroup>();
        }

        public void Configure(SO_Vessel ship, HangarScreen hangarScreen)
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

        /// <summary>
        /// Sets the card's visual alpha via CanvasGroup. Used by HangarScreen for fade animations.
        /// </summary>
        public void SetAlpha(float alpha)
        {
            if (!_canvasGroup)
                _canvasGroup = GetComponent<CanvasGroup>();
            if (_canvasGroup)
                _canvasGroup.alpha = alpha;
        }

        void OnCardClicked()
        {
            if (_hangarScreen && _ship)
                _hangarScreen.SelectVesselForDetail(_ship);
        }
    }
}
