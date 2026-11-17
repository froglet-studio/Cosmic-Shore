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
        [Tooltip("What the freestyle button does, drawn in the middle of its volume hex: leave " +
                 "freestyle (chrome_close). Empty = no glyph (Docs/MENU_REDESIGN_B.md section 22.5).")]
        [SerializeField] Sprite exitGlyph;
        [Tooltip("Toggles freestyle <-> menu state. Pressing the volume/pause button exits freestyle. Empty in the voyage " +
                 "scene, where the button (and Escape / the pad's Select) opens the pause menu instead.")]
        [SerializeField] MenuCrystalClickHandler crystalClickHandler;

        [Header("SOAP Events")]
        [SerializeField] ScriptableEventVesselHUDData onShipHUDInitialized;

        [Header("Pause Menu")]
        [SerializeField] GameObject pauseMenuPrefab;

        [Inject] Container _container;
        [Inject] GameDataSO gameData;
        [Inject] MenuFreestyleEventsContainerSO freestyleEvents;

        bool _isInFreestyle;
        PauseMenu _pauseMenu;

        /// <summary>In the voyage scene the pause window draws above the voyage HUD (20000-20001), its toasts and its end
        /// board (25000), and under the load veil (30000).</summary>
        const int VoyagePauseSortingOrder = 26000;

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
            if (exitGlyph) domainVolumeIndicator.SetCentreGlyph(exitGlyph, 40f);
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

        // While flying freestyle, ESCAPE or the pad's SELECT returns you to the appshell - the
        // counterpart to the on-screen Volume/Pause button, and the same one gesture the game
        // scenes use (OverviewGesture, asked by MiniGameHUD too, so the two cannot drift).
        //
        // This is what closes the freestyle loop for a mouse pilot: click the screen to fly,
        // Escape to come back. It is also why the mouse scheme never hands the cursor back
        // mid-flight - the one control they would need it for is on this key, and the cursor is
        // released when ToggleTransition pauses the input controller on the way out.
        //
        // Guarded on freestyle so it never interferes with menu navigation; ToggleTransition
        // itself guards against re-entrancy while a transition is mid-flight.
        void Update()
        {
            if (!crystalClickHandler)
            {
                // The voyage scene: Escape / Select opens the pause menu (Shombith, 2026-11-17), the same gesture every
                // game scene uses. Not while the end board is up (its B / Escape is Finish) or the intro plays.
                if (VoyageScoreboard.Current == null && !VoyageIntro.IsPlaying && OverviewGesture.RequestedThisFrame())
                    Exit();
                return;
            }
            if (!_isInFreestyle) return;
            if (OverviewGesture.RequestedThisFrame())
                Exit();
        }

        void OnVolumePauseClicked() => Exit();

        /// <summary>Leave the flight: freestyle back to the menu, or (in the voyage scene, which has no freestyle toggle)
        /// open the pause menu, whose Leave takes the pilot out of the voyage. With no pause menu the voyage is left.</summary>
        void Exit()
        {
            if (crystalClickHandler) crystalClickHandler.ToggleTransition();
            else if (_pauseMenu) _pauseMenu.OnClickPauseGameButton();
            else VoyageScene.Current?.Leave();
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
        /// When a vessel spawns, VesselHUD.Start() raises this SOAP event with the
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
            // The voyage scene's "Game UI" is a zero-size rect (Voyage.unity: stretched with a -1920 x -1080 size delta),
            // so a stretched pause panel made under it had no size and never showed (Shombith, 2026-11-17: "pressing
            // select/back doesn't open the screen"). There the panel goes on the root canvas, on its own sorting canvas
            // above the voyage HUD's overlays (20000-25000), filling the screen.
            bool voyage = !crystalClickHandler;
            var rootCanvas = voyage ? GetComponentInParent<Canvas>()?.rootCanvas : null;
            var parent = rootCanvas ? rootCanvas.transform : transform.parent;
            var go = Instantiate(pauseMenuPrefab, parent);
            if (voyage && go.transform is RectTransform rt)
            {
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = Vector2.zero;
                rt.sizeDelta = Vector2.zero;
                rt.localScale = Vector3.one;
                var sorting = go.AddComponent<Canvas>();
                sorting.overrideSorting = true;
                sorting.sortingOrder = VoyagePauseSortingOrder;
                go.AddComponent<GraphicRaycaster>();
            }
            GameObjectInjector.InjectRecursive(go, _container);
            go.SetActive(false);

            // Pay the panel's first-activation cost now, at menu boot, instead of as a
            // hitch on the player's first pause tap in freestyle (mirrors
            // MiniGameHUD.PrewarmPauseMenu for the gameplay scenes).
            var pauseMenu = go.GetComponentInChildren<PauseMenu>(true);
            if (pauseMenu != null)
                pauseMenu.Prewarm();
            _pauseMenu = pauseMenu;
        }
    }
}
