using CosmicShore.Data;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using Reflex.Attributes;
using Reflex.Core;
using Reflex.Injectors;
using UnityEngine;
using UnityEngine.InputSystem;
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
        [Tooltip("Three-wedge per-domain volume indicator on the pause button face. " +
                 "Optional - leave null if the button has no indicator wired yet.")]
        [SerializeField] DomainVolumeIndicator domainVolumeIndicator;
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

        /// <summary>
        /// Self-healing indicator attachment. Runs in Start (not Awake) so [Inject]
        /// fields are populated and can be handed to the indicator. Components added
        /// via AddComponent never receive Reflex injection, so the explicit
        /// SetGameData handoff is required for the vessel-position-based cell
        /// resolution to work.
        /// </summary>
        void EnsureDomainVolumeIndicator()
        {
            if (!volumePauseButton) return;

            if (!domainVolumeIndicator)
                domainVolumeIndicator = volumePauseButton.GetComponent<DomainVolumeIndicator>();
            if (!domainVolumeIndicator)
                domainVolumeIndicator = volumePauseButton.gameObject.AddComponent<DomainVolumeIndicator>();

            domainVolumeIndicator.SetGameData(gameData);
        }

        void OnEnable()
        {
            TrySubscribeEvents();
        }

        void Start()
        {
            // Deferred-subscription pattern (CLAUDE.md ▸ DI): freestyleEvents/gameData are
            // [Inject]ed AFTER OnEnable on scene load, so the OnEnable attempt silently
            // skipped them — leaving the freestyle HUD show/hide and gamepad-Start exit dead.
            TrySubscribeEvents();

            InstantiatePauseMenu();
            EnsureDomainVolumeIndicator();
        }

        void OnDisable()
        {
            UnsubscribeEvents();
        }

        void TrySubscribeEvents()
        {
            UnsubscribeEvents(); // dedup guard — safe to call from both OnEnable and Start

            if (onShipHUDInitialized)
                onShipHUDInitialized.OnRaised += OnShipHUDInitialized;
            if (freestyleEvents?.OnGameStateTransitionStart)
                freestyleEvents.OnGameStateTransitionStart.OnRaised += HandleGameStateTransitionStart;
            if (freestyleEvents?.OnMenuStateTransitionStart)
                freestyleEvents.OnMenuStateTransitionStart.OnRaised += HandleMenuStateTransitionStart;
            if (gameData?.OnPlayerPairInitialized)
                gameData.OnPlayerPairInitialized.OnRaised += HandlePlayerPairInitialized;
        }

        void UnsubscribeEvents()
        {
            if (onShipHUDInitialized)
                onShipHUDInitialized.OnRaised -= OnShipHUDInitialized;
            if (freestyleEvents?.OnGameStateTransitionStart)
                freestyleEvents.OnGameStateTransitionStart.OnRaised -= HandleGameStateTransitionStart;
            if (freestyleEvents?.OnMenuStateTransitionStart)
                freestyleEvents.OnMenuStateTransitionStart.OnRaised -= HandleMenuStateTransitionStart;
            if (gameData?.OnPlayerPairInitialized)
                gameData.OnPlayerPairInitialized.OnRaised -= HandlePlayerPairInitialized;
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
            Show();
            ShowLocalVesselHUD();
        }

        void HandleMenuStateTransitionStart()
        {
            _isInFreestyle = false;
            Hide();
            HideLocalVesselHUD();
        }

        /// <summary>
        /// A player-vessel pair finished (re)initializing. On a mid-freestyle vessel swap the new
        /// vessel's HUD is created HIDDEN (VesselController.Initialize → HideHUD) and the swap never
        /// re-raises OnGameStateTransitionStart, so nothing else would re-show it. When the local
        /// player's pair resolves while in freestyle, re-show the (new) HUD. Gated on freestyle so
        /// the initial menu-state pair init is a no-op (no double-show on first freestyle entry) and
        /// on the local player so remote swaps don't touch our HUD.
        /// </summary>
        void HandlePlayerPairInitialized(ulong playerNetObjId)
        {
            if (!_isInFreestyle) return;
            if (gameData?.LocalPlayer == null || gameData.LocalPlayer.PlayerNetId != playerNetObjId) return;

            Show();
            ShowLocalVesselHUD();
        }

        void ShowLocalVesselHUD() =>
            gameData?.LocalPlayer?.Vessel?.VesselStatus?.VesselHUDController?.ShowHUD();

        void HideLocalVesselHUD() =>
            gameData?.LocalPlayer?.Vessel?.VesselStatus?.VesselHUDController?.HideHUD();

        // ---------------------------------------------------------
        // UI
        // ---------------------------------------------------------

        // While flying freestyle, the gamepad Start button returns you to the appshell -
        // the counterpart to the on-screen Volume/Pause button, for pad players. Guarded on
        // freestyle so it never interferes with menu navigation; ToggleTransition itself guards
        // against re-entrancy while a transition is mid-flight.
        void Update()
        {
            if (!_isInFreestyle) return;
            var pad = Gamepad.current;
            if (pad != null && pad.startButton.wasPressedThisFrame)
                crystalClickHandler.ToggleTransition();
        }

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
        /// Vessel HUD reparenting - identical to MiniGameHUD.OnShipHUDInitialized().
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
            {
                Show();
                ShowLocalVesselHUD();
            }
        }

        void InstantiatePauseMenu()
        {
            if (!pauseMenuPrefab) return;
            var go = Instantiate(pauseMenuPrefab, transform.parent);
            GameObjectInjector.InjectRecursive(go, _container);
            go.SetActive(false);

            // Pay the panel's first-activation cost now, at menu boot, instead of as a
            // hitch on the player's first pause tap in freestyle (mirrors
            // MiniGameHUD.PrewarmPauseMenu for the gameplay scenes).
            var pauseMenu = go.GetComponentInChildren<PauseMenu>(true);
            if (pauseMenu != null)
                pauseMenu.Prewarm();
        }
    }
}
