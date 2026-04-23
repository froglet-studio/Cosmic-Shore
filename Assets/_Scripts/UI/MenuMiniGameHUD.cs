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
    /// Provides the Volume/Pause button that exits freestyle and returns to
    /// the main menu by invoking <see cref="MenuCrystalClickHandler.ToggleTransition"/>.
    /// Also handles vessel HUD reparenting via the onShipHUDInitialized SOAP
    /// event and PauseMenu instantiation.
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
        [Tooltip("Toggles freestyle <-> menu state. Pressing the volume/pause button exits freestyle.")]
        [SerializeField] MenuCrystalClickHandler crystalClickHandler;

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
            crystalClickHandler.ToggleTransition();
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

            foreach (Transform child in data.ShipHUD.GetComponentsInChildren<Transform>(false))
            {
                if (child == data.ShipHUD.transform) continue;
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
