using CosmicShore.Data;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using Reflex.Attributes;
using Reflex.Core;
using Reflex.Injectors;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// Menu-specific MiniGameHUD for freestyle mode in Menu_Main.
    /// Provides the Volume/Pause button that opens the vessel selection panel
    /// (matching the MinigameFreestyle scene pattern), vessel HUD reparenting
    /// via the onShipHUDInitialized SOAP event, and PauseMenu instantiation.
    ///
    /// Subscribes to <see cref="MenuFreestyleEventsContainerSO"/> transition
    /// bracket events to show/hide the local player's per-vessel HUD when
    /// entering/exiting freestyle mode. Also handles the vessel-swap edge case:
    /// when a new vessel spawns mid-freestyle, <see cref="OnShipHUDInitialized"/>
    /// checks <see cref="_isInFreestyle"/> to auto-show the replacement HUD.
    /// </summary>
    public class MenuMiniGameHUD : MonoBehaviour
    {
        [Header("HUD Visibility")]
        [SerializeField] CanvasGroup canvasGroup;

        [Header("Volume / Pause Button")]
        [SerializeField] Button volumePauseButton;

        [Header("Vessel Selection")]
        [SerializeField] MenuVesselSelectionPanelController vesselSelectionPanel;

        [Header("SOAP Events")]
        [SerializeField] ScriptableEventShipHUDData onShipHUDInitialized;

        [Header("Pause Menu")]
        [SerializeField] GameObject pauseMenuPrefab;

        [Inject] Container _container;
        [Inject] GameDataSO gameData;
        [Inject] MenuFreestyleEventsContainerSO freestyleEvents;

        bool _isInFreestyle;

        void Awake()
        {
            volumePauseButton.onClick.AddListener(OnVolumePauseClicked);
        }

        void OnEnable()
        {
            if (onShipHUDInitialized)
                onShipHUDInitialized.OnRaised += OnShipHUDInitialized;
            if (freestyleEvents?.OnGameStateTransitionStart)
                freestyleEvents.OnGameStateTransitionStart.OnRaised += HandleGameStateTransitionStart;
            if (freestyleEvents?.OnMenuStateTransitionStart)
                freestyleEvents.OnMenuStateTransitionStart.OnRaised += HandleMenuStateTransitionStart;
        }

        void Start()
        {
            InstantiatePauseMenu();
        }

        void OnDisable()
        {
            if (onShipHUDInitialized)
                onShipHUDInitialized.OnRaised -= OnShipHUDInitialized;
            if (freestyleEvents?.OnGameStateTransitionStart)
                freestyleEvents.OnGameStateTransitionStart.OnRaised -= HandleGameStateTransitionStart;
            if (freestyleEvents?.OnMenuStateTransitionStart)
                freestyleEvents.OnMenuStateTransitionStart.OnRaised -= HandleMenuStateTransitionStart;
        }

        void OnDestroy()
        {
            volumePauseButton?.onClick.RemoveListener(OnVolumePauseClicked);
        }

        // ---------------------------------------------------------
        // Freestyle transition handlers
        // ---------------------------------------------------------

        void HandleGameStateTransitionStart()
        {
            _isInFreestyle = true;
            ShowLocalVesselHUD();
        }

        void HandleMenuStateTransitionStart()
        {
            _isInFreestyle = false;
            HideLocalVesselHUD();
        }

        void ShowLocalVesselHUD() =>
            gameData?.LocalPlayer?.Vessel?.VesselStatus?.VesselHUDController?.ShowHUD();

        void HideLocalVesselHUD() =>
            gameData?.LocalPlayer?.Vessel?.VesselStatus?.VesselHUDController?.HideHUD();

        // ---------------------------------------------------------
        // UI
        // ---------------------------------------------------------

        void OnVolumePauseClicked()
        {
            vesselSelectionPanel.Open();
            Hide();
        }

        public void Show()
        {
            canvasGroup.alpha = 1;
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;
        }

        public void Hide()
        {
            canvasGroup.alpha = 0;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }

        /// <summary>
        /// Vessel HUD reparenting — identical to MiniGameHUD.OnShipHUDInitialized().
        /// When a vessel spawns, ShipHUD.Start() raises this SOAP event with the
        /// vessel's MiniGameHUD children. We reparent them under our parent
        /// (Game UI canvas) so they render as siblings.
        ///
        /// If the player is already in freestyle when a new vessel spawns (e.g.
        /// after a vessel swap), the replacement HUD is auto-shown.
        /// </summary>
        void OnShipHUDInitialized(ShipHUDData data)
        {
            if (!data.ShipHUD) return;

            Hide();

            // Move only direct children so nested hierarchies (e.g. VesselHUDView
            // with its own child elements) stay intact. This ensures HideHUD() on the
            // VesselHUDView properly hides all its descendants.
            for (int i = data.ShipHUD.transform.childCount - 1; i >= 0; i--)
            {
                var child = data.ShipHUD.transform.GetChild(i);
                child.SetParent(transform.parent, false);
                child.SetSiblingIndex(0);
            }

            data.ShipHUD.gameObject.SetActive(true);

            if (_isInFreestyle)
                ShowLocalVesselHUD();
        }

        void InstantiatePauseMenu()
        {
            if (!pauseMenuPrefab) return;
            var go = Instantiate(pauseMenuPrefab, transform.parent);
            GameObjectInjector.InjectRecursive(go, _container);
            go.SetActive(false);
        }
    }
}
